using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SpectralPathTracer
{
    public class PathTracerFeature : ScriptableRendererFeature
    {
        #region Inspector Fields
        
        [Tooltip("The compute shader containing the path tracer kernels.")]
        [SerializeField] private ComputeShader pathTracerShader;

        [Tooltip("The path tracer settings.")]
        [SerializeField] private PathTracerSettings settings;

        [Tooltip("The color to fill the canvas with.")]
        [SerializeField] private Color fillColor = Color.magenta;

        #endregion

        #region Private Fields

        private const string FillKernelName = "FillCanvas";

        private PathTracerPass _pass;

        #endregion

        public override void Create()
        {
            _pass?.Dispose();
            _pass = null;

            if (pathTracerShader == null)
            {
                Debug.LogWarning("PathTracerFeature: No compute shader assigned.");
                return;
            }

            if (settings == null)
            {
                Debug.LogWarning("PathTracerFeature: No settings assigned.");
                return;
            }

            int kernelIndex = pathTracerShader.FindKernel(FillKernelName);

            // convert to linear space for correct color representation
            _pass = new PathTracerPass(pathTracerShader, settings, kernelIndex, fillColor.linear)
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