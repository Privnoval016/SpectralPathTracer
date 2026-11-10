#ifndef SPT_SKY_INCLUDED
#define SPT_SKY_INCLUDED

#include "Common.hlsl"

float3 _SkyZenithColor;
float3 _SkyHorizonColor;
float3 _SkyGroundColor;

float3 SkyRadiance(float3 direction)
{
    if (direction.y < 0.0f)
    {
        // Below the horizon, use the ground color
        return _SkyGroundColor;
    }
    else
    {
        // Above the horizon, interpolate between horizon and zenith based on the angle from the zenith
        float elevation = asin(saturate(direction.y)) / (PI / 2.0f); // Normalize elevation to [0, 1]
        return lerp(_SkyHorizonColor, _SkyZenithColor, elevation);
    }
}

#endif