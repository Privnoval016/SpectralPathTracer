//
// This file was automatically generated. Please don't edit by hand. Execute Editor command [ Edit > Rendering > Generate Shader Includes ] instead
//

#ifndef GPUPATHSTATE_CS_HLSL
#define GPUPATHSTATE_CS_HLSL
// Generated from SpectralPathTracer.GpuPathState
// PackingRules = Exact
struct GpuPathState
{
    float4 Radiance;
    float4 Throughput;
    float4 Wavelengths;
    float4 WavelengthPdfs;
    float4 Time;
    float4 MisVc;
    float4 MisVcm;
    uint PixelIndex;
    uint Flags;
    uint MediumId;
    uint SampleIndex;
};


#endif
