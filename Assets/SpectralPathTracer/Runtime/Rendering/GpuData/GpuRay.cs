using System.Runtime.InteropServices;
using UnityEngine.Rendering;
using UnityEngine;

namespace SpectralPathTracer
{
    [GenerateHLSL(needAccessors: false)]
    [StructLayout(LayoutKind.Sequential)]
    public struct GpuRay
    {
        public Vector3 Origin;
        public Vector3 Direction;
        public Vector2Int Pixel;
    }
}