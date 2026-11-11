using NUnit.Framework;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * Checks the Display kernel: dividing the sums by the sample count, exposure in stops, the clamp to [0, 1], AOVs
     * skipping exposure, and the gray ramp.
     * </summary>
     */
    public class DisplayTests
    {
        #region Private Fields

        private const string ShaderPath = "Assets/SpectralPathTracer/Shaders/Wavefront/Display.compute";
        private const string KernelName = "Display";
        private const float Tolerance = 1e-6f;

        // below, inside and above the displayable range, and a negative value
        private static readonly float[] Inputs = { 0.1f, 0.4f, 0.75f, 3f, -0.5f };

        #endregion

        [TestCase(0f)]
        [TestCase(1f)]
        [TestCase(-2f)]
        public void ExposureScalesInStopsThenClamps(float exposure)
        {
            float[] outputs = Run(Mathf.Pow(2f, exposure), applyExposure: true, showGrayRamp: false, sampleCount: 1);
            for (int i = 0; i < Inputs.Length; i++)
                Assert.That(outputs[i], Is.EqualTo(Mathf.Clamp01(Inputs[i] * Mathf.Pow(2f, exposure))).Within(Tolerance), $"Input {Inputs[i]}");
        }

        [Test]
        public void AovsSkipExposure()
        {
            float[] outputs = Run(exposureScale: 16f, applyExposure: false, showGrayRamp: false, sampleCount: 1);
            for (int i = 0; i < Inputs.Length; i++)
                Assert.That(outputs[i], Is.EqualTo(Mathf.Clamp01(Inputs[i])).Within(Tolerance), $"Input {Inputs[i]}");
        }

        [Test]
        public void GrayRampIsLinearAcrossColumns()
        {
            float[] outputs = Run(exposureScale: 16f, applyExposure: true, showGrayRamp: true, sampleCount: 1);
            for (int i = 0; i < Inputs.Length; i++)
                Assert.That(outputs[i], Is.EqualTo((i + 0.5f) / Inputs.Length).Within(Tolerance), $"Column {i}");
        }

        [Test]
        public void SumsAreDividedBySampleCount()
        {
            // the inputs stand for sums of 4 samples, so the displayed mean is a quarter of each
            float[] outputs = Run(exposureScale: 1f, applyExposure: true, showGrayRamp: false, sampleCount: 4);
            for (int i = 0; i < Inputs.Length; i++)
                Assert.That(outputs[i], Is.EqualTo(Mathf.Clamp01(Inputs[i] / 4f)).Within(Tolerance), $"Input {Inputs[i]}");
        }

        #region Helpers

        /**
         * <summary>
         * Runs the Display kernel on a one-row accumulation buffer holding Inputs (in every color channel).
         * </summary>
         * <param name="exposureScale">The exposure multiplier, 2^EV.</param>
         * <param name="applyExposure">Whether exposure applies, as for the rendered image.</param>
         * <param name="showGrayRamp">Whether to show the gray ramp instead.</param>
         * <param name="sampleCount">How many samples each input sum stands for.</param>
         * <returns>The red channel of each output texel.</returns>
         */
        private static float[] Run(float exposureScale, bool applyExposure, bool showGrayRamp, int sampleCount)
        {
            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ShaderPath);
            Assert.That(shader, Is.Not.Null, $"Missing {ShaderPath}");
            int kernel = shader.FindKernel(KernelName);

            var sums = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Inputs.Length, sizeof(float) * 4);
            var values = new Vector4[Inputs.Length];
            for (int i = 0; i < Inputs.Length; i++)
                values[i] = new Vector4(Inputs[i], Inputs[i], Inputs[i], sampleCount); // w is the pixel's sample count
            sums.SetData(values);

            var display = new RenderTexture(Inputs.Length, 1, 0, GraphicsFormat.R32G32B32A32_SFloat) { enableRandomWrite = true };
            display.Create();

            GraphicsBuffer constants = TestFrameConstants.Bind(shader, new FrameConstants
            {
                _ExposureScale = exposureScale,
                _ApplyExposure = applyExposure ? 1u : 0u,
                _ShowGrayRamp = showGrayRamp ? 1u : 0u
            });

            try
            {
                shader.SetBuffer(kernel, ShaderIDs.AccumulationID, sums);
                shader.SetTexture(kernel, ShaderIDs.DisplayID, display);
                shader.SetInts(ShaderIDs.ResolutionID, Inputs.Length, 1);
                shader.Dispatch(kernel, 1, 1, 1);

                AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(display);
                request.WaitForCompletion();
                NativeArray<Vector4> texels = request.GetData<Vector4>();

                var result = new float[Inputs.Length];
                for (int i = 0; i < Inputs.Length; i++)
                    result[i] = texels[i].x;
                return result;
            }
            finally
            {
                sums.Release();
                constants.Release();
                display.Release();
                Object.DestroyImmediate(display);
            }
        }

        #endregion
    }
}