using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule.Util;
using System.Collections.Generic;

namespace SpectralPathTracer
{
    public class PathTracerPass : ScriptableRenderPass
    {
        #region Shader Property IDs

        private static readonly int OutputID = Shader.PropertyToID("_Output");
        private static readonly int ResolutionID = Shader.PropertyToID("_Resolution");

        private static readonly int CameraToWorldID = Shader.PropertyToID("_CameraToWorld");
        private static readonly int TanHalfFovYID = Shader.PropertyToID("_TanHalfFov");
        private static readonly int AspectRatioID = Shader.PropertyToID("_AspectRatio");

        private static readonly int SkyZenithID = Shader.PropertyToID("_SkyZenithColor");
        private static readonly int SkyHorizonID = Shader.PropertyToID("_SkyHorizonColor");
        private static readonly int SkyGroundID = Shader.PropertyToID("_SkyGroundColor");

        #endregion

        #region Private Fields
        private readonly ComputeShader _pathTracerShader;
        private readonly int _kernelIndex;

        private readonly int _threadGroupSizeX;
        private readonly int _threadGroupSizeY;

        private readonly PathTracerSettings _settings;
        private RTHandle _output;

        private bool _reportedMultipleSkies;

        #endregion

        private class RenderPassData
        {
            public ComputeShader Shader;
            public int Kernel;
            public TextureHandle Output;
            public readonly int[] Resolution = new int[2];
            public int GroupsX;
            public int GroupsY;

            public PinholeCamera Camera;
            public SkyColors Sky;
        }

        private class PresentPassData
        {
            public TextureHandle Source;
            public Rect Viewport;
        }

        /**
         * <summary>
         * Initializes a new instance of the <see cref="PathTracerPass"/> class.
         * </summary>
         * <param name="pathTracerShader">The compute shader containing the path tracer kernels.</param>
         * <param name="settings">The path tracer settings.</param>
         * <param name="kernelIndex">The index of the kernel that renders the image.</param>
         */
        public PathTracerPass(ComputeShader pathTracerShader, PathTracerSettings settings, int kernelIndex)
        {
            _pathTracerShader = pathTracerShader;
            _kernelIndex = kernelIndex;
            _settings = settings;

            pathTracerShader.GetKernelThreadGroupSizes(kernelIndex, out uint threadGroupSizeX, out uint threadGroupSizeY, out _);
            _threadGroupSizeX = (int)threadGroupSizeX;
            _threadGroupSizeY = (int)threadGroupSizeY;

            requiresIntermediateTexture = true;
        }

        /**
         * <summary>
         * Records this frame's passes: prepare the output, render into it unless paused, present it.
         * </summary>
         */
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (!TryResolveSky(out SkyColors skyColors))
                return;

            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            if (cameraData.cameraType != CameraType.Game)
                return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            TextureDesc cameraDesc = renderGraph.GetTextureDesc(resourceData.activeColorTexture);
            Vector2Int renderSize = _settings.RenderResolution;

            TextureHandle output = ImportOutput(renderGraph, cameraDesc, renderSize);

            if (!_settings.Paused)
                RecordRenderStage(renderGraph, output, renderSize, cameraData.camera, skyColors);

            RecordPresentStage(renderGraph, output, resourceData.activeColorTexture, cameraDesc);
        }

        /**
         * <summary>
         * Releases the persistent output texture.
         * </summary>
         */
        public void Dispose()
        {
            _output?.Release();
            _output = null;
        }

        #region Helpers

        /**
         * <summary>
         * Makes sure the persistent output texture matches this frame's size and format, then imports it.
         * </summary>
         * <param name="cameraDesc">The camera target's descriptor, used as the starting point.</param>
         * <param name="renderSize">The size to render at this frame.</param>
         * <returns>The output texture's handle for this frame's render graph.</returns>
         */
        private TextureHandle ImportOutput(RenderGraph renderGraph, TextureDesc cameraDesc, Vector2Int renderSize)
        {
            TextureDesc desc = cameraDesc;
            desc.format = GraphicsFormat.R16G16B16A16_SFloat;
            desc.enableRandomWrite = true;
            desc.msaaSamples = MSAASamples.None;
            desc.clearBuffer = false;
            desc.width = renderSize.x;
            desc.height = renderSize.y;

            RenderingUtils.ReAllocateHandleIfNeeded(ref _output, desc, "PathTracerOutput");
            return renderGraph.ImportTexture(_output);
        }

