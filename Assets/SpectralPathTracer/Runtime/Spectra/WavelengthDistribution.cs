using System.Collections.Generic;
using UnityEngine;

namespace SpectralPathTracer
{
    // when light rays are cast, we want to sample wavelengths smartly instead of purely uniformly so the image converges faster with less samples needed
    public static class WavelengthDistribution
    {
        #region Constants

        public const int BinsPerNm = (int)WavelengthBins.PerNm; // shared with the GPU through the generated enum
        public const float BinWidth = 1f / BinsPerNm; // 0.02 nm
        public const int BinCount = (int)WavelengthBins.Count; // pigeonhole bins between the CIE samples

        public const float UniformShare = 0.01f; // how much of the distribution is uniformly spread across all bins

        #endregion

        /**
         * <summary>
         * The weights of each wavelength bin for the visual spectrum.
         * </summary>
         */
        public static float[] VisualWeights()
        {
            var weights = new float[BinCount];
            for (int i = 0; i < BinCount; i++)
                weights[i] = 0.5f * (EyeAt(BinStart(i)) + EyeAt(BinStart(i + 1))); // trapezoid average


            return weights;
        }

        /**
         * <summary>
         * The weights of each wavelength bin for the scene's light as the eye sees it, with lines integrated exactly.
         * </summary>
         * <param name="smoothLight">The smooth lights' radiance at each nm from 360 to 830, 471 values.</param>
         * <param name="lines">Every line in the scene as (center, sigma, peak, 0).</param>
         * <returns>The weights of each bin.</returns>
         */
        public static float[] LightWeights(float[] smoothLight, List<Vector4> lines)
        {
            var weights = new float[BinCount];
            for (int i = 0; i < BinCount; i++)
            {
                float start = BinStart(i);
                float end = BinStart(i + 1);
                weights[i] = 0.5f * (SmoothAt(smoothLight, start) * EyeAt(start) + SmoothAt(smoothLight, end) * EyeAt(end)) * BinWidth; // trapezoid average
            }

            // a line's share of each bin is its area between the bin's edges
            foreach (Vector4 line in lines)
            {
                float area = line.z * line.y * Mathf.Sqrt(2f * Mathf.PI);
                int first = BinOf(line.x - LineSpectrum.ReachInSigmas * line.y);
                int last = BinOf(line.x + LineSpectrum.ReachInSigmas * line.y);
                for (int i = first; i <= last; i++)
                {
                    float inside = Normal((BinStart(i + 1) - line.x) / line.y) - Normal((BinStart(i) - line.x) / line.y);
                    weights[i] += area * inside * EyeAt(BinStart(i) + 0.5f * BinWidth);
                }
            }


            return weights;
        }

        /**
         * <summary>
         * Mixes the eye's weights with the light's, each normalized first so the share is a true fraction.
         * </summary>
         * <param name="visual">The eye's weights of each bin.</param>
         * <param name="light">The light's weights of each bin.</param>
         * <param name="lightShare">How much of the mix follows the light, from 0 to 1.</param>
         * <returns>The mixed weights, summing to 1.</returns>
         */
        public static float[] Mix(float[] visual, float[] light, float lightShare)
        {
            double visualSum = Sum(visual);
            double lightSum = Sum(light);

            // a scene without light has nothing to follow, so the eye takes the whole distribution
            if (lightSum <= 0.0)
                return visual;

            var mixed = new float[BinCount];
            for (int i = 0; i < BinCount; i++)
                mixed[i] = (float)((1.0 - lightShare) * visual[i] / visualSum + lightShare * light[i] / lightSum);


            return mixed;
        }

        /**
         * <summary>
         * Mixes a small uniform floor into the given weights so every bin keeps a float-resolvable probability.
         * </summary>
         * <param name="weights">The weights of each bin.</param>
         * <returns>Weights summing to 1, at least UniformShare / BinCount in every bin.</returns>
         */
        public static float[] WithUniformFloor(float[] weights)
        {
            double sum = Sum(weights);

            var mixed = new float[BinCount];
            for (int i = 0; i < BinCount; i++)
                mixed[i] = (float)((1.0 - UniformShare) * weights[i] / sum + UniformShare / BinCount);


            return mixed;
        }

