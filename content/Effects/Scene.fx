// Lighting of the 3D scene. The models are polygons of a single colour each (no textures),
// so the light makes the whole look: the sun with its shadows, the light of the sky from above
// and the light reflected by the ground from below (both dimmed in corners and crevices by
// ambient occlusion), a highlight of the sun, a rim of sky light at grazing angles and the
// lamps of the car and the street lamps (spot lights: the headlights and the street lamps at night, the rear lights). It is computed in linear colour space and
// mapped to the screen by a filmic curve; the distance fades into the colour of the sky.
// Glass (Alpha less than 1) is blended over the scene, less transparent where it shines.
// Smooth materials (Roughness less than 1) mirror the sky, the ground and the sun; metal
// (Metallic) only mirrors, in its own colour, like chrome. Technique Lit draws all that, technique
// LitCoarseShadows the same with the coarser filter of the shadows that phones need (see Sunlight).
//
// A part whose material names a surface (asphalt, earth, grass) is drawn by a technique of its surface and of the detail of
// the surfaces, as Lit<Surface><Detail> and LitCoarseShadows<Surface><Detail> (LitAsphaltMedium): the same light, with
// the colour and the relief of the surface computed per pixel (Surfaces.fxh).
//
// A part of the car with mud on it is drawn by LitMud<Detail> and LitCoarseShadowsMud<Detail> (Medium and High: none at
// the low detail): the same light, with the mud laid over its material per pixel (Mud.fxh).
//
// Technique Sky draws the sky behind everything, computed from the direction of each pixel (no
// picture): a smooth ramp from the horizon up, a glow and a disc of the sun (or the moon), stars,
// clouds on a plane high above (from a noise texture the renderer generates), distant hills along
// the horizon and the ground below them, dithered so that the ramp shows no bands.
//
// Technique Depth draws the shadow map: the depth of the scene seen from the sun, packed into
// the four 8-bit channels of an ordinary texture (phones may not render to float textures).
// Technique NormalDepth draws the normals and depths seen by the camera, for the ambient
// occlusion (Occlusion.fx).

#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0_level_9_3
	#define PS_SHADERMODEL ps_4_0_level_9_3
#endif

// Per frame
float4x4 View;
float4x4 ViewProjection;
float3 CameraPosition;
float3 SunDirection;        // unit vector towards the sun
float3 SunColor;            // linear, brighter than 1
float3 SkyLight;            // linear, light coming from above
float3 GroundLight;         // linear, light coming from below
float3 RimLight;            // linear, light at grazing angles
float Exposure;
float3 HazeColor;           // on the screen (gamma), the colour of the sky at the horizon
float3 Haze;                // start distance, 1 / (end distance - start distance), maximum amount

// What smooth materials mirror (linear): the sky overhead and at the horizon, the ground
float3 MirroredSky;
float3 MirroredHorizon;
float3 MirroredGround;

// The lamps, spot lights: two headlights and two rear lights of the car, and the four street lamps nearest to
// the camera, which shine down. Their positions are given relative to the car (LightOrigin), the vector from a
// point to the car is computed in the vertex shader
#define LIGHTS 8
float3 LightOrigin;
float3 LightOffset[LIGHTS];     // position of the lamp - LightOrigin
float3 LightDirection[LIGHTS];  // unit vector
float3 LightColor[LIGHTS];      // linear; black = off
float2 LightCone[LIGHTS];       // cosine of the outer angle, 1 / (cosine of the inner angle - cosine of the outer one)
float LightRange[LIGHTS];

// The sky (colours on the screen, gamma)
float4x4 InverseViewProjection;
float3 SkyZenith;
float3 SkyHorizon;
float3 SkyGround;           // far below the horizon
float3 SkyHills;            // the distant hills along the horizon
float3 SkySun;              // unit vector towards the sun (or the moon) of the sky
float3 SunDisc;             // its colour; black = none
float SunDiscCos;           // cosine of its radius
float3 SunGlow;
float Stars;                // how bright the stars are (0 = none)
float CloudCover;           // how much of the sky the clouds cover (0 = none)
float3 CloudLight;          // the side of the clouds lit by the sun
float3 CloudShade;          // their shaded side
float2 CloudDrift;          // how far the wind has moved them
float2 SkyScreenSize;       // pixels of the screen, for the dithering
float HorizonDip;           // the sine of how far below the eye the horizon is
// R: soft noise for the clouds, tiling; G: an independent random value in each texel (stars, dithering)
#define NOISE_SIZE 256.0
texture SkyNoise;
sampler2D NoiseSampler = sampler_state
{
	Texture = <SkyNoise>;
	MinFilter = Linear;
	MagFilter = Linear;
	MipFilter = Linear;
	AddressU = Wrap;
	AddressV = Wrap;
};

