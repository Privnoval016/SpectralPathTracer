using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace SpectralPathTracer
{
    public class DisplayStage : ComputeStage
    {
        #region Private Fields

        private const string KernelName = "Display";

        #endregion

        private class PassData
        {
            public ComputeShader Shader;
            public int Kernel;
            public BufferHandle Accumulation;
            public TextureHandle Display;
            public readonly int[] Resolution = new int[2];
            public int GroupsX;
            public int GroupsY;

            public float ExposureScale;
            public int ApplyExposure;
            public int ShowGrayRamp;
        }

        public DisplayStage(ComputeShader shader) : base(shader, KernelName)
        {
        }

        /**
         * <summary>
         * Records the pass that turns linear radiance into displayable values: exposure, then a clamp to [0, 1].
         * </summary>
         * <param name="renderGraph">The render graph recording this frame.</param>
         * <param name="frame">This frame's context.</param>
         * <param name="accumulation">The per-pixel sums of linear radiance.</param>
         * <returns>The display texture, valid for this frame's render graph.</returns>
         */
        public TextureHandle Record(RenderGraph renderGraph, in FrameContext frame, BufferHandle accumulation)
        {
            var desc = new TextureDesc(frame.RenderSize.x, frame.RenderSize.y)
            {
                format = GraphicsFormat.R16G16B16A16_SFloat,
                enableRandomWrite = true,
                clearBuffer = false,
                name = "PathTracerDisplay"
            };
            TextureHandle display = renderGraph.CreateTexture(desc);

            using var builder = renderGraph.AddComputePass("Path Tracer: Display", out PassData passData);

            passData.Shader = Shader;
            passData.Kernel = Kernel;
            passData.Accumulation = accumulation;
            passData.Display = display;
            passData.Resolution[0] = frame.RenderSize.x;
            passData.Resolution[1] = frame.RenderSize.y;
            passData.GroupsX = GroupCount(frame.RenderSize.x, GroupSizeX);
            passData.GroupsY = GroupCount(frame.RenderSize.y, GroupSizeY);

            passData.ExposureScale = Mathf.Pow(2f, frame.Exposure);
            passData.ApplyExposure = frame.Aov == Aov.None ? 1 : 0; // AOVs are data with fixed encodings so they skip exposure
            passData.ShowGrayRamp = frame.ShowGrayRamp ? 1 : 0;

            builder.UseBuffer(accumulation, AccessFlags.Read);
            builder.UseTexture(display, AccessFlags.Write);

            // static to reject captured variables from the outer scope
            builder.SetRenderFunc(static (PassData data, ComputeGraphContext context) =>
            {
                var cmd = context.cmd;
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.AccumulationID, data.Accumulation);
                cmd.SetComputeTextureParam(data.Shader, data.Kernel, ShaderIDs.DisplayID, data.Display);
                cmd.SetComputeIntParams(data.Shader, ShaderIDs.ResolutionID, data.Resolution);
                cmd.SetComputeFloatParam(data.Shader, ShaderIDs.ExposureScaleID, data.ExposureScale);
                cmd.SetComputeIntParam(data.Shader, ShaderIDs.ApplyExposureID, data.ApplyExposure);
                cmd.SetComputeIntParam(data.Shader, ShaderIDs.ShowGrayRampID, data.ShowGrayRamp);
                cmd.DispatchCompute(data.Shader, data.Kernel, data.GroupsX, data.GroupsY, 1);
            });

            return display;
        }
    }
}