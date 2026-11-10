using UnityEngine.Rendering.RenderGraphModule;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * The scene's GPU buffers imported into this frame's render graph.
     * </summary>
     */
    public readonly struct SceneBuffers
    {
        #region Properties

        public BufferHandle Objects { get; }
        public BufferHandle Materials { get; }
        public int ObjectCount { get; }

        #endregion

        public SceneBuffers(BufferHandle objects, BufferHandle materials, int objectCount)
        {
            Objects = objects;
            Materials = materials;
            ObjectCount = objectCount;
        }
    }
}