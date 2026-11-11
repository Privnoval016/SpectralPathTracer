using System;
using UnityEngine;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * Everything outside the scene that changes the rendered image, so any change restarts the sums.
     * </summary>
     */
    public readonly struct RenderParams : IEquatable<RenderParams>
    {
        #region Properties

        public PinholeCamera Camera { get; }
        public Vector2Int RenderSize { get; }
        public Vector2Int OutputSize { get; }
        public int MaxBounces { get; }
        public bool PixelJitter { get; }
        public bool RussianRoulette { get; }
        public Aov Aov { get; }
        public Vector2 DepthRange { get; }
        public int NoiseDimension { get; }

        /**
         * <summary>
         * The output resolution divided by the render resolution, per axis.
         * </summary>
         */
        public Vector2 OutputScale => new Vector2((float)OutputSize.x / RenderSize.x, (float)OutputSize.y / RenderSize.y);

        #endregion

        private RenderParams(PinholeCamera camera, Vector2Int renderSize, Vector2Int outputSize, int maxBounces, bool pixelJitter, bool russianRoulette, Aov aov, Vector2 depthRange, int noiseDimension)
        {
            Camera = camera;
            RenderSize = renderSize;
            OutputSize = outputSize;
            MaxBounces = maxBounces;
            PixelJitter = pixelJitter;
            RussianRoulette = russianRoulette;
            Aov = aov;
            DepthRange = depthRange;
            NoiseDimension = noiseDimension;
        }

        /**
         * <summary>
         * Gathers this frame's parameters from the settings and the camera.
         * </summary>
         * <param name="settings">The path tracer settings.</param>
         * <param name="aovSettings">Which AOV to show, and how.</param>
         * <param name="camera">This frame's camera.</param>
         * <returns>The parameters to render with.</returns>
         */
        public static RenderParams From(PathTracerSettings settings, AovSettings aovSettings, PinholeCamera camera)
        {
            var depthRange = new Vector2(aovSettings.DepthNear, aovSettings.DepthFar);
            return new RenderParams(camera, settings.RenderResolution, settings.OutputResolution, settings.MaxBounces, settings.PixelJitter, settings.RussianRoulette, aovSettings.Aov, depthRange, aovSettings.NoiseDimension);
        }

        public bool Equals(RenderParams other)
        {
            return Camera.CameraToWorld == other.Camera.CameraToWorld
                && Camera.TanHalfFov == other.Camera.TanHalfFov
                && Camera.AspectRatio == other.Camera.AspectRatio
                && RenderSize == other.RenderSize
                && OutputSize == other.OutputSize
                && MaxBounces == other.MaxBounces
                && PixelJitter == other.PixelJitter
                && RussianRoulette == other.RussianRoulette
                && Aov == other.Aov
                && DepthRange == other.DepthRange
                && NoiseDimension == other.NoiseDimension;
        }

        public override bool Equals(object obj)
        {
            return obj is RenderParams other && Equals(other);
        }

        public override int GetHashCode()
        {
            // a hash may skip fields; equal params still match
            return HashCode.Combine(Camera.CameraToWorld, RenderSize, MaxBounces, Aov);
        }
    }
}
