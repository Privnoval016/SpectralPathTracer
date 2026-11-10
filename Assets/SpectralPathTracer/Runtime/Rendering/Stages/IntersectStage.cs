using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace SpectralPathTracer
{
    public class IntersectStage : ComputeStage
    {
        #region Private Fields

        private const string KernelName = "Intersect";

        #endregion

        private class PassData
        {
            public ComputeShader Shader;
            public int Kernel;
            public BufferHandle Rays;
            public BufferHandle Hits;
            public int RayCount;
            public int Groups;

            public BufferHandle Objects;
            public int ObjectCount;
        }

        public IntersectStage(ComputeShader shader) : base(shader, KernelName)
        {
        }

        /**
         * <summary>
         * Records the pass that finds where each ray hits the scene.
         * </summary>
         * <param name="renderGraph">The render graph recording this frame.</param>
         * <param name="frame">This frame's context.</param>
         * <param name="rays">The ray buffer written by the generate stage.</param>
         * <param name="scene">The scene's object buffer, imported this frame.</param>
         * <returns>The hit buffer, one hit per ray, valid for this frame's render graph.</returns>
         */
        public BufferHandle Record(RenderGraph renderGraph, in FrameContext frame, BufferHandle rays, in SceneBuffers scene)
        {
            var desc = new BufferDesc(frame.RayCount, UnsafeUtility.SizeOf<GpuHit>(), GraphicsBuffer.Target.Structured) { name = "PathTracerHits" };
            BufferHandle hits = renderGraph.CreateBuffer(desc);

            using var builder = renderGraph.AddComputePass("Path Tracer: Intersect", out PassData passData);

            passData.Shader = Shader;
            passData.Kernel = Kernel;
            passData.Rays = rays;
            passData.Hits = hits;
            passData.RayCount = frame.RayCount;
            passData.Groups = GroupCount(frame.RayCount, GroupSizeX);

            passData.Objects = scene.Objects;
            passData.ObjectCount = scene.ObjectCount;

            builder.UseBuffer(rays, AccessFlags.Read);
            builder.UseBuffer(hits, AccessFlags.Write);
            builder.UseBuffer(scene.Objects, AccessFlags.Read);

            // static to reject captured variables from the outer scope
            builder.SetRenderFunc(static (PassData data, ComputeGraphContext context) =>
            {
                var cmd = context.cmd;
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.RaysID, data.Rays);
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.HitsID, data.Hits);
                cmd.SetComputeIntParam(data.Shader, ShaderIDs.RayCountID, data.RayCount);
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.ObjectsID, data.Objects);
                cmd.SetComputeIntParam(data.Shader, ShaderIDs.ObjectCountID, data.ObjectCount);
                cmd.DispatchCompute(data.Shader, data.Kernel, data.Groups, 1, 1);
            });

            return hits;
        }
    }
}