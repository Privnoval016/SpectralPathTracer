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
            public int MaxBounces;
            public BufferHandle QueueIn;
            public BufferHandle QueueOut;
            public BufferHandle Counters;
            public BufferHandle Args;
            public int QueueInOffset;
            public int QueueOutOffset;

            public SkyColors Sky;
            public PinholeCamera Camera;
            public Aov Aov;
            public Vector2 DepthRange;
            public int NoiseDimension;

            public uint SampleIndex;
            public Vector2 OutputScale;

            public BufferHandle Objects;
            public BufferHandle Materials;
        }

        public ShadeStage(ComputeShader shader) : base(shader, KernelName)
        {
        }

        /**
         * <summary>
         * Records the pass that adds the light each queued path finds and samples its next bounce.
         * </summary>
         * <param name="renderGraph">The render graph recording this frame.</param>
         * <param name="frame">This frame's context.</param>
         * <param name="paths">The rays and path states, where each path's state is updated by this stage.</param>
         * <param name="hits">The hit buffer written by the intersect stage.</param>
         * <param name="scene">The scene's object and material buffers, imported this frame.</param>
         * <param name="depth">Which hit along the path this is; 0 is the camera ray's.</param>
         */
        public void Record(RenderGraph renderGraph, in FrameContext frame, in PathBuffers paths, BufferHandle hits, in SceneBuffers scene, int depth)
        {
            using var builder = renderGraph.AddComputePass("Path Tracer: Shade", out PassData passData);

            passData.Shader = Shader;
            passData.Kernel = Kernel;
            passData.Rays = paths.Rays;
            passData.Hits = hits;
            passData.Paths = paths.Paths;
            passData.Depth = depth;
            passData.MaxBounces = frame.MaxBounces;
            passData.QueueIn = paths.QueueIn(depth);
            passData.QueueOut = paths.QueueOut(depth);
            passData.Counters = paths.Counters;
            passData.Args = paths.Args;
            passData.QueueInOffset = PathBuffers.QueueInOffset(depth);
            passData.QueueOutOffset = PathBuffers.QueueOutOffset(depth);

            passData.Sky = frame.Sky;
            passData.Camera = frame.Camera;
            passData.Aov = frame.Aov;
            passData.DepthRange = frame.DepthRange;
            passData.NoiseDimension = frame.NoiseDimension;

            passData.SampleIndex = frame.SampleIndex;
            passData.OutputScale = frame.OutputScale;

            passData.Objects = scene.Objects;
            passData.Materials = scene.Materials;

            builder.UseBuffer(paths.Rays, AccessFlags.ReadWrite);
            builder.UseBuffer(hits, AccessFlags.Read);
            builder.UseBuffer(paths.Paths, AccessFlags.ReadWrite);
            builder.UseBuffer(paths.QueueIn(depth), AccessFlags.Read);
            builder.UseBuffer(paths.QueueOut(depth), AccessFlags.ReadWrite);
            builder.UseBuffer(paths.Counters, AccessFlags.ReadWrite);
            builder.UseBuffer(paths.Args, AccessFlags.Read);
            builder.UseBuffer(scene.Objects, AccessFlags.Read);
            builder.UseBuffer(scene.Materials, AccessFlags.Read);

            // static to reject captured variables from the outer scope
            builder.SetRenderFunc(static (PassData data, ComputeGraphContext context) =>
            {
                var cmd = context.cmd;
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.RaysID, data.Rays);
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.HitsID, data.Hits);
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.PathsID, data.Paths);
                cmd.SetComputeIntParam(data.Shader, ShaderIDs.DepthID, data.Depth);
                cmd.SetComputeIntParam(data.Shader, ShaderIDs.MaxBouncesID, data.MaxBounces);
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.QueueInID, data.QueueIn);
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.QueueOutID, data.QueueOut);
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.CountersID, data.Counters);
                cmd.SetComputeIntParam(data.Shader, ShaderIDs.QueueInOffsetID, data.QueueInOffset);
                cmd.SetComputeIntParam(data.Shader, ShaderIDs.QueueOutOffsetID, data.QueueOutOffset);
                cmd.SetComputeVectorParam(data.Shader, ShaderIDs.SkyZenithID, data.Sky.Zenith.linear);
                cmd.SetComputeVectorParam(data.Shader, ShaderIDs.SkyHorizonID, data.Sky.Horizon.linear);
                cmd.SetComputeVectorParam(data.Shader, ShaderIDs.SkyGroundID, data.Sky.Ground.linear);
                SetCamera(cmd, data.Shader, data.Camera);
                cmd.SetComputeIntParam(data.Shader, ShaderIDs.AovID, (int)data.Aov);
                cmd.SetComputeVectorParam(data.Shader, ShaderIDs.DepthRangeID, data.DepthRange);
                cmd.SetComputeIntParam(data.Shader, ShaderIDs.NoiseDimensionID, data.NoiseDimension);
                SetSampler(cmd, data.Shader, data.SampleIndex, data.OutputScale);
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.ObjectsID, data.Objects);
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.MaterialsID, data.Materials);
                cmd.DispatchCompute(data.Shader, data.Kernel, data.Args, 0); // the group count comes from the GPU
            });
        }
    }
}