        /**
         * <summary>
         * Records the compute pass that renders the image into the output texture.
         * </summary>
         */
        private void RecordRenderStage(RenderGraph renderGraph, TextureHandle output, Vector2Int renderSize, Camera camera, SkyColors skyColors)
        {
            

            using var builder = renderGraph.AddComputePass("Path Tracer: Render", out RenderPassData passData);

            passData.Shader = _pathTracerShader;
            passData.Kernel = _kernelIndex;
            passData.Output = output;
            passData.Resolution[0] = renderSize.x;
            passData.Resolution[1] = renderSize.y;
            passData.GroupsX = GroupCount(renderSize.x, _threadGroupSizeX);
            passData.GroupsY = GroupCount(renderSize.y, _threadGroupSizeY);

            passData.Camera = PinholeCamera.From(camera, _settings);
            passData.Sky = skyColors;

            builder.UseTexture(output, AccessFlags.Write);

            // static to reject captured variables from the outer scope
            builder.SetRenderFunc(static (RenderPassData data, ComputeGraphContext context) =>
            {
                var cmd = context.cmd;
                cmd.SetComputeTextureParam(data.Shader, data.Kernel, OutputID, data.Output);
                cmd.SetComputeIntParams(data.Shader, ResolutionID, data.Resolution);
                cmd.SetComputeMatrixParam(data.Shader, CameraToWorldID, data.Camera.CameraToWorld);
                cmd.SetComputeFloatParam(data.Shader, TanHalfFovYID, data.Camera.TanHalfFov);
                cmd.SetComputeFloatParam(data.Shader, AspectRatioID, data.Camera.AspectRatio);
                cmd.SetComputeVectorParam(data.Shader, SkyZenithID, data.Sky.Zenith.linear);
                cmd.SetComputeVectorParam(data.Shader, SkyHorizonID, data.Sky.Horizon.linear);
                cmd.SetComputeVectorParam(data.Shader, SkyGroundID, data.Sky.Ground.linear);
                cmd.DispatchCompute(data.Shader, data.Kernel, data.GroupsX, data.GroupsY, 1);
            });
        }

        /**
         * <summary>
         * Records the raster pass that clears the camera target to black and draws the output into its
         * letterboxed rectangle.
         * </summary>
         */
        private void RecordPresentStage(RenderGraph renderGraph, TextureHandle source, TextureHandle target, TextureDesc targetDesc)
        {
            Rect viewport = FitInside(_settings.OutputResolution, new Vector2Int(targetDesc.width, targetDesc.height));

            using var builder = renderGraph.AddRasterRenderPass("Path Tracer: Present", out PresentPassData passData);

            passData.Source = source;
            passData.Viewport = viewport;

            builder.UseTexture(source, AccessFlags.Read);
            builder.SetRenderAttachment(target, 0, AccessFlags.Write);

            builder.SetRenderFunc(static (PresentPassData data, RasterGraphContext context) =>
            {
                var cmd = context.cmd;
                cmd.ClearRenderTarget(false, true, Color.black);
                cmd.SetViewport(data.Viewport);
                Blitter.BlitTexture(cmd, data.Source, new Vector4(1, 1, 0, 0), 0, true);
            });
        }

        /**
         * <summary>
         * Number of thread groups needed to cover <paramref name="size"/> threads, rounded up.
         * </summary>
         */
        private static int GroupCount(int size, int groupSize) => (size + groupSize - 1) / groupSize;

        /**
         * <summary>
         * The largest rectangle with the image's aspect ratio that fits inside the target, centered.
         * </summary>
         */
        private static Rect FitInside(Vector2Int image, Vector2Int target)
        {
            float scale = Mathf.Min((float)target.x / image.x, (float)target.y / image.y);
            float width = Mathf.Round(image.x * scale);
            float height = Mathf.Round(image.y * scale);
            return new Rect(Mathf.Floor((target.x - width) * 0.5f), Mathf.Floor((target.y - height) * 0.5f), width, height);
        }

        /**
        * <summary>
        * Picks this frame's sky.
        * </summary>
        */
        private bool TryResolveSky(out SkyColors sky)
        {
            IReadOnlyList<Sky> skies = TracedRegistry.Skies;

            if (skies.Count > 1)
            {
                if (!_reportedMultipleSkies)
                    Debug.LogError($"Path tracer: {skies.Count} Sky components are enabled; disable all but one.");

                _reportedMultipleSkies = true;
                sky = default;
                return false;
            }

            _reportedMultipleSkies = false;
            sky = skies.Count == 1 ? skies[0].Colors : SkyColors.Default;
            return true;
        }



        #endregion
    }
}