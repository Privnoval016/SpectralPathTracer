using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpectralPathTracer
{
    [GenerateHLSL(needAccessors: false)]
    [StructLayout(LayoutKind.Sequential)]
    public struct GpuPathState
    {
        public Vector3 Radiance; // light gathered so far
        public Vector3 Throughput; // how much of the light found next still reaches the camera
        public uint Active; // 0 once the path has finished
    }
}