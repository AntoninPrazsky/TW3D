// The surfaces of the blocks: a part of a model whose material names a surface (asphalt, earth; see
// tools/ContentPipeline/MtlModelProcessor.cs) gets its detail per pixel here, from the noise the game computes into the
// texture SurfaceNoise (Graphics/SurfaceNoise.cs): its colour varies and its relief tilts the normal, while the shape,
// the light and the shadows stay those of the scene. No picture of a surface is drawn.
//
// Where a point is: the vertex shader passes its position in cells of the level grid from the middle of its block's
// cell (SurfaceOrigin), small numbers that the half precision of a phone's pixel shader keeps (a position in the world
// of a long level it would not). The noise repeats exactly once per cell, so a surface joins the surface of the block
// next to it without a seam, and a turned block is no different. What varies from block to block comes from random
// values of the eight corners of the cell (SurfaceSeedsLow, SurfaceSeedsHigh), which a neighbour shares, blended across
// the cell. A face takes the noise on the plane it is turned to most (a top on x and z, a front on x and y, a side on z
// and y), once per cell (the coarse lookup) and SURFACE_FINE times (the fine lookup).
//
// The detail (SURFACE_LOW, SURFACE_MEDIUM, SURFACE_HIGH, the Surface detail row of the Options menu) and the surface are
// numbers written in the technique, so the compiler keeps only what one of them needs and nothing branches in the
// pixel shader (Shader Model 3.0 refuses a derivative in a branch). The mipmaps of the noise fade the detail out in the
// distance; the edges of what a threshold cuts out (stones, pebbles) are softened by the footprint of the pixel
// (fwidth), as multisampling smooths only the edges of polygons.
//
// Functions and constants only: the uniforms they read are declared by the includer (Scene.fx), before it includes
// this file.

#define SURFACE_NONE 0
#define SURFACE_ASPHALT 1
#define SURFACE_EARTH 2
#define SURFACE_GRASS 3

#define SURFACE_LOW 0
#define SURFACE_MEDIUM 1
#define SURFACE_HIGH 2

// How many times the fine lookup repeats along a cell (a whole number, so that it repeats once per cell too)
#define SURFACE_FINE 4.0

// The look of a surface at a pixel: its colour (linear), its normal and how much it shines (a factor of the highlight)
struct SurfaceLook
{
	float3 Albedo;
	float3 Normal;
	float Shine;
};

// Which plane a face takes the noise on, by its normal: x = the top or the bottom (x and z), y = the front or the back
// (x and y), z = a side (z and y); one of them is 1
float3 SurfaceAxes(float3 normal)
{
	float3 a = abs(normal);
	float top = step(max(a.x, a.z), a.y);
	float front = (1.0 - top) * step(a.x, a.z);
	return float3(top, front, 1.0 - top - front);
}

// For the vertex shader: the position of a point in cells from the middle of its block's cell, and its coordinates on
// the plane of its face for the coarse lookup (xy) and the fine one (zw)
void SurfaceCoordinates(float3 world, float3 normal, out float3 local, out float4 plane)
{
	local = (world - SurfaceOrigin) / SurfaceCell;
	float3 axes = SurfaceAxes(normal);
	float2 onPlane = axes.x * local.xz + axes.y * local.xy + axes.z * local.zy;
	plane = float4(onPlane, onPlane * SURFACE_FINE);
}

// The random value of the block's cell at a point: the values of its eight corners blended smoothly across it, the
// same at the edge of the cell as in the cell next to it
float SurfaceSeed(float3 local)
{
	float3 w = saturate(local + 0.5);
	w = w * w * (3.0 - 2.0 * w);
	// The corners at the height of the point: (x-, z-), (x+, z-), (x-, z+), (x+, z+)
	float4 corners = lerp(SurfaceSeedsLow, SurfaceSeedsHigh, w.y);
	float2 alongX = lerp(corners.xz, corners.yw, w.x);
	return lerp(alongX.x, alongX.y, w.z);
}

// The normal tilted by the slope of a relief (along the two axes of the plane of the noise)
float3 SurfaceTilt(float3 normal, float2 slope)
{
	float3 axes = SurfaceAxes(normal);
	float3 u = float3(axes.x + axes.y, 0.0, axes.z);
	float3 v = float3(0.0, axes.y + axes.z, axes.x);
	// Into the plane of the face, which need not be flat (a slope)
	u = normalize(u - normal * dot(u, normal));
	v = normalize(v - normal * dot(v, normal));
	return normalize(normal - slope.x * u - slope.y * v);
}

// The slope of the grain of a lookup of the noise (green and blue), -1 to 1
float2 SurfaceSlope(float4 noise)
{
	return noise.gb * 2.0 - 1.0;
}

// 1 where a field is below an edge, 0 above it, the boundary softened by the footprint of the pixel
float SurfaceBelow(float value, float edge, float softness)
{
	float width = fwidth(value) + softness;
	return 1.0 - smoothstep(edge - width, edge + width, value);
}

