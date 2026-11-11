/**
 * <summary>
 * Every struct, enum and constant buffer generated from C#.
 * </summary>
 */

#ifndef SPT_GPU_DATA_INCLUDED
#define SPT_GPU_DATA_INCLUDED

// the generated constant buffer expects these from Unity's core shader library
#ifndef CBUFFER_START
#define CBUFFER_START(name) cbuffer name {
#define CBUFFER_END };
#endif

#include "../../Runtime/Rendering/GpuData/FrameConstants.cs.hlsl"
#include "../../Runtime/Rendering/Settings/Aov.cs.hlsl"

#include "../../Runtime/Rendering/GpuData/GpuRay.cs.hlsl"
#include "../../Runtime/Rendering/GpuData/GpuHit.cs.hlsl"
#include "../../Runtime/Rendering/GpuData/GpuPathState.cs.hlsl"
#include "../../Runtime/Rendering/GpuData/PathFlags.cs.hlsl"

#include "../../Runtime/Scene/GpuData/ShapeType.cs.hlsl"
#include "../../Runtime/Scene/GpuData/MaterialType.cs.hlsl"
#include "../../Runtime/Scene/GpuData/GpuObject.cs.hlsl"
#include "../../Runtime/Scene/GpuData/GpuMaterial.cs.hlsl"

#endif
