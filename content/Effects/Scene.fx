// Lighting of the 3D scene. The models are polygons of a single colour each (no textures),
// so the light makes the whole look: the sun with its shadows, the light of the sky from above
// and the light reflected by the ground from below (both dimmed in corners and crevices by
// ambient occlusion), a highlight of the sun, a rim of sky light at grazing angles and the
// lamps of the car (spot lights: the headlights at night, the rear lights). It is computed in linear colour space and
// mapped to the screen by a filmic curve; the distance fades into the colour of the sky.
// Glass (Alpha less than 1) is blended over the scene, less transparent where it shines.
// Smooth materials (Roughness less than 1) mirror the sky, the ground and the sun; metal
// (Metallic) only mirrors, in its own colour, like chrome.
//
// Technique Sky draws the sky: its picture recoloured by the colours of the light from the
// horizon up (it keeps the brightness of the picture, the clouds), with a glow around the sun.
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

// The lamps of the car, spot lights: two headlights, two rear lights. Their positions are given
// relative to the car (LightOrigin), the vector from a point to the car is computed in the vertex shader
#define CAR_LIGHTS 4
float3 LightOrigin;
float3 LightOffset[CAR_LIGHTS];     // position of the lamp - LightOrigin
float3 LightDirection[CAR_LIGHTS];  // unit vector
float3 LightColor[CAR_LIGHTS];      // linear; black = off
float2 LightCone[CAR_LIGHTS];       // cosine of the outer angle, 1 / (cosine of the inner angle - cosine of the outer one)
float LightRange[CAR_LIGHTS];

// The sky: the brightness of its picture in these colours (on the screen) at the horizon and above,
// mixed with the picture as it is by SkyRecolor (0 = the picture as it is)
float3 SkyHorizon;
float3 SkyZenith;
float SkyRecolor;
float3 SunGlow;
texture SkyTexture;
sampler2D SkySampler = sampler_state
{
	Texture = <SkyTexture>;
	MinFilter = Linear;
	MagFilter = Linear;
	MipFilter = Linear;
	AddressU = Wrap;
	AddressV = Wrap;
};

// Shadows
float4x4 LightViewProjection;
float ShadowTexel;          // size of a texel of the shadow map (in texture coordinates)
float ShadowBias;           // in shadow map depth, for surfaces facing the sun
float NormalOffset;         // world units the shadow lookup moves along the normal
float ShadowStrength;       // 0 = no shadows
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
};

VertexOutput MainVS(VertexInput input)
{
	VertexOutput output;
	float4 world = mul(input.Position, World);
	float3 normal = normalize(mul(input.Normal, (float3x3)World));
	output.Position = mul(world, ViewProjection);
	output.ToCamera = CameraPosition - world.xyz;
	output.Normal = normal;
	float4 shadow = mul(float4(world.xyz + normal * NormalOffset, 1.0), LightViewProjection);
	// (w is 1 for the orthographic view of the sun, but dividing by it keeps the whole matrix in the
	// shader: MonoGame shares the parameter between the shaders and sizes it by its first use)
	shadow.xyz /= shadow.w;
	output.Shadow = float3(shadow.x * 0.5 + 0.5, -shadow.y * 0.5 + 0.5, shadow.z);
	output.Screen = output.Position;
	output.ToCar = LightOrigin - world.xyz;
	return output;
}

float UnpackDepth(float4 packed)
{
	return dot(packed, float4(1.0, 1.0 / 255.0, 1.0 / 65025.0, 1.0 / 16581375.0));
}

// How much of the sun reaches the point: 3 x 3 samples of the shadow map for soft edges
float SunVisibility(float3 shadow, float bias)
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

