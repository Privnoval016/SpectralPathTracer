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
            public TextureHandle Output;
            public int RayCount;
            public int Groups;

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
         * Records the pass that turns each ray's hit into a color in the output texture.
         * </summary>
         * <param name="renderGraph">The render graph recording this frame.</param>
         * <param name="frame">This frame's context.</param>
         * <param name="rays">The ray buffer written by the generate stage.</param>
         * <param name="hits">The hit buffer written by the intersect stage.</param>
         * <param name="output">The texture each ray's color is written into.</param>
         * <param name="scene">The scene's object and material buffers, imported this frame.</param>
         */
        public void Record(RenderGraph renderGraph, in FrameContext frame, BufferHandle rays, BufferHandle hits, TextureHandle output, in SceneBuffers scene)
        {
            using var builder = renderGraph.AddComputePass("Path Tracer: Shade", out PassData passData);

            passData.Shader = Shader;
            passData.Kernel = Kernel;
            passData.Rays = rays;
            passData.Hits = hits;
            passData.Output = output;
            passData.RayCount = frame.RayCount;
            passData.Groups = GroupCount(frame.RayCount, GroupSizeX);

            passData.Sky = frame.Sky;
            passData.Camera = frame.Camera;
            passData.Aov = frame.Aov;
            passData.DepthRange = frame.DepthRange;
            passData.NoiseDimension = frame.NoiseDimension;

            passData.SampleIndex = frame.SampleIndex;
            passData.OutputScale = frame.OutputScale;

            passData.Objects = scene.Objects;
            passData.Materials = scene.Materials;

            builder.UseBuffer(rays, AccessFlags.Read);
            builder.UseBuffer(hits, AccessFlags.Read);
            builder.UseTexture(output, AccessFlags.Write);
            builder.UseBuffer(scene.Objects, AccessFlags.Read);
            builder.UseBuffer(scene.Materials, AccessFlags.Read);

            // static to reject captured variables from the outer scope
            builder.SetRenderFunc(static (PassData data, ComputeGraphContext context) =>
            {
                var cmd = context.cmd;
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.RaysID, data.Rays);
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.HitsID, data.Hits);
                cmd.SetComputeTextureParam(data.Shader, data.Kernel, ShaderIDs.OutputID, data.Output);
                cmd.SetComputeIntParam(data.Shader, ShaderIDs.RayCountID, data.RayCount);
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
                cmd.DispatchCompute(data.Shader, data.Kernel, data.Groups, 1, 1);
            });
        }
    }
}