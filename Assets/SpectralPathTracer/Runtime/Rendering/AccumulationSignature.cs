using System;
using UnityEngine;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * Everything outside the scene that changes the rendered image. A different signature restarts the sums.
     * </summary>
     */
    public readonly struct AccumulationSignature : IEquatable<AccumulationSignature>
    {
        #region Private Fields

        private readonly Matrix4x4 _cameraToWorld;
        private readonly float _tanHalfFov;
        private readonly float _aspectRatio;
        private readonly Vector2Int _renderSize;
        private readonly int _maxBounces;
        private readonly bool _pixelJitter;
        private readonly SkyColors _sky;
        private readonly Aov _aov;
        private readonly Vector2 _depthRange;
        private readonly int _noiseDimension;

        #endregion

        public AccumulationSignature(PinholeCamera camera, Vector2Int renderSize, int maxBounces, bool pixelJitter, SkyColors sky, AovSettings aovSettings)
        {
            _cameraToWorld = camera.CameraToWorld;
            _tanHalfFov = camera.TanHalfFov;
            _aspectRatio = camera.AspectRatio;
            _renderSize = renderSize;
            _maxBounces = maxBounces;
            _pixelJitter = pixelJitter;
            _sky = sky;
            _aov = aovSettings.Aov;
            _depthRange = new Vector2(aovSettings.DepthNear, aovSettings.DepthFar);
            _noiseDimension = aovSettings.NoiseDimension;
        }

        public bool Equals(AccumulationSignature other)
        {
            return _cameraToWorld == other._cameraToWorld
                && _tanHalfFov == other._tanHalfFov
                && _aspectRatio == other._aspectRatio
                && _renderSize == other._renderSize
                && _maxBounces == other._maxBounces
                && _pixelJitter == other._pixelJitter
                && _sky.Zenith == other._sky.Zenith
                && _sky.Horizon == other._sky.Horizon
                && _sky.Ground == other._sky.Ground
                && _aov == other._aov
                && _depthRange == other._depthRange
                && _noiseDimension == other._noiseDimension;
        }

        public override bool Equals(object obj)
        {
            return obj is AccumulationSignature other && Equals(other);
        }

        public override int GetHashCode()
        {
            // a hash may skip fields; equal signatures still match
            return HashCode.Combine(_cameraToWorld, _renderSize, _maxBounces, _aov);
        }
    }
}