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
        private readonly ResolveStage _resolveStage;
        private readonly DisplayStage _displayStage;
        private readonly PresentStage _presentStage;

        private readonly SceneCompiler _sceneCompiler = new SceneCompiler();

        private RTHandle _radiance;

        private uint _sampleIndex;

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
            _resolveStage = new ResolveStage(shaders.Resolve);
            _displayStage = new DisplayStage(shaders.Display);
            _presentStage = new PresentStage();

            requiresIntermediateTexture = true;
        }

        /**
         * <summary>
         * Records this frame's passes into the render graph.
         * </summary>
         * <param name="renderGraph">The render graph recording this frame.</param>
         * <param name="frameData">URP's per frame data for the camera and its target.</param>
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
            var frame = new FrameContext(_settings.RenderResolution, _settings.OutputResolution, _sampleIndex, _settings.MaxBounces, PinholeCamera.From(cameraData.camera, _settings), skyColors, _aovSettings.Aov, depthRange, _aovSettings.NoiseDimension, _settings.Exposure, _settings.ShowGrayRamp);

            TextureHandle radiance = ImportRadiance(renderGraph, cameraDesc, frame.RenderSize);

            _sceneCompiler.Update();

            if (!_settings.Paused)
            {
                RecordWavefront(renderGraph, frame, radiance);
                _sampleIndex++;
            }

            // the display transform runs even while paused, so exposure can be adjusted on a frozen image
            TextureHandle display = _displayStage.Record(renderGraph, frame, radiance);
            _presentStage.Record(renderGraph, display, resourceData.activeColorTexture, cameraDesc, _settings.OutputResolution);
        }

        /**
         * <summary>
         * Releases the persistent output texture and scene buffers.
         * </summary>
         */
        public void Dispose()
        {
            _radiance?.Release();
            _radiance = null;
            _sceneCompiler.Dispose();
        }

        #region Helpers

        /**
         * <summary>
         * Makes sure the persistent radiance texture matches this frame's size and format, then imports it.
         * </summary>
         * <param name="renderGraph">The render graph recording this frame.</param>
         * <param name="cameraDesc">The camera target's descriptor, used as the starting point.</param>
         * <param name="renderSize">The size to render at this frame.</param>
         * <returns>The radiance texture's handle for this frame's render graph.</returns>
         */
        private TextureHandle ImportRadiance(RenderGraph renderGraph, TextureDesc cameraDesc, Vector2Int renderSize)
        {
            TextureDesc desc = cameraDesc;
            desc.format = GraphicsFormat.R32G32B32A32_SFloat; // accumulation needs 24 bits of precision and this is also platform agnostic
            desc.enableRandomWrite = true;
            desc.msaaSamples = MSAASamples.None;
            desc.clearBuffer = false;
            desc.width = renderSize.x;
            desc.height = renderSize.y;

            RenderingUtils.ReAllocateHandleIfNeeded(ref _radiance, desc, "PathTracerRadiance");
            return renderGraph.ImportTexture(_radiance);
        }

        /**
         * <summary>
         * Records the wavefront stages: generate camera rays, intersect and shade once per bounce, then resolve every path into the output.
         * </summary>
         * <param name="renderGraph">The render graph recording this frame.</param>
         * <param name="frame">This frame's context.</param>
         * <param name="radiance">The radiance texture to resolve into.</param>
         */
        private void RecordWavefront(RenderGraph renderGraph, in FrameContext frame, TextureHandle radiance)
        {
            SceneBuffers scene = _sceneCompiler.Import(renderGraph);

            PathBuffers paths = _generateStage.Record(renderGraph, frame);

            // depth 0 is the camera ray's hit, so MaxBounces bounces need MaxBounces + 1 trips around the loop
            for (int depth = 0; depth <= frame.MaxBounces; depth++)
            {
                BufferHandle hits = _intersectStage.Record(renderGraph, frame, paths, scene);
                _shadeStage.Record(renderGraph, frame, paths, hits, scene, depth);
            }

            _resolveStage.Record(renderGraph, frame, paths, radiance);
        }

        /**
        * <summary>
        * Picks this frame's sky.
        * </summary>
        * <param name="sky">The sky to render with.</param>
        * <returns>False if more than one Sky is enabled.</returns>
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