// Shadows. The texture holds two shadow maps: the shadow map, and beside it the deep one, coarser, for what
// lies deeper than the shadow map reaches (the deeper back layers)
float4x4 LightViewProjection;
float2 ShadowTexel;         // size of a texel of the texture (in texture coordinates)
float2 ShadowScale;         // the part of the texture the shadow map takes
float ShadowMargin;         // the edge of the shadow map that is not looked up (in its own coordinates)
float ShadowBias;           // in shadow map depth, for surfaces facing the sun
float NormalOffset;         // world units the shadow lookup moves along the normal
float ShadowStrength;       // 0 = no shadows
float4x4 DeepLightViewProjection;
float2 DeepShadowScale;
float2 DeepShadowOffset;    // where the deep shadow map starts in the texture
float DeepShadowMargin;
float DeepShadowBias;
float DeepNormalOffset;
float DeepShadowStrength;   // 0 = not drawn: what the shadow map does not cover is in the sun
texture ShadowMap;
sampler2D ShadowSampler = sampler_state
{
	Texture = <ShadowMap>;
	MinFilter = Point;
	MagFilter = Point;
	MipFilter = None;
	AddressU = Clamp;
	AddressV = Clamp;
};

// Ambient occlusion
float OcclusionStrength;    // 0 = no ambient occlusion
float OcclusionDepthRange;  // depths of the normal-depth map are stored as a part of this distance
texture OcclusionMap;
sampler2D OcclusionSampler = sampler_state
{
	Texture = <OcclusionMap>;
	MinFilter = Linear;
	MagFilter = Linear;
	MipFilter = None;
	AddressU = Clamp;
	AddressV = Clamp;
};

// Per model part
float4x4 World;
float3 DiffuseColor;        // linear
float3 EmissiveColor;       // linear
float Specular;             // strength of the sun's highlight
float Alpha;                // opacity (1 = opaque; glass is less)
float Metallic;             // 1 = metal: no diffuse light, mirrors in its own colour
float Roughness;            // 0 = a perfect mirror, 1 = mirrors nothing (most materials)

// Per model part with a surface (Surfaces.fxh)
float3 SurfaceOrigin;       // the middle of the cell of the level grid the block is in
float SurfaceCell;          // the size of a cell
float4 SurfaceSeedsLow;     // the random values of the lower corners of the cell: (x-, z-), (x+, z-), (x-, z+), (x+, z+)
float4 SurfaceSeedsHigh;    // the same of the upper corners
float SurfaceVerge;         // 1 = the earth and the grass blend at the verge of the off-road road, 0 = not (a test surface)
float3 VergeEarth;          // linear, the colours of the earth and the grass of the off-road blocks there
float3 VergeGrass;
// The noise of the surfaces (Graphics/SurfaceNoise.cs): it tiles, with mipmaps
texture SurfaceNoise;
sampler2D SurfaceSampler = sampler_state
{
	Texture = <SurfaceNoise>;
	MinFilter = Linear;
	MagFilter = Linear;
	MipFilter = Linear;
	AddressU = Wrap;
	AddressV = Wrap;
};

#include "Surfaces.fxh"

// Per model part with mud on it (Mud.fxh)
float4x4 MudSpace;          // from the coordinates of the part's mesh to the model's own
float MudAmount;            // 0 = clean, 1 = as muddy as it gets
float MudTread;             // a wheel: the same on the tread of its tyre
float MudWet;               // 0 = dry, 1 = fresh
float4 MudAxles;            // the body: the middles of its front and rear axle in its model (front x, y, rear x, y)
float MudBody;              // 1 = the body of the car (the mud gathers by MudAxles), 0 = a wheel or a holder (all over)
float3 MudColor;            // linear, the colour of the mud

#include "Mud.fxh"

struct VertexInput
{
	float4 Position : POSITION0;
	float3 Normal : NORMAL0;
};

