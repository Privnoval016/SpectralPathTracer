using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpectralPathTracer
{
    // in a uniform sky of 1 every lossless material reads exactly 1 at any sample count, so after a change
    // anything else in the furnace means a weight, a pdf or a leak is wrong
    public static class FurnaceCheck
    {
        // the Game view's class is internal to the Editor, so it can only be found by name
        private static readonly Type GameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");

        /**
         * <summary>
         * Measures the accumulated image and logs its mean, min, max and sample counts.
         * </summary>
         */
        [MenuItem("Tools/Spectral Path Tracer/Furnace Check")]
        public static void Run()
        {
            AccumulationCapture.Request(Report);

            // a converged image stops repainting, so draw one more frame to take the request
            foreach (Object view in Resources.FindObjectsOfTypeAll(GameViewType))
                ((EditorWindow)view).Repaint();
        }

        private static void Report(Color[] means, Vector2Int size)
        {
            Vector4 min = Vector4.positiveInfinity;
            Vector4 max = Vector4.negativeInfinity;
            double r = 0, g = 0, b = 0; // half a million floats summed as floats would drift

            foreach (Color mean in means)
            {
                min = Vector4.Min(min, mean);
                max = Vector4.Max(max, mean);
                r += mean.r;
                g += mean.g;
                b += mean.b;
            }


            int n = means.Length;
            var average = new Vector3((float)(r / n), (float)(g / n), (float)(b / n));
            string numbers = $"mean {average.ToString("F5")}, min {((Vector3)min).ToString("F5")}, " +
                             $"max {((Vector3)max).ToString("F5")}, samples {min.w} to {max.w} at {size.x} by {size.y}";

            var expectation = Object.FindAnyObjectByType<FurnaceExpectation>();
            if (expectation == null)
            {
                Debug.Log($"Furnace: {numbers}");
                return;
            }

            float error = Deviation(average, expectation.Expected);
            if (expectation.EveryPixel)
                error = Mathf.Max(error, Deviation(min, expectation.Expected), Deviation(max, expectation.Expected));

            bool passed = error <= expectation.Tolerance;
            string verdict = $"Furnace {(passed ? "PASS" : "FAIL")}: off by {error:F5} (tolerance {expectation.Tolerance:F5}), {numbers}";
            if (passed)
                Debug.Log(verdict);
            else
                Debug.LogError(verdict); // shows up in the console's error count without reading the message
        }

        /**
         * <summary>
         * The largest difference between any channel and the expected value.
         * </summary>
         * <param name="value">The reading, one value per channel.</param>
         * <param name="expected">The value every channel should have.</param>
         * <returns>The worst channel's absolute error.</returns>
         */
        private static float Deviation(Vector3 value, float expected)
        {
            return Mathf.Max(Mathf.Abs(value.x - expected), Mathf.Abs(value.y - expected), Mathf.Abs(value.z - expected));
        }
    }
}
