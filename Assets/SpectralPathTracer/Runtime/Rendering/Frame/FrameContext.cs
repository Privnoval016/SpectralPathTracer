using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;

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

        public RenderParams Params { get; }
        public BufferHandle Constants { get; }

        public Vector2Int RenderSize => Params.RenderSize;
        public int PixelCount => RenderSize.x * RenderSize.y;
        public int MaxBounces => Params.MaxBounces;

        #endregion

        public FrameContext(RenderParams renderParams, BufferHandle constants)
        {
            Params = renderParams;
            Constants = constants;
        }
    }
}
