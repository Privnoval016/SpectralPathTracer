//
// This file was automatically generated. Please don't edit by hand. Execute Editor command [ Edit > Rendering > Generate Shader Includes ] instead
//

#ifndef GPUOBJECT_CS_HLSL
#define GPUOBJECT_CS_HLSL
// Generated from SpectralPathTracer.GpuObject
// PackingRules = Exact
struct GpuObject
{
    float4x4 Quadric;
    float4x4 WorldToLocal;
    int Type;
    int MaterialIndex;
    int LightId;
    int MediumIn;
    int MediumOut;
};


#endif