// Phones run pixel shaders in half precision, too coarse for positions in a long level:
// the vectors to the camera and to the car and the position in the shadow map are
// computed here, in the vertex shader, where they are still precise
struct VertexOutput
{
	float4 Position : SV_POSITION;
	float3 ToCamera : TEXCOORD0;
	float3 Normal : TEXCOORD1;
	float3 Shadow : TEXCOORD2;     // texture coordinates and depth in the shadow map
	float4 Screen : TEXCOORD3;     // position on the screen (clip space)
	float3 ToCar : TEXCOORD4;
	float3 DeepShadow : TEXCOORD5; // the same in the deep shadow map
	float3 Local : TEXCOORD6;      // the position in cells from the middle of the block's cell (a surface, Surfaces.fxh)
	float4 Plane : TEXCOORD7;      // the coordinates of the lookups of the noise of a surface
};

// Where a point is in a shadow map seen by the sun: its coordinates in that map and its depth
float3 ShadowPosition(float4x4 lightViewProjection, float3 world)
{
	float4 shadow = mul(float4(world, 1.0), lightViewProjection);
	// (w is 1 for the orthographic view of the sun, but dividing by it keeps the whole matrix in the
	// shader: MonoGame shares the parameter between the shaders and sizes it by its first use)
	shadow.xyz /= shadow.w;
	return float3(shadow.x * 0.5 + 0.5, -shadow.y * 0.5 + 0.5, shadow.z);
}

// The vertex of a part drawn lit; Local and Plane for the noise laid on a block (a surface) or on a model (the mud)
VertexOutput LitVS(VertexInput input, bool muddy)
{
	VertexOutput output;
	float4 world = mul(input.Position, World);
	float3 normal = normalize(mul(input.Normal, (float3x3)World));
	output.Position = mul(world, ViewProjection);
	output.ToCamera = CameraPosition - world.xyz;
	output.Normal = normal;
	output.Shadow = ShadowPosition(LightViewProjection, world.xyz + normal * NormalOffset);
	output.DeepShadow = ShadowPosition(DeepLightViewProjection, world.xyz + normal * DeepNormalOffset);
	output.Screen = output.Position;
	output.ToCar = LightOrigin - world.xyz;
	// Read only by the techniques of the surfaces and of the mud
	if (muddy)
		MudCoordinates(input.Position, input.Normal, output.Local, output.Plane);
	else
		SurfaceCoordinates(world.xyz, normal, output.Local, output.Plane);
	return output;
}

VertexOutput MainVS(VertexInput input)
{
	return LitVS(input, false);
}

VertexOutput MudVS(VertexInput input)
{
	return LitVS(input, true);
}

float UnpackDepth(float4 packed)
{
	return dot(packed, float4(1.0, 1.0 / 255.0, 1.0 / 65025.0, 1.0 / 16581375.0));
}

// How much of the sun reaches the point, smoothly: the 3 x 3 texels around it, each lit or not, weighted as two
// by two lookups of them filtered bilinearly would weigh them (by how far into its texel the point is), so that the
// edges of the shadows are soft and move with the point, not in the steps of the texels
float SmoothSunVisibility(float3 shadow, float bias)
{
	float depth = shadow.z - bias;
	float2 texels = shadow.xy / ShadowTexel;
	float2 corner = floor(texels);
	float2 part = texels - corner;
	float2 centre = (corner + 0.5) * ShadowTexel;
	float3 weightX = float3(1.0 - part.x, 1.0, part.x);
	float3 weightY = float3(1.0 - part.y, 1.0, part.y);
	float lit = 0.0;
	for (int y = 0; y < 3; y++)
	{
		for (int x = 0; x < 3; x++)
		{
			float2 offset = float2(x - 1, y - 1) * ShadowTexel;
			lit += weightX[x] * weightY[y] * step(depth, UnpackDepth(tex2D(ShadowSampler, centre + offset)));
		}
	}
	return lit / 4.0;
}

