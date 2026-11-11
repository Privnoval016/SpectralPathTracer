/**
 * <summary>
 * The stateless random number source and the map of which dimension each use draws from.
 * </summary>
 */

#ifndef SPT_SAMPLER_INCLUDED
#define SPT_SAMPLER_INCLUDED

#include "../GpuData.hlsl"

// fixed dimension mapping for pixel jittering
static const uint DIMENSION_PIXEL_JITTER_X = 0u;
static const uint DIMENSION_PIXEL_JITTER_Y = 1u;

static const uint DIMENSION_BOUNCE_BASE = 2u; // each bounce uses 2 dimensions (u, v)
static const uint DIMENSIONS_PER_BOUNCE = 16u;

// mapping slots so each of these get its own dimension in the hash function, so the dimensions are consistent across bounces and kernels
static const uint SLOT_BSDF_U = 0u;
static const uint SLOT_BSDF_V = 1u;
static const uint SLOT_RUSSIAN_ROULETTE = 2u;

uint BounceDimension(uint depth, uint slot)
{
    return DIMENSION_BOUNCE_BASE + depth * DIMENSIONS_PER_BOUNCE + slot;
}

/**
 * https://jcgt.org/published/0009/03/02/paper.pdf funny hash function thats pretty good apparently
 */
uint4 Pcg4d(uint4 v)
{
    v = v * 1664525u + 1013904223u;
    v.x += v.y * v.w; v.y += v.z * v.x; v.z += v.x * v.y; v.w += v.y * v.z;
    v ^= v >> 16u;
    v.x += v.y * v.w; v.y += v.z * v.x; v.z += v.x * v.y; v.w += v.y * v.z;
    return v;
}

/**
 * <summary>
 * Computes the output pixel coordinates for a given render pixel, taking into account the output scale.
 * </summary>
 * <param name="renderPixel">The pixel coordinates in the render target.</param>
 * <returns>The corresponding pixel coordinates in the output buffer.</returns>
 */
uint2 OutputPixel(int2 renderPixel)
{
    return uint2((float2(renderPixel) + 0.5f) * _OutputScale);
}

float UintToUnitFloat(uint bits)
{
    return float(bits >> 8u) * (1.0f / 16777216.0f); // grabbing only the mantissa and normalizing 
}

/**
 * <summary>
 * Samples a uniform random float in the range [0, 1) based on the output pixel, sample index, and dimension.
 * </summary>
 * <param name="outputPixel">The pixel coordinates in the output buffer.</param>
 * <param name="sampleIndex">The index of the current sample.</param>
 * <param name="dimension">The dimension for which to sample (e.g., X or Y jitter).</param>
 * <returns>A uniform random float in the range [0, 1).</returns>
 */
float SampleUniform(uint2 outputPixel, uint sampleIndex, uint dimension)
{
    return UintToUnitFloat(Pcg4d(uint4(outputPixel, sampleIndex, dimension)).x); // arbitrarily picking x here instead of the other components
}

#endif