# cp-15-display-transform

Goal. Separate the light in the scene from the pixels on screen. Radiance stays unbounded linear float32 for
accumulation, a Display stage applies exposure and a clamp, and the blit to the screen encodes sRGB exactly once.

Scene `Assets/SpectralPathTracer/Scenes/Canvas.unity`, settings `Settings/PathTracerSettings.asset` (Display section).

## What changed

- The persistent output is now the radiance texture, `R32G32B32A32_SFloat` (`ImportRadiance`, "PathTracerRadiance").
- `Display.compute` and `DisplayStage` apply exposure ($2^{EV}$, rendered image only) and then `saturate` per pixel, writing a
  transient `R16G16B16A16_SFloat` display texture that Present blits. The stage runs even when paused.
- `PathTracerSettings` gains a Display section with `Exposure` (stops, -10 to 10) and `ShowGrayRamp`.
- `DisplayTests` checks exposure in stops, the clamp, AOVs skipping exposure and the ramp. `CameraRayMirrorTests` now binds
  the path buffer that Generate has written since cp-14.

## Decisions

**[Learn] Why does the display transform apply to the averaged radiance?** Clamping first biases the result, because
$E[\min(X, 1)] \ne \min(E[X], 1)$. A pixel worth 50 one time in a hundred is a true 0.5, yet its clamped samples average to
0.01, so rare bright paths such as caustics vanish. Exposure on its own is linear and commutes with averaging. The
nonlinear step (the clamp now, AgX at cp-55) has to come last.

**[Learn] What breaks when accumulating in half floats?** Half floats carry 11 bits of precision. Once a sum reaches 2048 its
spacing is 2, so a 0.5 sample rounds to no change and the pixel stalls at a wrong value after about 4096 samples. Float32
carries 24 bits (spacing $2^{-12}$ at 2048), which is why the radiance texture is float32 now.

**[Decide] Where does the display transform run?** In a Display compute stage that writes a separate display texture.
Radiance stays pure for accumulation, and gamut compression, AgX and later AOV handling join the same stage. Rejected was
doing it inside the Present blit's shader.

**[Decide] How is exposure set?** In stops. The radiance is multiplied by $2^{EV}$, so each +1 doubles it and 0 leaves it
unchanged. Rejected was a linear multiplier.

**[Learn] Where does sRGB encoding happen?** In hardware, when the blit writes into the sRGB camera target. The gray ramp
proves it happens exactly once.

**[Learn] Which format is the display texture?** `R16G16B16A16_SFloat`. The answer given was `R8G8B8A8_SRGB`. This Mac (Metal)
can write that format from a compute shader and its levels are well spaced, but DX12 can't use sRGB formats as compute
outputs and Vulkan rarely supports them as storage images, so it would break R10. `R8G8B8A8_UNorm` (linear 8-bit) bands
the shadows, since its darkest step of 1/255 displays as about 13/255.

**[Decide] Do AOVs go through exposure?** No. They are data with fixed encodings (normals as $n/2 + 1/2$, the depth ramp), so
only the rendered image is exposed.

**[Decide] How is single sRGB encoding checked?** A Show Gray Ramp toggle makes Display output the linear ramp
$(x + 0.5)/W$, and a capture is compared column by column with the sRGB formula. An EditMode test checks the kernel's math.

## The math

$$d = \operatorname{saturate}\!\left(2^{EV} L\right), \qquad \text{screen} = \text{sRGB}(d) = \begin{cases} 12.92\,d & d \le 0.0031308 \\ 1.055\,d^{1/2.4} - 0.055 & \text{otherwise} \end{cases}$$

All 960 columns of the gray ramp landed within 0.59 of 255 of $255\,\text{sRGB}((x + 0.5)/960)$. Without encoding the error
would reach 74, and with a double encoding the quarter mark would read 194 instead of 137.

## Problems hit and fixes

- `CameraRayMirrorTests` failed in the full run. Since cp-14 Generate also writes `_Paths`, which the test never bound, and on
  Metal an unbound writable buffer can make the dispatch do nothing (directions came back zero, an error of exactly 1).
  It passed at cp-14 only because the pipeline's buffer happened to still be bound. The test now binds its own path buffer.
- The first edit renamed the output texture to radiance but kept half precision. It is float32 now.

## Shots to capture

- [ ] Exposure 0 (clipped yellow-white speckles) sliding down to -4, where the red wall turns red, the green wall green and
  the lamp warm.
- [ ] Show Gray Ramp, the smooth ramp next to a graph of the sRGB curve.
- [ ] Exposure changing on a paused frame.

## Explainers to build (Manim)

- Why clamping must come after averaging, with a rare bright sample and its clamped mean.
- The float number line, half vs float spacing at 2048, and a 0.5 sample rounding away.
- Linear light vs sRGB encoding, with the same ramp encoded once, never and twice.