// How much of the sun reaches the point, coarsely (on phones, whose half precision has no fraction of a texel
// left for the smooth filter): 3 x 3 samples of the shadow map around it, a texel and a half apart
float CoarseSunVisibility(float3 shadow, float bias)
{
	float depth = shadow.z - bias;
	float lit = 0.0;
	for (int y = -1; y <= 1; y++)
	{
		for (int x = -1; x <= 1; x++)
		{
			float2 offset = float2(x, y) * ShadowTexel * 1.5;
			lit += step(depth, UnpackDepth(tex2D(ShadowSampler, shadow.xy + offset)));
		}
	}
	return lit / 9.0;
}

// How much of the sun reaches the point: from the shadow map, or where the deep shadow map is drawn and the
// shadow map does not cover the point (with the samples around it), from the deep one. One lookup in either
// case, outside the branches, which a lookup cannot be in. Smooth or coarse is fixed for each technique (see
// Shade), so the compiler keeps only one of them.
float Sunlight(float3 shadow, float3 deepShadow, float facing, bool smooth)
{
	bool covers = all(shadow.xy > ShadowMargin) && all(shadow.xy < 1.0 - ShadowMargin) && shadow.z < 1.0;
	bool near = covers || DeepShadowStrength == 0.0;
	// Without the deep shadow map, as before it: the edge of the shadow map stretches beyond it
	float3 nearShadow = float3(clamp(shadow.xy, 0.0, 1.0) * ShadowScale, shadow.z);
	float3 deep = float3(clamp(deepShadow.xy, DeepShadowMargin, 1.0 - DeepShadowMargin) * DeepShadowScale + DeepShadowOffset, deepShadow.z);
	float3 lookup = near ? nearShadow : deep;
	float bias = near ? ShadowBias : DeepShadowBias;
	bias *= 3.0 - 2.0 * facing;
	return smooth ? SmoothSunVisibility(lookup, bias) : CoarseSunVisibility(lookup, bias);
}

// Filmic curve mapping linear light to 0..1 (approximation of ACES by Krzysztof Narkowicz)
float3 Filmic(float3 x)
{
	return saturate((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14));
}

// What a mirror shows in a direction: the ground below the horizon, the sky above it, both brighter
// towards the horizon (the far ground fades into the haze). On a rough surface the horizon blurs.
float3 Mirrored(float3 direction, float roughness)
{
	float blur = 0.02 + roughness * 0.5;
	float3 sky = lerp(MirroredHorizon, MirroredSky, sqrt(saturate(direction.y)));
	float3 ground = lerp(MirroredHorizon * 0.5, MirroredGround, saturate(-direction.y * 2.5));
	return lerp(ground, sky, smoothstep(-blur, blur, direction.y));
}

