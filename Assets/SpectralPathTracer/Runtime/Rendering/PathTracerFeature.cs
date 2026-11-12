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

        [Tooltip("Which AOV the image shows.")]
        [SerializeField] private AovSettings aovSettings;

        [Tooltip("The fitted RGB to spectrum table, from Tools > Spectral Path Tracer > Build Sigmoid Table.")]
        [SerializeField] private TextAsset sigmoidTable;

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

            if (aovSettings == null)
            {
                Debug.LogWarning("PathTracerFeature: No AOV settings assigned.");
                return;
            }

            // spectral mode renders every color black without the table, but RGB mode doesn't need it
            if (sigmoidTable == null)
                Debug.LogWarning("PathTracerFeature: No sigmoid table assigned, so spectral mode has no colors.");

            SigmoidTable spectra = sigmoidTable != null ? SigmoidTable.Read(sigmoidTable.bytes) : null;
            _pass = new PathTracerPass(shaders, settings, aovSettings, spectra)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null || !settings.IsEnabledFor(Application.isPlaying))
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