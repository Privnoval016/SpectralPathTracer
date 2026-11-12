/**
 * <summary>
 * The four wavelengths a path carries and their pdfs, chosen once per sample.
 * </summary>
 */

#ifndef SPT_SAMPLED_WAVELENGTHS_INCLUDED
#define SPT_SAMPLED_WAVELENGTHS_INCLUDED

#include "../GpuData.hlsl"
#include "Cie.hlsl"

struct SampledWavelengths
{
    float4 Lambda; // nm per lane
    float4 Pdf; // pdf per lane
};

/**
 * <summary>
 * Samples a set of wavelengths for a path.
 * </summary>
 * <param name="u">A uniform random number in [0, 1).</param>
 * <returns>The sampled wavelengths.</returns>
 */
SampledWavelengths SampleWavelengths(float u)
{
    SampledWavelengths wavelengths;

#ifdef SPECTRAL
    // one wavelength, uniform over the matching functions' range (other lanes later)
    float range = CIE_SAMPLE_COUNT - 1.0f;
    wavelengths.Lambda = float4(CIE_FIRST_WAVELENGTH + u * range, 0.0f, 0.0f, 0.0f);
    wavelengths.Pdf = float4(1.0f / range, 1.0f, 1.0f, 1.0f); // the masked lanes never divide by zero
#else
    wavelengths.Lambda = 0.0f;
    wavelengths.Pdf = 1.0f;
#endif

    return wavelengths;
}

/**
 * <summary>
 * Loads the sampled wavelengths from the path state.
 * </summary>
 * <param name="path">The path state to load the wavelengths from.</param>
 * <returns>The sampled wavelengths.</returns>
 */
SampledWavelengths LoadWavelengths(GpuPathState path)
{
    SampledWavelengths wavelengths;
    wavelengths.Lambda = path.Wavelengths;
    wavelengths.Pdf = path.WavelengthPdfs;

    return wavelengths;
}

/**
 * <summary>
 * Stores the sampled wavelengths in the path state.
 * </summary>
 * <param name="path">The path state to store the wavelengths in.</param>
 * <param name="wavelengths">The sampled wavelengths to store.</param>
 */
void StoreWavelengths(inout GpuPathState path, SampledWavelengths wavelengths)
{
    path.Wavelengths = wavelengths.Lambda;
    path.WavelengthPdfs = wavelengths.Pdf;
}

#endif