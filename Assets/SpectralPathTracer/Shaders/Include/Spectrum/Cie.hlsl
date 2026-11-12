/**
 * <summary>
 * The CIE 1931 matching functions, the D65 illuminant and the conversion from XYZ to linear sRGB.
 * </summary>
 */

#ifndef SPT_CIE_INCLUDED
#define SPT_CIE_INCLUDED

#include "CieTables.hlsl"

static const float CIE_Y_INTEGRAL = 106.856895f; // the area under y-bar, so a flat spectrum of 1 gets Y = 1


// the same rows as Cie.cs, since float3x3 fills row by row
static const float3x3 XYZ_TO_SRGB = float3x3(
     3.2404542f, -1.5371385f, -0.4985314f,
    -0.9692660f,  1.8760108f,  0.0415560f,
     0.0556434f, -0.2040259f,  1.0572252f);

/**
 * <summary>
 * Samples the three matching functions at a wavelength, interpolating between the 1 nm rows.
 * </summary>
 * <param name="lambda">The wavelength in nm.</param>
 * <returns>x-bar, y-bar and z-bar, or zero outside 360 to 830 nm.</returns>
 */
float3 MatchingFunctions(float lambda)
{
    float position = lambda - CIE_FIRST_WAVELENGTH;
    float3 xyz = 0.0f;

    if (position >= 0.0f && position <= (float)(CIE_SAMPLE_COUNT - 1u))
    {
        uint row = min((uint)position, CIE_SAMPLE_COUNT - 2u); // the last wavelength interpolates from the row before
        xyz = lerp(CIE_XYZ[row], CIE_XYZ[row + 1u], position - row);
    }


    return xyz;
}

/**
 * <summary>
 * Samples D65's power at a wavelength, interpolating between the 5 nm rows.
 * </summary>
 * <param name="lambda">The wavelength in nm.</param>
 * <returns>The relative power, or zero outside 300 to 830 nm.</returns>
 */
float D65At(float lambda)
{
    float position = (lambda - CIE_D65_FIRST) / CIE_D65_STEP;
    float power = 0.0f;

    if (position >= 0.0f && position <= (float)(CIE_D65_COUNT - 1u))
    {
        uint row = min((uint)position, CIE_D65_COUNT - 2u); // the last wavelength interpolates from the row before
        power = lerp(CIE_D65[row], CIE_D65[row + 1u], position - row);
    }


    return power;
}

/**
 * <summary>
 * The hue of one wavelength as a label color.
 * </summary>
 * <param name="lambda">The wavelength in nm.</param>
 * <returns>The linear sRGB tint.</returns>
 */
float3 WavelengthTint(float lambda)
{
    float3 rgb = mul(XYZ_TO_SRGB, MatchingFunctions(lambda));

    // add white until it fits then scale so the brightest channel is 1
    rgb -= min(0.0f, min(rgb.r, min(rgb.g, rgb.b)));


    return rgb / max(max(rgb.r, max(rgb.g, rgb.b)), 1e-6f);
}

/**
 * <summary>
 * Converts CIE XYZ to linear sRGB.
 * </summary>
 * <param name="xyz">The color in XYZ.</param>
 * <returns>The linear sRGB color, negative outside the sRGB gamut.</returns>
 */
float3 XyzToLinearSrgb(float3 xyz)
{
    return mul(XYZ_TO_SRGB, xyz);
}

#endif
