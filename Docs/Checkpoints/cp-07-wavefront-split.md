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

## Wavefront vs megakernel

A megakernel runs a whole path (generate, intersect, shade, every bounce) in one kernel, one thread per
path. A wavefront tracer splits each step into its own kernel over all rays at once, with state passed
through buffers [Laine13].

Benefits of the wavefront design:

- **Less divergence.** A batch of 32 threads shares one instruction stream, so when its threads need
  different code (glass vs a diffuse wall), the batch steps through every branch with lanes masked off
  and takes roughly the sum of their times. Separate kernels, and later a queue per material, keep threads
  that run together on the same code.
- **Higher occupancy.** A kernel's register count is set by its most complex path, and every thread pays it.
  A megakernel holding glass, microfacets, media and bidirectional connections needs many registers, so
  fewer threads fit on the GPU at once, and there are fewer threads to switch to while waiting on memory.
  Small kernels use few registers and keep many more threads in flight.
- **No idle lanes on long paths (from cp-18).** In a megakernel, a thread whose path ended still occupies its
  lane until the longest path in its batch finishes. A wavefront tracer removes finished paths between
  bounces and dispatches only the rays still alive.
- **Specialization.** Each material, light or shape type can get its own small kernel, and rays can be sorted
  so neighboring threads take similar paths through memory.
- **Faster iteration and modular code.** Each stage compiles on its own (editing Shade doesn't recompile
  Intersect), and each stage is one small, readable kernel and C# class.
- **Inspectable intermediate state.** The ray and hit buffers can be read back and checked; that's how this
  checkpoint proved the split bit-identical, and the Render Graph Viewer shows every stage.

Costs:

- **Memory traffic.** State lives in registers in a megakernel but makes a round trip through GPU memory
  between wavefront kernels. Keeping records lean matters (cp-08 keeps normals out of the hit record).
- **More dispatches.** One per stage per bounce, plus the counters and indirect dispatches that compaction
  needs (cp-18).
- **More plumbing.** Buffers, accessors, queues, and the C# to record every stage.

When a megakernel wins: simple scenes with few materials and short paths, which is exactly this
checkpoint's state (one sphere, no bounces). cp-07 alone makes nothing faster; the payoff starts once paths
bounce and materials multiply.

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
- Occupancy: one register-heavy megakernel vs many small kernels, as threads in flight on a GPU core.

## References

[Laine13] Samuli Laine, Tero Karras, Timo Aila. 2013. Megakernels Considered Harmful: Wavefront Path Tracing on GPUs. In *Proceedings of High-Performance Graphics (HPG '13)*, 137-144. https://doi.org/10.1145/2492045.2492060
