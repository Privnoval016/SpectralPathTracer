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
            public TextureHandle Output;
            public int RayCount;
            public int Groups;
        }

        public ResolveStage(ComputeShader shader) : base(shader, KernelName)
        {
        }

        /**
         * <summary>
         * Records the pass that writes every path's radiance to its pixel in the output texture.
         * </summary>
         * <param name="renderGraph">The render graph recording this frame.</param>
         * <param name="frame">This frame's context.</param>
         * <param name="paths">The rays and path states after the bounce loop.</param>
         * <param name="output">The texture to write into.</param>
         */
        public void Record(RenderGraph renderGraph, in FrameContext frame, in PathBuffers paths, TextureHandle output)
        {
            using var builder = renderGraph.AddComputePass("Path Tracer: Resolve", out PassData passData);

            passData.Shader = Shader;
            passData.Kernel = Kernel;
            passData.Rays = paths.Rays;
            passData.Paths = paths.Paths;
            passData.Output = output;
            passData.RayCount = frame.RayCount;
            passData.Groups = GroupCount(frame.RayCount, GroupSizeX);

            builder.UseBuffer(paths.Rays, AccessFlags.Read);
            builder.UseBuffer(paths.Paths, AccessFlags.Read);
            builder.UseTexture(output, AccessFlags.Write);

            // static to reject captured variables from the outer scope
            builder.SetRenderFunc(static (PassData data, ComputeGraphContext context) =>
            {
                var cmd = context.cmd;
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.RaysID, data.Rays);
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.PathsID, data.Paths);
                cmd.SetComputeTextureParam(data.Shader, data.Kernel, ShaderIDs.OutputID, data.Output);
                cmd.SetComputeIntParam(data.Shader, ShaderIDs.RayCountID, data.RayCount);
                cmd.DispatchCompute(data.Shader, data.Kernel, data.Groups, 1, 1);
            });
        }
    }
}