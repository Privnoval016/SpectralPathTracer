/**
 * <summary>
 * Reads and writes a path's state and flags, so kernels never touch the buffer layout directly.
 * </summary>
 */

#ifndef SPT_PATH_STATE_INCLUDED
#define SPT_PATH_STATE_INCLUDED

#include "../GpuData.hlsl"

/**
 * <summary>
 * Whether a path is still being traced.
 * </summary>
 * <param name="path">The path's state.</param>
 * <returns>True until the path finishes.</returns>
 */
bool IsActive(GpuPathState path)
{
    return (path.Flags & PATHFLAGS_ACTIVE) != 0u;
}

/**
 * <summary>
 * Marks a path finished; later stages skip it.
 * </summary>
 * <param name="path">The path's state, changed in place.</param>
 */
void Finish(inout GpuPathState path)
{
    path.Flags &= ~PATHFLAGS_ACTIVE;
}

/**
 * <summary>
 * Reads a path's state.
 * </summary>
 * <param name="paths">The buffer to read from.</param>
 * <param name="index">The path's index, matching its ray's index.</param>
 * <returns>The path's state.</returns>
 */
GpuPathState LoadPath(StructuredBuffer<GpuPathState> paths, uint index)
{
    return paths[index];
}

/**
 * <summary>
 * Reads a path's state from a buffer the kernel also writes.
 * </summary>
 * <param name="paths">The buffer to read from.</param>
 * <param name="index">The path's index, matching its ray's index.</param>
 * <returns>The path's state.</returns>
 */
GpuPathState LoadPath(RWStructuredBuffer<GpuPathState> paths, uint index)
{
    return paths[index];
}

/**
 * <summary>
 * Writes a path's state.
 * </summary>
 * <param name="paths">The buffer to write to.</param>
 * <param name="index">The path's index, matching its ray's index.</param>
 * <param name="path">The state to write.</param>
 */
void StorePath(RWStructuredBuffer<GpuPathState> paths, uint index, GpuPathState path)
{
    paths[index] = path;
}

#endif