using UnityEngine.Rendering;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * Which BSDF a material samples and evaluates.
     * </summary>
     */
    [GenerateHLSL]
    public enum MaterialType
    {
        Lambert = 0
    }
}
