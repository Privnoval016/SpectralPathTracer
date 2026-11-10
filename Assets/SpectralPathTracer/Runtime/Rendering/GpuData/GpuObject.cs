using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpectralPathTracer
{
    [GenerateHLSL(needAccessors: false)]
    [StructLayout(LayoutKind.Sequential)]
    public struct GpuObject
    {
        public Matrix4x4 Quadric; // world space: M^-T Q_local M^-1
        public Matrix4x4 WorldToLocal;
        public int Type; // ShapeType
        public int MaterialIndex;
    }
}