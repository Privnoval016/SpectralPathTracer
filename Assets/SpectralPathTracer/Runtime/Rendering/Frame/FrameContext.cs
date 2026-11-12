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
        public bool Spectral { get; } // which color mode this pass renders

        public Vector2Int RenderSize => Params.RenderSize;
        public int PixelCount => RenderSize.x * RenderSize.y;
        public int MaxBounces => Params.MaxBounces;

        #endregion

        public FrameContext(RenderParams renderParams, BufferHandle constants, bool spectral)
        {
            Params = renderParams;
            Constants = constants;
            Spectral = spectral;
        }

        /**
         * <summary>
         * The same frame, for a pass rendered in one color mode.
         * </summary>
         * <param name="spectral">Whether the pass renders spectral mode.</param>
         * <returns>The frame with that mode.</returns>
         */
        public FrameContext InMode(bool spectral) => new FrameContext(Params, Constants, spectral);
    }
}
