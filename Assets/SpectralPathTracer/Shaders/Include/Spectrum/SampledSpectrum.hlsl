/**
 * <summary>
 * The per-lane values a path carries, which are four wavelengths in spectral mode or (r, g, b, 0) in RGB mode.
 * </summary>
 */

#ifndef SPT_SAMPLED_SPECTRUM_INCLUDED
#define SPT_SAMPLED_SPECTRUM_INCLUDED

#include "SampledWavelengths.hlsl"
#include "../GpuData.hlsl"

#ifdef SPECTRAL // render in spectral mode

#define ACTIVE_LANES 4
#define ALL_LANES (PATHFLAGS_LANE0 | PATHFLAGS_LANE1 | PATHFLAGS_LANE2 | PATHFLAGS_LANE3)

#else // render in RGB mode

#define ACTIVE_LANES 3
#define ALL_LANES (PATHFLAGS_LANE0 | PATHFLAGS_LANE1 | PATHFLAGS_LANE2)

#endif

typedef float4 SampledSpectrum; // typedef to preserve float4s built in operators

/**
 * <summary>
 * Converts an RGB color to a sampled spectrum.
 * </summary>
 * <param name="rgb">The RGB color.</param>
 * <param name="wavelengths">The sampled wavelengths.</param>
 * <returns>The sampled spectrum.</returns>
 */
SampledSpectrum RgbToSpectrum(float3 rgb, SampledWavelengths wavelengths)
{
    return float4(rgb, 0.0f); // todo
}

/**
 * <summary>
 * Converts a sampled spectrum to an RGB color.
 * </summary>
 * <param name="spectrum">The sampled spectrum.</param>
 * <param name="wavelengths">The sampled wavelengths.</param>
 * <returns>The RGB color.</returns>
 */
float3 SpectrumToRgb(SampledSpectrum spectrum, SampledWavelengths wavelengths)
{
    return spectrum.rgb; // todo
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