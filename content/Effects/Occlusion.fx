// Ambient occlusion computed from the normals and depths seen by the camera (screen-space
// ambient occlusion): how much the surroundings of a point hide the sky from it. Drawn at half
// the resolution of the screen, over the whole screen (the vertices are the corners of the
// screen in clip space with their texture coordinates).
//
// Technique Occlusion samples 8 points around each pixel, turned by a 4 x 4 pattern;
// technique Blur averages 4 x 4 pixels of the same depth, which removes the pattern.

#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0_level_9_3
	#define PS_SHADERMODEL ps_4_0_level_9_3
#endif

float2 TargetSize;          // size of the half resolution textures in pixels
float2 TanHalfFov;          // tangents of half the horizontal and vertical field of view
float DepthRange;           // depths are stored as a part of this distance
float Radius;               // world units around a point that can hide it
float Intensity;
float Bias;                 // ignored part of the radius, against self-occlusion of flat faces

texture NormalDepthMap;
sampler2D NormalDepthSampler = sampler_state
{
	Texture = <NormalDepthMap>;
	MinFilter = Point;
	MagFilter = Point;
	MipFilter = None;
	AddressU = Clamp;
	AddressV = Clamp;
};

texture OcclusionMap;
sampler2D OcclusionSampler = sampler_state
{
	Texture = <OcclusionMap>;
	MinFilter = Point;
	MagFilter = Point;
	MipFilter = None;
	AddressU = Clamp;
	AddressV = Clamp;
};

struct ScreenVertex
{
	float4 Position : POSITION0;
	float2 TexCoord : TEXCOORD0;
};

struct ScreenOutput
{
	float4 Position : SV_POSITION;
	float2 TexCoord : TEXCOORD0;
};

ScreenOutput ScreenVS(ScreenVertex input)
{
	ScreenOutput output;
	output.Position = input.Position;
	output.TexCoord = input.TexCoord;
	return output;
}

float DepthAt(float2 uv)
{
	float4 sample = tex2D(NormalDepthSampler, uv);
	return (sample.b + sample.a / 255.0) * DepthRange;
}

// Position in view space of the point seen at the texture coordinates at the depth
float3 ViewPosition(float2 uv, float depth)
{
	float2 ndc = float2(uv.x * 2.0 - 1.0, 1.0 - uv.y * 2.0);
	return float3(ndc * TanHalfFov * depth, -depth);
}

float4 OcclusionPS(ScreenOutput input) : COLOR0
{
	float4 center = tex2D(NormalDepthSampler, input.TexCoord);
	float depth = (center.b + center.a / 255.0) * DepthRange;

	float2 normalXY = center.rg * 2.0 - 1.0;
	float3 normal = float3(normalXY, sqrt(saturate(1.0 - dot(normalXY, normalXY))));
	float3 position = ViewPosition(input.TexCoord, depth);

	// The radius on the screen (in texture coordinates) at this depth
	float2 screenRadius = Radius / (TanHalfFov * depth) * 0.5;

	// One of 16 turns of the sample pattern, repeating every 4 x 4 pixels
	float2 pixel = floor(input.TexCoord * TargetSize);
	float turn = (frac(pixel.x * 0.25) * 4.0 + frac(pixel.y * 0.25) * 16.0) * (6.2831853 / 16.0);

	float occlusion = 0.0;
	for (int i = 0; i < 8; i++)
	{
		// A spiral of points filling the disk evenly
		float angle = i * 2.3999632 + turn;
		float distance = sqrt((i + 0.5) / 8.0);
		float2 uv = input.TexCoord + float2(cos(angle), sin(angle)) * distance * screenRadius;
		float3 toSample = ViewPosition(uv, DepthAt(uv)) - position;
		float squared = dot(toSample, toSample);
		// Points above the surface hide it, the closer the more; points too far away do not count
		float inRange = step(squared, Radius * Radius);
		occlusion += inRange * max(0.0, dot(toSample, normal) - Bias * Radius) / (squared + 0.01 * Radius * Radius);
	}
	float visible = saturate(1.0 - Intensity * Radius * occlusion / 8.0);
	// Nothing drawn here (the sky), or too far
	visible = depth < DepthRange * 0.99 ? visible : 1.0;
	return float4(visible, visible, visible, 1.0);
}

technique Occlusion
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL ScreenVS();
		PixelShader = compile PS_SHADERMODEL OcclusionPS();
	}
};

float4 BlurPS(ScreenOutput input) : COLOR0
{
	float depth = DepthAt(input.TexCoord);
	float2 texel = 1.0 / TargetSize;
	float sum = 0.0;
	float weight = 0.0;
	for (int y = -2; y < 2; y++)
	{
		for (int x = -2; x < 2; x++)
		{
			float2 uv = input.TexCoord + float2(x, y) * texel;
			// Only pixels of a similar depth, so the occlusion does not spread over the edges of objects
			float w = step(abs(DepthAt(uv) - depth), depth * 0.05 + 0.02);
			sum += tex2D(OcclusionSampler, uv).r * w;
			weight += w;
		}
	}
	float visible = weight > 0.0 ? sum / weight : 1.0;
	return float4(visible, visible, visible, 1.0);
}

technique Blur
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL ScreenVS();
		PixelShader = compile PS_SHADERMODEL BlurPS();
	}
};
