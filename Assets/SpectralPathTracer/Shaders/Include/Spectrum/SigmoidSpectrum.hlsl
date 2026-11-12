/**
 * <summary>
 * Materials, lights and the sky as spectra at a path's wavelengths, from sigmoid coefficients fitted on the CPU.
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

/**
 * <summary>
 * A material's reflectance at the path's wavelengths.
 * </summary>
 * <param name="material">The surface's material.</param>
 * <param name="wavelengths">The path's wavelengths.</param>
 * <returns>The reflectance per lane.</returns>
 */
SampledSpectrum ReflectanceSpectrum(GpuMaterial material, SampledWavelengths wavelengths)
{
#ifdef SPECTRAL
    SampledSpectrum reflectance = EvaluateSigmoid(material.AlbedoSpectrum, wavelengths.Lambda);
#else
    SampledSpectrum reflectance = float4(material.Albedo, 0.0f);
#endif

    return reflectance;
}

/**
 * <summary>
 * A material's emitted radiance at the path's wavelengths.
 * </summary>
 * <param name="material">The surface's material.</param>
 * <param name="wavelengths">The path's wavelengths.</param>
 * <returns>The emitted radiance per lane.</returns>
 */
SampledSpectrum EmissionSpectrum(GpuMaterial material, SampledWavelengths wavelengths)
{
#ifdef SPECTRAL
    // a light's white is D65, so its color is a shape on top of daylight
    SampledSpectrum emission = EvaluateSigmoid(material.EmissionSpectrum, wavelengths.Lambda) * D65Spectrum(wavelengths.Lambda);
#else
    SampledSpectrum emission = float4(material.Emission, 0.0f);
#endif

    return emission;
}

#endif
