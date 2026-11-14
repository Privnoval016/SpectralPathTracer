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
        // names match the shader uniforms and each group of fields fills one 16 byte register
        public Matrix4x4 _CameraToWorld; // bytes 0 to 63 (four registers)

        public float _TanHalfFov; // 64
        public float _AspectRatio; // 68
        public Vector2 _OutputScale; // 72 to 79

        // float4 because Metal pads a float3 to 16 bytes and would shift every field after it
        public Vector4 _SkyZenithColor; // 80 to 95
        public Vector4 _SkyHorizonColor; // 96 to 111
        public Vector4 _SkyGroundColor; // 112 to 127

        public Vector2 _DepthRange; // 128 to 135
        public uint _MaxBounces; // 136
        public uint _Aov; // 140

        public uint _NoiseDimension; // 144
        public uint _PixelJitter; // 148
        public uint _RussianRoulette; // 152
        public uint _ApplyExposure; // 156

        public float _ExposureScale; // 160
        public uint _ShowGrayRamp; // 164
        public uint _ShowSpectrumStrip; // 168
        public uint _AccumulatesXyz; // 172

        // the same sky as sigmoid coefficients in xyz and a scale in w, for spectral mode
        public Vector4 _SkyZenithSpectrum; // 176 to 191
        public Vector4 _SkyHorizonSpectrum; // 192 to 207
        public Vector4 _SkyGroundSpectrum; // 208 to 223

        public float _SplitPosition; // 224
        public uint _SplitScreen; // 228
        public float _LockedWavelength; // 232, nm, or 0 while spectral paths draw random wavelengths
        public uint _StratifyWavelengths; // 236, 1 to spread each pixel's wavelength draws evenly over its samples

        // size = 240, a multiple of 16
    }
}
