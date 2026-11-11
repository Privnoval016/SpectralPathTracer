using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * Every value that stays the same for a whole frame, uploaded once into one constant buffer.
     * </summary>
     */
    [GenerateHLSL(needAccessors: false, generateCBuffer: true)]
    [StructLayout(LayoutKind.Sequential)]
    public struct FrameConstants
    {
        // names match the shader uniforms, and each group of fields fills one 16 byte register
        public Matrix4x4 _CameraToWorld;
        public float _TanHalfFov;
        public float _AspectRatio;
        public Vector2 _OutputScale;

        // float4 because Metal pads a float3 to 16 bytes and would shift every field after it
        public Vector4 _SkyZenithColor;
        public Vector4 _SkyHorizonColor;
        public Vector4 _SkyGroundColor;

        public Vector2 _DepthRange;
        public uint _SampleIndex;
        public uint _MaxBounces;

        public uint _Aov;
        public uint _NoiseDimension;
        public uint _PixelJitter;
        public uint _RussianRoulette;

        public float _ExposureScale;
        public uint _ApplyExposure;
        public uint _ShowGrayRamp;
        public uint _Padding;
    }
}
