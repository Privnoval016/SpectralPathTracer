using UnityEngine;

namespace SpectralPathTracer
{
    public static class ShaderIDs
    {
        public static readonly int OutputID = Shader.PropertyToID("_Output");
        public static readonly int AccumulationID = Shader.PropertyToID("_Accumulation");
        public static readonly int SampleCountID = Shader.PropertyToID("_SampleCount");
        public static readonly int InverseSampleCountID = Shader.PropertyToID("_InverseSampleCount");
        public static readonly int DisplayID = Shader.PropertyToID("_Display");
        public static readonly int ExposureScaleID = Shader.PropertyToID("_ExposureScale");
        public static readonly int ApplyExposureID = Shader.PropertyToID("_ApplyExposure");
        public static readonly int ShowGrayRampID = Shader.PropertyToID("_ShowGrayRamp");
        public static readonly int ResolutionID = Shader.PropertyToID("_Resolution");
        public static readonly int RayCountID = Shader.PropertyToID("_RayCount");
        public static readonly int WaveStartID = Shader.PropertyToID("_WaveStart");
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
        public static readonly int MaxBouncesID = Shader.PropertyToID("_MaxBounces");
        public static readonly int PixelJitterID = Shader.PropertyToID("_PixelJitter");
        public static readonly int RussianRouletteID = Shader.PropertyToID("_RussianRoulette");

        public static readonly int ObjectsID = Shader.PropertyToID("_Objects");
        public static readonly int MaterialsID = Shader.PropertyToID("_Materials");
        public static readonly int ObjectCountID = Shader.PropertyToID("_ObjectCount");

        public static readonly int CameraToWorldID = Shader.PropertyToID("_CameraToWorld");
        public static readonly int TanHalfFovYID = Shader.PropertyToID("_TanHalfFov");
        public static readonly int AspectRatioID = Shader.PropertyToID("_AspectRatio");

        public static readonly int SkyZenithID = Shader.PropertyToID("_SkyZenithColor");
        public static readonly int SkyHorizonID = Shader.PropertyToID("_SkyHorizonColor");
        public static readonly int SkyGroundID = Shader.PropertyToID("_SkyGroundColor");

        public static readonly int AovID = Shader.PropertyToID("_Aov");
        public static readonly int DepthRangeID = Shader.PropertyToID("_DepthRange");
        public static readonly int NoiseDimensionID = Shader.PropertyToID("_NoiseDimension");

        public static readonly int SampleIndexID = Shader.PropertyToID("_SampleIndex");
        public static readonly int OutputScaleID = Shader.PropertyToID("_OutputScale");
    }
}