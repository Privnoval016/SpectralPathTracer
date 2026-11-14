using UnityEngine.Rendering;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * The wavelength distribution's bins, shared by the CPU that builds the table and the GPU that searches it.
     * </summary>
     */
    [GenerateHLSL]
    public enum WavelengthBins
    {
        PerNm = 50, // fine enough that a 0.05 nm line spans 2.5 bins
        Count = (Cie.LastWavelength - Cie.FirstWavelength) * PerNm // 23,500 bins from 360 to 830 nm
    }
}
