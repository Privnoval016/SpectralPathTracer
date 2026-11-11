# cp-24-color-carrier

Goal. A structural checkpoint that opens Chapter 4. Every color a path carries becomes a `SampledSpectrum`, the path
gets four immutable wavelengths and a lane mask, and the kernels compile an RGB and a `SPECTRAL` variant. The spectral
math arrives in later checkpoints, so the check is that nothing changes: the RGB image stays byte-identical, and the
spectral variant renders the same image too.

Scene `Assets/SpectralPathTracer/Scenes/CornellBox.unity`. The new switch is Spectral under Pixel Jitter in
`Settings/PathTracerSettings.asset`, off by default.

## What changed

1. `Lanes.hlsl` became `Spectrum/SampledSpectrum.hlsl`. `SampledSpectrum` is a `float4` typedef; `ACTIVE_LANES` is 3 in
   RGB mode and 4 in spectral mode; `RgbToSpectrum` and `SpectrumToRgb` take the path's wavelengths and pass colors
   through for now; `LaneMask`, `MaxValue` and `AverageValue` work on lanes.
2. `Spectrum/SampledWavelengths.hlsl` holds the four wavelengths and their pdfs. Generate stores them once
   (`SampleWavelengths` returns no wavelength and a pdf of 1 until cp-26), and Shade and Resolve only load them.
3. `PathFlags` gained four lane bits. Generate sets all active lanes, and Resolve divides the radiance by the mask's
   average, which is exactly 1 while every lane is alive.
4. `SampleBsdf`, `EvalBsdf` and Russian roulette take and return `SampledSpectrum`.
5. Generate, Shade and Resolve declare `#pragma multi_compile_local _ SPECTRAL`. `ComputeStage` finds the keyword once,
   each of those passes sets it both ways on its own command buffer, and `Spectral` is part of `RenderParams`, so
   switching restarts the sums.

## Decisions

**[Learn] Why make SampledSpectrum a typedef of float4 instead of a struct?** So `a * b` and `a + b` keep working.
Unity's default HLSL has no operator overloading, so a struct would turn every line of light transport into calls
like `Mul(a, b)`. The answer given was that the compiler checks it, but a typedef is only another name: a position
assigned to a spectrum compiles without a warning. Type safety is the price of readable math. Structs go in buffers
fine (`GpuPathState` is one) and cost the same registers.

**[Learn] Why are the wavelengths set once and never changed?** Each lane's value means radiance at its wavelength,
built from reflectances at that wavelength bounce after bounce, and Resolve converts it at that wavelength and pdf.
BDPT's light subpath must share the same four. When dispersion makes the lanes disagree, lanes are terminated through
the mask instead. The answer given was that it keeps the random numbers, but the sampler is stateless and its
dimensions come from stage, depth and slot, never from values.

**[Decide] How is the SPECTRAL keyword switched?** Per dispatch inside the graph, with `SetKeyword` on each pass's
command buffer and `AllowGlobalStateModification(true)`. It's the only way cp-27's split screen can run both variants
in one frame. Switching it on the shader asset outside the graph would have been simpler now and reworked later.

**[Decide] What does the SPECTRAL variant render before the spectral math exists?** The same image as RGB, through
pass-through stubs, so each later checkpoint replaces one stub against an identical baseline. Rejected were rendering
magenta until cp-26 and adding the keyword only at cp-26.

**[Learn] What does `#pragma multi_compile_local _ SPECTRAL` build?** Two compiled variants of the kernel, one with
`SPECTRAL` defined and one without, and each dispatch runs the one matching the keyword state. Right. `_` is the
variant without the keyword, and `_local` keeps the keyword private to the shader.

**[Learn] Why does every pass set the keyword, on or off?** The state stays on the shader after a pass, so a pass that
only enabled it would inherit the previous pass's state. Right.

## The math

The lane mask stands in for terminated lanes. With mask $m$ and $n$ active lanes, Resolve adds

$$\frac{L}{\frac{1}{n}\sum_i m_i},$$

which is $L$ while every lane is alive ($3/3$ in RGB mode, $4/4$ in spectral mode) and $4L$ once dispersion leaves
only the hero lane, the same correction as PBRT's `TerminateSecondary`.

## Verification

| Check | Spectral off | Spectral on |
|---|---|---|
| Keyword state on Generate, Shade and Resolve after a frame | off | on |
| Beauty and normals at 1 sample, pinned | byte-identical | byte-identical |
| AOVs 2 to 5 | byte-identical | byte-identical |
| 8 samples at budget 0 | byte-identical | byte-identical |

## Problems hit and fixes

- The first include generation ran before Unity had recompiled the new `PathFlags`, so the kernels couldn't find
  `PATHFLAGS_LANE0`. Generating again after the recompile fixed it.
- Reimporting the kernels brought back the "Blitter is already initialized" state, fixed by resetting the Blitter.
- An identical image alone can't tell a working switch from a dead one, so the check also reads the keyword state
  each shader is left with.

## Shots to capture

None; this checkpoint is structural.

## Explainers to build (Manim)

- Optional: a path's float4 relabeled from (r, g, b, 0) to four wavelengths, the same arithmetic underneath.
