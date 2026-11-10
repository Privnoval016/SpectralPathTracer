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
        Active = 1
    }
}