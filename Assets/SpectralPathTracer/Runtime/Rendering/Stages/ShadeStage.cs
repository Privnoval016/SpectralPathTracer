using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace SpectralPathTracer
{
    public class ShadeStage : ComputeStage
    {
        #region Private Fields

        private const string KernelName = "Shade";

        #endregion

        private class PassData
        {
            public ComputeShader Shader;
            public int Kernel;
            public BufferHandle Rays;
            public BufferHandle Hits;
            public BufferHandle Paths;
            public int Depth;
            public BufferHandle QueueIn;
            public BufferHandle QueueOut;
            public BufferHandle Counters;
            public BufferHandle Args;
            public int QueueInOffset;
            public int QueueOutOffset;

            public SceneBuffers Scene;
            public BufferHandle Constants;
            public LocalKeyword SpectralKeyword;
            public bool Spectral;
        }

        public ShadeStage(ComputeShader shader) : base(shader, KernelName)
        {
            CheckGroupSize((int)QueueSizes.GroupSize);
        }

        /**
         * <summary>
         * Records the pass that adds the light each queued path finds and samples its next bounce.
         * </summary>
         * <param name="renderGraph">The render graph recording this frame.</param>
         * <param name="frame">This frame's context.</param>
         * <param name="paths">The rays and path states, where each path's state is updated by this stage.</param>
         * <param name="scene">The scene's object and material buffers, imported this frame.</param>
         * <param name="depth">Which hit along the path this is; 0 is the camera ray's.</param>
         */
        public void Record(RenderGraph renderGraph, in FrameContext frame, in PathBuffers paths, in SceneBuffers scene, int depth)
        {
            using var builder = renderGraph.AddComputePass("Path Tracer: Shade", out PassData passData);

            passData.Shader = Shader;
            passData.Kernel = Kernel;
            passData.Rays = paths.Rays;
            passData.Hits = paths.Hits;
            passData.Paths = paths.Paths;
            passData.Depth = depth;
            WorkQueue queueIn = paths.QueueIn(depth);
            WorkQueue queueOut = paths.QueueOut(depth);

            passData.QueueIn = queueIn.Slots;
            passData.QueueOut = queueOut.Slots;
            passData.Counters = paths.Counters;
            passData.Args = paths.Args;
            passData.QueueInOffset = queueIn.CountOffset;
            passData.QueueOutOffset = queueOut.CountOffset;

            passData.Scene = scene;
            passData.Constants = frame.Constants;
            passData.SpectralKeyword = SpectralKeyword;
            passData.Spectral = frame.Params.Spectral;

            builder.UseBuffer(paths.Rays, AccessFlags.ReadWrite);
            builder.UseBuffer(paths.Hits, AccessFlags.Read);
            builder.UseBuffer(paths.Paths, AccessFlags.ReadWrite);
            builder.UseBuffer(queueIn.Slots, AccessFlags.Read);
            builder.UseBuffer(queueOut.Slots, AccessFlags.ReadWrite);
            builder.UseBuffer(paths.Counters, AccessFlags.ReadWrite);
            builder.UseBuffer(paths.Args, AccessFlags.Read);
            scene.Declare(builder);
            builder.UseBuffer(frame.Constants, AccessFlags.Read);
            builder.AllowGlobalStateModification(true); // the keyword is state on the shader, outside the graph's resources

            // static to reject captured variables from the outer scope
            builder.SetRenderFunc(static (PassData data, ComputeGraphContext context) =>
            {
                var cmd = context.cmd;
                SetSpectral(cmd, data.Shader, data.SpectralKeyword, data.Spectral);
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.RaysID, data.Rays);
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.HitsID, data.Hits);
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.PathsID, data.Paths);
                cmd.SetComputeIntParam(data.Shader, ShaderIDs.DepthID, data.Depth);
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.QueueInID, data.QueueIn);
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.QueueOutID, data.QueueOut);
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.CountersID, data.Counters);
                cmd.SetComputeIntParam(data.Shader, ShaderIDs.QueueInOffsetID, data.QueueInOffset);
                cmd.SetComputeIntParam(data.Shader, ShaderIDs.QueueOutOffsetID, data.QueueOutOffset);
                data.Scene.Bind(cmd, data.Shader, data.Kernel);
                SetFrameConstants(cmd, data.Shader, data.Constants);
                cmd.DispatchCompute(data.Shader, data.Kernel, data.Args, 0); // the group count comes from the GPU
            });
        }
    }
}