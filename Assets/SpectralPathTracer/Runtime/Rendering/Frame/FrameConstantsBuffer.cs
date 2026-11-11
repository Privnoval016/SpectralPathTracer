using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * The constant buffer every stage reads this frame's constants from.
     * </summary>
     */
    public class FrameConstantsBuffer
    {
        #region Private Fields

        private GraphicsBuffer _buffer;

        #endregion

        private class PassData
        {
            public BufferHandle Buffer;
            public readonly FrameConstants[] Constants = new FrameConstants[1];
        }

        #region Properties

        public static int Size => UnsafeUtility.SizeOf<FrameConstants>();

        #endregion

        /**
         * <summary>
         * Records the pass that uploads this frame's constants.
         * </summary>
         * <param name="renderGraph">The render graph recording this frame.</param>
         * <param name="constants">This frame's constants.</param>
         * <returns>The constant buffer, valid for this frame's render graph.</returns>
         */
        public BufferHandle Record(RenderGraph renderGraph, in FrameConstants constants)
        {
            _buffer ??= new GraphicsBuffer(GraphicsBuffer.Target.Constant, 1, Size) { name = "PathTracerFrameConstants" };
            BufferHandle buffer = renderGraph.ImportBuffer(_buffer);

            using var builder = renderGraph.AddComputePass("Path Tracer: Frame Constants", out PassData passData);

            passData.Buffer = buffer;
            passData.Constants[0] = constants;

            builder.UseBuffer(buffer, AccessFlags.Write);

            // static to reject captured variables from the outer scope
            builder.SetRenderFunc(static (PassData data, ComputeGraphContext context) =>
            {
                context.cmd.SetBufferData(data.Buffer, data.Constants);
            });

            return buffer;
        }

        public void Dispose()
        {
            _buffer?.Release();
            _buffer = null;
        }
    }
}
