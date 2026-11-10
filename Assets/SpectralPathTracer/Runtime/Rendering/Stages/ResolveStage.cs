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
            public BufferHandle Rays;
            public BufferHandle Paths;
            public BufferHandle Accumulation;
            public readonly int[] Resolution = new int[2];
            public int SampleCount;
            public int RayCount;
            public int Groups;
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
         * <param name="paths">The rays and path states after the bounce loop.</param>
         * <param name="accumulation">The per-pixel sums of every sample since the last reset.</param>
         */
        public void Record(RenderGraph renderGraph, in FrameContext frame, in PathBuffers paths, BufferHandle accumulation)
        {
            using var builder = renderGraph.AddComputePass("Path Tracer: Resolve", out PassData passData);

            passData.Shader = Shader;
            passData.Kernel = Kernel;
            passData.Rays = paths.Rays;
            passData.Paths = paths.Paths;
            passData.Accumulation = accumulation;
            passData.Resolution[0] = frame.RenderSize.x;
            passData.Resolution[1] = frame.RenderSize.y;
            passData.SampleCount = (int)frame.SampleIndex; // samples already in the sum before this one
            passData.RayCount = frame.RayCount;
            passData.Groups = GroupCount(frame.RayCount, GroupSizeX);

            builder.UseBuffer(paths.Rays, AccessFlags.Read);
            builder.UseBuffer(paths.Paths, AccessFlags.Read);
            builder.UseBuffer(accumulation, AccessFlags.ReadWrite);

            // static to reject captured variables from the outer scope
            builder.SetRenderFunc(static (PassData data, ComputeGraphContext context) =>
            {
                var cmd = context.cmd;
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.RaysID, data.Rays);
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.PathsID, data.Paths);
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.AccumulationID, data.Accumulation);
                cmd.SetComputeIntParams(data.Shader, ShaderIDs.ResolutionID, data.Resolution);
                cmd.SetComputeIntParam(data.Shader, ShaderIDs.SampleCountID, data.SampleCount);
                cmd.SetComputeIntParam(data.Shader, ShaderIDs.RayCountID, data.RayCount);
                cmd.DispatchCompute(data.Shader, data.Kernel, data.Groups, 1, 1);
            });
        }
    }
}