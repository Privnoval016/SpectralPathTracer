using UnityEngine.Rendering;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * The type of shape a traced object is.
     * </summary>
     */
    [GenerateHLSL]
    public enum ShapeType
    {
        Quadric = 0,
        Quad = 1
    }
}