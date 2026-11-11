using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace SpectralPathTracer
{
    public class AccumulationCapture
    {
        private static Action<Color[], Vector2Int> _pending;

        private class PassData
        {
            public BufferHandle Sums;
            public Vector2Int Size;
            public Action<Color[], Vector2Int> OnCaptured;
        }

        /**
         * <summary>
         * Asks for the next frame's accumulated radiance as each pixel's mean, with its sample count in alpha.
         * </summary>
         * <param name="onCaptured">Called a few frames later with the means and the image size.</param>
         */
        public static void Request(Action<Color[], Vector2Int> onCaptured)
        {
            _pending = onCaptured;
        }

        /**
         * <summary>
         * Records a readback of the accumulation sums if a capture was requested.
         * </summary>
         * <param name="renderGraph">The render graph recording this frame.</param>
         * <param name="sums">The per-pixel sums, with each pixel's count in w.</param>
         * <param name="size">The render size.</param>
         */
        public void Record(RenderGraph renderGraph, BufferHandle sums, Vector2Int size)
        {
            if (_pending == null)
                return;

            using var builder = renderGraph.AddUnsafePass("Path Tracer: Capture Sums", out PassData passData);
            passData.Sums = sums;
            passData.Size = size;
            passData.OnCaptured = _pending;
            _pending = null; // one request captures one frame

            builder.UseBuffer(sums, AccessFlags.Read);
            builder.AllowPassCulling(false); // the readback writes nothing the graph can see
            builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
            {
                Action<Color[], Vector2Int> onCaptured = data.OnCaptured;
                Vector2Int size = data.Size;
                context.cmd.RequestAsyncReadback(data.Sums, request =>
                {
                    if (!request.hasError)
                        onCaptured(ToMeans(request.GetData<Vector4>()), size);
                });
            });
        }


        /**
         * <summary>
         * Divides each pixel's sum by its own sample count.
         * </summary>
         * <param name="sums">The sums as read back, count in w.</param>
         * <returns>Each pixel's mean radiance, with its count in alpha.</returns>
         */
        private static Color[] ToMeans(NativeArray<Vector4> sums)
        {
            var means = new Color[sums.Length];
            for (int i = 0; i < means.Length; i++)
            {
                Vector4 sum = sums[i];
                float count = Mathf.Max(sum.w, 1f); // a pixel with no samples yet reads 0 instead of nan
                means[i] = new Color(sum.x / count, sum.y / count, sum.z / count, sum.w);
            }

            return means;
        }
    }
}
