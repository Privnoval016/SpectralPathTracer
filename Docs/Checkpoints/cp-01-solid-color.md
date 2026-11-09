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

## Decisions

**How should threads map to pixels?** One thread per pixel, in 8x8 groups. The hardware hands each
thread its $(x, y)$ for free, an 8x8 group reads one compact tile of texture memory, and the group
count stays far below the 65,535 limit per dimension. Rejected: a 1D line of threads (a divide and
modulo per thread, strip-shaped memory access, 4K needs 129,600 groups); one thread per row (1,080
threads leaves most of the GPU idle); one thread for the whole image (CPU code on a GPU).

**What type is the output?** `RWTexture2D<float4>`, a writable texture that can go straight to the
screen. Rejected: `Texture2D` (read only), `RWStructuredBuffer` (a flat array: index math, no 2D
tiling, and an extra copy to display it), `sampler2D` (old read-only material shader type).

**Where does the color come from?** A uniform set from C#, so it is data editable in the Inspector
without recompiling. Rejected: hard-coded or `#define` (a magic number, every change recompiles), a
1x1 texture (a resource and a memory fetch per thread for four numbers).

**What happens to threads past the image edge?** Launch $\lceil W/8 \rceil$ groups so every pixel gets a
thread, and return early outside the image. Rejected: rounding the group count down (leaves a strip of
pixels unpainted), trusting the GPU to skip them (a dispatch knows nothing about textures), relying on
dropped out-of-bounds writes (only some APIs promise it, and buffers wrap into the next row instead).

**How does the kernel know the image size?** A `_Resolution` uniform. Most later kernels work on buffers
with no texture to ask, the render size will differ from texture sizes, and a uniform read is the
cheapest shader operation. Rejected: `GetDimensions` (ties the guard to one texture), hard-coding
1920x1080 (breaks the preview and scaled modes).

**What does an asmdef with no references see?** Only its own code and Unity's core modules, so the
runtime asmdef must reference the URP and Core RP assemblies. Packages are not visible automatically,
and Unity does not add references on its own.

**When is the kernel index looked up?** Once in `Create()`, which re-runs on every Inspector edit. Rejected:
every frame (a string search each frame), hard-coding 0 (breaks silently when more kernels are added).

**What if the compute shader is not assigned?** Skip the pass and warn once in `Create()`. Rejected:
throwing (fires every frame inside URP's render loop, for what is just setup in progress), skipping
silently (a blank view with no explanation), warning every frame (floods the console).

**Which color value goes to the shader?** `fillColor.linear`, because the Inspector stores sRGB-encoded
colors and `SetComputeVectorParam` does no conversion.

**Why can't the kernel write the camera's texture directly?** Its format (packed HDR or 8-bit sRGB) or
multisampling cannot be bound as `RWTexture2D`, and it can even be the backbuffer. The resolution is
not the problem; it matches.

**How is the output texture's descriptor built?** Copied from the camera's color texture, changing only
format, random write, MSAA, name and clearing. Rejected: a descriptor from scratch (drops settings we
did not mean to change), a hard-coded size.

**Which render graph pass type runs the dispatch?** `AddComputePass`. Rejected: a raster pass (no
dispatch), an unsafe pass (opts out of the graph's checks), dispatching from `Update` (outside the
camera's frame, no access to its textures).

**Why is the render function static?** It runs after `RecordRenderGraph` returns, and `PassData`
objects are pooled. `static` forbids captured state, which would be stale and allocate a closure every
frame.

**Which renderer setup?** A dedicated `PathTracer_Renderer`, with `PC_Renderer` kept as the default so
the Scene view and previews stay rasterized. The camera's culling mask is Nothing (the path tracer never
reads Unity's rasterized scene, so drawing it is wasted work), and post-processing is off (URP's post
passes run just before ours, so they only cost time, and our own display transform owns tone mapping).

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
