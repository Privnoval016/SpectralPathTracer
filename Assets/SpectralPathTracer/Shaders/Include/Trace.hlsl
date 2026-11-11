/**
 * <summary>
 * Finds where a ray hits the scene, and rebuilds the surface at that hit.
 * </summary>
 */

#ifndef SPT_TRACE_INCLUDED
#define SPT_TRACE_INCLUDED

#include "Paths/Rays.hlsl"
#include "SceneData.hlsl"

struct SurfaceHit
{
    float3 Position;
    float3 Normal;
    float3 Facing; // the normal on the side the ray arrived from
    bool FrontFace;
    GpuMaterial Material;
};

/**
 * <summary>
 * Finds the nearest object a ray hits.
 * </summary>
 * <param name="ray">The ray to trace.</param>
 * <returns>The hit record, with Hit 0 on a miss.</returns>
 */
GpuHit ClosestHit(GpuRay ray)
{
    GpuHit hit;
    hit.T = 0.0f;
    hit.Hit = 0u;
    hit.ObjectIndex = -1;

    // keep the nearest hit across every object; with no objects the loop never runs and the ray misses
    for (uint i = 0; i < _ObjectCount; i++)
    {
        float t;
        bool hitObject = IntersectObject(LoadObject(_Objects, i), ray.Origin, ray.Direction, 0.0f, t);

        if (hitObject && (hit.Hit == 0u || t < hit.T))
        {
            hit.T = t;
            hit.Hit = 1u;
            hit.ObjectIndex = (int)i;
        }
    }

    return hit;
}

/**
 * <summary>
 * Rebuilds the surface a ray hit from the ray and its hit record.
 * </summary>
 * <param name="ray">The ray.</param>
 * <param name="hit">The ray's hit record.</param>
 * <returns>The surface at the hit, zeroed on a miss.</returns>
 */
SurfaceHit BuildSurfaceHit(GpuRay ray, GpuHit hit)
{
    SurfaceHit surface = (SurfaceHit)0;
    surface.FrontFace = true;

    if (hit.Hit != 0u)
    {
        GpuObject hitObject = LoadObject(_Objects, hit.ObjectIndex);
        surface.Position = ray.Origin + hit.T * ray.Direction;
        surface.Normal = ObjectNormal(hitObject, surface.Position);
        surface.FrontFace = dot(surface.Normal, ray.Direction) < 0.0f;
        surface.Facing = surface.FrontFace ? surface.Normal : -surface.Normal;
        surface.Material = LoadMaterial(_Materials, hitObject.MaterialIndex);
    }

    return surface;
}

#endif
