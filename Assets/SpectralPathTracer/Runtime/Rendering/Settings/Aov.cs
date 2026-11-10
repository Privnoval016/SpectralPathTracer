using UnityEngine.Rendering;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * Arbitrary output variables: what the image shows instead of the rendered result.
     * </summary>
     */
    [GenerateHLSL]
    public enum Aov
    {
        None = 0,
        Normals = 1,
        Depth = 2,
        FrontFace = 3,
        Noise = 4,
        BounceDirection = 5,
    }
}