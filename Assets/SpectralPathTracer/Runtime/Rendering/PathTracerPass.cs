using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;
using System.Collections.Generic;

namespace SpectralPathTracer
{
    public class PathTracerPass : ScriptableRenderPass
    {
        #region Private Fields

        private readonly PathTracerSettings _settings;
        private readonly AovSettings _aovSettings;

        private readonly GenerateStage _generateStage;
        private readonly IntersectStage _intersectStage;
        private readonly ShadeStage _shadeStage;
        private readonly PresentStage _presentStage;

        private RTHandle _output;

        private bool _reportedMultipleSkies;

        #endregion

        /**
         * <summary>
         * Initializes a new instance of the <see cref="PathTracerPass"/> class.
         * </summary>
         * <param name="shaders">The compute shaders for each wavefront stage.</param>
         * <param name="settings">The path tracer settings.</param>
         * <param name="aovSettings">Which AOV to show, and how.</param>
         */
        public PathTracerPass(PathTracerShaders shaders, PathTracerSettings settings, AovSettings aovSettings)
        {
            _settings = settings;
            _aovSettings = aovSettings;

            _generateStage = new GenerateStage(shaders.Generate);
            _intersectStage = new IntersectStage(shaders.Intersect);
            _shadeStage = new ShadeStage(shaders.Shade);
            _presentStage = new PresentStage();

            requiresIntermediateTexture = true;
        }

        /**
         * <summary>
         * Records this frame's passes: prepare the output, run the wavefront stages into it unless paused, present it.
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
            var depthRange = new Vector2(_aovSettings.DepthNear, _aovSettings.DepthFar);
            var frame = new FrameContext(_settings.RenderResolution, PinholeCamera.From(cameraData.camera, _settings), skyColors, _aovSettings.Aov, depthRange);

            TextureHandle output = ImportOutput(renderGraph, cameraDesc, frame.RenderSize);

            if (!_settings.Paused)
                RecordWavefront(renderGraph, frame, output);

            _presentStage.Record(renderGraph, output, resourceData.activeColorTexture, cameraDesc, _settings.OutputResolution);
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
         * Records the wavefront stages in order: generate camera rays, intersect them, shade the hits into the output.
         * </summary>
         */
        private void RecordWavefront(RenderGraph renderGraph, in FrameContext frame, TextureHandle output)
        {
            BufferHandle rays = _generateStage.Record(renderGraph, frame);
            BufferHandle hits = _intersectStage.Record(renderGraph, frame, rays);
            _shadeStage.Record(renderGraph, frame, rays, hits, output);
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