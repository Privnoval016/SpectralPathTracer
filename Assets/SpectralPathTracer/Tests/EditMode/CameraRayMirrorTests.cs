using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * Checks that the GPU and the CPU build the same rays for a given camera.
     * </summary>
     */
    public class CameraRayMirrorTests
    {
        #region Private Fields

        private const string GenerateShaderPath = "Assets/SpectralPathTracer/Shaders/Wavefront/Generate.compute";
        private const string GenerateKernelName = "Generate";

        private const int Width = 16;
        private const int Height = 9;
        private const float Tolerance = 1e-5f;

        #endregion

        [Test]
        public void GeneratedDirectionsMatchPinholeCamera()
        {
            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(GenerateShaderPath);
            Assert.That(shader, Is.Not.Null, $"Missing {GenerateShaderPath}");

            // an off-origin, rotated camera so translation and rotation both get exercised
            Matrix4x4 cameraToWorld = Matrix4x4.TRS(new Vector3(1f, 2f, 3f), Quaternion.Euler(10f, 30f, 0f), Vector3.one);
            var camera = new PinholeCamera(cameraToWorld, Mathf.Tan(30f * Mathf.Deg2Rad), (float)Width / Height);

            GpuRay[] rays = DispatchGenerate(shader, camera);

            for (int i = 0; i < rays.Length; i++)
            {
                int x = i % Width;
                int y = i / Width;
                Vector2 ndc = new Vector2((x + 0.5f) / Width * 2f - 1f, (y + 0.5f) / Height * 2f - 1f);

                Vector3 expected = camera.RayDirection(ndc);
                Assert.That(Vector3.Distance(rays[i].Direction, expected), Is.LessThan(Tolerance), $"Direction mismatch at pixel ({x}, {y})");
            }
        }

        #region Helpers

        /**
         * <summary>
         * Runs the Generate kernel once for a Width x Height image.
         * </summary>
         * <param name="shader">The Generate compute shader.</param>
         * <param name="camera">The camera to generate rays for.</param>
         * <returns>The generated rays.</returns>
         */
        private static GpuRay[] DispatchGenerate(ComputeShader shader, PinholeCamera camera)
        {
            int kernel = shader.FindKernel(GenerateKernelName);
            using var rays = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Width * Height, UnsafeUtility.SizeOf<GpuRay>());

            shader.SetBuffer(kernel, ShaderIDs.RaysID, rays);
            shader.SetInts(ShaderIDs.ResolutionID, Width, Height);
            shader.SetMatrix(ShaderIDs.CameraToWorldID, camera.CameraToWorld);
            shader.SetFloat(ShaderIDs.TanHalfFovYID, camera.TanHalfFov);
            shader.SetFloat(ShaderIDs.AspectRatioID, camera.AspectRatio);

            shader.GetKernelThreadGroupSizes(kernel, out uint groupSizeX, out uint groupSizeY, out _);
            shader.Dispatch(kernel, (Width + (int)groupSizeX - 1) / (int)groupSizeX, (Height + (int)groupSizeY - 1) / (int)groupSizeY, 1);

            var result = new GpuRay[Width * Height];
            rays.GetData(result);
            return result;
        }

        #endregion
    }
}