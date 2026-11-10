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

        private GraphicsBuffer _accumulation;
        private AccumulationSignature _signature;
        private uint _sampleCount;

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
            var camera = PinholeCamera.From(cameraData.camera, _settings);
            Vector2Int renderSize = _settings.RenderResolution;

            UpdateAccumulation(camera, renderSize, skyColors);
            bool accumulating = !_settings.Paused && (_settings.TargetSamples == 0 || _sampleCount < _settings.TargetSamples);

            var depthRange = new Vector2(_aovSettings.DepthNear, _aovSettings.DepthFar);
            var frame = new FrameContext(renderSize, _settings.OutputResolution, _sampleCount, _settings.MaxBounces, camera, skyColors, _aovSettings.Aov, depthRange, _aovSettings.NoiseDimension, _settings.Exposure, _settings.ShowGrayRamp, _settings.PixelJitter);

            BufferHandle accumulation = renderGraph.ImportBuffer(_accumulation);
            if (accumulating)
            {
                RecordWavefront(renderGraph, frame, accumulation);
                _sampleCount++;
            }

            AccumulationProgress.Report((int)_sampleCount, _settings.TargetSamples, accumulating);

            // the display transform runs even while paused, so exposure can be adjusted on a frozen image
            TextureHandle display = _displayStage.Record(renderGraph, frame, accumulation, (int)_sampleCount);
            _presentStage.Record(renderGraph, display, resourceData.activeColorTexture, cameraDesc, _settings.OutputResolution);
        }

        /**
         * <summary>
         * Releases the persistent accumulation buffer and scene buffers.
         * </summary>
         */
        public void Dispose()
        {
            _accumulation?.Release();
            _accumulation = null;
            _sceneCompiler.Dispose();
        }

        #region Helpers

        /**
         * <summary>
         * Restarts the running sums when anything upstream of them changed: the scene, the camera, the render size
         * or any setting that changes the rendered image. Exposure isn't one of them; it applies after the average.
         * </summary>
         * <param name="camera">This frame's camera.</param>
         * <param name="renderSize">The size to render at this frame.</param>
         * <param name="sky">This frame's sky.</param>
         */
        private void UpdateAccumulation(PinholeCamera camera, Vector2Int renderSize, SkyColors sky)
        {
            bool resized = EnsureAccumulationBuffer(renderSize);
            bool sceneChanged = _sceneCompiler.Update();
            var signature = new AccumulationSignature(camera, renderSize, _settings.MaxBounces, _settings.PixelJitter, sky, _aovSettings);

            if (resized || sceneChanged || !signature.Equals(_signature))
            {
                _sampleCount = 0;
                _signature = signature;
            }
        }

        /**
         * <summary>
         * Makes sure the accumulation buffer holds one float4 sum per pixel, reallocating it when the render size changes.
         * </summary>
         * <param name="renderSize">The size to render at this frame.</param>
         * <returns>True if the buffer was reallocated, so its contents are undefined.</returns>
         */
        private bool EnsureAccumulationBuffer(Vector2Int renderSize)
        {
            int count = renderSize.x * renderSize.y;
            if (_accumulation != null && _accumulation.count == count)
                return false;

            _accumulation?.Release();
            _accumulation = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, sizeof(float) * 4) { name = "PathTracerAccumulation" };
            return true;
        }

        /**
         * <summary>
         * Records the wavefront stages: generate camera rays, intersect and shade once per bounce, then resolve every path into the output.
         * </summary>
         * <param name="renderGraph">The render graph recording this frame.</param>
         * <param name="frame">This frame's context.</param>
         * <param name="accumulation">The running sums to add this frame's samples to.</param>
         */
        private void RecordWavefront(RenderGraph renderGraph, in FrameContext frame, BufferHandle accumulation)
        {
            SceneBuffers scene = _sceneCompiler.Import(renderGraph);

            PathBuffers paths = _generateStage.Record(renderGraph, frame);

            // depth 0 is the camera ray's hit, so MaxBounces bounces need MaxBounces + 1 trips around the loop
            for (int depth = 0; depth <= frame.MaxBounces; depth++)
            {
                BufferHandle hits = _intersectStage.Record(renderGraph, frame, paths, scene);
                _shadeStage.Record(renderGraph, frame, paths, hits, scene, depth);
            }

            _resolveStage.Record(renderGraph, frame, paths, accumulation);
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