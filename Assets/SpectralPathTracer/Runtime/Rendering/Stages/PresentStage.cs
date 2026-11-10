using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace SpectralPathTracer
{
    public class PresentStage
    {
        private class PassData
        {
            public TextureHandle Source;
            public Rect Viewport;
        }

        /**
         * <summary>
         * Records the raster pass that clears the camera target to black and draws the output into its
         * letterboxed rectangle.
         * </summary>
         */
        public void Record(RenderGraph renderGraph, TextureHandle source, TextureHandle target, TextureDesc targetDesc, Vector2Int outputResolution)
        {
            Rect viewport = FitInside(outputResolution, new Vector2Int(targetDesc.width, targetDesc.height));

            using var builder = renderGraph.AddRasterRenderPass("Path Tracer: Present", out PassData passData);

            passData.Source = source;
            passData.Viewport = viewport;

            builder.UseTexture(source, AccessFlags.Read);
            builder.SetRenderAttachment(target, 0, AccessFlags.Write);

            builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
            {
                var cmd = context.cmd;
                cmd.ClearRenderTarget(false, true, Color.black);
                cmd.SetViewport(data.Viewport);
                Blitter.BlitTexture(cmd, data.Source, new Vector4(1, 1, 0, 0), 0, true);
            });
        }

        #region Helpers

        /**
         * <summary>
         * The largest rectangle with the image's aspect ratio that fits inside the target, centered.
         * </summary>
         */
        private static Rect FitInside(Vector2Int image, Vector2Int target)
        {
            float scale = Mathf.Min((float)target.x / image.x, (float)target.y / image.y);
            float width = Mathf.Round(image.x * scale);
            float height = Mathf.Round(image.y * scale);
            return new Rect(Mathf.Floor((target.x - width) * 0.5f), Mathf.Floor((target.y - height) * 0.5f), width, height);
        }

        #endregion
    }
}