using System;
using UnityEditor;
using UnityEngine;

namespace SpectralPathTracer
{
    // the matching functions and the sRGB matrix have answers known from published tables so anything wrong can be sanity checked here
    public static class SpectrumCheck
    {
        private const int MonteCarloSamples = 65536;

        [MenuItem("Tools/Spectral Path Tracer/Spectrum Check")]
        public static void Run()
        {
            Vector3 flat = Integrate(lambda => 1f);
            float flatTotal = flat.x + flat.y + flat.z;

            Report("integral of y-bar", flat.y, Cie.YIntegral, 1e-3f);
            Report("flat light x", flat.x / flatTotal, 1f / 3f, 1e-3f);
            Report("flat light y", flat.y / flatTotal, 1f / 3f, 1e-3f);


            Vector3 d65 = Integrate(Cie.D65At);
            Vector3 white = Cie.XyzToLinearSrgb(d65 / d65.y); // D65 at Y = 1 should be sRGB's white

            Report("D65 red", white.x, 1f, 1e-3f);
            Report("D65 green", white.y, 1f, 1e-3f);
            Report("D65 blue", white.z, 1f, 1e-3f);


            Report("flat Y by Monte Carlo", EstimateFlatY() / Cie.YIntegral, 1f, 0.02f);


            ReportFit("mid gray", new Vector3(0.5f, 0.5f, 0.5f));
            ReportFit("Cornell red", CornellAlbedo("CornellRed"));
            ReportFit("Cornell green", CornellAlbedo("CornellGreen"));
            ReportFit("Cornell white", CornellAlbedo("CornellWhite"));

            // the primaries need the steepest spectra in sRGB
            ReportFit("pure red", Vector3.right);
            ReportFit("pure green", Vector3.up);
            ReportFit("pure blue", Vector3.forward);
        }

        /**
         * <summary>
         * Fits a color, then rebuilds it from the fitted spectrum and reports both errors.
         * </summary>
         * <param name="name">The color's name in the log.</param>
         * <param name="rgb">The linear sRGB color.</param>
         */
        private static void ReportFit(string name, Vector3 rgb)
        {
            Vector3 coefficients = Vector3.zero; // a flat 0.5 spectrum to start from
            float deltaE = SigmoidFit.Fit(rgb, ref coefficients);
            Vector3 nanometres = SigmoidFit.ToNanometres(coefficients);

            // rebuild the color from the nm coefficients with Cie alone, so a slip in ToNanometres shows up too
            Vector3 xyz = Integrate(lambda => SigmoidSpectrum.Evaluate(nanometres, lambda) * Cie.D65At(lambda));
            Vector3 rebuilt = Cie.XyzToLinearSrgb(xyz / Integrate(Cie.D65At).y);
            Vector3 difference = rebuilt - rgb;

            Report($"{name} delta E", deltaE, 0f, 0.1f);
            Report($"{name} round trip", Mathf.Max(Mathf.Abs(difference.x), Mathf.Abs(difference.y), Mathf.Abs(difference.z)), 0f, 1e-3f);
        }

        /**
         * <summary>
         * Reads a Cornell material's albedo as the GPU sees it.
         * </summary>
         * <param name="material">The material asset's name.</param>
         * <returns>The linear albedo.</returns>
         */
        private static Vector3 CornellAlbedo(string material)
        {
            var asset = AssetDatabase.LoadAssetAtPath<SpectralMaterial>($"Assets/SpectralPathTracer/Materials/Cornell/{material}.asset");
            Color linear = asset.Albedo.linear; // the picker holds sRGB, and the compiler linearizes it the same way


            return new Vector3(linear.r, linear.g, linear.b);
        }

        /**
         * <summary>
         * Integrates a spectrum against the matching functions in 1 nm steps.
         * </summary>
         * <param name="spectrum">The spectrum's value at a wavelength in nm.</param>
         * <returns>The spectrum's X, Y and Z, not yet normalized.</returns>
         */
        private static Vector3 Integrate(Func<float, float> spectrum)
        {
            Vector3 sum = Vector3.zero;
            for (int lambda = Cie.FirstWavelength; lambda <= Cie.LastWavelength; lambda++)
                sum += spectrum(lambda) * Cie.MatchingFunctions(lambda); // each row is worth 1 nm

            return sum;
        }

        /**
         * <summary>
         * Estimates a flat spectrum's Y from random wavelengths, the way paths will.
         * </summary>
         * <returns>The estimate of the integral of y-bar.</returns>
         */
        private static float EstimateFlatY()
        {
            var random = new System.Random(1); // seeded, so the check gives the same answer every run
            const float range = Cie.LastWavelength - Cie.FirstWavelength;
            const float pdf = 1f / range; // uniform over the table's range

            double sum = 0;
            for (int i = 0; i < MonteCarloSamples; i++)
            {
                float lambda = Cie.FirstWavelength + (float)random.NextDouble() * range;
                sum += Cie.MatchingFunctions(lambda).y / pdf;
            }


            return (float)(sum / MonteCarloSamples);
        }

        private static void Report(string name, float value, float expected, float tolerance)
        {
            float error = Mathf.Abs(value - expected);
            bool passed = error <= tolerance;

            string line = $"Spectrum {(passed ? "PASS" : "FAIL")}: {name} {value:F5}, expected {expected:F5} (off by {error:F5}, tolerance {tolerance:F5})";
            if (passed)
                Debug.Log(line);
            else
                Debug.LogError(line);
        }
    }
}
