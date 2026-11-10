/**
 * <summary>
 * The pinhole camera's uniforms and the ray through any point on the image.
 * </summary>
 */

#ifndef SPT_CAMERA_INCLUDED
#define SPT_CAMERA_INCLUDED

float4x4 _CameraToWorld;
float _TanHalfFov;
float _AspectRatio;

/**
 * <summary>
 * Computes the origin of the camera ray in world space.
 * </summary>
 * <returns>The origin of the camera ray in world space.</returns>
 */
float3 CameraRayOrigin()
{
    return mul(_CameraToWorld, float4(0.0f, 0.0f, 0.0f, 1.0f)).xyz;
}

/**
 * <summary>
 * Computes the direction of a ray from the camera through a pixel in normalized device coordinates.
 * </summary>
 * <param name="ndc">The normalized device coordinates of the pixel, where (-1,-1) is the bottom-left and (1,1) is the top-right.</param>
 * <returns>The normalized direction of the ray in world space.</returns>
 */
float3 CameraRayDirection(float2 ndc)
{
    float3 local = float3(ndc.x * _AspectRatio * _TanHalfFov, ndc.y * _TanHalfFov, 1.0f);
    return normalize(mul(_CameraToWorld, float4(local, 0.0f)).xyz);
}

/**
 * <summary>
 * Computes the camera's forward direction in world space.
 * </summary>
 * <returns>The normalized direction the camera looks along.</returns>
 */
float3 CameraForward()
{
    return normalize(mul(_CameraToWorld, float4(0.0f, 0.0f, 1.0f, 0.0f)).xyz);
}

#endif