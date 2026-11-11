using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * Renders the path traced image into a render graph.
     * </summary>
     */
    public class PathTracerRenderer
    {
        #region Private Fields

        // path slots per wave, so the wavefront stages can be dispatched in parallel
        private const int WaveSize = 1 << 18;

        private readonly PathTracerSettings _settings;
        private readonly AovSettings _aovSettings;

        private readonly GenerateStage _generateStage;
        private readonly IntersectStage _intersectStage;
        private readonly ShadeStage _shadeStage;
        private readonly ResolveStage _resolveStage;
        private readonly ArgsStage _argsStage;
        private readonly DisplayStage _displayStage;

        private readonly SceneCompiler _sceneCompiler = new SceneCompiler();
        private readonly AccumulationBuffer _accumulation = new AccumulationBuffer();
        private readonly FrameConstantsBuffer _constants = new FrameConstantsBuffer();

        private RenderParams _params;
        private uint _sampleCount;

        private bool _reportedMultipleSkies;

        #endregion

        /**
         * <summary>
         * Creates the renderer and its stages.
         * </summary>
         * <param name="shaders">The compute shaders for each wavefront stage.</param>
         * <param name="settings">The path tracer settings.</param>
         * <param name="aovSettings">Which AOV to show, and how.</param>
         */
        public PathTracerRenderer(PathTracerShaders shaders, PathTracerSettings settings, AovSettings aovSettings)
        {
            _settings = settings;
            _aovSettings = aovSettings;

            _generateStage = new GenerateStage(shaders.Generate);
            _intersectStage = new IntersectStage(shaders.Intersect);
            _shadeStage = new ShadeStage(shaders.Shade);
            _resolveStage = new ResolveStage(shaders.Resolve);
            _argsStage = new ArgsStage(shaders.Args);
            _displayStage = new DisplayStage(shaders.Display);
        }

        /**
         * <summary>
         * Records this frame's sample, if still accumulating, and the display transform.
         * </summary>
         * <param name="renderGraph">The render graph recording this frame.</param>
         * <param name="camera">The camera to render from.</param>
         * <param name="display">The displayable image, valid for this frame's render graph.</param>
         * <returns>False if the scene can't be rendered this frame.</returns>
         */
        public bool TryRecord(RenderGraph renderGraph, Camera camera, out TextureHandle display)
        {
            display = TextureHandle.nullHandle;
            if (!TryResolveSky(out SkyColors skyColors))
                return false;

            var pinhole = PinholeCamera.From(camera, _settings);
            var renderParams = RenderParams.From(_settings, _aovSettings, pinhole, skyColors);

            UpdateAccumulation(renderParams);
            bool accumulating = !_settings.Paused && (_settings.TargetSamples == 0 || _sampleCount < _settings.TargetSamples);

            BufferHandle constants = _constants.Record(renderGraph, BuildConstants(renderParams));
            var frame = new FrameContext(renderParams, constants);

            BufferHandle accumulation = _accumulation.Import(renderGraph);
            if (accumulating)
            {
                RecordWavefront(renderGraph, frame, accumulation);
                _sampleCount++;
            }

            AccumulationProgress.Report((int)_sampleCount, _settings.TargetSamples, accumulating);

            // runs even while paused, so exposure works on a frozen image
            display = _displayStage.Record(renderGraph, frame, accumulation);
            return true;
        }

        /**
         * <summary>
         * Releases the persistent accumulation buffer and scene buffers.
         * </summary>
         */
        public void Dispose()
        {
            _accumulation.Dispose();
            _constants.Dispose();
            _sceneCompiler.Dispose();
        }

        #region Helpers

        /**
         * <summary>
         * Restarts the running sums when anything upstream of them changed: the scene, the camera, the render size
         * or any setting that changes the rendered image. Exposure applies after the average.
         * </summary>
         * <param name="renderParams">This frame's parameters.</param>
         */
        private void UpdateAccumulation(in RenderParams renderParams)
        {
            bool resized = _accumulation.Ensure(renderParams.RenderSize);
            bool sceneChanged = _sceneCompiler.Update();

            if (resized || sceneChanged || !renderParams.Equals(_params))
            {
                _sampleCount = 0;
                _params = renderParams;
            }
        }

        /**
         * <summary>
         * Packs this frame's values into the layout the shaders read.
         * </summary>
         * <param name="renderParams">This frame's parameters.</param>
         * <returns>This frame's constants.</returns>
         */
        private FrameConstants BuildConstants(in RenderParams renderParams)
        {
            return new FrameConstants
            {
                _CameraToWorld = renderParams.Camera.CameraToWorld,
                _TanHalfFov = renderParams.Camera.TanHalfFov,
                _AspectRatio = renderParams.Camera.AspectRatio,
                _OutputScale = renderParams.OutputScale,

                _SkyZenithColor = renderParams.Sky.Zenith.linear,
                _SkyHorizonColor = renderParams.Sky.Horizon.linear,
                _SkyGroundColor = renderParams.Sky.Ground.linear,

                _SampleIndex = _sampleCount,
                _MaxBounces = (uint)renderParams.MaxBounces,
                _PixelJitter = renderParams.PixelJitter ? 1u : 0u,
                _RussianRoulette = renderParams.RussianRoulette ? 1u : 0u,

                _Aov = (uint)renderParams.Aov,
                _DepthRange = renderParams.DepthRange,
                _NoiseDimension = (uint)renderParams.NoiseDimension,

                _ExposureScale = Mathf.Pow(2f, _settings.Exposure),
                _ApplyExposure = renderParams.Aov == Aov.None ? 1u : 0u, // AOVs are data with fixed encodings so they skip exposure
                _ShowGrayRamp = _settings.ShowGrayRamp ? 1u : 0u
            };
        }

        /**
         * <summary>
         * Records every wave: generate camera rays, intersect and shade once per bounce, then resolve into the sums.
         * </summary>
         * <param name="renderGraph">The render graph recording this frame.</param>
         * <param name="frame">This frame's context.</param>
         * <param name="accumulation">The running sums to add this frame's samples to.</param>
         */
        private void RecordWavefront(RenderGraph renderGraph, in FrameContext frame, BufferHandle accumulation)
        {
            SceneBuffers scene = _sceneCompiler.Import(renderGraph);

            for (int waveStart = 0; waveStart < frame.PixelCount; waveStart += WaveSize)
            {
                int waveCount = Mathf.Min(WaveSize, frame.PixelCount - waveStart);
                PathBuffers paths = _generateStage.Record(renderGraph, frame, waveStart, waveCount);

                // depth 0 is the camera ray's hit, so N bounces take N + 1 passes
                for (int depth = 0; depth <= frame.MaxBounces; depth++)
                {
                    BufferHandle hits = _intersectStage.Record(renderGraph, frame, paths, scene, depth);
                    _shadeStage.Record(renderGraph, frame, paths, hits, scene, depth);

                    // only the live paths Shade queued get dispatched next, so after the last bounce nothing needs to read the queue
                    if (depth < frame.MaxBounces)
                        _argsStage.Record(renderGraph, paths, depth);
                }

                _resolveStage.Record(renderGraph, frame, paths, accumulation);
            }
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