using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SpectralPathTracer
{
    public class PathTracerFeature : ScriptableRendererFeature
    {
        #region Inspector Fields
        
        [Tooltip("The compute shader containing the path tracer kernels.")]
        [SerializeField] private ComputeShader pathTracerShader;

        [Tooltip("The color to fill the canvas with.")]
        [SerializeField] private Color fillColor = Color.magenta;

        #endregion

        #region Private Fields

        private const string FillKernelName = "FillCanvas";

        private PathTracerPass _pass;

        #endregion

        public override void Create()
        {
            _pass = null;

            if (pathTracerShader == null)
            {
                Debug.LogWarning("PathTracerFeature: No compute shader assigned.");
                return;
            }

            int kernelIndex = pathTracerShader.FindKernel(FillKernelName);

            // convert to linear space for correct color representation
            _pass = new PathTracerPass(pathTracerShader, kernelIndex, fillColor.linear)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null)
            {
                return;
            }

            renderer.EnqueuePass(_pass);
        }

    }
}