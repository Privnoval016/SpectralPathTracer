using UnityEngine;

namespace SpectralPathTracer
{
    public static class SigmoidSpectrum
    {
        /**
         * <summary>
         * Evaluates a sigmoid-polynomial spectrum at a wavelength.
         * </summary>
         * <param name="coefficients">c0, c1 and c2 for a wavelength in nm.</param>
         * <param name="lambda">The wavelength in nm.</param>
         * <returns>The spectrum's value, always between 0 and 1.</returns>
         */
        public static float Evaluate(Vector3 coefficients, float lambda)
        {
            float x = (coefficients.x * lambda + coefficients.y) * lambda + coefficients.z; // the quadratic in horner form


            return Sigmoid(x);
        }

        /**
         * <summary>
         * Squashes any real number into the range (0, 1).
         * </summary>
         * <param name="x">The number.</param>
         * <returns>The squashed number.</returns>
         */
        public static float Sigmoid(float x)
        {
            return 0.5f + x / (2f * Mathf.Sqrt(1f + x * x));
        }
    }
}
