using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SpectralPathTracer
{
    public class PathTracerFeature : ScriptableRendererFeature
    {
        #region Inspector Fields
        
        [Tooltip("The compute shaders for each wavefront stage.")]
        [SerializeField] private PathTracerShaders shaders;

        [Tooltip("The path tracer settings.")]
        [SerializeField] private PathTracerSettings settings;

        #endregion

        #region Private Fields

        private PathTracerPass _pass;

        #endregion

        public override void Create()
        {
            _pass?.Dispose();
            _pass = null;

            if (shaders == null || !shaders.IsComplete)
            {
                Debug.LogWarning("PathTracerFeature: Shaders asset missing or incomplete.");
                return;
            }

            if (settings == null)
            {
                Debug.LogWarning("PathTracerFeature: No settings assigned.");
                return;
            }

            _pass = new PathTracerPass(shaders, settings)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null || !settings.IsEnabledForCurrentMode)
            {
                return;
            }

            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _pass?.Dispose();
            _pass = null;
        }

    }
}