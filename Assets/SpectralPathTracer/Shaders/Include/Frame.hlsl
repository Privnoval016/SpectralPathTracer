/**
 * <summary>
 * This frame's constants, shared by every stage through one constant buffer.
 * </summary>
 */

#ifndef SPT_FRAME_INCLUDED
#define SPT_FRAME_INCLUDED

// the generated header expects these from Unity's core shader library
#ifndef CBUFFER_START
#define CBUFFER_START(name) cbuffer name {
#define CBUFFER_END };
#endif

#include "../../Runtime/Rendering/GpuData/FrameConstants.cs.hlsl"

#endif
