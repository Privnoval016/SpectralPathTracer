/**
 * <summary>
 * Scene object and material records, and the per-shape choice of intersection and normal.
 * </summary>
 */

#ifndef SPT_SCENE_DATA_INCLUDED
#define SPT_SCENE_DATA_INCLUDED

#include "Shapes/Quadric.hlsl"
#include "Shapes/Quad.hlsl"
#include "../../Runtime/Rendering/GpuData/ShapeType.cs.hlsl"
#include "../../Runtime/Rendering/GpuData/GpuObject.cs.hlsl"
#include "../../Runtime/Rendering/GpuData/GpuMaterial.cs.hlsl"

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

/**
 * <summary>
 * Computes the intersection of a ray with an object, which can be a quadric or a quad.
 * </summary>
 * <param name="obj">The object to test for intersection.</param>
 * <param name="origin">The origin of the ray in world space.</param>
 * <param name="direction">The direction of the ray in world space.</param>
 * <param name="tmin">The minimum valid t value for the intersection.</param>
 * <param name="t">The computed t value for the intersection, if it exists.</param>
 * <returns>True if the ray intersects the object, false otherwise.</returns>
 */
bool IntersectObject(GpuObject obj, float3 origin, float3 direction, float tmin, out float t)
{
    bool hit = false;
    if (obj.Type == SHAPETYPE_QUADRIC)
    {
        hit = IntersectQuadric(obj.Quadric, origin, direction, tmin, t);
    }
    else
    {
        hit = IntersectQuad(obj.WorldToLocal, origin, direction, tmin, t);
    }

    return hit;
}

/**
 * <summary>
 * Computes the surface normal at a given position on an object, which can be a quadric or a quad.
 * </summary>
 * <param name="obj">The object to compute the normal for.</param>
 * <param name="position">The position on the object's surface in world space.</param>
 * <returns>The normalized surface normal at the given position.</returns>
 */
float3 ObjectNormal(GpuObject obj, float3 position)
{
    float3 normal = float3(0.0f, 0.0f, 0.0f);
    if (obj.Type == SHAPETYPE_QUADRIC)
    {
        normal = QuadricNormal(obj.Quadric, position);
    }
    else
    {
        normal = QuadNormal(obj.WorldToLocal, position);
    }

    return normal;
}

#endif