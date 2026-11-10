/**
 * <summary>
 * Ray intersection and normal for a quad, the unit square in its local XY plane.
 * </summary>
 */

#ifndef SPT_QUAD_INCLUDED
#define SPT_QUAD_INCLUDED

static const float QUAD_EPSILON = 1e-6f;

/**
 * <summary>
 * Computes the intersection of a ray with a quad in local space.
 * </summary>
 * <param name="worldToLocal">The transformation matrix from world space to the quad's local space.</param>
 * <param name="origin">The origin of the ray in world space.</param>
 * <param name="direction">The direction of the ray in world space.</param>
 * <param name="tMin">The minimum valid t value for the intersection.</param>
 * <param name="t">The computed t value for the intersection, if it exists.</param>
 * <returns>True if the ray intersects the quad, false otherwise.</returns>
 */
bool IntersectQuad(float4x4 worldToLocal, float3 origin, float3 direction, float tMin, out float t)
{
    float3 o = mul(worldToLocal, float4(origin, 1.0f)).xyz; // Transform the ray origin to local space
    float3 d = mul(worldToLocal, float4(direction, 0.0f)).xyz; // Transform the ray direction to local space
    t = 0.0f;
    bool hit = false;

    if (abs(d.z) >= QUAD_EPSILON)
    {
        t = -o.z / d.z;
        float3 p = o + t * d;
        hit = t > tMin && abs(p.x) <= 0.5f && abs(p.y) <= 0.5f;
    }

    return hit;
}

/**
 * <summary>
 * Computes the normal of a quad at a given point in world space.
 * </summary>
 * <param name="worldToLocal">The transformation matrix from world space to the quad's local space.</param>
 * <param name="position">The point in world space where the normal is computed.</param>
 * <returns>The normalized surface normal at the given point.</returns>
 */
float3 QuadNormal(float4x4 worldToLocal, float3 position)
{
    return normalize(mul(transpose(worldToLocal), float4(0.0f, 0.0f, -1.0f, 0.0f)).xyz); // -1 because the quad's front face is in the -Z direction in local space
}

#endif