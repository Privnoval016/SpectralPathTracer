using System;
using UnityEditor;
using UnityEngine;

namespace SpectralPathTracer
{
    // the matching functions and the sRGB matrix have answers known from published tables so anything wrong can be sanity checked here
    public static class SpectrumCheck
    {
        private const int MonteCarloSamples = 65536;
        private const int TableSamples = 1000; // random colors sent through the sigmoid table

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


            ReportTable();


            ReportMeasured("White");
            ReportMeasured("Red");
            ReportMeasured("Green");
        }

        /**
         * <summary>
         * Compares a measured Cornell wall's color with the hand-picked color the RGB scene uses.
         * </summary>
         * <param name="wall">The wall's column name in the Cornell data.</param>
         */
        private static void ReportMeasured(string wall)
        {
            var spectrum = AssetDatabase.LoadAssetAtPath<MeasuredSpectrum>($"Assets/SpectralPathTracer/Data/Spectra/CornellBox/CornellBoxWalls{wall}.asset");
            if (spectrum == null)
            {
                Debug.LogError($"Spectrum FAIL: no measured {wall} wall, import Data/Spectra/CornellBox/CornellBoxWalls.csv");
                return;
            }

            Vector3 measured = spectrum.ReflectanceRgb();
            Vector3 picked = CornellAlbedo($"Cornell{wall}");
            float deltaE = (SigmoidFit.SrgbToLab(measured) - SigmoidFit.SrgbToLab(picked)).magnitude;


            // a report rather than a pass, since the picked colors were never meant to match exactly
            Debug.Log($"Spectrum note: measured {wall} wall is {measured.ToString("F3")}, the picked color {picked.ToString("F3")}, delta E {deltaE:F2}");
        }

        /**
         * <summary>
         * Sends random colors through the sigmoid table and measures how far each comes back.
         * </summary>
         */
        private static void ReportTable()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(SigmoidTableBuilder.OutputPath);
            if (asset == null)
            {
                Debug.LogError("Spectrum FAIL: no sigmoid table yet, run Tools > Spectral Path Tracer > Build Sigmoid Table");
                return;
            }

            SigmoidTable table = SigmoidTable.Read(asset.bytes);
            float whiteY = Integrate(Cie.D65At).y;
            var random = new System.Random(2); // seeded, so the same colors every run

            float worst = 0f, total = 0f;
            for (int n = 0; n < TableSamples; n++)
            {
                var rgb = new Vector3((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble());
                Vector3 coefficients = table.Lookup(rgb);

                // light the looked-up spectrum with D65 and see which color comes back
                Vector3 xyz = Integrate(lambda => SigmoidSpectrum.Evaluate(coefficients, lambda) * Cie.D65At(lambda));
                Vector3 rebuilt = Cie.XyzToLinearSrgb(xyz / whiteY);

                float deltaE = (SigmoidFit.SrgbToLab(rebuilt) - SigmoidFit.SrgbToLab(rgb)).magnitude;
                worst = Mathf.Max(worst, deltaE);
                total += deltaE;
            }


            Report("table worst delta E", worst, 0f, 1f); // below 1 is invisible side by side
            Debug.Log($"Spectrum note: table mean delta E {total / TableSamples:F4} over {TableSamples} random colors");
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
