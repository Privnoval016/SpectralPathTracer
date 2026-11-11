using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * A persistent structured buffer that doubles its capacity whenever an upload outgrows it.
     * </summary>
     */
    public class GrowableBuffer<T> where T : struct
    {
        #region Private Fields

        private readonly string _name;
        private GraphicsBuffer _buffer;

        #endregion

        #region Properties

        public bool IsCreated => _buffer != null;

        #endregion

        public GrowableBuffer(string name)
        {
            _name = name;
        }

        /**
         * <summary>
         * Uploads the data, growing the buffer first if it's too small.
         * </summary>
         * <param name="data">The elements to upload.</param>
         */
        public void Upload(List<T> data)
        {
            EnsureCapacity(data.Count);
            _buffer.SetData(data);
        }

        /**
         * <summary>
         * Imports the buffer into this frame's render graph.
         * </summary>
         * <param name="renderGraph">The render graph recording this frame.</param>
         * <returns>The buffer's handle for this frame.</returns>
         */
        public BufferHandle Import(RenderGraph renderGraph)
        {
            return renderGraph.ImportBuffer(_buffer);
        }

        public void Dispose()
        {
            _buffer?.Release();
            _buffer = null;
        }

        #region Helpers

        /**
         * <summary>
         * Makes sure the buffer holds at least <paramref name="count"/> elements, doubling
         * its capacity when it has to grow.
         * </summary>
         * <param name="count">The number of elements needed.</param>
         */
        private void EnsureCapacity(int count)
        {
            int needed = Mathf.Max(1, count);
            if (_buffer != null && _buffer.count >= needed)
                return;

            int capacity = _buffer == null ? 1 : _buffer.count;
            while (capacity < needed)
                capacity *= 2;

            _buffer?.Release();
            _buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, UnsafeUtility.SizeOf<T>()) { name = _name };
        }

        #endregion
    }
}
