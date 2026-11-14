using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpectralPathTracer
{
    [Serializable]
    public struct SpectralLine
    {
        [Tooltip("The line's center in nm.")]
        public float Center;

        [Tooltip("The line's full width at half its peak, in nm.")]
        [Min(0.05f)]
        public float Width;

        [Tooltip("The line's share of the lamp's power, relative to the other lines.")]
        [Min(0f)]
        public float Strength;
    }

    // a lamp that emits only at a few sharp wavelengths, each line a narrow Gaussian
    [CreateAssetMenu(fileName = "LineSpectrum", menuName = "Spectral Path Tracer/Line Spectrum")]
    public class LineSpectrum : ScriptableObject
    {
        #region Constants

        private const float WidthToSigma = 0.4246609f; // 1 / (2 sqrt(2 ln 2)) a Gaussian's full width at half maximum = 2.355 sigma
        private const int StepsPerSigma = 8; // enough that the sum matches the Gaussian's integral to 1e-6
        public const float ReachInSigmas = 6f; // 1 - 2e-9 of a Gaussian's area lies within 6 sigma of the center

        #endregion

        #region Inspector Fields

        [field: Tooltip("The lines as a narrow Gaussian.")]
        [field: SerializeField] public List<SpectralLine> Lines { get; private set; } = new List<SpectralLine>();

        #endregion

        /**
         * <summary>
         * Each line as seen by the GPU, scaled such that the lamp's total power is the given scale.
         * </summary>
         * <param name="scale">The factor every line's peak is multiplied by.</param>
         * <returns>One (center, sigma, peak, 0) per line.</returns>
         */
        public Vector4[] GpuLines(float scale = 1f)
        {
            float total = 0f;
            foreach (SpectralLine line in Lines)
                total += line.Strength;

            var lines = new Vector4[Lines.Count];
            for (int i = 0; i < lines.Length; i++)
            {
                float sigma = Lines[i].Width * WidthToSigma;

                // a Gaussian of peak 1 has area sigma sqrt(2 pi), so this peak gives the line its share of the power
                float peak = Lines[i].Strength / total / (sigma * Mathf.Sqrt(2f * Mathf.PI)) * scale;
                lines[i] = new Vector4(Lines[i].Center, sigma, peak, 0f);
            }


            return lines;
        }

        /**
         * <summary>
         * The color of the lines as a light, at a brightness of Y = 1.
         * </summary>
         * <returns>The linear sRGB color of the lamp.</returns>
         */
        public Vector3 EmissionRgb()
        {
            Vector3 xyz = Integrate();
            return Cie.XyzToLinearSrgb(xyz / xyz.y);
        }

        /**
         * <summary>
         * The factor that normalizes the lines to Y = 1, so the lamp's total power is 1.
         * </summary>
         * <returns>The integral of y-bar over the integral of the lines times y-bar.</returns>
         */
        public float EmissionNormalization()
        {
            return Cie.YIntegral / Integrate().y;
        }

        /**
         * <summary>
         * One line's value at a wavelength.
         * </summary>
         * <param name="line">The line as (center, sigma, peak, 0).</param>
         * <param name="lambda">The wavelength in nm.</param>
         * <returns>The line's radiance at the wavelength.</returns>
         */
        public static float Gaussian(Vector4 line, float lambda)
        {
            float x = (lambda - line.x) / line.y; // how many sigmas from the center
            return line.z * Mathf.Exp(-0.5f * x * x);
        }

        private void OnValidate()
        {
            TracedRegistry.MarkChanged(); // lamps that use these lines rebuild
        }

        #region Helpers

        private Vector3 Integrate()
        {
            // each line is far narrower than 1 nm steps could see so walk across it in fractions of its sigma
            Vector3 xyz = Vector3.zero;
            foreach (Vector4 line in GpuLines())
            {
                float step = line.y / StepsPerSigma;
                for (float lambda = line.x - ReachInSigmas * line.y; lambda <= line.x + ReachInSigmas * line.y; lambda += step)
                    xyz += Gaussian(line, lambda) * step * Cie.MatchingFunctions(lambda);
            }


            return xyz;
        }

        #endregion
    }
}
