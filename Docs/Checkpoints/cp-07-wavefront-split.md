# cp-07-wavefront-split

Goal: split the single kernel into a wavefront pipeline (Generate, Intersect, Shade) connected by GPU
buffers, restructure the C# into one class per stage, and split the HLSL into topic includes. The image
must not change.

Scene: `Assets/SpectralPathTracer/Scenes/Canvas.unity`.

## What changed

Shaders:

- `Shaders/Include/`: `Common.hlsl` (`PI`), `Camera.hlsl`, `Sky.hlsl`, `Quadric.hlsl` (the existing
  functions and uniforms, moved unchanged) and `Rays.hlsl` (ray and hit buffer accessors). Every include
  has a guard.
- `Shaders/Wavefront/`: `Generate.compute` (one ray per pixel, 2D), `Intersect.compute` (one hit per ray,
  1D) and `Shade.compute` (one color per ray into its pixel, 1D). `PathTracer.compute` is deleted.

C#:

- `PathTracerPass` is now an orchestrator: it resolves the sky, builds a `FrameContext`, imports the output,
  runs `RecordWavefront` (generate, intersect, shade) unless paused, then presents.
- `Runtime/Rendering/Stages/`: `ComputeStage` (shader, kernel index, thread group size, `GroupCount`) and
  `GenerateStage`, `IntersectStage`, `ShadeStage` built on it, each with its own `PassData` and a typed
  `Record` method; `PresentStage` (the raster pass) stands alone.
- `ShaderIDs` holds every shader property ID. `FrameContext` carries the render size, camera and sky.
- `GpuRay` and `GpuHit` are marked `[GenerateHLSL]`: the C# struct is the single source, and
  `GpuRay.cs.hlsl` / `GpuHit.cs.hlsl` are generated from it (Edit > Rendering > Generate Shader Includes).
- `PathTracerShaders` is an asset bundling the three compute shaders; the feature references it.
- `Tests/EditMode/CameraRayMirrorTests.cs`: the first test.

## Decisions

**[Learn] What happens when threads in one batch take different branches?** The batch runs both branches one
after the other, masking off the threads that didn't take each, so the time is roughly the sum. Splitting
the work into a kernel per step (and later a queue per material) keeps threads that run together on the
same code. Rejected: branches running side by side (impossible with one shared instruction stream),
automatic regrouping (not something portable code can count on).

**[Learn] How does data get from one kernel to the next?** Buffers in GPU memory. Kernels have no return
values, uniforms are read-only values from C#, and `groupshared` memory only lives within one dispatch.

**[Learn] Who owns the ray and hit buffers?** The render graph (`CreateBuffer`), since they only live from
Generate to Shade within one frame; the graph pools them and knows their exact lifetime. Rejected: owning
and importing them like the output texture, which is persistent only because pausing needs last frame's
image. Rule: persistent only if something reads it next frame.

**[Learn] How are Intersect and Shade dispatched?** 1D, one thread per ray. Rays are a list; once finished
paths are removed (cp-18), ray $i$ is no longer pixel $i$, so each ray stores its pixel. 1D dispatches hit the
65,535-group limit at about 4.19 million rays (64 threads per group), fine at 1080p; fixed-size waves at
cp-18 remove it.

**[Decide] Where do the kernels live?** One `.compute` per stage. Uniforms belong to each shader object, so
shared values are set per stage; editing one stage recompiles only that file.

**[Decide] How is a ray laid out?** An array of structs, accessed only through `LoadRay` / `StoreRay` /
`LoadHit` / `StoreHit`, so the layout can become packed bytes at cp-18 without touching any kernel.

**[Decide] How are stages shaped?** A shared `ComputeStage` base holding what every compute stage needs,
with each stage keeping its own typed `Record` method so data flow stays visible in the signatures. The
raster present stage doesn't inherit from it.

**[Learn] How do stages share per-frame values?** One `FrameContext` struct built once by the orchestrator and
passed in. Rejected: each stage fetching its own (duplicated, could disagree), global shader properties
(hidden coupling, needs global state modification), statics (breaks with two cameras).

**[Learn] What if a file is included twice?** Redefinition errors, because `#include` pastes text. Include
guards (`#ifndef` / `#define` / `#endif`) make every later include a no-op.

**[Learn] Which uniforms does each shader have?** Only those from the files it includes (and that it actually
reads), so each stage sets only its own.

**[Decide] How is a miss represented?** A flag, `uint Hit` (0 or 1); `bool` isn't portable in buffers.

**[Decide] Where do shader property IDs live?** One static `ShaderIDs` class, so each name is spelled once.

**[Decide] What does Shade output?** Exactly cp-06's image, so the restructure's test is an exact match.

**[Learn] Where does the buffer stride come from?** A C# mirror struct measured with `UnsafeUtility.SizeOf`.
Rejected: asking the shader (C# allocates the buffer before any shader sees it, and Unity has no struct
reflection), hard-coding 32 (a stale stride silently corrupts every ray after the first).

**[Learn] What makes Intersect run after Generate's writes finish?** Declared reads and writes
(`UseBuffer(rays, Write)` / `Read`): the graph orders the passes and inserts the barrier between a write and
a later read. Rejected: separate `renderPassEvent`s (they order whole URP passes and say nothing about
memory hazards), a manual fence (for syncing separate GPU queues).

**[Decide] How does the feature reference the shaders?** A `PathTracerShaders` bundle asset.

**[Decide] CPU/GPU data mirrors?** Core RP's `[GenerateHLSL]`: the C# struct generates the HLSL one. Logic
mirrors (`PinholeCamera.RayDirection` vs `CameraRayDirection`) can't be generated, so they're tested.

**[Decide] What does the mirror test check?** That every ray direction from the `Generate` kernel (16x9,
an off-origin rotated camera) matches `PinholeCamera.RayDirection` within $10^{-5}$.

## Verification

- With the same camera and sky, the old single kernel and the new pipeline produced bit-identical output:
  0 of 129,600 pixels differ (10,546 of them sphere pixels). Measured strides: `GpuRay` 32 bytes,
  `GpuHit` 8, matching HLSL's tight packing of structured buffers.
- `CameraRayMirrorTests.GeneratedDirectionsMatchPinholeCamera` passes.

## Problems hit and fixes

- Test runs timed out: an unanswered "save scene?" dialog blocked the Editor, and a run started during a
  script reload also times out. A later `save_all` then saved an untitled scene instead of Canvas.
  Tests now run only through a port of Chains of Contract's `utest.sh`, which compiles first, refuses to
  run while an untitled scene is open, saves only dirty scenes that have a path, and restores scenes after.
- `[GenerateHLSL]` maps `Vector2Int` to `int2`, so the ray's pixel became signed (`int2(id.xy)` in Generate).

## Shots to capture

- [ ] Render Graph Viewer: Generate, Intersect, Shade and Present, with the ray and hit buffers between them.
- [ ] Frame Debugger: three dispatches where there was one.

## Explainers to build (Manim)

- Divergence: a batch of 32 threads stepping through two branches with lanes masked off.
- Megakernel vs wavefront: one thread doing everything vs stages handing buffers along.
- Ray $i$ stops being pixel $i$ once finished paths are removed (setting up cp-18).
