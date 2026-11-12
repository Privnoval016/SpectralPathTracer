using UnityEngine.Rendering;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * The fixed wavelength grid every measured spectrum is resampled onto for the GPU.
     * </summary>
     */
    [GenerateHLSL]
    public enum SpectrumTables
    {
        FirstWavelength = 360, // nm, the start of the matching functions
        Step = 5, // nm between values
        Count = 95 // values per spectrum, so the last one sits at 830 nm
    }
}
