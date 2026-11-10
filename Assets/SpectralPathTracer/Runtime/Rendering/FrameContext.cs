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
        public int RayCount => RenderSize.x * RenderSize.y;

        /**
         * <summary>
         * The output resolution divided by the render resolution, per axis.
         * </summary>
         */
        public Vector2 OutputScale => new Vector2((float)OutputSize.x / RenderSize.x, (float)OutputSize.y / RenderSize.y);

        public uint SampleIndex { get; }
        public PinholeCamera Camera { get; }
        public SkyColors Sky { get; }
        public Aov Aov { get; }
        public Vector2 DepthRange { get; }
        public int NoiseDimension { get; }

        #endregion

        public FrameContext(Vector2Int renderSize, Vector2Int outputSize, uint sampleIndex, PinholeCamera camera, SkyColors sky, Aov aov, Vector2 depthRange, int noiseDimension)
        {
            RenderSize = renderSize;
            OutputSize = outputSize;
            SampleIndex = sampleIndex;
            Camera = camera;
            Sky = sky;
            Aov = aov;
            DepthRange = depthRange;
            NoiseDimension = noiseDimension;
        }
    }
}