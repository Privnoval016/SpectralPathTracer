using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace SpectralPathTracer
{
    public class ArgsStage : ComputeStage
    {
        #region Private Fields

        private const string KernelName = "Args";

        #endregion

        private class PassData
        {
            public ComputeShader Shader;
            public int Kernel;
            public BufferHandle Counters;
            public BufferHandle Args;
            public int QueueInOffset;
            public int QueueOutOffset;
            public int Depth;
            public BufferHandle LivePaths;
        }

        public ArgsStage(ComputeShader shader) : base(shader, KernelName)
        {
        }

        /**
         * <summary>
         * Records the one-thread pass that sizes the next bounce's dispatch from the queue Shade filled.
         * </summary>
         * <param name="renderGraph">The render graph recording this frame.</param>
         * <param name="paths">The wave's queues, counters and dispatch arguments.</param>
         * <param name="depth">The depth Shade just ran at.</param>
         * <param name="livePaths">The per-depth live path counts.</param>
         */
        public void Record(RenderGraph renderGraph, in PathBuffers paths, int depth, BufferHandle livePaths)
        {
            using var builder = renderGraph.AddComputePass("Path Tracer: Args", out PassData passData);

            passData.Shader = Shader;
            passData.Kernel = Kernel;
            passData.Counters = paths.Counters;
            passData.Args = paths.Args;
            passData.QueueInOffset = paths.QueueIn(depth).CountOffset;
            passData.QueueOutOffset = paths.QueueOut(depth).CountOffset;
            passData.Depth = depth;
            passData.LivePaths = livePaths;

            builder.UseBuffer(paths.Counters, AccessFlags.ReadWrite);
            builder.UseBuffer(paths.Args, AccessFlags.ReadWrite);
            builder.UseBuffer(livePaths, AccessFlags.ReadWrite);

            // static to reject captured variables from the outer scope
            builder.SetRenderFunc(static (PassData data, ComputeGraphContext context) =>
            {
                var cmd = context.cmd;
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.CountersID, data.Counters);
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.ArgsID, data.Args);
                cmd.SetComputeIntParam(data.Shader, ShaderIDs.QueueInOffsetID, data.QueueInOffset);
                cmd.SetComputeIntParam(data.Shader, ShaderIDs.QueueOutOffsetID, data.QueueOutOffset);
                cmd.SetComputeIntParam(data.Shader, ShaderIDs.DepthID, data.Depth);
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.LivePathsID, data.LivePaths);
                cmd.DispatchCompute(data.Shader, data.Kernel, 1, 1, 1);
            });
        }
    }
}