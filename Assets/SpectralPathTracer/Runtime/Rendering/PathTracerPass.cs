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
        private static readonly int ColorID = Shader.PropertyToID("_Color");
        private static readonly int ResolutionID = Shader.PropertyToID("_Resolution");

        private readonly ComputeShader _pathTracerShader;
        private readonly int _fillKernelIndex;
        private readonly Color _linearColor;

        private readonly int _threadGroupSizeX;
        private readonly int _threadGroupSizeY;

        #endregion

        private class PassData
        {
            public ComputeShader Shader;
            public int Kernel;
            public TextureHandle Output;
            public Vector4 Color;
            public readonly int[] Resolution = new int[2];
            public int GroupsX;
            public int GroupsY;
        }

        /**
         * <summary>
         * Initializes a new instance of the <see cref="PathTracerPass"/> class.
         * </summary>
         * <param name="pathTracerShader">The compute shader containing the path tracer kernels.</param>
         * <param name="fillKernelIndex">The index of the kernel used to fill the canvas.</param>
         * <param name="linearColor">The color to fill the canvas with, in linear space.</param>
         */
        public PathTracerPass(ComputeShader pathTracerShader, int fillKernelIndex, Color linearColor)
        {
            _pathTracerShader = pathTracerShader;
            _fillKernelIndex = fillKernelIndex;
            _linearColor = linearColor;

            pathTracerShader.GetKernelThreadGroupSizes(fillKernelIndex, out uint threadGroupSizeX, out uint threadGroupSizeY, out _);
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

            TextureDesc desc = renderGraph.GetTextureDesc(resourceData.activeColorTexture);

            desc.name = "PathTracerOutput";
            desc.format = GraphicsFormat.R16G16B16A16_SFloat;
            desc.enableRandomWrite = true;
            desc.msaaSamples = MSAASamples.None; // no anti-aliasing
            desc.clearBuffer = false;
            TextureHandle outputTexture = renderGraph.CreateTexture(desc);

            using (var builder = renderGraph.AddComputePass("Path Tracer: Fill Canvas", out PassData passData))
            {
                passData.Shader = _pathTracerShader;
                passData.Kernel = _fillKernelIndex;
                passData.Output = outputTexture;
                passData.Color = _linearColor;
                passData.Resolution[0] = desc.width;
                passData.Resolution[1] = desc.height;

                passData.GroupsX = (desc.width + _threadGroupSizeX - 1) / _threadGroupSizeX;
                passData.GroupsY = (desc.height + _threadGroupSizeY - 1) / _threadGroupSizeY;

                builder.UseTexture(outputTexture, AccessFlags.Write);
                
                // static to reject captured variables from the outer scope
                builder.SetRenderFunc(static (PassData data, ComputeGraphContext context) =>
                {
                    var cmd = context.cmd;
                    cmd.SetComputeTextureParam(data.Shader, data.Kernel, OutputID, data.Output);
                    cmd.SetComputeVectorParam(data.Shader, ColorID, data.Color);
                    cmd.SetComputeIntParams(data.Shader, ResolutionID, data.Resolution);
                    cmd.DispatchCompute(data.Shader, data.Kernel, data.GroupsX, data.GroupsY, 1);
                });
            }

            renderGraph.AddBlitPass(outputTexture, resourceData.activeColorTexture, Vector2.one, Vector2.zero, passName: "Path Tracer: Blit Output");
        }
    }
}