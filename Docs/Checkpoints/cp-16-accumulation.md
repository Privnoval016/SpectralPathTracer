# cp-16-accumulation

Goal. Average samples across frames so the noise melts away, restart the average whenever the image it estimates
changes, stop at a target sample count, and keep Edit Mode rendering while samples are still needed.

Scene `Assets/SpectralPathTracer/Scenes/Canvas.unity`, settings `Settings/PathTracerSettings.asset` (Target Samples, with a
live sample count in its Inspector).

## What changed

- The radiance texture became a persistent accumulation buffer of per-pixel float4 sums, owned by the pass and imported
  each frame. Resolve adds each path's radiance to its pixel's sum (overwriting on the first sample after a reset), and
  Display divides by the sample count before exposure.
- `AccumulationSignature` holds everything upstream of the sums that isn't scene data (camera matrix, field of view,
  aspect, render size, Max Bounces, sky colors, the AOV and its parameters). `SceneCompiler.Update()` now returns whether
  it rebuilt. Either one changing restarts the count, and the sampler's sample index restarts with it.
- `TargetSamples` (default 1024, 0 for no limit) stops accumulation. The GPU time budget moved to cp-21.
- `AccumulationProgress` (a data-only static) carries the count to the Editor. `AccumulationRepaint` repaints the Game
  view every Editor tick while accumulating in Edit Mode, and `PathTracerSettingsEditor` shows "Samples N / target".
- `DisplayTests` feeds the kernel a buffer and checks the division by the sample count.

## Decisions

**[Decide] How do samples combine?** A sum plus a count, divided at display time. Light tracing in the BDPT chapters adds
contributions to arbitrary pixels, which a sum absorbs and a running mean can't. Rejected was a running mean.

**[Learn] Which changes must reset the sums?** Camera, scene and render settings, but not exposure. The answer given was
camera and scene. Every sample in the sum has to estimate the same image. After a Max Bounces change, old one-bounce
samples fade only as $1/N$ (1000 of them need 9000 more samples to drop under 10%), switching the AOV would average normal
colors into light, and a new render size reallocates the buffer with undefined contents. Exposure applies after the
average, so it never costs samples.

**[Decide] Does the sample index restart after a reset?** Yes, so the same scene and camera always give the same image
after N samples, which keeps comparisons and movie-mode frames reproducible. Rejected was continuing to count.

**[Decide] When does accumulation stop?** At a target sample count, with one sample per frame. The GPU time budget moved to
cp-21, where the stats tool measures frame timing anyway.

**[Decide] How does the pass decide to reset?** By comparing a frame signature with last frame's, plus the scene
compiler's report. Rejected were change events from every setting and component.

**[Learn] How does the scene compiler tell the pass about changes?** `Update()` returns whether it rebuilt. The answer given
was a read-and-clear dirty flag, which fails with two readers (whichever clears it hides the change from the other, the
`transform.hasChanged` problem from cp-09) and never sees moved objects, since moves change no flag. Comparing `Version` in
the pass misses moves too. The compiler already knows everything that changed, so it reports it.

**[Learn] How does Edit Mode keep rendering?** An Editor-only `EditorApplication.update` hook that repaints the Game view
while the pass reports it is accumulating, so the Editor goes idle again once the target is reached. Rejected were
`Application.targetFrameRate` (it limits frame rate and never makes the Edit Mode Game view render) and accumulating in
Play Mode only (R11 requires Edit Mode rendering). It lives in the Editor assembly because runtime code can't reference
`UnityEditor`, and `[InitializeOnLoad]` re-registers it after every domain reload.

**[Decide] Where does progress show?** As a live line in the settings Inspector, keeping footage clean. Rejected was an
overlay on the Game view.

**[Learn] Where do the sums live?** A structured buffer. Reading and writing a four-channel float texture in one kernel needs
typed UAV loads for that format, which are optional on DX12, need tier-2 read-write textures on Metal and a separate
feature on Vulkan, so R10 can't rely on them. Buffers are read-write everywhere and support the atomic adds that BDPT
splats need. Ping-pong textures work but double the memory (2 x 33 MB at 1080p) and swap every frame.

**[Learn] How is the old sum cleared after a reset?** The first sample overwrites instead of adding. `_SampleCount` is one
uniform for the dispatch, so the branch is uniform and costs nothing. A clear pass would write 33 MB on every reset (every
frame while dragging an object), and reallocating stalls and leaves undefined contents on some APIs.

## The math

$$S_N = \sum_{k=1}^{N} L_k, \qquad \bar{L}_N = \frac{S_N}{N}, \qquad \sigma\!\left(\bar{L}_N\right) = \frac{\sigma(L)}{\sqrt{N}}$$

Check. At 256 samples, a 55 by 45 pixel patch of the back wall averaged (in linear) to within about 1% of
$a\,L_e\,\frac{1}{\pi}\int_\text{light}\frac{\cos\theta\cos\theta'}{r^2}\,dA$ in every channel (ratios 1.011, 1.012 and 0.992), so the estimator
converges to the right answer.

## Problems hit and fixes

- `EditorApplication.QueuePlayerLoopUpdate()` didn't redraw the Game view in Edit Mode, even with the tab in front, so
  accumulation stalled after one or two samples. The loop now repaints the Game view windows directly, finding their
  internal class by name. With that, samples climbed by themselves at about 15 per second.
- A hidden Game view tab never renders, whatever the loop asks, so the Game view has to be visible while accumulating.
- The first attempt to set Target Samples in a test script failed silently and left 1024. The progress readout showed
  it at once.

## Shots to capture

- [ ] The noise melting from 1 to 256 samples, with the Inspector count ticking up.
- [ ] Dragging a sphere or the camera, where the image snaps back to noise and melts again.
- [ ] Moving the exposure slider on a converged image without losing a sample.

## Explainers to build (Manim)

- $1/\sqrt{N}$, where four times the samples halves the noise.
- Why mixed samples converge to a blend of two images, with the old samples' weight fading as $1/N$.
