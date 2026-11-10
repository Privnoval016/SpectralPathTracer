/**
 * <summary>
 * Ray and hit records, and the accessors every kernel reads and writes them through.
 * </summary>
 */

#ifndef SPT_RAYS_INCLUDED
#define SPT_RAYS_INCLUDED

// kernels only touch the ray and hit buffers through the accessors below, so the layout can change later without touching any kernel

#include "../../Runtime/Rendering/GpuData/GpuRay.cs.hlsl"
#include "../../Runtime/Rendering/GpuData/GpuHit.cs.hlsl"

/**
 * <summary>
 * Reads a ray from a ray buffer.
 * </summary>
 * <param name="rays">The buffer to read from.</param>
 * <param name="index">The index of the ray in the buffer.</param>
 * <returns>The ray at the given index.</returns>
 */
GpuRay LoadRay(StructuredBuffer<GpuRay> rays, uint index)
{
    return rays[index];
}

/**
 * <summary>
 * Reads a ray from a ray buffer the kernel also writes.
 * </summary>
 * <param name="rays">The buffer to read from.</param>
 * <param name="index">The index of the ray in the buffer.</param>
 * <returns>The ray at the given index.</returns>
 */
GpuRay LoadRay(RWStructuredBuffer<GpuRay> rays, uint index)
{
    return rays[index];
}

/**
 * <summary>
 * Writes a ray into a ray buffer.
 * </summary>
 * <param name="rays">The buffer to write to.</param>
 * <param name="index">The index to write the ray at.</param>
 * <param name="ray">The ray to write.</param>
 */
void StoreRay(RWStructuredBuffer<GpuRay> rays, uint index, GpuRay ray)
{
    rays[index] = ray;
}

/**
 * <summary>
 * Reads a hit from a hit buffer.
 * </summary>
 * <param name="hits">The buffer to read from.</param>
 * <param name="index">The index of the hit in the buffer, matching its ray's index.</param>
 * <returns>The hit at the given index.</returns>
 */
GpuHit LoadHit(StructuredBuffer<GpuHit> hits, uint index)
{
    return hits[index];
}

/**
 * <summary>
 * Writes a hit into a hit buffer.
 * </summary>
 * <param name="hits">The buffer to write to.</param>
 * <param name="index">The index to write the hit at, matching its ray's index.</param>
 * <param name="hit">The hit to write.</param>
 */
void StoreHit(RWStructuredBuffer<GpuHit> hits, uint index, GpuHit hit)
{
    hits[index] = hit;
}

#endif