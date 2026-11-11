using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace SpectralPathTracer
{
    public class ResolveStage : ComputeStage
    {
        #region Private Fields

        private const string KernelName = "Resolve";

        #endregion

        private class PassData
        {
            public ComputeShader Shader;
            public int Kernel;
            public BufferHandle Paths;
            public BufferHandle Accumulation;
            public int RayCount;
            public int Groups;
            public BufferHandle Constants;
        }

        public ResolveStage(ComputeShader shader) : base(shader, KernelName)
        {
        }

        /**
         * <summary>
         * Records the pass that adds every path's radiance to its pixel's running sum.
         * </summary>
         * <param name="renderGraph">The render graph recording this frame.</param>
         * <param name="frame">This frame's context.</param>
         * <param name="paths">The wave's path states after the bounce loop.</param>
         * <param name="accumulation">The per-pixel sums of every sample since the last reset.</param>
         */
        public void Record(RenderGraph renderGraph, in FrameContext frame, in PathBuffers paths, BufferHandle accumulation)
        {
            using var builder = renderGraph.AddComputePass("Path Tracer: Resolve", out PassData passData);

            passData.Shader = Shader;
            passData.Kernel = Kernel;
            passData.Paths = paths.Paths;
            passData.Accumulation = accumulation;
            passData.RayCount = paths.Count;
            passData.Groups = GroupCount(paths.Count, GroupSizeX);
            passData.Constants = frame.Constants;

            builder.UseBuffer(paths.Paths, AccessFlags.Read);
            builder.UseBuffer(accumulation, AccessFlags.ReadWrite);
            builder.UseBuffer(frame.Constants, AccessFlags.Read);

            // static to reject captured variables from the outer scope
            builder.SetRenderFunc(static (PassData data, ComputeGraphContext context) =>
            {
                var cmd = context.cmd;
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.PathsID, data.Paths);
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.AccumulationID, data.Accumulation);
                cmd.SetComputeIntParam(data.Shader, ShaderIDs.RayCountID, data.RayCount);
                SetFrameConstants(cmd, data.Shader, data.Constants);
                cmd.DispatchCompute(data.Shader, data.Kernel, data.Groups, 1, 1);
            });
        }
    }
}