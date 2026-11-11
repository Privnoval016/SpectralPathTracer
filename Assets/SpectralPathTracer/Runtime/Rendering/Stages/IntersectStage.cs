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
            CheckGroupSize((int)QueueSizes.GroupSize);
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
         */
        public void Record(RenderGraph renderGraph, in FrameContext frame, in PathBuffers paths, in SceneBuffers scene, int depth)
        {
            using var builder = renderGraph.AddComputePass("Path Tracer: Intersect", out PassData passData);

            passData.Shader = Shader;
            passData.Kernel = Kernel;
            passData.Rays = paths.Rays;
            WorkQueue queueIn = paths.QueueIn(depth);

            passData.Hits = paths.Hits;
            passData.QueueIn = queueIn.Slots;
            passData.Counters = paths.Counters;
            passData.Args = paths.Args;
            passData.QueueInOffset = queueIn.CountOffset;
            passData.Scene = scene;

            builder.UseBuffer(paths.Rays, AccessFlags.Read);
            builder.UseBuffer(paths.Hits, AccessFlags.Write);
            builder.UseBuffer(queueIn.Slots, AccessFlags.Read);
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
        }
    }
}