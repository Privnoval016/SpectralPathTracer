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
        private const float JitterTolerance = 1e-3f; // in pixels

        #endregion

        [Test]
        public void GeneratedDirectionsMatchPinholeCamera()
        {
            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(GenerateShaderPath);
            Assert.That(shader, Is.Not.Null, $"Missing {GenerateShaderPath}");

            // an off-origin, rotated camera so translation and rotation both get exercised
            Matrix4x4 cameraToWorld = Matrix4x4.TRS(new Vector3(1f, 2f, 3f), Quaternion.Euler(10f, 30f, 0f), Vector3.one);
            var camera = new PinholeCamera(cameraToWorld, Mathf.Tan(30f * Mathf.Deg2Rad), (float)Width / Height);

            GpuRay[] rays = DispatchGenerate(shader, camera, pixelJitter: false);

            for (int i = 0; i < rays.Length; i++)
            {
                int x = i % Width;
                int y = i / Width;
                Vector2 ndc = new Vector2((x + 0.5f) / Width * 2f - 1f, (y + 0.5f) / Height * 2f - 1f);

                Vector3 expected = camera.RayDirection(ndc);
                Assert.That(Vector3.Distance(rays[i].Direction, expected), Is.LessThan(Tolerance), $"Direction mismatch at pixel ({x}, {y})");
            }
        }

        [Test]
        public void JitteredDirectionsStayInsideTheirPixel()
        {
            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(GenerateShaderPath);
            Assert.That(shader, Is.Not.Null, $"Missing {GenerateShaderPath}");

            Matrix4x4 cameraToWorld = Matrix4x4.TRS(new Vector3(1f, 2f, 3f), Quaternion.Euler(10f, 30f, 0f), Vector3.one);
            var camera = new PinholeCamera(cameraToWorld, Mathf.Tan(30f * Mathf.Deg2Rad), (float)Width / Height);
            Matrix4x4 worldToCamera = cameraToWorld.inverse;

            GpuRay[] rays = DispatchGenerate(shader, camera, pixelJitter: true);

            int offCenter = 0;
            for (int i = 0; i < rays.Length; i++)
            {
                int x = i % Width;
                int y = i / Width;

                // back into image coordinates: divide by the depth along the camera's axis, then undo the FOV and aspect scale
                Vector3 d = worldToCamera.MultiplyVector(rays[i].Direction);
                float pixelX = ((d.x / d.z) / (camera.TanHalfFov * camera.AspectRatio) * 0.5f + 0.5f) * Width;
                float pixelY = ((d.y / d.z) / camera.TanHalfFov * 0.5f + 0.5f) * Height;

                Assert.That(pixelX, Is.InRange(x - JitterTolerance, x + 1 + JitterTolerance), $"Ray {i} left its pixel in x: {pixelX}");
                Assert.That(pixelY, Is.InRange(y - JitterTolerance, y + 1 + JitterTolerance), $"Ray {i} left its pixel in y: {pixelY}");

                if (Mathf.Abs(pixelX - (x + 0.5f)) > 0.1f || Mathf.Abs(pixelY - (y + 0.5f)) > 0.1f)
                    offCenter++;
            }

            // with uniform jitter about 96% of rays land more than a tenth of a pixel from the center in some axis
            Assert.That(offCenter, Is.GreaterThan(rays.Length / 2), "Jitter left most rays at the pixel center");
        }

        #region Helpers

        /**
         * <summary>
         * Runs the Generate kernel once for a Width x Height image.
         * </summary>
         * <param name="shader">The Generate compute shader.</param>
         * <param name="camera">The camera to generate rays for.</param>
         * <param name="pixelJitter">Whether rays start at random spots inside their pixels instead of the centers.</param>
         * <returns>The generated rays.</returns>
         */
        private static GpuRay[] DispatchGenerate(ComputeShader shader, PinholeCamera camera, bool pixelJitter)
        {
            int kernel = shader.FindKernel(GenerateKernelName);
            using var rays = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Width * Height, UnsafeUtility.SizeOf<GpuRay>());
            using var paths = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Width * Height, UnsafeUtility.SizeOf<GpuPathState>());
            using var queue = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Width * Height, sizeof(uint));
            using var counters = new GraphicsBuffer(GraphicsBuffer.Target.Raw, 2, sizeof(uint));
            using var args = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw, 3, sizeof(uint));

            // Generate also starts each path, so every buffer it writes must be bound or the dispatch may do nothing
            shader.SetBuffer(kernel, ShaderIDs.RaysID, rays);
            shader.SetBuffer(kernel, ShaderIDs.PathsID, paths);
            shader.SetBuffer(kernel, ShaderIDs.QueueOutID, queue);
            shader.SetBuffer(kernel, ShaderIDs.CountersID, counters);
            shader.SetBuffer(kernel, ShaderIDs.ArgsID, args);
            shader.SetInts(ShaderIDs.ResolutionID, Width, Height);
            shader.SetInt(ShaderIDs.WaveStartID, 0);
            shader.SetInt(ShaderIDs.RayCountID, Width * Height);
            shader.SetInt(ShaderIDs.SampleIndexID, 0);
            using GraphicsBuffer constants = TestFrameConstants.Bind(shader, new FrameConstants
            {
                _CameraToWorld = camera.CameraToWorld,
                _TanHalfFov = camera.TanHalfFov,
                _AspectRatio = camera.AspectRatio,
                _OutputScale = Vector2.one,
                _PixelJitter = pixelJitter ? 1u : 0u
            });

            // one wave covering the whole image, one thread per pixel
            shader.GetKernelThreadGroupSizes(kernel, out uint groupSizeX, out _, out _);
            shader.Dispatch(kernel, (Width * Height + (int)groupSizeX - 1) / (int)groupSizeX, 1, 1);

            var result = new GpuRay[Width * Height];
            rays.GetData(result);
            return result;
        }

        #endregion
    }
}