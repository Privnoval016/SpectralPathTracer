using UnityEngine;

namespace SpectralPathTracer
{
    public static class ShaderIDs
    {
        public static readonly int OutputID = Shader.PropertyToID("_Output");
        public static readonly int AccumulationID = Shader.PropertyToID("_Accumulation");
        public static readonly int DisplayID = Shader.PropertyToID("_Display");
        public static readonly int ResolutionID = Shader.PropertyToID("_Resolution");
        public static readonly int RayCountID = Shader.PropertyToID("_RayCount");
        public static readonly int WaveStartID = Shader.PropertyToID("_WaveStart");
        public static readonly int SampleIndexID = Shader.PropertyToID("_SampleIndex");
        public static readonly int QueueInID = Shader.PropertyToID("_QueueIn");
        public static readonly int QueueOutID = Shader.PropertyToID("_QueueOut");
        public static readonly int CountersID = Shader.PropertyToID("_Counters");
        public static readonly int ArgsID = Shader.PropertyToID("_Args");
        public static readonly int QueueInOffsetID = Shader.PropertyToID("_QueueInOffset");
        public static readonly int QueueOutOffsetID = Shader.PropertyToID("_QueueOutOffset");

        public static readonly int RaysID = Shader.PropertyToID("_Rays");
        public static readonly int HitsID = Shader.PropertyToID("_Hits");
        public static readonly int PathsID = Shader.PropertyToID("_Paths");
        public static readonly int DepthID = Shader.PropertyToID("_Depth");
        public static readonly int LivePathsID = Shader.PropertyToID("_LivePaths");

        public static readonly int ObjectsID = Shader.PropertyToID("_Objects");
        public static readonly int MaterialsID = Shader.PropertyToID("_Materials");
        public static readonly int SpectraID = Shader.PropertyToID("_Spectra");
        public static readonly int WavelengthCdfID = Shader.PropertyToID("_WavelengthCdf");
        public static readonly int SpectralAccumulationID = Shader.PropertyToID("_SpectralAccumulation");
        public static readonly int ObjectCountID = Shader.PropertyToID("_ObjectCount");

        public static readonly int FrameConstantsID = Shader.PropertyToID("FrameConstants");
    }
}