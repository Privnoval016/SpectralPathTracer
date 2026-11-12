using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * Renders Game cameras with the path tracer and presents the result.
     * </summary>
     */
    public class PathTracerPass : ScriptableRenderPass
    {
        #region Private Fields

        private readonly PathTracerSettings _settings;
        private readonly PathTracerRenderer _renderer;
        private readonly PresentStage _presentStage = new PresentStage();

        #endregion

        /**
         * <summary>
         * Initializes a new instance of the <see cref="PathTracerPass"/> class.
         * </summary>
         * <param name="shaders">The compute shaders for each wavefront stage.</param>
         * <param name="settings">The path tracer settings.</param>
         * <param name="aovSettings">Which AOV to show, and how.</param>
         * <param name="spectra">The table that turns RGB colors into spectra.</param>
         */
        public PathTracerPass(PathTracerShaders shaders, PathTracerSettings settings, AovSettings aovSettings, SigmoidTable spectra)
        {
            _settings = settings;
            _renderer = new PathTracerRenderer(shaders, settings, aovSettings, spectra);

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
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            if (cameraData.cameraType != CameraType.Game)
                return;

            if (!_renderer.TryRecord(renderGraph, cameraData.camera, out TextureHandle display))
                return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            TextureDesc cameraDesc = renderGraph.GetTextureDesc(resourceData.activeColorTexture);
            _presentStage.Record(renderGraph, display, resourceData.activeColorTexture, cameraDesc, _settings.OutputResolution);
        }

        /**
         * <summary>
         * Releases the renderer's persistent buffers.
         * </summary>
         */
        public void Dispose()
        {
            _renderer.Dispose();
        }
    }
}