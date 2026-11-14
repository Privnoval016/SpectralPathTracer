using System;
using UnityEngine;

namespace SpectralPathTracer
{
    // a perfect thermal emitter, whose spectrum follows Planck's law at its temperature
    public static class Blackbody
    {
        #region Constants

        private const double PlanckConstant = 6.62607015e-34; // h, in J s
        private const double SpeedOfLight = 2.99792458e8; // c, in m / s
        private const double BoltzmannConstant = 1.380649e-23; // k, in J / K

        public const float MinTemperature = 1000f; // kelvin, the coolest an emitter or a sweep can be set to
        public const float MaxTemperature = 20000f; // kelvin, the hottest

        public const float WienConstant = 2.897771955e6f; // nm K so the peak sits at WienConstant / T nm

        #endregion

        /**
         * <summary>
         * Planck's law, which is the radiance a blackbody at a given temperature gives off at one wavelength.
         * </summary>
         * <param name="lambdaNm">The wavelength in nm.</param>
         * <param name="kelvin">The temperature in kelvin.</param>
         * <returns>The spectral radiance, in W per square metre, steradian and metre of wavelength.</returns>
         */
        public static double Radiance(double lambdaNm, double kelvin)
        {
            double lambda = lambdaNm * 1e-9; // the constants are in metres

            // 2 h c^2 / lambda^5 times how strongly the temperature fills light at this wavelength
            return 2.0 * PlanckConstant * SpeedOfLight * SpeedOfLight / Math.Pow(lambda, 5.0)
                / (Math.Exp(PlanckConstant * SpeedOfLight / (lambda * BoltzmannConstant * kelvin)) - 1.0);
        }

        /**
         * <summary>
         * Resamples a blackbody onto the GPU's fixed grid, scaled so its luminance Y equals the given value.
         * </summary>
         * <param name="kelvin">The temperature in kelvin.</param>
         * <param name="luminance">The luminance Y the light should have.</param>
         * <returns>One value every 5 nm from 360 to 830 nm.</returns>
         */
        public static float[] Resample(float kelvin, float luminance)
        {
            // the same normalization as a measured light so only the color changes with temperature
            double scale = luminance * Cie.YIntegral / Integrate(kelvin).y;

            var values = new float[(int)SpectrumTables.Count];
            for (int i = 0; i < values.Length; i++)
                values[i] = (float)(Radiance((int)SpectrumTables.FirstWavelength + i * (int)SpectrumTables.Step, kelvin) * scale);


            return values;
        }

        /**
         * <summary>
         * The color of a blackbody at a luminance of Y = 1.
         * </summary>
         * <param name="kelvin">The temperature in kelvin.</param>
         * <returns>The linear sRGB color, which can be negative outside the sRGB gamut.</returns>
         */
        public static Vector3 Rgb(float kelvin)
        {
            Vector3 xyz = Integrate(kelvin);
            return Cie.XyzToLinearSrgb(xyz / xyz.y);
        }

        #region Helpers

        private static Vector3 Integrate(float kelvin)
        {
            // doubles since the raw radiance can be super big
            double x = 0.0, y = 0.0, z = 0.0;

            
            for (int lambda = Cie.FirstWavelength; lambda <= Cie.LastWavelength; lambda++)
            {
                double radiance = Radiance(lambda, kelvin);
                Vector3 cmf = Cie.MatchingFunctions(lambda);

                x += radiance * cmf.x;
                y += radiance * cmf.y;
                z += radiance * cmf.z;
            }


            return new Vector3((float)x, (float)y, (float)z);
        }

        #endregion
    }
}
