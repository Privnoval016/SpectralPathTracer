using UnityEngine.Rendering.RenderGraphModule;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * A queue of live path slots and where its count sits in the counter buffer.
     * </summary>
     */
    public readonly struct WorkQueue
    {
        #region Properties

        public BufferHandle Slots { get; }
        public int CountOffset { get; }

        #endregion

        public WorkQueue(BufferHandle slots, int countOffset)
        {
            Slots = slots;
            CountOffset = countOffset;
        }
    }
}
