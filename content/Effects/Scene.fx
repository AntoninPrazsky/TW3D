// Lighting of the 3D scene. The models are polygons of a single colour each (no textures),
// so the light makes the whole look: the sun with its shadows, the light of the sky from above
// and the light reflected by the ground from below (both dimmed in corners and crevices by
// ambient occlusion), a highlight of the sun and a rim of sky light at grazing angles. It is
// computed in linear colour space and mapped to the screen by a filmic curve; the distance
// fades into the colour of the sky.
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

struct VertexInput
{
	float4 Position : POSITION0;
	float3 Normal : NORMAL0;
};

// Phones run pixel shaders in half precision, too coarse for positions in a long level:
// the vector to the camera and the position in the shadow map are computed here,
// in the vertex shader, where they are still precise
struct VertexOutput
{
	float4 Position : SV_POSITION;
	float3 ToCamera : TEXCOORD0;
	float3 Normal : TEXCOORD1;
	float3 Shadow : TEXCOORD2;     // texture coordinates and depth in the shadow map
	float4 Screen : TEXCOORD3;     // position on the screen (clip space)
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

float4 MainPS(VertexOutput input) : COLOR0
{
	float3 normal = normalize(input.Normal);
	float distance = length(input.ToCamera);
	float3 view = input.ToCamera / distance;

	float sun = saturate(dot(normal, SunDirection));
	float visible = SunVisibility(input.Shadow, ShadowBias * (3.0 - 2.0 * sun));
	sun *= lerp(1.0, visible, ShadowStrength);

	// Ambient occlusion darkens the light of the sky and the ground, and a little the sun,
	// which makes the contact of objects visible in the sun too
	float2 screen = input.Screen.xy / input.Screen.w * float2(0.5, -0.5) + 0.5;
	float occlusion = lerp(1.0, tex2D(OcclusionSampler, screen).r, OcclusionStrength);
	sun *= lerp(1.0, occlusion, 0.5);

	float highlight = pow(saturate(dot(normal, normalize(SunDirection + view))), 40.0) * Specular * sun;
	float3 ambient = lerp(GroundLight, SkyLight, normal.y * 0.5 + 0.5) * occlusion;
	float rim = pow(1.0 - saturate(dot(normal, view)), 4.0) * occlusion;

	float3 light = DiffuseColor * (SunColor * sun + ambient) + SunColor * highlight + RimLight * rim + EmissiveColor;
	float3 color = pow(Filmic(light * Exposure), 1.0 / 2.2);

	float haze = saturate((distance - Haze.x) * Haze.y) * Haze.z;
	return float4(lerp(color, HazeColor, haze), 1.0);
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
