/**
 * <summary>
 * A light's emission resolved into a color and a table or lines when the spectrum isn't a sigmoid
 * </summary>
 */

#ifndef SPT_LINE_SPECTRUM_INCLUDED
#define SPT_LINE_SPECTRUM_INCLUDED

#include "../GpuData.hlsl"
#include "../SceneData.hlsl"
#include "SampledSpectrum.hlsl"

/**
 * <summary>
 * Sums an emitter's Gaussian lines at one wavelength.
 * </summary>
 * <param name="start">The emitter's first line in the line buffer.</param>
 * <param name="count">How many lines the emitter has.</param>
 * <param name="lambda">The wavelength in nm.</param>
 * <returns>The emitter's radiance at the wavelength.</returns>
 */
float LinesAt(uint start, uint count, float lambda)
{
    float value = 0.0f;
    for (uint i = 0u; i < count; i++)
    {
        float4 spectralLine = _Lines[start + i];
        float x = (lambda - spectralLine.x) / spectralLine.y; // how many sigmas from the center
        value += spectralLine.z * exp(-0.5f * x * x);
    }


    return value;
}

/**
 * <summary>
 * Sums an emitter's Gaussian lines at each lane's wavelength.
 * </summary>
 * <param name="start">The emitter's first line in the line buffer.</param>
 * <param name="count">How many lines the emitter has.</param>
 * <param name="lambda">One wavelength per lane, in nm.</param>
 * <returns>The emitter's radiance per lane.</returns>
 */
SampledSpectrum LineSpectrum(uint start, uint count, float4 lambda)
{
    return float4(LinesAt(start, count, lambda.x), LinesAt(start, count, lambda.y), LinesAt(start, count, lambda.z), LinesAt(start, count, lambda.w));
}

#endif
