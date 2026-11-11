using UnityEngine.Rendering.RenderGraphModule;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * Buffers that carry one wave of paths through the bounce loop.
     * </summary>
     */
    public readonly struct PathBuffers
    {
        #region Private Fields

        // byte offsets of each queue's count in the counter buffer
        private const int QueueAOffset = 0;
        private const int QueueBOffset = sizeof(uint);

        #endregion

        #region Properties

        public BufferHandle Rays { get; }
        public BufferHandle Hits { get; }
        public BufferHandle Paths { get; }

        /**
         * <summary>
         * The first pixel this wave covers, where slot i is pixel WaveStart + i.
         * </summary>
         */
        public int WaveStart { get; }

        public int Count { get; }

        // live path slots, their counts and the next dispatch size
        public BufferHandle QueueA { get; }
        public BufferHandle QueueB { get; }
        public BufferHandle Counters { get; }
        public BufferHandle Args { get; }

        #endregion

        public PathBuffers(BufferHandle rays, BufferHandle hits, BufferHandle paths, int waveStart, int count, BufferHandle queueA, BufferHandle queueB, BufferHandle counters, BufferHandle args)
        {
            Rays = rays;
            Hits = hits;
            Paths = paths;
            WaveStart = waveStart;
            Count = count;
            QueueA = queueA;
            QueueB = queueB;
            Counters = counters;
            Args = args;
        }

        // queues keep alternating between bounces in and out so read and write stay separate always
        
        /**
         * <summary>
         * The queue a bounce reads: A on even depths, B on odd ones.
         * </summary>
         * <param name="depth">The bounce depth.</param>
         * <returns>The queue of live paths at that depth.</returns>
         */
        public WorkQueue QueueIn(int depth) => depth % 2 == 0 ? new WorkQueue(QueueA, QueueAOffset) : new WorkQueue(QueueB, QueueBOffset);

        /**
         * <summary>
         * The queue a bounce appends to: B on even depths, A on odd ones.
         * </summary>
         * <param name="depth">The bounce depth.</param>
         * <returns>The queue for the next depth.</returns>
         */
        public WorkQueue QueueOut(int depth) => QueueIn(depth + 1);
    }
}