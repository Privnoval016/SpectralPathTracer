/**
 * <summary>
 * What each AOV shows for a ray's first hit, in place of the rendered result.
 * </summary>
 */

#ifndef SPT_AOV_INCLUDED
#define SPT_AOV_INCLUDED

#include "Camera.hlsl"
#include "Rays.hlsl"
#include "Sampler.hlsl"
#include "Sampling.hlsl"
#include "../../Runtime/Rendering/Settings/Aov.cs.hlsl"

uint _Aov;
float2 _DepthRange;
uint _NoiseDimension;

/**
 * <summary>
 * Computes the color the selected AOV shows for one ray, and black where nothing was hit.
 * </summary>
 * <param name="ray">The ray, for its direction.</param>
 * <param name="hit">The ray's hit record.</param>
 * <param name="normal">The surface normal at the hit.</param>
 * <param name="frontFace">Whether the ray hit the outside of the surface.</param>
 * <returns>The AOV's color for this ray.</returns>
 */
float3 AovColor(GpuRay ray, GpuHit hit, float3 normal, bool frontFace)
{
    float3 color = float3(0.0f, 0.0f, 0.0f);

    if (_Aov == AOV_NOISE)
    {
        color = SampleUniform(OutputPixel(ray.Pixel), _SampleIndex, _NoiseDimension).xxx;
    }
    else if (hit.Hit != 0u)
    {
        if (_Aov == AOV_NORMALS)
            color = normal * 0.5f + 0.5f;
        else if (_Aov == AOV_DEPTH)
        {
            float depth = hit.T * dot(ray.Direction, CameraForward()); // planar depth along the camera's view axis
            color = saturate((depth - _DepthRange.x) / (_DepthRange.y - _DepthRange.x)).xxx;
        }
        else if (_Aov == AOV_FRONT_FACE)
            color = frontFace ? float3(0.0f, 1.0f, 0.0f) : float3(1.0f, 0.0f, 0.0f);
        else if (_Aov == AOV_BOUNCE_DIRECTION)
        {
            uint2 pixel = OutputPixel(ray.Pixel);
            float2 u = float2(SampleUniform(pixel, _SampleIndex, BounceDimension(0u, SLOT_BSDF_U)),
                              SampleUniform(pixel, _SampleIndex, BounceDimension(0u, SLOT_BSDF_V)));
            float3 facing = frontFace ? normal : -normal;
            color = LocalToWorld(SampleCosineHemisphere(u), facing) * 0.5f + 0.5f;
        }
    }

    return color;
}

#endif