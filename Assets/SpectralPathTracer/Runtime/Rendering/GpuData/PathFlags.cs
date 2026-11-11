using System;
using UnityEngine.Rendering;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * Simple enum determining flags for a path.
     * </summary>
     */
    [Flags]
    [GenerateHLSL]
    public enum PathFlags
    {
        None = 0,
        Active = 1,
        Lane0 = 2,
        Lane1 = 4,
        Lane2 = 8,
        Lane3 = 16,
    }
}