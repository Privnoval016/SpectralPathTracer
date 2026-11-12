/**
 * <summary>
 * The sky's radiance in a direction, seen by camera rays and used as a light by bounces.
 * </summary>
 */

#ifndef SPT_SKY_INCLUDED
#define SPT_SKY_INCLUDED

#include "Common.hlsl"
#include "GpuData.hlsl"
#include "Spectrum/SigmoidSpectrum.hlsl"

float3 SkyRadiance(float3 direction)
{
    if (direction.y < 0.0f)
    {
        // Below the horizon, use the ground color
        return _SkyGroundColor.rgb;
    }
    else
    {
        // Above the horizon, interpolate between horizon and zenith based on the angle from the zenith
        float elevation = asin(saturate(direction.y)) / (PI / 2.0f); // Normalize elevation to [0, 1]
        return lerp(_SkyHorizonColor.rgb, _SkyZenithColor.rgb, elevation);
    }
}

/**
 * <summary>
 * The sky's radiance in a direction at the path's wavelengths.
 * </summary>
 * <param name="direction">The direction to look in.</param>
 * <param name="wavelengths">The path's wavelengths.</param>
 * <returns>The sky's radiance per lane.</returns>
 */
SampledSpectrum SkySpectrum(float3 direction, SampledWavelengths wavelengths)
{
#ifdef SPECTRAL
    // the same blend as SkyRadiance, done on the spectra, which converts to the same blend of colors
    float4 shape = direction.y < 0.0f
        ? EvaluateSigmoid(_SkyGroundSpectrum, wavelengths.Lambda)
        : lerp(EvaluateSigmoid(_SkyHorizonSpectrum, wavelengths.Lambda), EvaluateSigmoid(_SkyZenithSpectrum, wavelengths.Lambda),
               asin(saturate(direction.y)) / (PI / 2.0f));
    SampledSpectrum radiance = shape * D65Spectrum(wavelengths.Lambda); // the sky is a light, so its white is D65
#else
    SampledSpectrum radiance = float4(SkyRadiance(direction), 0.0f);
#endif

    return radiance;
}

#endif