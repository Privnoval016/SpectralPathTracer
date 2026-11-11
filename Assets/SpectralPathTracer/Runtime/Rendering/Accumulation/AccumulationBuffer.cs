using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * Persistent per-pixel radiance sums, with each pixel's sample count in w.
     * </summary>
     */
    public class AccumulationBuffer
    {
        #region Private Fields

        private GraphicsBuffer _sums;

        #endregion

        /**
         * <summary>
         * Makes sure there is one sum per pixel, reallocating when the render size changes.
         * </summary>
         * <param name="renderSize">The size to render at this frame.</param>
         * <returns>True if the buffer was reallocated, so its contents are undefined.</returns>
         */
        public bool Ensure(Vector2Int renderSize)
        {
            int count = renderSize.x * renderSize.y;
            if (_sums != null && _sums.count == count)
                return false;

            _sums?.Release();
            _sums = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, sizeof(float) * 4) { name = "PathTracerAccumulation" };
            return true;
        }

        /**
         * <summary>
         * Imports the sums into this frame's render graph.
         * </summary>
         * <param name="renderGraph">The render graph recording this frame.</param>
         * <returns>The sums' handle for this frame.</returns>
         */
        public BufferHandle Import(RenderGraph renderGraph)
        {
            return renderGraph.ImportBuffer(_sums);
        }

        public void Dispose()
        {
            _sums?.Release();
            _sums = null;
        }
    }
}