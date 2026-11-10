using UnityEngine.Rendering.RenderGraphModule;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * Buffers that carry every path through the bounce loop.
     * </summary>
     */
    public readonly struct PathBuffers
    {
        #region Properties

        public BufferHandle Rays { get; }
        public BufferHandle Paths { get; }

        #endregion

        public PathBuffers(BufferHandle rays, BufferHandle paths)
        {
            Rays = rays;
            Paths = paths;
        }
    }
}