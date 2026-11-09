# cp-02-render-controls

Goal: control when and how the path tracer renders: per-mode toggles, pause, a fixed output size
letterboxed into any Game view, and a cheaper resolution while editing.

Scene: `Assets/SpectralPathTracer/Scenes/Canvas.unity`. Settings: `Settings/PathTracerSettings.asset`.

## What changed

`Runtime/Rendering/PathTracerSettings.cs` is a Config ScriptableObject holding: render in Edit Mode,
render in Play Mode, paused, the output resolution (1920x1080), and a resolution scale per mode
(0.5 in Edit Mode, 1 in Play Mode). It exposes `IsEnabledForCurrentMode` and `RenderResolution`, so
the rules for "which toggle applies" and "how big to render" live in one place.

`PathTracerFeature` holds a reference to the settings asset, skips the pass when the current mode is
off, releases the old pass's GPU memory before `Create()` rebuilds it, and releases it again in
`Dispose(bool)`.

`PathTracerPass` now owns a persistent output texture (an `RTHandle`). Each frame it:

1. Reallocates the texture only if its size or format changed
   (`RenderingUtils.ReAllocateHandleIfNeeded`), then imports it into the render graph.
2. Records the compute pass at the render resolution, unless paused.
3. Records a present pass: a raster pass that clears the camera target to black, sets the viewport to
   the letterboxed rectangle, and blits the output into it with bilinear filtering.

## Decisions

