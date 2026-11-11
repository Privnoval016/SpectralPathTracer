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
        #region Properties

        public BufferHandle Rays { get; }
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

        public PathBuffers(BufferHandle rays, BufferHandle paths, int waveStart, int count, BufferHandle queueA, BufferHandle queueB, BufferHandle counters, BufferHandle args)
        {
            Rays = rays;
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
        public BufferHandle QueueIn(int depth) => depth % 2 == 0 ? QueueA : QueueB;

        /**
         * <summary>
         * The queue a bounce appends to: B on even depths, A on odd ones.
         * </summary>
         * <param name="depth">The bounce depth.</param>
         * <returns>The queue for the next depth.</returns>
         */
        public BufferHandle QueueOut(int depth) => depth % 2 == 0 ? QueueB : QueueA;

        /**
         * <summary>
         * Byte offset of the read queue's count in the counter buffer.
         * </summary>
         * <param name="depth">The bounce depth.</param>
         * <returns>0 for queue A, 4 for queue B.</returns>
         */
        public static int QueueInOffset(int depth) => depth % 2 * sizeof(uint);

        /**
         * <summary>
         * Byte offset of the append queue's count in the counter buffer.
         * </summary>
         * <param name="depth">The bounce depth.</param>
         * <returns>4 for queue B, 0 for queue A.</returns>
         */
        public static int QueueOutOffset(int depth) => (depth + 1) % 2 * sizeof(uint);
    }
}