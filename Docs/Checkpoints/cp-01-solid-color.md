# cp-01-solid-color

Goal: a compute kernel fills a texture with one color, and a URP renderer feature draws it over the
Game camera.

Scene: `Assets/SpectralPathTracer/Scenes/Canvas.unity`. Renderer: `Settings/PathTracer_Renderer.asset`.

## What changed

`Shaders/PathTracer.compute` has one kernel, `FillCanvas`. It runs one thread per pixel in 8x8 thread
groups and writes `_Color` into `_Output`, skipping threads that fall outside `_Resolution`.

`Runtime/Rendering/PathTracerFeature.cs` is a `ScriptableRendererFeature` holding the compute shader and
the fill color. `Create()` looks up the kernel index and builds the pass, or logs one warning if no
shader is assigned. `AddRenderPasses()` enqueues the pass.

`Runtime/Rendering/PathTracerPass.cs` records two render graph passes each frame:

1. A compute pass that creates a half-float RGBA texture (descriptor copied from the camera's color
   texture, with random write on and MSAA off) and dispatches `FillCanvas` into it.
2. A blit of that texture onto the camera's color texture.

The pass reads the kernel's thread group size from the compiled shader, so the HLSL `numthreads` is
the only place that number lives.

Editor setup: `PathTracer_Renderer` sits at index 1 in `PC_RPAsset`'s renderer list, with `PC_Renderer`
still the default, so the Scene view and previews stay rasterized. The Canvas camera uses
`PathTracer_Renderer` with post-processing off, no anti-aliasing, and a culling mask of Nothing. The
runtime asmdef now references `Unity.RenderPipelines.Core.Runtime` and
`Unity.RenderPipelines.Universal.Runtime`.

## The math

Each thread's global ID is its pixel coordinate:

$$\text{id} = \text{groupID} \times (8, 8, 1) + \text{groupThreadID}$$

The dispatch must cover every pixel, so the group count rounds up:

$$G_x = \left\lceil \frac{W}{8} \right\rceil = \left\lfloor \frac{W + 7}{8} \right\rfloor$$

At 1920x1080 that is $240 \times 135$ groups of 64 threads, 2,073,600 threads in total. When $W$ is not a
multiple of 8, the extra threads in the last column of groups return early.

Why 2D groups instead of a 1D line of threads: a 1D layout makes every thread recover $(x, y)$ with an
integer divide and modulo, reads textures in thin strips instead of compact tiles, and hits Direct3D 12's
limit of 65,535 groups per dispatch dimension at 4K ($8{,}294{,}400 / 64 = 129{,}600$ groups).

The Inspector color is sRGB encoded, while the shader works in linear values. `Color.linear` decodes it:

$$c_{\text{lin}} = \left(\frac{c + 0.055}{1.055}\right)^{2.4} \quad (c > 0.04045)$$

Without it, a picked mid-gray of 0.5 is treated as linear 0.5 and shows up as about 0.735 on screen.

## Problems hit and fixes

- An empty `.compute` file fails to import ("was not imported correctly"). It needs at least one
  `#pragma kernel` and a matching function.
- The asmdef references were edited in the Inspector but never applied, which gave 10 compile errors
  (`ScriptableRendererFeature`, `RenderGraph`, `ContextContainer` not found).
- `GraphicsFormat` needs `using UnityEngine.Experimental.Rendering;`, and `AddBlitPass` is an extension
  method that only appears with `using UnityEngine.Rendering.RenderGraphModule.Util;`.
- `GetTextureDesc` throws when the camera's color target is the backbuffer, because URP imports it
  without a full descriptor. `requiresIntermediateTexture = true` makes URP use a real color texture.

## Shots to capture

- [ ] The first magenta frame in the Game view, next to the unchanged Scene view.
- [ ] Window > Analysis > Render Graph Viewer: the compute pass feeding the blit.
- [ ] Frame Debugger: the dispatch at $240 \times 135 \times 1$ groups.

## Explainers to build (Manim)

- The screen tiling into 8x8 thread groups, every thread running the same function.
- 1D vs 2D thread layouts: the divide and modulo, strips vs tiles, and the 65,535 group limit.

## References

[HLSL-numthreads] Microsoft. numthreads attribute, HLSL Shader Model 5. https://learn.microsoft.com/en-us/windows/win32/direct3dhlsl/sm5-attributes-numthreads

[HLSL-DispatchThreadID] Microsoft. SV_DispatchThreadID semantic. https://learn.microsoft.com/en-us/windows/win32/direct3dhlsl/sv-dispatchthreadid

[D3D12-Dispatch] Microsoft. ID3D12GraphicsCommandList::Dispatch, and the DirectX spec "Increased Dispatch Dimension" (D3D12_CS_DISPATCH_MAX_THREAD_GROUPS_PER_DIMENSION = 65535). https://learn.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist-dispatch, https://microsoft.github.io/DirectX-Specs/d3d/D3D12IncreasedDispatchDimension.html

[sRGB] IEC 61966-2-1:1999. Multimedia systems and equipment: Colour measurement and management, Part 2-1: Default RGB colour space, sRGB.

[URP-17.5] Unity Technologies. Universal Render Pipeline 17.5 and Core RP package source (`com.unity.render-pipelines.universal`, `com.unity.render-pipelines.core`), read for the render graph API, `requiresIntermediateTexture`, and post-processing pass order.
