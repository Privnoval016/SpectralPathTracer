/**
 * <summary>
 * Warps that turn uniform numbers into disk points and hemisphere directions, with their pdfs.
 * </summary>
 */

#ifndef SPT_SAMPLING_INCLUDED
#define SPT_SAMPLING_INCLUDED

#include "Common.hlsl"

/**
 * <summary>
 * Samples a point uniformly on a unit disk using concentric mapping.
 * </summary>
 * <param name="u">A 2D uniform random sample in the range [0, 1).</param>
 * <returns>A 2D point sampled uniformly on the unit disk.</returns>
 */
float2 SampleDiscConcentric(float2 u)
{
    float2 offset = 2.0f * u - 1.0f;
    float2 disk = float2(0.0f, 0.0f);

    if (any(offset != 0.0f))
    {
        float r;
        float theta;

        
        if (abs(offset.x) > abs(offset.y))
        {
            r = offset.x;
            theta = (PI / 4.0f) * (offset.y / offset.x);
        }
        else
        {
            r = offset.y;
            theta = (PI / 2.0f) - (PI / 4.0f) * (offset.x / offset.y);
        }

        disk = r * float2(cos(theta), sin(theta));
    }

    return disk;
}

/**
 * <summary>
 * Samples a point on a cosine-weighted hemisphere.
 * </summary>
 * <param name="u">A 2D uniform random sample in the range [0, 1).</param>
 * <returns>A 3D point sampled on the hemisphere with cosine weighting.</returns>
 */
float3 SampleCosineHemisphere(float2 u)
{
    float2 d = SampleDiscConcentric(u);
    float z = sqrt(max(0.0f, 1.0f - dot(d, d)));
    return float3(d, z);
}

/**
 * <summary>
 * pdf from stat511 holy crap
 * </summary>
 */
float CosineHemispherePdf(float cosTheta)
{
    return cosTheta * (1.0f / PI);
}

/**
 * <summary>
 * Samples a point uniformly on a hemisphere.
 * </summary>
 * <param name="u">A 2D uniform random sample in the range [0, 1).</param>
 * <returns>A 3D point sampled uniformly on the hemisphere.</returns>
 */
float3 SampleUniformHemisphere(float2 u)
{
    float z = u.x;
    float r = sqrt(max(0.0f, 1.0f - z * z));
    float phi = 2.0f * PI * u.y;
    return float3(r * cos(phi), r * sin(phi), z);
}

float UniformHemispherePdf()
{
    return 1.0f / (2.0f * PI);
}

/**
 * <summary>
 * orthonormal basis orthonormal basis
 * </summary>
 * <param name="n">The normal vector.</param>
 * <param name="b1">The first basis vector (output).</param>
 * <param name="b2">The second basis vector (output).</param>
 */
void BuildOrthonormalBasis(float3 n, out float3 b1, out float3 b2)
{
    float zSign = n.z >= 0.0f ? 1.0f : -1.0f;
    float a = -1.0f / (zSign + n.z);
    float b = n.x * n.y * a;
    b1 = float3(1.0f + zSign * n.x * n.x * a, zSign * b, -zSign * n.x);
    b2 = float3(b, zSign + n.y * n.y * a, -n.y);
}

/**
 * <summary>
 * Transforms a point from local space to world space using an orthonormal basis.
 * </summary>
 * <param name="local">The point in local space.</param>
 * <param name="n">The normal vector defining the orientation of the local space.</param>
 * <returns>The point transformed to world space.</returns>
 */
float3 LocalToWorld(float3 local, float3 n)
{
    float3 b1;
    float3 b2;
    BuildOrthonormalBasis(n, b1, b2);
    return local.x * b1 + local.y * b2 + local.z * n;
}

#endif