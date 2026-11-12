/**
 * <summary>
 * Sampling, evaluating and the pdf of each material's BSDF.
 * </summary>
 */

#ifndef SPT_BSDF_INCLUDED
#define SPT_BSDF_INCLUDED

#include "../Common.hlsl"
#include "../GpuData.hlsl"
#include "../Spectrum/SigmoidSpectrum.hlsl"
#include "../Sampling/Sampling.hlsl"

struct BsdfSample
{
    float3 Direction;
    SampledSpectrum Weight; // f cos / pdf
    float Pdf;
};

/**
 * <summary>
 * Samples the direction a path bounces in.
 * </summary>
 * <param name="material">The surface's material.</param>
 * <param name="facing">The normal on the side the ray arrived from.</param>
 * <param name="wavelengths">The path's wavelengths.</param>
 * <param name="u">A 2D uniform random sample in the range [0, 1).</param>
 * <returns>The bounce direction, its weight and its pdf.</returns>
 */
BsdfSample SampleBsdf(GpuMaterial material, float3 facing, SampledWavelengths wavelengths, float2 u)
{
    float3 local = SampleCosineHemisphere(u);

    BsdfSample bsdf;
    bsdf.Direction = LocalToWorld(local, facing);
    bsdf.Weight = ReflectanceSpectrum(material, wavelengths); // f cos / pdf for a Lambertian surface is the albedo
    bsdf.Pdf = CosineHemispherePdf(local.z);
    return bsdf;
}

/**
 * <summary>
 * Evaluates the BSDF for a pair of directions.
 * </summary>
 * <param name="material">The surface's material.</param>
 * <param name="facing">The normal on the side the ray arrived from.</param>
 * <param name="wavelengths">The path's wavelengths.</param>
 * <param name="direction">The direction light leaves toward.</param>
 * <returns>The BSDF value f, without the cosine.</returns>
 */
SampledSpectrum EvalBsdf(GpuMaterial material, float3 facing, SampledWavelengths wavelengths, float3 direction)
{
    // light from below the surface never reaches the front
    return dot(facing, direction) > 0.0f ? ReflectanceSpectrum(material, wavelengths) / PI : (SampledSpectrum)0;
}

/**
 * <summary>
 * Computes the pdf SampleBsdf has of picking a direction.
 * </summary>
 * <param name="material">The surface's material.</param>
 * <param name="facing">The normal on the side the ray arrived from.</param>
 * <param name="direction">The sampled direction.</param>
 * <returns>The solid angle pdf.</returns>
 */
float PdfBsdf(GpuMaterial material, float3 facing, float3 direction)
{
    return CosineHemispherePdf(max(0.0f, dot(facing, direction)));
}

#endif
