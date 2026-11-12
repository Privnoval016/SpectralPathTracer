/**
 * <summary>
 * Sigmoid spectra fitted on the CPU, and D65, evaluated at a path's wavelengths.
 * </summary>
 */

#ifndef SPT_SIGMOID_SPECTRUM_INCLUDED
#define SPT_SIGMOID_SPECTRUM_INCLUDED

#include "../GpuData.hlsl"
#include "SampledSpectrum.hlsl"
#include "Cie.hlsl"

/**
 * <summary>
 * Evaluates a sigmoid spectrum at each lane's wavelength.
 * </summary>
 * <param name="spectrum">c0, c1 and c2 for nm in xyz, and the scale in w.</param>
 * <param name="lambda">One wavelength per lane, in nm.</param>
 * <returns>The scaled sigmoid at each wavelength.</returns>
 */
SampledSpectrum EvaluateSigmoid(float4 spectrum, float4 lambda)
{
    // float4 math is so fire
    float4 x = (spectrum.x * lambda + spectrum.y) * lambda + spectrum.z;
    float4 sigmoid = 0.5f + x / (2.0f * sqrt(1.0f + x * x));


    return sigmoid * spectrum.w;
}

/**
 * <summary>
 * D65 at each lane's wavelength, scaled so a light of white 1 has Y = 1.
 * </summary>
 * <param name="lambda">One wavelength per lane, in nm.</param>
 * <returns>D65's power at each wavelength.</returns>
 */
SampledSpectrum D65Spectrum(float4 lambda)
{
    return float4(D65At(lambda.x), D65At(lambda.y), D65At(lambda.z), D65At(lambda.w)) * CIE_D65_SCALE;
}

#endif
