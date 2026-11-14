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
            bool spectral = IsSpectral(out float lockedWavelength);
            bool locked = spectral && lockedWavelength > 0f;

            Vector4 min = Vector4.positiveInfinity;
            Vector4 max = Vector4.negativeInfinity;
            double r = 0, g = 0, b = 0; // half a million floats summed as floats would drift

            foreach (Color mean in means)
            {
                // spectral mode accumulates XYZ, so compare in the same linear sRGB as RGB mode
                Vector4 color = mean;
                if (spectral && !locked)
                {
                    Vector3 rgb = Cie.XyzToLinearSrgb(new Vector3(mean.r, mean.g, mean.b));
                    color = new Vector4(rgb.x, rgb.y, rgb.z, mean.a);
                }

                min = Vector4.Min(min, color);
                max = Vector4.Max(max, color);
                r += color.x;
                g += color.y;
                b += color.z;
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

            // a locked image is radiance at one wavelength, so white light reads D65 there rather than 1
            float expected = locked ? expectation.Expected * D65Radiance(lockedWavelength) : expectation.Expected;

            // one random wavelength per sample leaves single pixels noisy, but a locked wavelength is exact again
            bool meanOnly = spectral && !locked;
            float tolerance = meanOnly ? expectation.SpectralTolerance : expectation.Tolerance;
            float error = Deviation(average, expected);
            if (expectation.EveryPixel && !meanOnly)
                error = Mathf.Max(error, Deviation(min, expected), Deviation(max, expected));

            bool passed = error <= tolerance;
            string mode = locked ? $"locked at {lockedWavelength:F1} nm, expecting {expected:F5}" : spectral ? "spectral, mean only" : "RGB";
            string verdict = $"Furnace {(passed ? "PASS" : "FAIL")} ({mode}): off by {error:F5} (tolerance {tolerance:F5}), {numbers}";
            if (passed)
                Debug.Log(verdict);
            else
                Debug.LogError(verdict); // shows up in the console's error count without reading the message
        }

        /**
         * <summary>
         * Finds the project's path tracer settings asset.
         * </summary>
         * <returns>The settings, or null if there are none.</returns>
         */
        internal static PathTracerSettings LoadSettings()
        {
            string[] guids = AssetDatabase.FindAssets("t:PathTracerSettings");
            return guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<PathTracerSettings>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
        }

        internal static bool IsSpectral(out float lockedWavelength)
        {
            PathTracerSettings settings = LoadSettings();

            lockedWavelength = PathTracerOverrides.LockedWavelength ?? (settings != null && settings.LockWavelength ? settings.LockedWavelength : 0f);


            return settings != null && settings.Spectral;
        }

        /**
         * <summary>
         * D65 at one wavelength, scaled the way the GPU scales white light.
         * </summary>
         * <param name="lambda">The wavelength in nm.</param>
         * <returns>The radiance a white light of 1 has at that wavelength.</returns>
         */
        private static float D65Radiance(float lambda)
        {
            return Cie.D65At(lambda) * Cie.D65Scale(); // the same scale as CIE_D65_SCALE
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
