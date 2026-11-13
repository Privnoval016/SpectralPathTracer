using UnityEngine;

namespace SpectralPathTracer
{
    // when light rays are cast, we want to sample wavelengths smartly instead of purely uniformly so the image converges faster with less samples needed
    public static class WavelengthDistribution
    {
        #region Constants

        public const int BinCount = Cie.SampleCount - 1; // pigeonhole bins between the CIE samples

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
            {
                Vector3 start = Cie.MatchingFunctions(Cie.FirstWavelength + i);
                Vector3 end = Cie.MatchingFunctions(Cie.FirstWavelength + i + 1);
                weights[i] = 0.5f * (start.x + start.y + start.z + end.x + end.y + end.z); // trapezoid average
            }


            return weights;
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
            double sum = 0.0;
            foreach (float weight in weights)
                sum += weight;

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
            double sum = 0.0;
            foreach (float weight in weights)
                sum += weight;
            
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
            pdf = width;

            return Cie.FirstWavelength + bin + (u - cdf[bin]) / width; // the bin's start plus the fraction of the bin's width
        }
    }
}