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
        private readonly LivePathStats _liveStats = new LivePathStats();
        private readonly DisplayCapture _capture = new DisplayCapture();
        private readonly AccumulationCapture _sumsCapture = new AccumulationCapture();
        private readonly WaveScheduler _scheduler = new WaveScheduler();

        private RenderParams _params;
        private uint _sampleCount;

        private Camera _camera;
        private Camera _reportedCamera;

        #endregion

        #region Properties

        // check overrides first
        private int TargetSamples => PathTracerOverrides.TargetSamples ?? _settings.TargetSamples;
        private float FrameBudgetMs => PathTracerOverrides.FrameBudgetMs ?? _settings.FrameBudgetMs;

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
            if (!TryClaimCamera(camera) || !_sceneCompiler.TryUpdate(out bool sceneChanged))
                return false;

            var pinhole = PinholeCamera.From(camera, _settings.OutputAspectRatio);
            var renderParams = RenderParams.From(_settings, _aovSettings, pinhole, Application.isPlaying);

            UpdateAccumulation(renderParams, sceneChanged);
            bool accumulating = !_settings.Paused && (TargetSamples == 0 || _sampleCount < TargetSamples);

            BufferHandle constants = _constants.Record(renderGraph, BuildConstants(renderParams));
            var frame = new FrameContext(renderParams, constants);

            BufferHandle accumulation = _accumulation.Import(renderGraph);
            if (accumulating)
            {
                BufferHandle livePaths = _liveStats.RecordClear(renderGraph);
                RecordWavefront(renderGraph, frame, accumulation, livePaths);
                _liveStats.RecordReadback(renderGraph, livePaths);
            }
            else
                _scheduler.Idle();

            AccumulationProgress.Report((int)_sampleCount, TargetSamples, accumulating);

            // runs even while paused, so exposure works on a frozen image
            display = _displayStage.Record(renderGraph, frame, accumulation);
            _capture.Record(renderGraph, display, frame.RenderSize);
            _sumsCapture.Record(renderGraph, accumulation, frame.RenderSize);
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
            _liveStats.Dispose();
            _sceneCompiler.Dispose();
        }

        #region Helpers

        /**
         * <summary>
         * Keeps the renderer on one camera, since its sums belong to a single view.
         * </summary>
         * <param name="camera">The camera asking to render.</param>
         * <returns>False if another active camera already owns the renderer.</returns>
         */
        private bool TryClaimCamera(Camera camera)
        {
            if (_camera == null || !_camera.isActiveAndEnabled)
                _camera = camera;

            if (camera == _camera)
                return true;

            if (camera != _reportedCamera)
                Debug.LogWarning($"Path tracer: rendering {_camera.name} only, so {camera.name} is skipped.");

            _reportedCamera = camera;
            return false;
        }

        /**
         * <summary>
         * Restarts the running sums when anything upstream of them changed: the scene, the camera, the render size
         * or any setting that changes the rendered image. Exposure applies after the average.
         * </summary>
         * <param name="renderParams">This frame's parameters.</param>
         * <param name="sceneChanged">Whether the scene or sky changed this frame.</param>
         */
        private void UpdateAccumulation(in RenderParams renderParams, bool sceneChanged)
        {
            bool resized = _accumulation.Ensure(renderParams.RenderSize);
            bool requested = PathTracerOverrides.TakeRestart();

            if (resized || sceneChanged || requested || !renderParams.Equals(_params))
            {
                _sampleCount = 0;
                _scheduler.Restart();
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

                _SkyZenithColor = _sceneCompiler.Sky.Zenith.linear,
                _SkyHorizonColor = _sceneCompiler.Sky.Horizon.linear,
                _SkyGroundColor = _sceneCompiler.Sky.Ground.linear,

                _MaxBounces = (uint)renderParams.MaxBounces,
                _PixelJitter = renderParams.PixelJitter ? 1u : 0u,
                _RussianRoulette = renderParams.RussianRoulette ? 1u : 0u,

                _Aov = (uint)renderParams.Aov,
                _DepthRange = renderParams.DepthRange,
                _NoiseDimension = (uint)renderParams.NoiseDimension,

                _ExposureScale = Mathf.Pow(2f, _settings.Exposure),
                _ApplyExposure = renderParams.Aov == Aov.None ? 1u : 0u, // AOVs are data with fixed encodings so they skip exposure
                _ShowGrayRamp = _settings.ShowGrayRamp ? 1u : 0u,
                _ShowSpectrumStrip = _settings.ShowSpectrumStrip ? 1u : 0u
            };
        }

        /**
         * <summary>
         * Records as many waves as fit in the frame budget, continuing from the scheduler's cursor.
         * </summary>
         * <param name="renderGraph">The render graph recording this frame.</param>
         * <param name="frame">This frame's context.</param>
         * <param name="accumulation">The running sums to add this frame's samples to.</param>
         * <param name="livePaths">The per-depth live path counts, cleared this frame.</param>
         */
        private void RecordWavefront(RenderGraph renderGraph, in FrameContext frame, BufferHandle accumulation, BufferHandle livePaths)
        {
            SceneBuffers scene = _sceneCompiler.Import(renderGraph);

            int wavesPerSample = (frame.PixelCount + WaveSize - 1) / WaveSize;
            int waves = _scheduler.Plan(FrameBudgetMs, wavesPerSample);
            RenderStats.ReportFrame(_scheduler.MsPerWave * waves, waves);

            for (int i = 0; i < waves; i++)
            {
                // a new sample only starts below the target
                if (_scheduler.Cursor == 0 && TargetSamples != 0 && _sampleCount >= TargetSamples)
                    break;

                RecordWave(renderGraph, frame, scene, accumulation, livePaths, _scheduler.Cursor * WaveSize);

                if (_scheduler.Advance(wavesPerSample))
                    _sampleCount++;
            }
        }

        /**
         * <summary>
         * Records one wave: generate camera rays, intersect and shade once per bounce, then resolve into the sums.
         * </summary>
         * <param name="renderGraph">The render graph recording this frame.</param>
         * <param name="frame">This frame's context.</param>
         * <param name="scene">The scene's buffers, imported this frame.</param>
         * <param name="accumulation">The running sums to add this wave's samples to.</param>
         * <param name="livePaths">The per-depth live path counts.</param>
         * <param name="waveStart">The first pixel the wave covers.</param>
         */
        private void RecordWave(RenderGraph renderGraph, in FrameContext frame, in SceneBuffers scene, BufferHandle accumulation, BufferHandle livePaths, int waveStart)
        {
            int waveCount = Mathf.Min(WaveSize, frame.PixelCount - waveStart);
            PathBuffers paths = _generateStage.Record(renderGraph, frame, waveStart, waveCount, _sampleCount);

            // depth 0 is the camera ray's hit, so N bounces take N + 1 passes
            for (int depth = 0; depth <= frame.MaxBounces; depth++)
            {
                _intersectStage.Record(renderGraph, frame, paths, scene, depth);
                _shadeStage.Record(renderGraph, frame, paths, scene, depth);

                // only the live paths Shade queued get dispatched next, so after the last bounce nothing needs to read the queue
                if (depth < frame.MaxBounces)
                    _argsStage.Record(renderGraph, paths, depth, livePaths);
            }

            _resolveStage.Record(renderGraph, frame, paths, accumulation);
        }

        #endregion
    }
}