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
            public BufferHandle QueueIn;
            public BufferHandle Counters;
            public BufferHandle Args;
            public int QueueInOffset;
            public SceneBuffers Scene;
        }

        public IntersectStage(ComputeShader shader) : base(shader, KernelName)
        {
        }

        /**
         * <summary>
         * Records the pass that finds where each queued path's ray hits the scene, sized on the GPU by indirect dispatch.
         * </summary>
         * <param name="renderGraph">The render graph recording this frame.</param>
         * <param name="frame">This frame's context.</param>
         * <param name="paths">The wave's rays and queues.</param>
         * <param name="scene">The scene's buffers, imported this frame.</param>
         * <param name="depth">The bounce depth, which picks the queue to read.</param>
         * <returns>The hit buffer, one entry per path slot, valid for this frame's render graph.</returns>
         */
        public BufferHandle Record(RenderGraph renderGraph, in FrameContext frame, in PathBuffers paths, in SceneBuffers scene, int depth)
        {
            var desc = new BufferDesc(paths.Count, UnsafeUtility.SizeOf<GpuHit>(), GraphicsBuffer.Target.Structured) { name = "PathTracerHits" };
            BufferHandle hits = renderGraph.CreateBuffer(desc);

            using var builder = renderGraph.AddComputePass("Path Tracer: Intersect", out PassData passData);

            passData.Shader = Shader;
            passData.Kernel = Kernel;
            passData.Rays = paths.Rays;
            passData.Hits = hits;
            passData.QueueIn = paths.QueueIn(depth);
            passData.Counters = paths.Counters;
            passData.Args = paths.Args;
            passData.QueueInOffset = PathBuffers.QueueInOffset(depth);
            passData.Scene = scene;

            builder.UseBuffer(paths.Rays, AccessFlags.Read);
            builder.UseBuffer(hits, AccessFlags.Write);
            builder.UseBuffer(paths.QueueIn(depth), AccessFlags.Read);
            builder.UseBuffer(paths.Counters, AccessFlags.Read);
            builder.UseBuffer(paths.Args, AccessFlags.Read);
            scene.Declare(builder);

            // static to reject captured variables from the outer scope
            builder.SetRenderFunc(static (PassData data, ComputeGraphContext context) =>
            {
                var cmd = context.cmd;
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.RaysID, data.Rays);
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.HitsID, data.Hits);
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.QueueInID, data.QueueIn);
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.CountersID, data.Counters);
                cmd.SetComputeIntParam(data.Shader, ShaderIDs.QueueInOffsetID, data.QueueInOffset);
                data.Scene.Bind(cmd, data.Shader, data.Kernel);
                cmd.DispatchCompute(data.Shader, data.Kernel, data.Args, 0); // the group count comes from the GPU
            });

            return hits;
        }
    }
}