// The lit colour of a pixel, with the shadows filtered smoothly or coarsely (see Sunlight), of a surface (or
// SURFACE_NONE, the plain colour of the material) at a detail, muddy or not; all four are written in the technique
float4 Shade(VertexOutput input, bool smooth, int surface, int detail, bool muddy)
{
	float3 normal = normalize(input.Normal);
	float distance = length(input.ToCamera);
	float3 view = input.ToCamera / distance;

	// The shadows and the rim by the face, the rest of the light by the relief of its surface (a rim of the relief would
	// sparkle all over a face seen at a grazing angle)
	float3 face = normal;
	float visible = Sunlight(input.Shadow, input.DeepShadow, saturate(dot(face, SunDirection)), smooth);
	float3 albedo = DiffuseColor;
	float specular = Specular;
	float sheen = 1.0;
	float metallic = Metallic;
	float roughness = Roughness;
	if (surface != SURFACE_NONE)
	{
		SurfaceLook look = SurfaceOf(SurfaceSampler, surface, detail, DiffuseColor, normal, input.Plane, input.Local);
		albedo = look.Albedo;
		normal = look.Normal;
		specular *= look.Shine;
		// A matte surface (earth) has little of the rim too, which lined the walls of its ruts seen edge-on
		sheen = look.Shine;
	}
	// The mud scatters the light it gets more evenly than the paint: less of the sun, more of the sky and the ground, so
	// that it looks of one colour in the sun and in the shade (MUD_SUN, MUD_AMBIENT of Mud.fxh)
	float softened = 0.0;
	if (muddy)
	{
		MudLook mud = MudOver(SurfaceSampler, albedo, normal, metallic, roughness, input.Plane, input.Local, detail);
		softened = mud.Cover;
		// The mud takes the place of the finish of the material too: its own highlight, whatever the material had
		albedo = mud.Albedo;
		normal = mud.Normal;
		metallic = mud.Metallic;
		roughness = mud.Roughness;
		specular = lerp(specular, mud.Specular, mud.Cover);
		sheen = mud.Shine;
	}
	float facing = saturate(dot(normal, SunDirection));

	// Ambient occlusion darkens the light of the sky and the ground, and a little the sun,
	// which makes the contact of objects visible in the sun too
	float2 screen = input.Screen.xy / input.Screen.w * float2(0.5, -0.5) + 0.5;
	float occlusion = lerp(1.0, tex2D(OcclusionSampler, screen).r, OcclusionStrength);
	float sunlit = lerp(1.0, visible, ShadowStrength) * lerp(1.0, occlusion, 0.5);
	float sun = facing * sunlit;

	float highlight = pow(saturate(dot(normal, normalize(SunDirection + view))), 40.0) * specular * sun;
	float3 ambient = lerp(GroundLight, SkyLight, normal.y * 0.5 + 0.5) * occlusion;
	float rim = pow(1.0 - saturate(dot(face, view)), 4.0) * occlusion * sheen;

	// The mirror image of the surroundings, with the sun as a glint, sharper and brighter the smoother
	// the surface. Metal mirrors all the light in its own colour (blurred if rough), other materials
	// 4 % of it and only if smooth; all of them mirror more at grazing angles. Metal has no diffuse
	// light, its glint takes the place of the highlight.
	float smoothness = 1.0 - roughness;
	float3 reflected = reflect(-view, normal);
	float3 reflectance = lerp(0.04, albedo, metallic);
	reflectance += (1.0 - reflectance) * pow(1.0 - saturate(dot(normal, view)), 5.0);
	float glintPower = exp2(4.0 + smoothness * 7.0);
	float glint = pow(saturate(dot(reflected, SunDirection)), glintPower) * glintPower * 0.05 * sunlit;
	float3 mirror = (Mirrored(reflected, roughness) * occlusion + SunColor * glint) * reflectance * lerp(smoothness, 1.0, metallic);
	float diffuse = 1.0 - metallic;
	highlight *= diffuse;
	rim *= diffuse;

	float3 spot = 0.0;
	for (int i = 0; i < LIGHTS; i++)
	{
		float3 toLight = input.ToCar + LightOffset[i];
		float lightDistance = length(toLight);
		toLight /= max(lightDistance, 0.001);
		float cone = saturate((dot(-toLight, LightDirection[i]) - LightCone[i].x) * LightCone[i].y);
		float fade = saturate(1.0 - lightDistance / LightRange[i]);
		spot += LightColor[i] * (saturate(dot(normal, toLight)) * cone * fade * fade);
	}

	// The light of the sky and the ground on the mud in the hue of the sun, half of it: the blue of the sky in the shade
	// and the warm sun made one mud look grey in the shade and tan in the sun
	float3 sunHue = SunColor / max(dot(SunColor, float3(0.3, 0.59, 0.11)), 0.001);
	float3 mudAmbient = lerp(ambient, dot(ambient, float3(0.3, 0.59, 0.11)) * sunHue, 0.5) * MUD_AMBIENT;
	float3 light = albedo * diffuse * (SunColor * sun * lerp(1.0, MUD_SUN, softened) + lerp(ambient, mudAmbient, softened) + spot)
		+ SunColor * highlight + RimLight * rim + mirror + EmissiveColor * (1.0 - softened);
	float3 color = pow(Filmic(light * Exposure), 1.0 / 2.2);

	float haze = saturate((distance - Haze.x) * Haze.y) * Haze.z;
	// Glass lets the scene through, except where it reflects the sun or the sky at a grazing angle
	float opacity = saturate(Alpha + highlight + rim * 0.5);
	return float4(lerp(color, HazeColor, haze), opacity);
}

float4 MainPS(VertexOutput input) : COLOR0
{
	return Shade(input, true, SURFACE_NONE, SURFACE_LOW, false);
}

float4 CoarseShadowsPS(VertexOutput input) : COLOR0
{
	return Shade(input, false, SURFACE_NONE, SURFACE_LOW, false);
}

// The scene lit, with smooth shadows (desktops)
technique Lit
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};

// The same with the coarse filter of the shadows (phones, see Sunlight)
technique LitCoarseShadows
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL CoarseShadowsPS();
	}
};

