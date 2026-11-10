using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace SpectralPathTracer
{
    public class GenerateStage : ComputeStage
    {
        #region Private Fields

        private const string KernelName = "Generate";

        #endregion

        private class PassData
        {
            public ComputeShader Shader;
            public int Kernel;
            public BufferHandle Rays;
            public BufferHandle Paths;
            public readonly int[] Resolution = new int[2];
            public int GroupsX;
            public int GroupsY;

            public PinholeCamera Camera;
        }

        public GenerateStage(ComputeShader shader) : base(shader, KernelName)
        {
        }

        /**
         * <summary>
         * Records the pass that writes one camera ray per pixel and starts its path.
         * </summary>
         * <param name="renderGraph">The render graph recording this frame.</param>
         * <param name="frame">This frame's context.</param>
         * <returns>The ray and path buffers, valid for this frame's render graph.</returns>
         */
        public PathBuffers Record(RenderGraph renderGraph, in FrameContext frame)
        {
            var desc = new BufferDesc(frame.RayCount, UnsafeUtility.SizeOf<GpuRay>(), GraphicsBuffer.Target.Structured) { name = "PathTracerRays" };
            BufferHandle rays = renderGraph.CreateBuffer(desc);

            var pathDesc = new BufferDesc(frame.RayCount, UnsafeUtility.SizeOf<GpuPathState>(), GraphicsBuffer.Target.Structured) { name = "PathTracerPaths" };
            BufferHandle paths = renderGraph.CreateBuffer(pathDesc);

            using var builder = renderGraph.AddComputePass("Path Tracer: Generate", out PassData passData);

            passData.Shader = Shader;
            passData.Kernel = Kernel;
            passData.Rays = rays;
            passData.Paths = paths;
            passData.Resolution[0] = frame.RenderSize.x;
            passData.Resolution[1] = frame.RenderSize.y;
            passData.GroupsX = GroupCount(frame.RenderSize.x, GroupSizeX);
            passData.GroupsY = GroupCount(frame.RenderSize.y, GroupSizeY);

            passData.Camera = frame.Camera;

            builder.UseBuffer(rays, AccessFlags.Write);
            builder.UseBuffer(paths, AccessFlags.Write);

            // static to reject captured variables from the outer scope
            builder.SetRenderFunc(static (PassData data, ComputeGraphContext context) =>
            {
                var cmd = context.cmd;
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.RaysID, data.Rays);
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.PathsID, data.Paths);
                cmd.SetComputeIntParams(data.Shader, ShaderIDs.ResolutionID, data.Resolution);
                SetCamera(cmd, data.Shader, data.Camera);
                cmd.DispatchCompute(data.Shader, data.Kernel, data.GroupsX, data.GroupsY, 1);
            });

            return new PathBuffers(rays, paths);
        }
    }
}