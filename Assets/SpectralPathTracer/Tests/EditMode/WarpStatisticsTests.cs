using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * Checks the hemisphere warps on the GPU: directions are unit length and on the normal's side, and their angles
     * follow the cosine-weighted or uniform distribution, around normals that break naive tangent frames.
     * </summary>
     */
    public class WarpStatisticsTests
    {
        #region Private Fields

        private const string ShaderPath = "Assets/SpectralPathTracer/Tests/EditMode/Kernels/WarpTest.compute";
        private const string KernelName = "SampleDirections";

        private const int CosineWarp = 0;
        private const int UniformWarp = 1;

        private const int GridSize = 256; // 65,536 directions per draw
        private const double Sigmas = 5.0; // a correct warp fails a bound less than once in a million runs
        private const int HistogramBins = 16;
        private const double ChiSquareLimit = 37.70; // 99.9th percentile of chi-square with 15 degrees of freedom
        private const float LengthTolerance = 1e-5f;
        private const float SideTolerance = 1e-5f;

        private static readonly int GridSizeID = Shader.PropertyToID("_GridSize");
        private static readonly int WarpID = Shader.PropertyToID("_Warp");
        private static readonly int NormalID = Shader.PropertyToID("_Normal");
        private static readonly int DirectionsID = Shader.PropertyToID("_Directions");

        #endregion

        /**
         * <summary>
         * Floor and ceiling (+-y), the basis's sign flip (+-z), a side wall (+x) and two oblique normals.
         * </summary>
         */
        private static IEnumerable<Vector3> Normals()
        {
            yield return Vector3.up;
            yield return Vector3.down;
            yield return Vector3.forward;
            yield return Vector3.back;
            yield return Vector3.right;
            yield return new Vector3(1f, 2f, 3f).normalized;
            yield return new Vector3(-0.3f, 0.2f, -0.93f).normalized;
        }

        [TestCaseSource(nameof(Normals))]
        public void DirectionsAreUnitAndOnTheNormalsSide(Vector3 normal)
        {
            foreach (int warp in new[] { CosineWarp, UniformWarp })
            {
                Vector3[] directions = Draw(warp, normal);
                for (int i = 0; i < directions.Length; i++)
                {
                    Assert.That(Mathf.Abs(directions[i].magnitude - 1f), Is.LessThan(LengthTolerance), $"Warp {warp}, length of direction {i}: {directions[i]}");
                    Assert.That(Vector3.Dot(directions[i], normal), Is.GreaterThan(-SideTolerance), $"Warp {warp}, direction {i} below the surface: {directions[i]}");
                }
            }
        }

        [TestCaseSource(nameof(Normals))]
        public void CosineWarpHasUniformCosineSquared(Vector3 normal)
        {
            double[] cosines = Cosines(Draw(CosineWarp, normal), normal);
            AssertUniform(cosines.Select(c => c * c));
        }

        [TestCaseSource(nameof(Normals))]
        public void UniformWarpHasUniformCosine(Vector3 normal)
        {
            double[] cosines = Cosines(Draw(UniformWarp, normal), normal);
            AssertUniform(cosines);
        }

        [TestCaseSource(nameof(Normals))]
        public void CosineWarpAveragesToTwoThirdsOfTheNormal(Vector3 normal)
        {
            AssertMeanDirection(Draw(CosineWarp, normal), normal * (2f / 3f));
        }

        [TestCaseSource(nameof(Normals))]
        public void UniformWarpAveragesToHalfTheNormal(Vector3 normal)
        {
            AssertMeanDirection(Draw(UniformWarp, normal), normal * 0.5f);
        }

        #region Helpers

        /**
         * <summary>
         * Runs the test kernel over a GridSize x GridSize grid.
         * </summary>
         * <param name="warp">CosineWarp or UniformWarp.</param>
         * <param name="normal">The unit normal to sample around.</param>
         * <returns>One direction per grid cell.</returns>
         */
        private static Vector3[] Draw(int warp, Vector3 normal)
        {
            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ShaderPath);
            Assert.That(shader, Is.Not.Null, $"Missing {ShaderPath}");

            int kernel = shader.FindKernel(KernelName);
            using var directions = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GridSize * GridSize, sizeof(float) * 3);

            shader.SetBuffer(kernel, DirectionsID, directions);
            shader.SetInts(GridSizeID, GridSize, GridSize);
            shader.SetInt(WarpID, warp);
            shader.SetVector(NormalID, normal);

            shader.GetKernelThreadGroupSizes(kernel, out uint groupSizeX, out uint groupSizeY, out _);
            shader.Dispatch(kernel, (GridSize + (int)groupSizeX - 1) / (int)groupSizeX, (GridSize + (int)groupSizeY - 1) / (int)groupSizeY, 1);

            var result = new Vector3[GridSize * GridSize];
            directions.GetData(result);
            return result;
        }

        private static double[] Cosines(Vector3[] directions, Vector3 normal)
        {
            return directions.Select(d => (double)Mathf.Clamp01(Vector3.Dot(d, normal))).ToArray();
        }

        /**
         * <summary>
         * Chi-square test that values in [0, 1] fill HistogramBins equal bins evenly.
         * </summary>
         * <param name="values">The values to test.</param>
         */
        private static void AssertUniform(IEnumerable<double> values)
        {
            var counts = new int[HistogramBins];
            int total = 0;
            foreach (double v in values)
            {
                counts[Math.Min((int)(v * HistogramBins), HistogramBins - 1)]++; // a value of exactly 1 joins the top bin
                total++;
            }

            double expected = (double)total / HistogramBins;
            double chiSquare = counts.Sum(c => (c - expected) * (c - expected) / expected);
            Assert.That(chiSquare, Is.LessThan(ChiSquareLimit), $"Bin counts: {string.Join(", ", counts)}");
        }

        /**
         * <summary>
         * Checks the average direction against its expected value; every component of a unit vector has variance at
         * most 1, so 5 / sqrt(N) bounds each component's error.
         * </summary>
         * <param name="directions">The sampled directions.</param>
         * <param name="expected">The expected average.</param>
         */
        private static void AssertMeanDirection(Vector3[] directions, Vector3 expected)
        {
            Vector3 sum = Vector3.zero;
            foreach (Vector3 d in directions)
                sum += d;

            Vector3 mean = sum / directions.Length;
            double bound = Sigmas / Math.Sqrt(directions.Length);
            Assert.That(float.IsNaN(mean.x) || float.IsNaN(mean.y) || float.IsNaN(mean.z), Is.False, "A NaN direction poisons the mean"); // NaN would slip past the bound below
            Assert.That((mean - expected).magnitude, Is.LessThan(bound), $"Mean {mean}, expected {expected}");
        }

        #endregion
    }
}