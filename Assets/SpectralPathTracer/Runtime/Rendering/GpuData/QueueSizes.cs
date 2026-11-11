using UnityEngine.Rendering;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * Sizes shared by the live-path queues, their kernels and their indirect dispatches.
     * </summary>
     */
    [GenerateHLSL]
    public enum QueueSizes
    {
        GroupSize = 64
    }
}
