/**
 * <summary>
 * The per-lane values a path carries, which are four wavelengths in spectral mode or (r, g, b, 0) in RGB mode.
 * </summary>
 */

#ifndef SPT_SAMPLED_SPECTRUM_INCLUDED
#define SPT_SAMPLED_SPECTRUM_INCLUDED

#include "SampledWavelengths.hlsl"
#include "../GpuData.hlsl"
#include "Cie.hlsl"

#ifdef SPECTRAL // render in spectral mode

#define ACTIVE_LANES 4
#define ALL_LANES (PATHFLAGS_LANE0) // one wavelength for now

#else // render in RGB mode

#define ACTIVE_LANES 3
#define ALL_LANES (PATHFLAGS_LANE0 | PATHFLAGS_LANE1 | PATHFLAGS_LANE2)

#endif

typedef float4 SampledSpectrum; // typedef to preserve float4s built in operators

/**
 * <summary>
 * Converts a sampled spectrum to the color the accumulation buffer holds: XYZ in spectral mode, linear sRGB in RGB mode.
 * </summary>
 * <param name="spectrum">The sampled spectrum.</param>
 * <param name="wavelengths">The sampled wavelengths.</param>
 * <returns>The color to accumulate.</returns>
 */
float3 SpectrumToColor(SampledSpectrum spectrum, SampledWavelengths wavelengths)
{
#ifdef SPECTRAL
    // each lane estimates XYZ as L cmf / p, and their average over the y-bar integral is the sample's XYZ
    float3 xyz = 0.0f;
    for (uint lane = 0u; lane < 4u; lane++)
        xyz += spectrum[lane] * MatchingFunctions(wavelengths.Lambda[lane]) / wavelengths.Pdf[lane];

    float3 color = xyz / (ACTIVE_LANES * CIE_Y_INTEGRAL);
#else
    float3 color = spectrum.rgb;
#endif


    return color;
}


/**
 * <summary>
 * Creates a lane mask from the path flags.
 * </summary>
 * <param name="flags">The path flags.</param>
 * <returns>The lane mask.</returns>
 */
SampledSpectrum LaneMask(uint flags)
{
    return float4(
        (flags & PATHFLAGS_LANE0) != 0u,
        (flags & PATHFLAGS_LANE1) != 0u,
        (flags & PATHFLAGS_LANE2) != 0u,
        (flags & PATHFLAGS_LANE3) != 0u);
}

/**
 * <summary>
 * Finds the largest of the lanes.
 * </summary>
 * <param name="spectrum">The sampled spectrum.</param>
 * <returns>The largest lane.</returns>
 */
float MaxValue(SampledSpectrum spectrum)
{
    return max(max(spectrum.x, spectrum.y), max(spectrum.z, spectrum.w));
}

/**
 * <summary>
 * Computes the average value of the lanes.
 * </summary>
 * <param name="spectrum">The sampled spectrum.</param>
 * <returns>The average value of the lanes.</returns>
 */
float AverageValue(SampledSpectrum spectrum)
{
    return dot(spectrum, 1.0f) / ACTIVE_LANES;
}

#endif