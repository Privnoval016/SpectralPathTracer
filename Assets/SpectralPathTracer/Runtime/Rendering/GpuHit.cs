using System.Runtime.InteropServices;
using UnityEngine.Rendering;

namespace SpectralPathTracer
{
    [GenerateHLSL(needAccessors: false)]
    [StructLayout(LayoutKind.Sequential)]
    public struct GpuHit
    {
        public float T;
        public uint Hit;
    }
}