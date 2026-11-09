# cp-03-uv-gradient

Goal: color each pixel by its position, the first image where a thread's result depends on which pixel
it is.

Scene: `Assets/SpectralPathTracer/Scenes/Canvas.unity`.

## What changed

The kernel, now named `Render`, computes each pixel's normalized coordinate
`uv = (float2(id.xy) + 0.5) / float2(_Resolution)` and writes `float4(uv, 0, 1)`: red grows to the right,
green grows upward.

The solid-color plumbing is gone: the feature's `fillColor` field, the linear conversion, the pass's color
parameter, `_Color` and its `SetComputeVectorParam`. The kernel-name constant in the feature is `"Render"`
to match the HLSL, and the pass's names follow (`_kernelIndex`, `RenderPassData`, "Path Tracer: Render").

## Decisions

**How should a pixel index become a 0 to 1 coordinate?** `(id + 0.5) / res`, the pixel's center. The
first and last pixels sit at $\frac{0.5}{W}$ and $\frac{W - 0.5}{W}$, symmetric about 0.5, so the image center
is exactly $u = 0.5$ and cp-04's central ray points straight down the view axis. In cp-16 the 0.5 becomes a
random offset for anti-aliasing. Rejected: `id / res` (everything shifts half a pixel toward the
bottom-left), `id / (res - 1)` (pixels spaced $\frac{1}{W-1}$ apart, a slight stretch, and a divide by zero for
a 1-pixel image).

**What does `id.xy / _Resolution` compute?** Unsigned integer division, which drops the fraction, so every
pixel would be 0 (solid black). HLSL promotes to float once either side is a float; writing `float2(...)`
states that intent instead of relying on the `+ 0.5` to trigger it.

**Where does pixel (0, 0) appear on screen?** Bottom-left, predicted and then confirmed by the render.
Metal and DirectX store textures top row first and OpenGL bottom row first, but Unity renders flipped on
the top-first APIs and flips back when presenting, so texture coordinate $(0, 0)$ is bottom-left everywhere.
cp-04 can therefore treat increasing $v$ as up without a flip.

**What happens to the unused color and the old kernel name?** Removed and renamed. A parameter that does
nothing reads as if it matters, and keeping it as a tint would be a feature nobody asked for. The C#
constant must change with the HLSL kernel name, because `FindKernel` looks it up by that exact string.

## The math

$$u = \frac{x + 0.5}{W}, \qquad v = \frac{y + 0.5}{H}$$

The middle of the image looks much brighter than halfway: the values are linear, and the display's sRGB
encoding maps linear 0.5 to about 0.735. That's correct for linear light (the cp-01 decode curve in
reverse).

## Problems hit and fixes

- The constructor still took and stored the removed color after the first pass of edits; the rest of the
  rename was finished in one refactor.

## Shots to capture

- [ ] The gradient at 1920x1080: black bottom-left, red bottom-right, green top-left, yellow top-right.
- [ ] Side by side: the magenta fill (cp-01 tag) next to the gradient.

## Explainers to build (Manim)

- Each pixel's thread computing its own $(u, v)$ from its ID: the same code, a different answer per pixel.
- Pixel centers vs pixel corners on a small grid, showing the half-pixel shift.
- Texture origin conventions: top-first storage vs Unity's bottom-left $(0, 0)$.
