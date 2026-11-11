using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * Everything a path carries between kernels, padded to 128 bytes.
     * </summary>
     */
    [GenerateHLSL(needAccessors: false)]
    [StructLayout(LayoutKind.Sequential)]
    public struct GpuPathState
    {
        public Vector4 Radiance; // per lane; RGB mode uses (r, g, b, 0)
        public Vector4 Throughput; // beta per lane
        public Vector4 Wavelengths; // nm per lane
        public Vector4 WavelengthPdfs; // pdf of each lane's wavelength
        public Vector4 Time; // time of flight per lane
        public Vector4 MisVc; // recursive MIS running quantities
        public Vector4 MisVcm;
        public uint PixelIndex; // the pixel this path's sample lands in
        public uint Flags; // PathFlags bits
        public uint MediumId; // the medium the path is in
        public uint SampleIndex; // the sample this path belongs to since one frame can hold two
        
        // size = 128, a multiple of 16
    }
}