/**
 * <summary>
 * The stateless hash behind every random number.
 * </summary>
 */

#ifndef SPT_HASH_INCLUDED
#define SPT_HASH_INCLUDED

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
