using UnityEngine;

namespace SpectralPathTracer
{
    public readonly struct PinholeCamera
    {
        #region Properties

        public Matrix4x4 CameraToWorld { get; }
        public float TanHalfFov { get; }
        public float AspectRatio { get; }
        public Vector3 Position => CameraToWorld.GetColumn(3);

        #endregion

        /**
         * <summary>
         * Creates a pinhole camera.
         * </summary>
         */
        public PinholeCamera(Matrix4x4 cameraToWorld, float tanHalfFov, float aspectRatio)
        {
            CameraToWorld = cameraToWorld;
            TanHalfFov = tanHalfFov;
            AspectRatio = aspectRatio;
        }

        /**
         * <summary>
         * Builds the pinhole camera for a Unity camera rendering at the settings' output resolution.
         * </summary>
         */
        public static PinholeCamera From(Camera camera, PathTracerSettings settings)
        {
            return new PinholeCamera(
                camera.transform.localToWorldMatrix,
                Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad),
                (float)settings.OutputResolution.x / settings.OutputResolution.y);
        }

        /**
         * <summary>
         * World-space direction of the ray through a point on the image. CPU mirror of
         * <c>CameraRayDirection</c> in <c>PathTracer.compute</c>.
         * </summary>
         * <param name="ndc">The normalized device coordinates of the pixel, in the range [-1, 1] for both axes.</param>
         * <returns>The unit-length ray direction.</returns>
         */
        public Vector3 RayDirection(Vector2 ndc)
        {
            Vector3 local = new Vector3(ndc.x * AspectRatio * TanHalfFov, ndc.y * TanHalfFov, 1f);
            return CameraToWorld.MultiplyVector(local).normalized;
        }
    }
}
