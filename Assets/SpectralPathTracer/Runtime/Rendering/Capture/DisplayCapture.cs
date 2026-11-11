using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace SpectralPathTracer
{
    public class DisplayCapture
    {
        private static Action<Color32[], Vector2Int> _pending;

        private class PassData
        {
            public TextureHandle Display;
            public Vector2Int Size;
            public Action<Color32[], Vector2Int> OnCaptured;
        }

        /**
         * <summary>
         * Asks for the next frame the renderer displays as sRGB pixels from the bottom row up.
         * </summary>
         * <param name="onCaptured">Called a few frames later with the pixels and their size.</param>
         */
        public static void Request(Action<Color32[], Vector2Int> onCaptured)
        {
            _pending = onCaptured;
        }

        /**
         * <summary>
         * Records a readback of this frame's display texture if a capture was requested.
         * </summary>
         * <param name="renderGraph">The render graph recording this frame.</param>
         * <param name="display">This frame's display texture.</param>
         * <param name="size">The display texture's size.</param>
         */
        public void Record(RenderGraph renderGraph, TextureHandle display, Vector2Int size)
        {
            if (_pending == null)
                return;

            using var builder = renderGraph.AddUnsafePass("Path Tracer: Capture", out PassData passData);
            passData.Display = display;
            passData.Size = size;
            passData.OnCaptured = _pending;
            _pending = null; // one request captures one frame

            builder.UseTexture(display, AccessFlags.Read);
            builder.AllowPassCulling(false); // the readback writes nothing the graph can see
            builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
            {
                Action<Color32[], Vector2Int> onCaptured = data.OnCaptured;
                Vector2Int size = data.Size;
                context.cmd.RequestAsyncReadback(data.Display, request =>
                {
                    if (!request.hasError)
                        onCaptured(ToSrgb(request.GetData<ushort>()), size);
                });
            });
        }


        /**
         * <summary>
         * Turns half-float linear RGBA into 8-bit sRGB.
         * </summary>
         * <param name="halves">Four halves per pixel, as the display texture stores them.</param>
         * <returns>One sRGB color per pixel.</returns>
         */
        private static Color32[] ToSrgb(NativeArray<ushort> halves)
        {
            var pixels = new Color32[halves.Length / 4];
            for (int i = 0; i < pixels.Length; i++)
            {
                // display values are linear and a png holds srgb like the camera target does
                // except the camera target automatically converts to srgb but we dont
                pixels[i] = new Color(
                    Mathf.HalfToFloat(halves[i * 4]),
                    Mathf.HalfToFloat(halves[i * 4 + 1]),
                    Mathf.HalfToFloat(halves[i * 4 + 2])).gamma;
            }

            return pixels;
        }
    }
}
