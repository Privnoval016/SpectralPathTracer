/**
 * <summary>
 * Measured spectra, resampled on the CPU to one value every 5 nm from 360 to 830 nm.
 * </summary>
 */

#ifndef SPT_TABULATED_SPECTRUM_INCLUDED
#define SPT_TABULATED_SPECTRUM_INCLUDED

#include "../GpuData.hlsl"
#include "../SceneData.hlsl"
#include "SampledSpectrum.hlsl"

/**
 * <summary>
 * Samples one measured spectrum at a wavelength, interpolating between its 5 nm values.
 * </summary>
 * <param name="table">The spectrum's index in the spectrum buffer.</param>
 * <param name="lambda">The wavelength in nm.</param>
 * <returns>The spectrum's value, holding its ends outside 360 to 830 nm.</returns>
 */
float TabulatedAt(uint table, float lambda)
{
    float position = clamp((lambda - SPECTRUMTABLES_FIRST_WAVELENGTH) / SPECTRUMTABLES_STEP, 0.0f, SPECTRUMTABLES_COUNT - 1.0f);
    uint row = min((uint)position, (uint)SPECTRUMTABLES_COUNT - 2u); // the last wavelength interpolates from the row before
    uint start = table * (uint)SPECTRUMTABLES_COUNT;


    return lerp(_Spectra[start + row], _Spectra[start + row + 1u], position - row);
}

/**
 * <summary>
 * Samples one measured spectrum at each lane's wavelength.
 * </summary>
 * <param name="table">The spectrum's index in the spectrum buffer.</param>
 * <param name="lambda">One wavelength per lane, in nm.</param>
 * <returns>The spectrum's value per lane.</returns>
 */
SampledSpectrum TabulatedSpectrum(uint table, float4 lambda)
{
    return float4(TabulatedAt(table, lambda.x), TabulatedAt(table, lambda.y), TabulatedAt(table, lambda.z), TabulatedAt(table, lambda.w));
}

#endif
