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
        public Vector4 Wavelengths; // nm per lane (spectral chapters)
        public Vector4 WavelengthPdfs; // pdf of each lane's wavelength (spectral chapters)
        public Vector4 Time; // time of flight per lane (transient chapters)
        public Vector4 MisVc; // recursive MIS running quantities (BDPT chapters)
        public Vector4 MisVcm;
        public uint PixelIndex; // the pixel this path's sample lands in
        public uint Flags; // PathFlags bits
        public uint MediumId; // the medium the path is in (media chapters)
        public uint Reserved; // pads to 128 bytes
    }
}