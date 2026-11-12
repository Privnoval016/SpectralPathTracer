//
// This file was automatically generated. Please don't edit by hand. Execute Editor command [ Edit > Rendering > Generate Shader Includes ] instead
//

#ifndef FRAMECONSTANTS_CS_HLSL
#define FRAMECONSTANTS_CS_HLSL
// Generated from SpectralPathTracer.FrameConstants
// PackingRules = Exact
CBUFFER_START(FrameConstants)
    float4x4 _CameraToWorld;
    float _TanHalfFov;
    float _AspectRatio;
    float2 _OutputScale;
    float4 _SkyZenithColor;
    float4 _SkyHorizonColor;
    float4 _SkyGroundColor;
    float2 _DepthRange;
    uint _MaxBounces;
    uint _Aov;
    uint _NoiseDimension;
    uint _PixelJitter;
    uint _RussianRoulette;
    uint _ApplyExposure;
    float _ExposureScale;
    uint _ShowGrayRamp;
    uint _ShowSpectrumStrip;
    uint _Padding1;
    float4 _SkyZenithSpectrum;
    float4 _SkyHorizonSpectrum;
    float4 _SkyGroundSpectrum;
CBUFFER_END


#endif
