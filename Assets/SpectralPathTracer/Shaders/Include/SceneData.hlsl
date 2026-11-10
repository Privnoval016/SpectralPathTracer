#ifndef SPT_SCENE_DATA_INCLUDED
#define SPT_SCENE_DATA_INCLUDED

#include "../../Runtime/Rendering/GpuObject.cs.hlsl"
#include "../../Runtime/Rendering/GpuMaterial.cs.hlsl"

/**
 * <summary>
 * Reads an object record from the scene's object buffer.
 * </summary>
 * <param name="objects">The buffer to read from.</param>
 * <param name="index">The object's index.</param>
 * <returns>The object at the given index.</returns>
 */
GpuObject LoadObject(StructuredBuffer<GpuObject> objects, uint index)
{
    return objects[index];
}

/**
 * <summary>
 * Reads a material from the scene's material table.
 * </summary>
 * <param name="materials">The buffer to read from.</param>
 * <param name="index">The material's index, as stored on an object.</param>
 * <returns>The material at the given index.</returns>
 */
GpuMaterial LoadMaterial(StructuredBuffer<GpuMaterial> materials, uint index)
{
    return materials[index];
}

#endif