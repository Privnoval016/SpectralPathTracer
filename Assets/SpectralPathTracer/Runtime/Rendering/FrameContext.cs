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
        public int RayCount => RenderSize.x * RenderSize.y;
        public PinholeCamera Camera { get; }
        public SkyColors Sky { get; }

        #endregion

        public FrameContext(Vector2Int renderSize, PinholeCamera camera, SkyColors sky)
        {
            RenderSize = renderSize;
            Camera = camera;
            Sky = sky;
        }
    }
}