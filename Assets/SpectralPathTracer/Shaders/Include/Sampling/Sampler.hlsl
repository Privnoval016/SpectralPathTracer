/**
 * <summary>
 * The map of which dimension each random decision draws from, and the pixel each draw is keyed by.
 * </summary>
 */

#ifndef SPT_SAMPLER_INCLUDED
#define SPT_SAMPLER_INCLUDED

#include "../GpuData.hlsl"
#include "Hash.hlsl"

// fixed dimension mapping for pixel jittering
static const uint DIMENSION_PIXEL_JITTER_X = 0u;
static const uint DIMENSION_PIXEL_JITTER_Y = 1u;

static const uint DIMENSION_BOUNCE_BASE = 2u; // each bounce uses 2 dimensions (u, v)
static const uint DIMENSIONS_PER_BOUNCE = 16u;

// the top 8 bits of a dimension are reserved for a stream id; the camera path is stream 0
static const uint DIMENSION_STREAM_SHIFT = 24u;

// mapping slots so each of these get its own dimension in the hash function, so the dimensions are consistent across bounces and kernels
static const uint SLOT_BSDF_U = 0u;
static const uint SLOT_BSDF_V = 1u;
static const uint SLOT_RUSSIAN_ROULETTE = 2u;
static const uint SLOT_WAVELENGTH = 3u; // drawn once per sample, so only depth 0 uses it

static const uint GOLDEN_RATIO_STEP = 2654435769u; // 2^32 / phi: gyro! its the golden spin! STEEL BALL RUN STEEL BALL RUN

uint BounceDimension(uint depth, uint slot)
{
    return DIMENSION_BOUNCE_BASE + depth * DIMENSIONS_PER_BOUNCE + slot;
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

/**
 * <summary>
 * Draws the two numbers that choose a bounce's direction.
 * </summary>
 * <param name="outputPixel">The pixel coordinates in the output buffer.</param>
 * <param name="sampleIndex">The path's sample index.</param>
 * <param name="depth">The bounce depth.</param>
 * <returns>A 2D uniform random sample in the range [0, 1).</returns>
 */
float2 SampleBsdf2D(uint2 outputPixel, uint sampleIndex, uint depth)
{
    return float2(SampleUniform(outputPixel, sampleIndex, BounceDimension(depth, SLOT_BSDF_U)),
                  SampleUniform(outputPixel, sampleIndex, BounceDimension(depth, SLOT_BSDF_V)));
}

/**
 * <summary>
 * Draws a number whose values over a pixel's samples spread evenly across [0, 1), starting at a random point per pixel.
 * </summary>
 * <param name="outputPixel">The pixel coordinates in the output buffer.</param>
 * <param name="sampleIndex">The path's sample index.</param>
 * <param name="dimension">The decision's dimension, which picks the starting point.</param>
 * <returns>A uniform number in [0, 1), stratified across the pixel's samples.</returns>
 */
float SampleStratified(uint2 outputPixel, uint sampleIndex, uint dimension)
{
    // a random start per pixel so pixels don't draw the same value at the same sample
    uint start = Pcg4d(uint4(outputPixel, 0u, dimension)).x;
    return UintToUnitFloat(start + sampleIndex * GOLDEN_RATIO_STEP); // uint math wraps exactly
}

#endif