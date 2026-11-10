using UnityEngine;

namespace SpectralPathTracer
{
    public static class ShaderIDs
    {
        public static readonly int OutputID = Shader.PropertyToID("_Output");
        public static readonly int ResolutionID = Shader.PropertyToID("_Resolution");
        public static readonly int RayCountID = Shader.PropertyToID("_RayCount");

        public static readonly int RaysID = Shader.PropertyToID("_Rays");
        public static readonly int HitsID = Shader.PropertyToID("_Hits");

        public static readonly int CameraToWorldID = Shader.PropertyToID("_CameraToWorld");
        public static readonly int TanHalfFovYID = Shader.PropertyToID("_TanHalfFov");
        public static readonly int AspectRatioID = Shader.PropertyToID("_AspectRatio");

        public static readonly int SkyZenithID = Shader.PropertyToID("_SkyZenithColor");
        public static readonly int SkyHorizonID = Shader.PropertyToID("_SkyHorizonColor");
        public static readonly int SkyGroundID = Shader.PropertyToID("_SkyGroundColor");

        public static readonly int AovID = Shader.PropertyToID("_Aov");
        public static readonly int DepthRangeID = Shader.PropertyToID("_DepthRange");
    }
}