float4 MainPS(VertexOutput input) : COLOR0
{
	float3 normal = normalize(input.Normal);
	float distance = length(input.ToCamera);
	float3 view = input.ToCamera / distance;

	float facing = saturate(dot(normal, SunDirection));
	float visible = SunVisibility(input.Shadow, ShadowBias * (3.0 - 2.0 * facing));

	// Ambient occlusion darkens the light of the sky and the ground, and a little the sun,
	// which makes the contact of objects visible in the sun too
	float2 screen = input.Screen.xy / input.Screen.w * float2(0.5, -0.5) + 0.5;
	float occlusion = lerp(1.0, tex2D(OcclusionSampler, screen).r, OcclusionStrength);
	float sunlit = lerp(1.0, visible, ShadowStrength) * lerp(1.0, occlusion, 0.5);
	float sun = facing * sunlit;

	float highlight = pow(saturate(dot(normal, normalize(SunDirection + view))), 40.0) * Specular * sun;
	float3 ambient = lerp(GroundLight, SkyLight, normal.y * 0.5 + 0.5) * occlusion;
	float rim = pow(1.0 - saturate(dot(normal, view)), 4.0) * occlusion;

	// The mirror image of the surroundings, with the sun as a glint, sharper and brighter the smoother
	// the surface. Metal mirrors all the light in its own colour (blurred if rough), other materials
	// 4 % of it and only if smooth; all of them mirror more at grazing angles. Metal has no diffuse
	// light, its glint takes the place of the highlight.
	float smoothness = 1.0 - Roughness;
	float3 reflected = reflect(-view, normal);
	float3 reflectance = lerp(0.04, DiffuseColor, Metallic);
	reflectance += (1.0 - reflectance) * pow(1.0 - saturate(dot(normal, view)), 5.0);
	float glintPower = exp2(4.0 + smoothness * 7.0);
	float glint = pow(saturate(dot(reflected, SunDirection)), glintPower) * glintPower * 0.05 * sunlit;
	float3 mirror = (Mirrored(reflected, Roughness) * occlusion + SunColor * glint) * reflectance * lerp(smoothness, 1.0, Metallic);
	float diffuse = 1.0 - Metallic;
	highlight *= diffuse;
	rim *= diffuse;

	float3 spot = 0.0;
	for (int i = 0; i < CAR_LIGHTS; i++)
	{
		float3 toLight = input.ToCar + LightOffset[i];
		float lightDistance = length(toLight);
		toLight /= max(lightDistance, 0.001);
		float cone = saturate((dot(-toLight, LightDirection[i]) - LightCone[i].x) * LightCone[i].y);
		float fade = saturate(1.0 - lightDistance / LightRange[i]);
		spot += LightColor[i] * (saturate(dot(normal, toLight)) * cone * fade * fade);
	}

	float3 light = DiffuseColor * diffuse * (SunColor * sun + ambient + spot) + SunColor * highlight + RimLight * rim + mirror + EmissiveColor;
	float3 color = pow(Filmic(light * Exposure), 1.0 / 2.2);

	float haze = saturate((distance - Haze.x) * Haze.y) * Haze.z;
	// Glass lets the scene through, except where it reflects the sun or the sky at a grazing angle
	float opacity = saturate(Alpha + highlight + rim * 0.5);
	return float4(lerp(color, HazeColor, haze), opacity);
}

technique Lit
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};

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

struct SkyInput
{
	float4 Position : POSITION0;
	float2 TexCoord : TEXCOORD0;
};

struct SkyOutput
{
	float4 Position : SV_POSITION;
	float2 TexCoord : TEXCOORD0;
	float3 Direction : TEXCOORD1;  // from the camera
};

SkyOutput SkyVS(SkyInput input)
{
	SkyOutput output;
	float4 world = mul(input.Position, World);
	output.Position = mul(world, ViewProjection);
	output.TexCoord = input.TexCoord;
	output.Direction = world.xyz - CameraPosition;
	return output;
}

float4 SkyPS(SkyOutput input) : COLOR0
{
	float3 direction = normalize(input.Direction);
	float3 picture = tex2D(SkySampler, input.TexCoord).rgb;
	float3 tint = lerp(SkyHorizon, SkyZenith, saturate(direction.y * 2.0));
	float3 recoloured = dot(picture, float3(0.3, 0.59, 0.11)) * tint;
	float3 glow = SunGlow * pow(saturate(dot(direction, SunDirection)), 12.0);
	return float4(lerp(picture, recoloured, SkyRecolor) + glow, 1.0);
}

technique Sky
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL SkyVS();
		PixelShader = compile PS_SHADERMODEL SkyPS();
	}
};
