#ifndef SPT_RAY_OFFSET_INCLUDED
#define SPT_RAY_OFFSET_INCLUDED

static const float OFFSET_ORIGIN = 1.0f / 32.0f; // closer to 0 than this, one float step is too small to escape the surface
static const float OFFSET_FLOAT_SCALE = 1.0f / 65536.0f; // the fixed distance used near the origin instead
static const float OFFSET_INT_SCALE = 256.0f; // float steps to move per unit of normal everywhere else

/**
 * <summary>
 * Offsets a ray's origin along the surface normal to avoid self-intersections.
 * </summary>
 * <param name="p">The original ray origin.</param>
 * <param name="n">The surface normal at the intersection point, flipped to the side the new ray leaves from.</param>
 * <returns>The offset ray origin.</returns>
 */
float3 OffsetRayOrigin(float3 p, float3 n)
{
    int3 offsetInt = int3(n * OFFSET_INT_SCALE);
    float3 pInt = asfloat(asint(p) + (p < 0.0f ? -offsetInt : offsetInt));
    return abs(p) < OFFSET_ORIGIN ? p + n * OFFSET_FLOAT_SCALE : pInt;
}

#endif