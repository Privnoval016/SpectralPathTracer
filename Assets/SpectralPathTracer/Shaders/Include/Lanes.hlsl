/**
 * <summary>
 * The per-lane values a path has, extending to spectral wavelengths.
 * </summary>
 */

#ifndef SPT_LANES_INCLUDED
#define SPT_LANES_INCLUDED

// RGB mode fills (r, g, b, 0); spectral mode will fill four wavelengths
typedef float4 Lanes;

/**
 * <summary>
 * Spreads an RGB color across the lanes.
 * </summary>
 * <param name="rgb">The color.</param>
 * <returns>The color as lanes.</returns>
 */
Lanes RgbToLanes(float3 rgb)
{
    return float4(rgb, 0.0f);
}

/**
 * <summary>
 * Reads the lanes back as an RGB color.
 * </summary>
 * <param name="lanes">The lanes.</param>
 * <returns>The color.</returns>
 */
float3 LanesToRgb(Lanes lanes)
{
    return lanes.rgb;
}

/**
 * <summary>
 * Finds the largest of the lanes.
 * </summary>
 * <param name="lanes">The lanes.</param>
 * <returns>The largest lane.</returns>
 */
float MaxLane(Lanes lanes)
{
    return max(max(lanes.x, lanes.y), max(lanes.z, lanes.w));
}

#endif
