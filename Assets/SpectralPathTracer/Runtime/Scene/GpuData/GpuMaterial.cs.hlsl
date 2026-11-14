//
// This file was automatically generated. Please don't edit by hand. Execute Editor command [ Edit > Rendering > Generate Shader Includes ] instead
//

#ifndef GPUMATERIAL_CS_HLSL
#define GPUMATERIAL_CS_HLSL
// Generated from SpectralPathTracer.GpuMaterial
// PackingRules = Exact
struct GpuMaterial
{
    float4 AlbedoSpectrum;
    float4 EmissionSpectrum;
    float3 Albedo;
    float3 Emission;
    int Type;
    int ReflectanceTable;
    int EmissionTable;
    int LineStart;
    int LineCount;
};


#endif
