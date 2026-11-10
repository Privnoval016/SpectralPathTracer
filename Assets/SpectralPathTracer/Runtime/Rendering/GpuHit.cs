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
        public int ObjectIndex; // -1 on a miss
    }
}