// The techniques of the surfaces: Lit<Surface><Detail> with the smooth shadows, LitCoarseShadows<Surface><Detail> with
// the coarse ones (see Shade)
#define SURFACE_TECHNIQUE(name, smooth, surface, detail) 	float4 name##PS(VertexOutput input) : COLOR0 	{ 		return Shade(input, smooth, surface, detail, false); 	} 	technique name 	{ 		pass P0 		{ 			VertexShader = compile VS_SHADERMODEL MainVS(); 			PixelShader = compile PS_SHADERMODEL name##PS(); 		} 	};

#define SURFACE_TECHNIQUES(name, surface) 	SURFACE_TECHNIQUE(Lit##name##Low, true, surface, SURFACE_LOW) 	SURFACE_TECHNIQUE(Lit##name##Medium, true, surface, SURFACE_MEDIUM) 	SURFACE_TECHNIQUE(Lit##name##High, true, surface, SURFACE_HIGH) 	SURFACE_TECHNIQUE(LitCoarseShadows##name##Low, false, surface, SURFACE_LOW) 	SURFACE_TECHNIQUE(LitCoarseShadows##name##Medium, false, surface, SURFACE_MEDIUM) 	SURFACE_TECHNIQUE(LitCoarseShadows##name##High, false, surface, SURFACE_HIGH)

SURFACE_TECHNIQUES(Asphalt, SURFACE_ASPHALT)
SURFACE_TECHNIQUES(Earth, SURFACE_EARTH)
SURFACE_TECHNIQUES(Grass, SURFACE_GRASS)

// The techniques of the mud: Lit<Mud><Detail> and LitCoarseShadowsMud<Detail>, Medium and High
#define MUD_TECHNIQUE(name, smooth, detail) 	float4 name##PS(VertexOutput input) : COLOR0 	{ 		return Shade(input, smooth, SURFACE_NONE, detail, true); 	} 	technique name 	{ 		pass P0 		{ 			VertexShader = compile VS_SHADERMODEL MudVS(); 			PixelShader = compile PS_SHADERMODEL name##PS(); 		} 	};

MUD_TECHNIQUE(LitMudMedium, true, SURFACE_MEDIUM)
MUD_TECHNIQUE(LitMudHigh, true, SURFACE_HIGH)
MUD_TECHNIQUE(LitCoarseShadowsMudMedium, false, SURFACE_MEDIUM)
MUD_TECHNIQUE(LitCoarseShadowsMudHigh, false, SURFACE_HIGH)

struct DepthOutput
{
	float4 Position : SV_POSITION;
	float Depth : TEXCOORD0;
};

DepthOutput DepthVS(VertexInput input)
{
	DepthOutput output;
	output.Position = mul(mul(input.Position, World), LightViewProjection);
	output.Depth = output.Position.z;
	return output;
}

float4 DepthPS(DepthOutput input) : COLOR0
{
	float4 packed = frac(float4(1.0, 255.0, 65025.0, 16581375.0) * min(input.Depth, 0.99999));
	return packed - packed.yzww * float4(1.0 / 255.0, 1.0 / 255.0, 1.0 / 255.0, 0.0);
}

technique Depth
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL DepthVS();
		PixelShader = compile PS_SHADERMODEL DepthPS();
	}
};

struct NormalDepthOutput
{
	float4 Position : SV_POSITION;
	float3 Normal : TEXCOORD0;     // in view space
	float Depth : TEXCOORD1;       // distance in front of the camera, as a part of OcclusionDepthRange
};

NormalDepthOutput NormalDepthVS(VertexInput input)
{
	NormalDepthOutput output;
	float4 world = mul(input.Position, World);
	output.Position = mul(world, ViewProjection);
	output.Normal = mul(mul(input.Normal, (float3x3)World), (float3x3)View);
	output.Depth = -mul(world, View).z / OcclusionDepthRange;
	return output;
}

// The normal in view space as x and y (z faces the camera), the depth in 16 bits
float4 NormalDepthPS(NormalDepthOutput input) : COLOR0
{
	float2 normal = normalize(input.Normal).xy * 0.5 + 0.5;
	float depth = saturate(input.Depth) * 255.0;
	return float4(normal, floor(depth) / 255.0, frac(depth));
}