        /**
         * <summary>
         * The cumulative distribution function of the given weights.
         * </summary>
         * <param name="weights">The weights of each bin.</param>
         * <returns>The cumulative distribution function of the weights.</returns>
         */
        public static float[] Cdf(float[] weights)
        {
            double sum = Sum(weights);

            var cdf = new float[BinCount + 1];
            double cumulative = 0.0;
            for (int i = 0; i < BinCount; i++)
            {
                cumulative += weights[i];
                cdf[i + 1] = (float)(cumulative / sum);
            }

            cdf[BinCount] = 1.0f; // the last value is always 1

            return cdf;
        }

        /**
         * <summary>
         * Samples a wavelength from the given cumulative distribution function.
         * </summary>
         * <param name="cdf">The cumulative distribution function of the weights.</param>
         * <param name="u">A uniform random number in [0, 1).</param>
         * <param name="pdf">The probability density function value at the sampled wavelength.</param>
         * <returns>A wavelength in nm.</returns>
         */
        public static float Sample(float[] cdf, float u, out float pdf)
        {
            // binary search for the first index with cdf[index] > u
            int low = 0;
            int high = BinCount;
            while (low < high)
            {
                int middle = (low + high) / 2;
                if (cdf[middle] <= u)
                    low = middle + 1;
                else
                    high = middle;
            }

            int bin = low - 1; // cdf[0] = 0 <= u, so low is at least 1
            float width = cdf[bin + 1] - cdf[bin];
            pdf = width / BinWidth; // the bin's probability per nm

            return Cie.FirstWavelength + (bin + (u - cdf[bin]) / width) * BinWidth; // the bin's start plus the fraction of the bin's width
        }

        #region Helpers

        // in double, so 23,500 small additions don't drift
        private static double Sum(float[] weights)
        {
            double sum = 0.0;
            foreach (float weight in weights)
                sum += weight;


            return sum;
        }

        private static float BinStart(int bin)
        {
            return Cie.FirstWavelength + bin * BinWidth;
        }

        private static int BinOf(float lambda)
        {
            return Mathf.Clamp(Mathf.FloorToInt((lambda - Cie.FirstWavelength) / BinWidth), 0, BinCount - 1);
        }

        // how strongly any of the three matching functions responds at a wavelength
        private static float EyeAt(float lambda)
        {
            Vector3 cmf = Cie.MatchingFunctions(lambda);
            return cmf.x + cmf.y + cmf.z;
        }

        // the smooth lights between their whole nm readings
        private static float SmoothAt(float[] smoothLight, float lambda)
        {
            float position = Mathf.Clamp(lambda - Cie.FirstWavelength, 0f, smoothLight.Length - 1);
            int row = Mathf.Min((int)position, smoothLight.Length - 2);


            return Mathf.Lerp(smoothLight[row], smoothLight[row + 1], position - row);
        }

        /**
         * <summary>
         * The share of a unit Gaussian's area below x sigmas, the standard normal CDF.
         * </summary>
         * <param name="x">The distance from the center in sigmas.</param>
         * <returns>The area below x, from 0 to 1.</returns>
         */
        private static float Normal(float x)
        {
            return 0.5f * (1f + Erf(x / Mathf.Sqrt(2f)));
        }

        // gaussian error function
        private static float Erf(float x)
        {
            float sign = Mathf.Sign(x);
            x = Mathf.Abs(x);

            float t = 1f / (1f + 0.3275911f * x);
            float polynomial = ((((1.061405429f * t - 1.453152027f) * t + 1.421413741f) * t - 0.284496736f) * t + 0.254829592f) * t;


            return sign * (1f - polynomial * Mathf.Exp(-x * x));
        }

        #endregion
    }
}