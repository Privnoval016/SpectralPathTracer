using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpectralPathTracer
{
    [GenerateHLSL(needAccessors: false)]
    [StructLayout(LayoutKind.Sequential)]
    public struct GpuMaterial
    {
        public Vector3 Albedo; // linear
        public Vector3 Emission; // linear radiance: color times strength
        public int Type; // MaterialType
    }
}