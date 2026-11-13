/**
 * <summary>
 * The four wavelengths a path carries and their pdfs, chosen once per sample.
 * </summary>
 */

#ifndef SPT_SAMPLED_WAVELENGTHS_INCLUDED
#define SPT_SAMPLED_WAVELENGTHS_INCLUDED

#include "../GpuData.hlsl"
#include "../SceneData.hlsl"
#include "Cie.hlsl"

static const uint WAVELENGTH_BINS = CIE_SAMPLE_COUNT - 1u; // one bin per nm

struct SampledWavelengths
{
    float4 Lambda; // nm per lane
    float4 Pdf; // pdf per lane
};

/**
 * <summary>
 * Draws one wavelength from the scene's wavelength CDF.
 * this is like the same thing as the C# mirror
 * </summary>
 * <param name="u">A uniform random number in [0, 1).</param>
 * <param name="pdf">The density at the drawn wavelength, per nm.</param>
 * <returns>The wavelength in nm.</returns>
 */
float SampleWavelengthCdf(float u, out float pdf)
{
    uint low = 0u;
    uint high = WAVELENGTH_BINS;
    while (low < high)
    {
        uint middle = (low + high) / 2u;
        if (_WavelengthCdf[middle] <= u)
            low = middle + 1u;
        else
            high = middle;
    }

    uint bin = low - 1u; // cdf[0] = 0 <= u, so low is at least 1
    float start = _WavelengthCdf[bin];
    float width = _WavelengthCdf[bin + 1u] - start;
    pdf = width; // the bin's probability over its 1 nm width


    return CIE_FIRST_WAVELENGTH + bin + (u - start) / width;
}

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
    // one wavelength, drawn from the scene's wavelength distribution (other lanes later)
    float pdf = 1.0f; // unused while locked
    float lambda = _LockedWavelength > 0.0f ? _LockedWavelength : SampleWavelengthCdf(u, pdf); // a locked wavelength replaces the draw
    wavelengths.Lambda = float4(lambda, 0.0f, 0.0f, 0.0f);
    wavelengths.Pdf = float4(pdf, 1.0f, 1.0f, 1.0f); // the masked lanes never divide by zero
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