**Where do the settings live?** A separate Config ScriptableObject referenced by the feature. Presets can
be swapped whole (an "Editing" and a "Footage" asset), edits don't rebuild the feature, and the feature
keeps wiring while the asset keeps tuning. Rejected: fields on the feature (every edit re-runs
`Create()`), a camera MonoBehaviour (an asset can't reference scene objects without a runtime `Find`), a
static class (unserialized global state).

**How does runtime code tell Edit Mode from Play Mode?** `Application.isPlaying`: false in Edit Mode, true
in Play Mode and builds. Rejected: `EditorApplication.isPlaying` (UnityEditor, breaks builds),
`#if UNITY_EDITOR` (answers editor vs build at compile time), the camera type (Game cameras exist in
Edit Mode too).

**What does pausing need?** A texture that outlives the frame. Render graph textures are transient: pooled
and possibly aliased after their last use in a frame, so skipping the dispatch alone would show garbage.

**What happens to the old pass's texture when `Create()` rebuilds?** It is released explicitly, in
`Create()` and in `Dispose(bool)`. C#'s garbage collector frees the small `RTHandle` wrapper, never the GPU
texture behind it, so every Inspector edit would leak about 16.6 MB. Rejected: a static shared texture
(still leaks on script reload, and every camera would share one image), assuming Unity frees RTHandles
per frame (that is the transient render graph textures).

**When is the texture allocated?** Checked every frame and reallocated only when the wanted size or format
differs. Rejected: once in the constructor (wrong after any resize or mode change), fresh every frame
(constant allocation churn, and it throws away the paused image).

**How does the render graph use our texture?** `ImportTexture` every frame. The graph is rebuilt each
frame, and a `TextureHandle` indexes only that frame's resources. Rejected: importing once (stale handle),
bypassing the graph (it can't order the passes and culls a compute pass with no known output), copying
into a graph texture (an extra full-screen copy).

**What runs while paused?** Only the present pass. The camera target is cleared every frame too, so
enqueueing nothing would show the skybox, and a kernel-side pause flag would still launch every thread.

**How is a 16:9 image placed in any Game view?** Fit inside, centered, with bars. Rejected: crop to fill (in
a square view almost half the frame is cut off, so framing a shot lies), stretch (spheres become eggs),
pixel-exact (overflows small views).

**What does a resolution scale of 0.5 render?** A 960x540 texture, enlarged when presented. Cost grows with
the number of pixels processed, $\propto s^2$, so 0.5 is a quarter of the work. Rejected: rendering full
size and shrinking (no savings), rendering every other pixel (holes, broken thread mapping).

**How is the image drawn into only part of the target?** A raster pass that sets the viewport, then blits.
Rejected: `AddBlitPass` scale and offset (they select which part of the source to read), changing
`Camera.rect` (a renderer feature silently editing a scene object), writing the camera texture from the
kernel (format limits from cp-01).

**What fills the bars?** Whatever the camera cleared to, the skybox here, so the present pass clears to
black first. Render targets are plain memory; nothing turns undrawn pixels black on its own.

## The math

Fit inside, for an image $w \times h$ in a target $W \times H$:

$$s = \min\left(\frac{W}{w}, \frac{H}{h}\right), \qquad \text{rect} = \left(\frac{W - ws}{2},\ \frac{H - hs}{2},\ ws,\ hs\right)$$

A 1000x1000 view gives $s \approx 0.521$: a centered $1000 \times 563$ image with bars of 219 and 218
pixels. The `(float)` casts matter: in integer division $1000 / 1920 = 0$.

Rendering cost with resolution scale $s$:

$$\text{cost} \propto (sW)(sH) = s^2 W H$$

At $s = 0.5$ the kernel dispatches $\lceil 960/8 \rceil \times \lceil 540/8 \rceil = 120 \times 68$ groups instead
of $240 \times 135$.

GPU memory per output texture: $1920 \times 1080 \times 8$ bytes (four 16-bit floats) $\approx 16.6$ MB.

## Problems hit and fixes

- Unity sometimes hadn't recompiled after a save, so the console showed a stale "no errors" state.
  Refreshing the asset database showed the real errors.
- `RenderingUtils.ReallocateIfNeeded` does not exist, and `ReAllocateIfNeeded` is the older overload set
  taking a `RenderTextureDescriptor` (CS1503, cannot convert argument). The `TextureDesc` version is
  `ReAllocateHandleIfNeeded`.
- The feature still called the old three-argument pass constructor (CS7036).
- The feature first lacked a `Dispose(bool)` override, which would have leaked the last texture on every
  script reload.
- The image rendered into the bottom-left quarter: `desc` was used both as "the camera's size" and as
  "our texture's size", so after overriding it to 960x540 the fit was computed for a 960x540 screen.
  Fixed by keeping `cameraDesc` and copying it into `desc` (a struct assignment copies the values).
- `[Tooltip]` on a `[field: SerializeField]` auto-property attaches to the property, not the serialized
  backing field; it needs `[field: Tooltip(...)]`.
- Leak check: the count of `PathTracerOutput` render textures stayed at 1 across three forced script
  recompiles.

## Shots to capture

- [ ] Toggling "Render In Edit Mode": magenta to skybox and back.
- [ ] Pausing, changing the color with nothing happening, then unpausing.
- [ ] Free Aspect Game view being resized while the image stays 16:9 with black bars.
- [ ] Frame Debugger: the dispatch at $120 \times 68$ groups in Edit Mode vs $240 \times 135$ in Play Mode.
- [ ] Render Graph Viewer: the imported output texture, the compute pass and the present pass.

## Explainers to build (Manim)

- Transient vs persistent textures: the render graph pool handing memory out and back each frame, next to
  an RTHandle that stays put.
- Fit inside vs crop vs stretch on a square screen.
- Cost $\propto s^2$: a grid of pixels shrinking to a quarter.

## References

[URP-17.5] Unity Technologies. Universal Render Pipeline 17.5 and Core RP package source (`com.unity.render-pipelines.universal`, `com.unity.render-pipelines.core`), read for `RenderingUtils.ReAllocateHandleIfNeeded`, `RenderGraph.ImportTexture`, raster passes, `Blitter.BlitTexture`, and `ScriptableRendererFeature` lifecycle (`Create()` on enable and `OnValidate`).
