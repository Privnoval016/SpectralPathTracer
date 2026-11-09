using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule.Util;

namespace SpectralPathTracer
{
    public class PathTracerPass : ScriptableRenderPass
    {
        #region Private Fields

        private static readonly int OutputID = Shader.PropertyToID("_Output");
        private static readonly int ResolutionID = Shader.PropertyToID("_Resolution");

        private readonly ComputeShader _pathTracerShader;
        private readonly int _kernelIndex;

        private readonly int _threadGroupSizeX;
        private readonly int _threadGroupSizeY;

        private readonly PathTracerSettings _settings;
        private RTHandle _output;

        #endregion

        private class RenderPassData
        {
            public ComputeShader Shader;
            public int Kernel;
            public TextureHandle Output;
            public readonly int[] Resolution = new int[2];
            public int GroupsX;
            public int GroupsY;
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

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            if (cameraData.cameraType != CameraType.Game)
                return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            TextureDesc cameraDesc = renderGraph.GetTextureDesc(resourceData.activeColorTexture);
            TextureDesc desc = cameraDesc;

            desc.format = GraphicsFormat.R16G16B16A16_SFloat;
            desc.enableRandomWrite = true;
            desc.msaaSamples = MSAASamples.None; // no anti-aliasing
            desc.clearBuffer = false;

            Vector2Int renderResolution = _settings.RenderResolution;
            desc.width = renderResolution.x;
            desc.height = renderResolution.y;
            

            RenderingUtils.ReAllocateHandleIfNeeded(ref _output, desc, "PathTracerOutput");
            TextureHandle outputTexture = renderGraph.ImportTexture(_output);

            if (!_settings.Paused)
            {
                using (var builder = renderGraph.AddComputePass("Path Tracer: Render", out RenderPassData passData))
                {
                    passData.Shader = _pathTracerShader;
                    passData.Kernel = _kernelIndex;
                    passData.Output = outputTexture;
                    passData.Resolution[0] = desc.width;
                    passData.Resolution[1] = desc.height;

                    passData.GroupsX = (desc.width + _threadGroupSizeX - 1) / _threadGroupSizeX;
                    passData.GroupsY = (desc.height + _threadGroupSizeY - 1) / _threadGroupSizeY;

                    builder.UseTexture(outputTexture, AccessFlags.Write);
                    
                    // static to reject captured variables from the outer scope
                    builder.SetRenderFunc(static (RenderPassData data, ComputeGraphContext context) =>
                    {
                        var cmd = context.cmd;
                        cmd.SetComputeTextureParam(data.Shader, data.Kernel, OutputID, data.Output);
                        cmd.SetComputeIntParams(data.Shader, ResolutionID, data.Resolution);
                        cmd.DispatchCompute(data.Shader, data.Kernel, data.GroupsX, data.GroupsY, 1);
                    });
                }
            }

            Rect viewport = FitInside(_settings.OutputResolution, new Vector2Int(cameraDesc.width, cameraDesc.height));

            // raster pass to blit the path tracer output to the camera's color buffer
            using (var builder = renderGraph.AddRasterRenderPass("Path Tracer: Present", out PresentPassData passData))
            {
                passData.Source = outputTexture;
                passData.Viewport = viewport;

                builder.UseTexture(outputTexture, AccessFlags.Read);
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);


                builder.SetRenderFunc(static (PresentPassData data, RasterGraphContext context) =>
                {
                    var cmd = context.cmd;
                    cmd.ClearRenderTarget(false, true, Color.black);
                    cmd.SetViewport(data.Viewport);
                    Blitter.BlitTexture(cmd, data.Source, new Vector4(1, 1, 0, 0), 0, true);
                });
            }

        }

        public void Dispose()
        {
            _output?.Release();
            _output = null;
        }

        #region Helpers

        /** <summary>The largest rectangle with the image's aspect ratio that fits inside the target, centered.</summary> */
        private static Rect FitInside(Vector2Int image, Vector2Int target)
        {
            float scale = Mathf.Min((float)target.x / image.x, (float)target.y / image.y);
            float width = Mathf.Round(image.x * scale);
            float height = Mathf.Round(image.y * scale);
            return new Rect(Mathf.Floor((target.x - width) * 0.5f), Mathf.Floor((target.y - height) * 0.5f), width, height);
        }


        #endregion
    }
}