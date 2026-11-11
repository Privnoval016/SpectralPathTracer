# cp-20-structure

Goal. A structural checkpoint with no video and an identical image as its check. Two independent architecture reviews
of everything up to cp-19 agreed on most problems, so the fixes landed here in the free cp-20 slot, before cp-21's
tooling builds on top. Every step was its own commit, and every step had to reproduce the pre-refactor capture byte for
byte.

Scene `Assets/SpectralPathTracer/Scenes/Canvas.unity`. The check pins every setting that affects pixels (1 sample,
4 bounces, exposure -3, roulette and jitter on) and captures the beauty image and the normals AOV at 960 by 540.

## What changed

1. The renderer leaves URP. `PathTracerRenderer` owns the stages, the scene compiler, the accumulation and the
   sample count, and knows nothing about URP. `PathTracerPass` only filters Game cameras and presents the result. Each
   pixel's sample count now lives in the accumulation buffer's w, Resolve overwrites at sample 0, and Display divides by
   w. That makes the sums correct even when waves of one sample land on different frames, which cp-21's time budget
   needs.
2. One list of image settings and one constant buffer. `RenderParams` replaces `AccumulationSignature` as the
   single list of values that change the image, and a change to it restarts the sums. Per-frame values reach every
   kernel through one generated `FrameConstants` cbuffer uploaded once per frame, so the include headers stopped
   declaring their own uniforms.
3. Scene owns its data. `GpuObject`, `GpuMaterial` and `ShapeType` moved into `Scene/GpuData`, which removed the
   Scene to Rendering dependency. `GpuMaterial` gained a `MaterialType`, and `GpuObject` gained `LightId`, `MediumIn`
   and `MediumOut`, all -1 for now. `SceneCompiler` uploads through a reusable `GrowableBuffer<T>` and picks the sky.
   `SceneData.hlsl` declares every scene table, `SceneBuffers.Declare/Bind` handles them per pass, and `GpuData.hlsl`
   is the one header that reaches into `Runtime/` for generated files.
4. Shade reads as steps. `Trace.hlsl` holds `ClosestHit` and `BuildSurfaceHit`, `Materials/Bsdf.hlsl` holds the
   Lambert sample, eval and pdf, `Paths/RussianRoulette.hlsl` plays roulette, and `Lanes.hlsl` names the float4 lanes a
   path carries.
5. Queues and small fixes. `WorkQueue` pairs a queue with its count offset, with `QueueCount` and `Enqueue` on the
   GPU side. The queue group size is one generated enum used directly in `numthreads`. Hits are one buffer per wave
   instead of one per bounce. Settings take `isPlaying` instead of reading global state, the renderer sticks to one
   camera, the pure hash moved to `Hash.hlsl`, and the top 8 bits of a sampler dimension are reserved for a stream id.
6. Docs. Both READMEs and their diagrams were brought up to date, and every new shader file has a purpose header.

## Decisions

**[Decide] Run a second review before acting?** Yes. A second reviewer, given the code and not the first review's
conclusions, found most of the same problems and a few new ones, so the merged list was trustworthy.

**[Decide] Fix everything now, or only what cp-21 needs?** Everything except new tests. The structure gets harder to
change with every chapter, and cp-21 onward builds directly on the renderer, the queues and the frame constants.

**[Decide] One assembly or several?** One runtime assembly, with the scene's GPU records moved into Scene to break the
cycle. Separate assemblies would add boilerplate without a second consumer to justify them. Debug tools still get
their own assemblies under `Tools/`, so the core never depends on the Input System.

**[Decide] Split the path state into hot and cold halves now?** Not yet. The split only pays off if Shade is limited by
memory traffic, and cp-21's per-bounce stats and GPU timing will show whether it is.

**[Decide] How to land it?** In six steps, each committed and pushed on its own and checked against the same pinned
capture, then tagged once at the end.

## The math

A constant buffer packs fields into 16-byte registers, and no field may straddle a register boundary. C# lays the
struct out back to back. The two agree only if every group of fields fills exactly one register:

$$\underbrace{64}_{\text{camera matrix}} + \underbrace{16 \cdot 3}_{\text{sky colors}} + \underbrace{16 \cdot 4}_{\text{scalars and float2s}} = 176 \text{ bytes} = 11 \text{ registers}$$

## Problems hit and fixes

- The first `FrameConstants` layout put a `uint` right after each sky `float3`. On Metal every Display test then drew
  the gray ramp, and the sampler gave identical values for two sample indices. Metal pads a `float3` in a cbuffer to
  16 bytes, so every field after the first color sat 4 bytes later than C# wrote it. The colors became `float4`, and
  a throwaway kernel that copied all 44 words back to the CPU matched C# exactly, matrix orientation included.
- The pinned captures leave the sky black, so they never exercised it. Setting the zenith to red in memory showed a red
  sky and reset the sums, and setting it back reset them again.
- Black captures after a reimport were URP's "Blitter is already initialized" state, not the refactor. Resetting the
  Blitter fixed them, as before.
- The first roulette helper returned early, and Metal warned about a potentially uninitialized variable. One return at
  the end, like the other helpers, removed it.
- Beauty and normals don't cover the other AOVs, so for step 4 the depth, front face, noise and bounce direction views
  were captured with the old and the new shaders and compared. All four matched.

## Shots to capture

None; this checkpoint is structural.

## Explainers to build (Manim)

- Optional: cbuffer packing, with a `float3` followed by a `uint` landing differently on Metal and in C#.
