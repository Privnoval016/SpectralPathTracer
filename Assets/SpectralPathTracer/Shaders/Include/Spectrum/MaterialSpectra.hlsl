/**
 * <summary>
 * A material's reflectance and emission at a path's wavelengths, from a measured table or a fitted sigmoid.
 * </summary>
 */

#ifndef SPT_MATERIAL_SPECTRA_INCLUDED
#define SPT_MATERIAL_SPECTRA_INCLUDED

#include "../GpuData.hlsl"
#include "SampledSpectrum.hlsl"
#include "SigmoidSpectrum.hlsl"
#include "TabulatedSpectrum.hlsl"

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
    // a measured table wins over the sigmoid fitted to the picker color
    SampledSpectrum reflectance = material.ReflectanceTable >= 0
        ? TabulatedSpectrum((uint)material.ReflectanceTable, wavelengths.Lambda)
        : EvaluateSigmoid(material.AlbedoSpectrum, wavelengths.Lambda);
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
    // a measured lamp is its own spectrum, while an RGB light's white is D65 with a shape on top
    SampledSpectrum emission = material.EmissionTable >= 0
        ? TabulatedSpectrum((uint)material.EmissionTable, wavelengths.Lambda)
        : EvaluateSigmoid(material.EmissionSpectrum, wavelengths.Lambda) * D65Spectrum(wavelengths.Lambda);
#else
    SampledSpectrum emission = float4(material.Emission, 0.0f);
#endif

    return emission;
}

#endif