// The fine lookup; at the high detail two of them, the second turned and moved, which take turns by the coarse mottle,
// so that the fine pattern does not show its repeating four times along a cell
float4 SurfaceFine(sampler2D noise, float4 plane, float4 coarse, int detail)
{
	float4 fine = tex2D(noise, plane.zw);
	if (detail >= SURFACE_HIGH)
	{
		float4 other = tex2D(noise, plane.wz + 0.5);
		other.gb = other.bg;
		fine = lerp(fine, other, smoothstep(0.42, 0.58, coarse.r));
	}
	return fine;
}

// Asphalt: small stones of the aggregate, each lighter or darker, in a binder a little darker than the base colour,
// showing more where the asphalt is worn; patches worn lighter or mended darker, different on every block; a fine
// relief of the grain
SurfaceLook SurfaceAsphalt(sampler2D noise, float3 base, float3 normal, float4 plane, float3 local, int detail)
{
	float4 coarse = tex2D(noise, plane.xy);
	float seed = SurfaceSeed(local);
	float wear = saturate(coarse.r + (seed - 0.5) * 0.5);
	SurfaceLook look;
	look.Albedo = base * lerp(0.84, 1.16, wear);
	look.Normal = normal;
	look.Shine = 1.0;
	if (detail >= SURFACE_MEDIUM)
	{
		float4 fine = SurfaceFine(noise, plane, coarse, detail);
		float stones = SurfaceBelow(fine.a, 0.4, 0.05) * lerp(0.35, 0.7, wear);
		float3 binder = look.Albedo * lerp(0.82, 1.0, fine.r);
		look.Albedo = lerp(binder, look.Albedo * lerp(1.0, 1.8, fine.r), stones);
		look.Normal = SurfaceTilt(normal, SurfaceSlope(fine) * 0.4 + SurfaceSlope(coarse) * 0.08);
		look.Shine = lerp(0.8, 1.5, stones);
	}
	return look;
}

// Earth: drier and wetter patches, small clods with darker cracks between them, scattered grey pebbles and a rough
// relief; it hardly shines
SurfaceLook SurfaceEarth(sampler2D noise, float3 base, float3 normal, float4 plane, float3 local, int detail)
{
	float4 coarse = tex2D(noise, plane.xy);
	float seed = SurfaceSeed(local);
	float dry = smoothstep(0.25, 0.75, coarse.r + (seed - 0.5) * 0.6);
	SurfaceLook look;
	look.Albedo = base * lerp(0.7, 1.2, dry);
	look.Normal = normal;
	look.Shine = 0.3;
	if (detail >= SURFACE_MEDIUM)
	{
		float4 fine = SurfaceFine(noise, plane, coarse, detail);
		// The cracks between the clods: the edges of the cells of the coarse lookup
		look.Albedo *= lerp(0.86, 1.1, fine.r) * lerp(1.0, 0.78, smoothstep(0.42, 0.62, coarse.a));
		// Grey pebbles of many sizes (the points of the cellular noise have sizes, the small ones make none), more where dry
		float pebbles = SurfaceBelow(fine.a, 0.3, 0.04) * lerp(0.5, 1.0, dry);
		look.Albedo = lerp(look.Albedo, float3(0.11, 0.105, 0.095) * lerp(0.7, 1.3, fine.r), pebbles);
		look.Normal = SurfaceTilt(normal, SurfaceSlope(coarse) * 0.5 + SurfaceSlope(fine) * 0.3);
		look.Shine = lerp(0.3, 1.0, pebbles);
	}
	return look;
}

// Grass: a carpet of short blades, darker in the gaps between its clumps and between the blades, lighter and yellower
// where it is drier, different on every block, with a strong relief of the blades and a little sheen on their tips
SurfaceLook SurfaceGrass(sampler2D noise, float3 base, float3 normal, float4 plane, float3 local, int detail)
{
	float4 coarse = tex2D(noise, plane.xy);
	float seed = SurfaceSeed(local);
	float dry = smoothstep(0.35, 0.8, coarse.r + (seed - 0.5) * 0.5);
	SurfaceLook look;
	look.Albedo = lerp(base * 0.85, base * float3(1.35, 1.2, 0.8), dry);
	look.Normal = normal;
	look.Shine = 0.4;
	if (detail >= SURFACE_MEDIUM)
	{
		float4 fine = SurfaceFine(noise, plane, coarse, detail);
		// The gaps between the clumps: the edges of the cells of the coarse lookup
		float gaps = smoothstep(0.45, 0.7, coarse.a);
		look.Albedo *= lerp(0.6, 1.25, fine.r) * lerp(1.0, 0.75, gaps);
		look.Normal = SurfaceTilt(normal, SurfaceSlope(fine) * 0.8 + SurfaceSlope(coarse) * 0.25);
		look.Shine = lerp(0.2, 0.6, fine.r);
	}
	return look;
}

// The look of a surface (a number written in the technique, as the detail is)
SurfaceLook SurfaceOf(sampler2D noise, int surface, int detail, float3 base, float3 normal, float4 plane, float3 local)
{
	if (surface == SURFACE_EARTH)
		return SurfaceEarth(noise, base, normal, plane, local, detail);
	if (surface == SURFACE_GRASS)
		return SurfaceGrass(noise, base, normal, plane, local, detail);
	return SurfaceAsphalt(noise, base, normal, plane, local, detail);
}
