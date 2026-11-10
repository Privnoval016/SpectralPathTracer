using UnityEngine;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * Context for a single frame of path tracing.
     * </summary>
     */
    public readonly struct FrameContext
    {
        #region Properties

        public Vector2Int RenderSize { get; }
        public Vector2Int OutputSize { get; }
        public int PixelCount => RenderSize.x * RenderSize.y;

        /**
         * <summary>
         * The output resolution divided by the render resolution, per axis.
         * </summary>
         */
        public Vector2 OutputScale => new Vector2((float)OutputSize.x / RenderSize.x, (float)OutputSize.y / RenderSize.y);

        public uint SampleIndex { get; }
        public int MaxBounces { get; }
        public PinholeCamera Camera { get; }
        public SkyColors Sky { get; }
        public Aov Aov { get; }
        public Vector2 DepthRange { get; }
        public int NoiseDimension { get; }
        public float Exposure { get; }
        public bool ShowGrayRamp { get; }
        public bool PixelJitter { get; }

        #endregion

        public FrameContext(Vector2Int renderSize, Vector2Int outputSize, uint sampleIndex, int maxBounces, PinholeCamera camera, SkyColors sky, Aov aov, Vector2 depthRange, int noiseDimension, float exposure, bool showGrayRamp, bool pixelJitter)
        {
            RenderSize = renderSize;
            OutputSize = outputSize;
            SampleIndex = sampleIndex;
            MaxBounces = maxBounces;
            Camera = camera;
            Sky = sky;
            Aov = aov;
            DepthRange = depthRange;
            NoiseDimension = noiseDimension;
            Exposure = exposure;
            ShowGrayRamp = showGrayRamp;
            PixelJitter = pixelJitter;
        }
    }
}