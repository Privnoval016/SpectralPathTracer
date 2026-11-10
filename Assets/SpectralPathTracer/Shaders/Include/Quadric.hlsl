#ifndef SPT_QUADRIC_INCLUDED
#define SPT_QUADRIC_INCLUDED

static const float QUADRIC_EPSILON = 1e-6f;

/**
 * <summary>
 * Computes the quadric matrix for a sphere given its center and radius.
 * </summary>
 * <param name="center">The center of the sphere in world space.</param>
 * <param name="radius">The radius of the sphere.</param>
 * <returns>A 4x4 matrix representing the quadric surface of the sphere.</returns>
 */
float4x4 SphereQuadric(float3 center, float radius)
{
    float4x4 Q = float4x4(
        1.0f, 0.0f, 0.0f, -center.x,
        0.0f, 1.0f, 0.0f, -center.y,
        0.0f, 0.0f, 1.0f, -center.z,
        -center.x, -center.y, -center.z, dot(center, center) - radius * radius
    );
    return Q;
}


/**
 * <summary>
 * Computes the intersection of a ray with a quadric surface defined by a 4x4 matrix.
 * </summary>
 * <param name="q">The 4x4 matrix representing the quadric surface.</param>
 * <param name="origin">The origin of the ray in world space.</param>
 * <param name="direction">The direction of the ray in world space.</param>
 * <param name="tMin">The minimum valid t value for the intersection.</param>
 * <param name="t">The computed t value for the intersection, if it exists.</param>
 * <returns>True if the ray intersects the quadric surface, false otherwise.</returns>
 */
bool IntersectQuadric(float4x4 q, float3 origin, float3 direction, float tMin, out float t)
{
    float4 o = float4(origin, 1.0f); // Homogeneous coordinates for the ray origin
    float4 d = float4(direction, 0.0f); // Homogeneous coordinates for the ray direction
    float4 qo = mul(q, o);

    float a = dot(d, mul(q, d)); // Quadratic coefficient
    float h = dot(d, qo); // Linear coefficient
    float c = dot(o, qo); // Constant term
    t = 0.0f;
    bool hit = false; // for some reason unitys hlsl compiler doesnt like having multiple return paths so its easier to have a variable here

    if (abs(a) < QUADRIC_EPSILON)
    {
        // Linear case: the ray is tangent to the quadric or parallel to it
        if (abs(h) >= QUADRIC_EPSILON)
        {
            t = -c / (2.0f * h);
            hit = t > tMin;
        }
    }
    else
    {
        float discriminant = h * h - a * c;
        if (discriminant >= 0.0f)
        {
            // dividing by negative will flip the order of the roots, so we need to take the minimum of the two roots
            float sqrtDiscriminant = sqrt(discriminant);
            float t0 = min((-h - sqrtDiscriminant) / a, (-h + sqrtDiscriminant) / a);
            float t1 = max((-h - sqrtDiscriminant) / a, (-h + sqrtDiscriminant) / a);

            t = t0 > tMin ? t0 : t1; // closest intersection that is in front of the ray origin
            hit = t > tMin;
        }
    }

    return hit;
}

#endif