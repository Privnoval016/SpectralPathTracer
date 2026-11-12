using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpectralPathTracer
{
    [GenerateHLSL(needAccessors: false)]
    [StructLayout(LayoutKind.Sequential)]
    public struct GpuMaterial
    {
        public Vector4 AlbedoSpectrum; // sigmoid coefficients in xyz and 1 in w, first so every float4 starts on 16 bytes
        public Vector4 EmissionSpectrum; // sigmoid coefficients in xyz and the scale in w

        public Vector3 Albedo; // linear
        public Vector3 Emission; // linear radiance: color times strength
        public int Type; // MaterialType
    }
}