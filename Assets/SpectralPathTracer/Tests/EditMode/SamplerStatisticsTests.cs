using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * Checks that the GPU sampler's numbers are uniform in [0, 1) and independent across dimensions and samples.
     * </summary>
     */
    public class SamplerStatisticsTests
    {
        #region Private Fields

        private const string ShaderPath = "Assets/SpectralPathTracer/Tests/EditMode/Kernels/SamplerTest.compute";
        private const string KernelName = "SampleGrid";

        private const int GridSize = 256; // 65,536 values per draw
        private const double Sigmas = 5.0; // a correct sampler fails a bound less than once in a million runs
        private const int HistogramBins = 16;
        private const double ChiSquareLimit = 37.70; // 99.9th percentile of chi-square with 15 degrees of freedom

        private static readonly int GridSizeID = Shader.PropertyToID("_GridSize");
        private static readonly int DimensionID = Shader.PropertyToID("_Dimension");
        private static readonly int SamplesID = Shader.PropertyToID("_Samples");

        #endregion

        [Test]
        public void ValuesStayInUnitInterval()
        {
            float[] values = Draw(dimension: 0, sampleIndex: 0);
            Assert.That(values.Min(), Is.GreaterThanOrEqualTo(0f));
            Assert.That(values.Max(), Is.LessThan(1f));
        }

        [Test]
        public void MeanAndVarianceMatchUniform()
        {
            double[] values = ToDoubles(Draw(dimension: 0, sampleIndex: 0));
            double mean = values.Average();
            double variance = values.Select(v => (v - mean) * (v - mean)).Average();

            double meanError = Math.Sqrt(1.0 / 12.0 / values.Length);
            double varianceError = Math.Sqrt((1.0 / 80.0 - 1.0 / 144.0) / values.Length); // (mu4 - sigma^4) / N for a uniform
            Assert.That(Math.Abs(mean - 0.5), Is.LessThan(Sigmas * meanError), $"Mean {mean}");
            Assert.That(Math.Abs(variance - 1.0 / 12.0), Is.LessThan(Sigmas * varianceError), $"Variance {variance}");
        }

        [Test]
        public void HistogramIsFlat()
        {
            float[] values = Draw(dimension: 0, sampleIndex: 0);
            var counts = new int[HistogramBins];
            foreach (float v in values)
                counts[(int)(v * HistogramBins)]++;

            double expected = (double)values.Length / HistogramBins;
            double chiSquare = counts.Sum(c => (c - expected) * (c - expected) / expected);
            Assert.That(chiSquare, Is.LessThan(ChiSquareLimit), $"Bin counts: {string.Join(", ", counts)}");
        }

        [Test]
        public void DimensionsAreUncorrelated()
        {
            double[] first = ToDoubles(Draw(dimension: 0, sampleIndex: 0));
            double[] second = ToDoubles(Draw(dimension: 1, sampleIndex: 0));

            double correlation = Correlation(first, second);
            Assert.That(correlation, Is.Not.NaN, "A constant series has no correlation"); // NaN would slip past the bound below
            Assert.That(Math.Abs(correlation), Is.LessThan(Sigmas / Math.Sqrt(first.Length)), $"Correlation {correlation}");
        }

        [Test]
        public void SamplesAreUncorrelated()
        {
            double[] first = ToDoubles(Draw(dimension: 0, sampleIndex: 0));
            double[] second = ToDoubles(Draw(dimension: 0, sampleIndex: 1));

            double correlation = Correlation(first, second);
            Assert.That(correlation, Is.Not.NaN, "A constant series has no correlation"); // NaN would slip past the bound below
            Assert.That(Math.Abs(correlation), Is.LessThan(Sigmas / Math.Sqrt(first.Length)), $"Correlation {correlation}");
        }

        #region Helpers

        /**
         * <summary>
         * Runs the test kernel over a GridSize x GridSize grid of output pixels.
         * </summary>
         * <param name="dimension">The sampler dimension to draw.</param>
         * <param name="sampleIndex">The sample index to draw.</param>
         * <returns>One value per grid cell, row after row.</returns>
         */
        private static float[] Draw(int dimension, int sampleIndex)
        {
            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ShaderPath);
            Assert.That(shader, Is.Not.Null, $"Missing {ShaderPath}");

            int kernel = shader.FindKernel(KernelName);
            using var samples = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GridSize * GridSize, sizeof(float));

            shader.SetBuffer(kernel, SamplesID, samples);
            shader.SetInts(GridSizeID, GridSize, GridSize);
            shader.SetInt(DimensionID, dimension);
            shader.SetInt(ShaderIDs.SampleIndexID, sampleIndex);

            shader.GetKernelThreadGroupSizes(kernel, out uint groupSizeX, out uint groupSizeY, out _);
            shader.Dispatch(kernel, (GridSize + (int)groupSizeX - 1) / (int)groupSizeX, (GridSize + (int)groupSizeY - 1) / (int)groupSizeY, 1);

            var result = new float[GridSize * GridSize];
            samples.GetData(result);
            return result;
        }

        private static double[] ToDoubles(float[] values)
        {
            return values.Select(v => (double)v).ToArray();
        }

        /**
         * <summary>
         * Pearson correlation of two equally long series.
         * </summary>
         * <param name="a">The first series.</param>
         * <param name="b">The second series.</param>
         * <returns>The correlation, from -1 to 1.</returns>
         */
        private static double Correlation(double[] a, double[] b)
        {
            double meanA = a.Average();
            double meanB = b.Average();
            double covariance = 0.0, varianceA = 0.0, varianceB = 0.0;
            for (int i = 0; i < a.Length; i++)
            {
                covariance += (a[i] - meanA) * (b[i] - meanB);
                varianceA += (a[i] - meanA) * (a[i] - meanA);
                varianceB += (b[i] - meanB) * (b[i] - meanB);
            }

            return covariance / Math.Sqrt(varianceA * varianceB);
        }

        #endregion
    }
}