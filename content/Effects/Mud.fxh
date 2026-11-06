// The mud on the car (#60): a part of the car (its body, a wheel, the holder of an axle) with mud on it is drawn by the
// technique LitMud<Detail> or LitCoarseShadowsMud<Detail> (Scene.fx), which lays the mud over the colour and the finish
// of its material: the colour of the earth, matt, darker where it is thick, with a little relief at the high detail.
// How much mud the part has (MudAmount, 0 clean to 1) comes from the game (GameObjects/CarMud.cs), where it lies from
// here.
//
// Where it lies is worked out in the model's own coordinates (MudSpace): the mud stays where it is on a turning wheel,
// and it is laid the same way on every car and wheel without their models marking anything. A wheel and the holder of an
// axle get it all over; the body (MudBody 1) first around and behind its axles (MudAxles, where the physics holds them)
// and on its lower parts, then further up as the amount grows. The cars share the layout of the physics and are about the
// same size, so the distances are in model units. Splashes rather than a layer: blotches where the mottle of the noise of
// the surfaces (Surfaces.fxh), laid on the model, reaches above an edge that drops as the amount grows, and specks of its
// cellular noise thrown around them.
//
// The coordinates of a model stay small (a few units), so the half precision of a phone's pixel shader keeps them.
//
// Functions and constants only: the uniforms they read are declared by the includer (Scene.fx), before it includes this
// file, after Surfaces.fxh.

// Tiles of the noise a model unit (the blotches and the specks) and how many times as often the fine lookup repeats
#define MUD_COARSE (1.0 / 6.0)
#define MUD_FINE 4.0

// How far round the wheels of the physics are (model units)
#define MUD_WHEEL 0.7

// How much of the light of the sun and of the sky and the ground the mud takes (Scene.fx, which also gives the light of
// the sky on it half the hue of the sun): its rough crumbs scatter the light more evenly than a painted face does, and
// the mud should look of one colour in the sun and in the shade (taken fully, the faces turned to the sun looked of a
// lighter, warmer mud than the faces in the shade, a stripe of the paint seemed to show through it)
#define MUD_SUN 0.4
#define MUD_AMBIENT 2.2

// The colour of wet mud: a part of the colour of the dried mud (MudColor), darker and a little browner
#define MUD_WET float3(0.3, 0.26, 0.22)

// The look of the mud at a pixel, over the material: its colour (linear), the normal and the finish
struct MudLook
{
	float3 Albedo;
	float3 Normal;
	float Metallic;
	float Roughness;
	float Shine;    // a factor of the rim of light
	float Specular; // the strength of the highlight of the sun, in place of the material's
	float Cover;    // how much of the material the mud covers, 0 to 1
};

// For the vertex shader: the position of a point in the model's own coordinates, its coordinates on the plane of its
// face (by the normal in the model) for the coarse lookup (xy; the fine one is MUD_FINE times them), and how much of the
// tread of a wheel it is (z: the outermost faces of a wheel turned away from its axis, z of its model; nothing on the
// body, see MudAmountAt). Worked out here, where the matrices are: the pixel shader must not read World (MonoGame sizes
// a parameter shared by the shaders by its first use, and a pixel shader reading a part of the matrix got it wrong,
// which changed the light of a part with no mud on it yet)
void MudCoordinates(float4 position, float3 normal, out float3 local, out float4 plane)
{
	local = mul(position, MudSpace).xyz;
	float3 own = normalize(mul(normal, (float3x3)MudSpace));
	float3 axes = SurfaceAxes(own);
	float2 onPlane = (axes.x * local.xz + axes.y * local.xy + axes.z * local.zy) * MUD_COARSE;
	float tread = saturate(1.0 - abs(own.z) * 2.0) * saturate((length(local.xy) / MUD_WHEEL - 0.82) * 6.0);
	plane = float4(onPlane, tread, 0.0);
}

// How readily the mud gathers at a point of the model, 0 to 1: on a wheel most on the tyre (the wheels of the physics are
// MUD_WHEEL units round), less towards the middle; on the body most around its axles and behind them (the car faces -x
// of its model, the wheels throw the mud back), and on its lower parts
float MudExposure(float3 local)
{
	float wheel = lerp(0.3, 0.75, saturate(length(local.xy) / MUD_WHEEL));
	float2 front = local.xy - MudAxles.xy;
	float2 rear = local.xy - MudAxles.zw;
	front.x *= front.x > 0.0 ? 0.55 : 1.0;
	rear.x *= rear.x > 0.0 ? 0.55 : 1.0;
	float near = saturate(1.6 - min(length(front), length(rear)) / 1.5);
	float axles = (MudAxles.y + MudAxles.w) * 0.5;
	float low = saturate((axles + 2.0 - local.y) / 2.4);
	return lerp(wheel, max(low * 0.85, near), MudBody);
}

// How much mud there is at a point: on the tread of a wheel (how much of it the point is, from the vertex shader) that of
// the tread, which the road wipes first; elsewhere the amount of the part
float MudAmountAt(float tread)
{
	return lerp(MudAmount, MudTread, tread * (1.0 - MudBody));
}

// The mud over a material at a pixel
MudLook MudOver(sampler2D noise, float3 albedo, float3 normal, float metallic, float roughness, float4 plane, float3 local, int detail)
{
	float4 coarse = tex2D(noise, plane.xy);
	float4 fine = tex2D(noise, plane.xy * MUD_FINE);
	float amount = MudAmountAt(plane.z);
	// The blotches: where the exposure with the mottle reaches above the edge, which drops as the amount grows; none on
	// a clean part
	float reach = MudExposure(local) + (coarse.r * 0.7 + fine.r * 0.3 - 0.5) * 0.8;
	float edge = 1.25 - amount * 1.5;
	float cover = 1.0 - SurfaceBelow(reach, edge, 0.02);
	// The specks thrown around them, uneven
	float specks = SurfaceBelow(coarse.a + (fine.r - 0.5) * 0.2, 0.12, 0.02) * (1.0 - SurfaceBelow(reach, edge - 0.25, 0.05));
	cover = max(cover, specks) * saturate(amount * 20.0);
	// Thicker further in: a little darker; the thin edges a little lighter
	float thick = saturate((reach - edge) * 3.0);

	// Wet where it is fresh, longer where it is thick: darker and a little glossy; dry, light and matt
	float wet = saturate(MudWet * 1.2 + (thick - 0.6) * 0.5);

	MudLook look;
	float3 mud = MudColor * lerp(0.85, 1.15, fine.r) * lerp(1.06, 0.94, thick) * lerp(float3(1.0, 1.0, 1.0), MUD_WET, wet);
	look.Albedo = lerp(albedo, mud, cover);
	look.Normal = normal;
	// The relief tilts the normal in the world (as the surfaces of the blocks do), a little
	if (detail >= SURFACE_HIGH)
		look.Normal = SurfaceTilt(normal, SurfaceSlope(fine) * (0.1 + 0.12 * thick) * cover);
	look.Metallic = metallic * (1.0 - cover);
	look.Roughness = lerp(roughness, lerp(0.95, 0.7, wet), cover);
	look.Shine = lerp(1.0, 0.3, cover);
	look.Specular = lerp(0.03, 0.2, wet);
	look.Cover = cover;
	return look;
}