technique NormalDepth
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL NormalDepthVS();
		PixelShader = compile PS_SHADERMODEL NormalDepthPS();
	}
};

// The sky is drawn by a quad over the whole screen; each corner gets the direction from the camera
struct SkyInput
{
	float4 Position : POSITION0;   // the corner of the screen, -1 to 1
	float2 TexCoord : TEXCOORD0;   // the same, 0 to 1 from the top left
};

struct SkyOutput
{
	float4 Position : SV_POSITION;
	float3 Direction : TEXCOORD0;  // from the camera
	float2 Pixel : TEXCOORD1;      // the pixel of the screen
};

SkyOutput SkyVS(SkyInput input)
{
	SkyOutput output;
	output.Position = float4(input.Position.xy, 0.5, 1.0);
	// A point of the scene seen in that corner (any depth gives the same direction)
	float4 world = mul(float4(input.Position.xy, 0.5, 1.0), InverseViewProjection);
	output.Direction = world.xyz / world.w - CameraPosition;
	output.Pixel = input.TexCoord * SkyScreenSize;
	return output;
}

float4 SkyPS(SkyOutput input) : COLOR0
{
	float3 direction = normalize(input.Direction);
	// How far above the horizon the direction points (the horizon may lie below the eye)
	float up = direction.y + HorizonDip;

	// The ramp from the horizon up, fast near the horizon and slow overhead
	float3 sky = lerp(SkyHorizon, SkyZenith, sqrt(saturate(up)));

	// Stars: in some cells of a grid laid over the dome (seen from below it, so that they stay round) a point at a
	// random place, fading towards the horizon
	float2 dome = direction.xz / (1.0 + direction.y) * 180.0;
	float2 cell = floor(dome);
	float random = tex2D(NoiseSampler, (cell + 0.5) / NOISE_SIZE).g;
	float2 place = cell + 0.5 + (float2(frac(random * 13.0), frac(random * 29.0)) - 0.5) * 0.6;
	float star = saturate((random - 0.94) / 0.06) * saturate(1.0 - length(dome - place) / 0.22);
	sky += Stars * star * smoothstep(0.03, 0.25, up);

	// The glow around the sun and its disc
	float facing = dot(direction, SkySun);
	sky += SunGlow * pow(saturate(facing), 12.0);
	sky = lerp(sky, SunDisc, smoothstep(SunDiscCos - 0.00015, SunDiscCos + 0.00015, facing) * step(0.001, dot(SunDisc, 1.0)));

	// Clouds on a plane above: their thickness here and a little towards the sun (thicker there, this side is shaded)
	float2 hit = direction.xz / max(up, 0.04) * 0.18 + CloudDrift;
	float thickness = tex2D(NoiseSampler, hit).r;
	float towardsSun = tex2D(NoiseSampler, hit + SkySun.xz * 0.025).r;
	float cloud = smoothstep(1.0 - CloudCover, 1.0 - CloudCover + 0.25, thickness) * smoothstep(0.02, 0.16, up);
	float3 cloudColour = lerp(CloudLight, CloudShade, saturate((towardsSun - thickness) * 4.0 + 0.35));
	cloudColour += SunGlow * pow(saturate(facing), 6.0) * 0.6;
	sky = lerp(sky, cloudColour, cloud * step(0.001, CloudCover));

	// Distant hills along the horizon, and the ground below them, hazy where they are far
	float azimuth = atan2(direction.x, direction.z);
	float hills = 0.022 + 0.011 * sin(azimuth * 3.0 + 1.3) + 0.006 * sin(azimuth * 7.0 + 0.4) + 0.003 * sin(azimuth * 19.0 + 2.1);
	float below = saturate(-up / 0.25);
	float3 land = lerp(lerp(SkyHills, SkyHorizon, 0.35 * saturate(1.0 - below * 4.0)), SkyGround, sqrt(below));
	sky = lerp(sky, land, smoothstep(hills + 0.0015, hills - 0.0015, up));

	// Dithering of a part of a step of the screen's colours, against bands in the ramps
	float dither = tex2D(NoiseSampler, input.Pixel / NOISE_SIZE).g - 0.5;
	return float4(sky + dither / 255.0, 1.0);
}

technique Sky
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL SkyVS();
		PixelShader = compile PS_SHADERMODEL SkyPS();
	}
};
