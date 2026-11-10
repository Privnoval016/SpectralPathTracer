# cp-12-sampler

Goal: one source of random numbers for every later Monte Carlo step, built and tested before anything uses it.
Every number is a hash of who asks and why: (output pixel, sample index, dimension).

Scene: `Assets/SpectralPathTracer/Scenes/Canvas.unity`. Settings: `Settings/AovSettings.asset` (AOV Noise).

## What changed

- `Shaders/Include/Sampler.hlsl`: `Pcg4d` [JarzynskiOlano20], `OutputPixel` (render pixel to output-frame pixel),
  `UintToUnitFloat` (top 24 bits times $2^{-24}$), `SampleUniform(outputPixel, sampleIndex, dimension)`, and the first
  entries of the fixed dimension map (pixel jitter x and y).
- `PathTracerPass` counts the frames it renders as the sample index (paused frames don't count) and passes it, the output
  size and the noise dimension through `FrameContext`. `ComputeStage.SetSampler` sets the sampler uniforms.
- A Noise AOV that shows one sampler dimension as gray for every pixel, hit or not, with the dimension chosen in
  `AovSettings`.
- EditMode tests: a test-only kernel (`Tests/EditMode/SamplerTest.compute`) writes 65,536 sampler values per draw, and
  `SamplerStatisticsTests` checks the range, mean, variance, histogram, and the correlation across dimensions and samples.

## Decisions

**[Learn] Where do a thread's random numbers come from?** A stateless hash. Rejected: a per-pixel state buffer (every
draw becomes a read, update and write to memory, and every wavefront stage would have to carry the state and draw in
the same order) and uploading `UnityEngine.Random` values from the CPU (about $1920 \times 1080 \times 20 \times 4 \approx 166$ MB per
frame, generated on the main thread). A hash needs no storage, any kernel can compute any number, and a pixel's
numbers are reproducible for debugging.

**[Learn] Which inputs does each number need?** Pixel, sample index and dimension. Pixel only freezes the noise, so
averaging never converges. Pixel and sample give every use within a sample the same number: jitter x equals jitter y,
so samples fall on the pixel's diagonal, and correlated samples bias the image.

**[Learn] How do 32 random bits become a float in [0, 1)?** `(bits >> 8) * 2^-24`. Every multiple of $2^{-24}$ below 1 is exact
in a float, so the largest result is $1 - 2^{-24}$. Rejected: dividing by $2^{32} - 1$ (the top 128 values round to exactly
1.0, which picks index $N$ in `floor(u * N)` and gives infinity in $-\ln(1 - u)$, about once every 33 million draws) and
`asfloat` (random exponent bits: huge values, negatives and NaN).

**[Decide] Which hash?** `pcg4d` from Jarzynski and Olano's survey, which recommends it for four inputs hashed to one
output; ours are exactly four. Rejected: the Wang hash (measurably worse in the survey) and `frac(sin(...))` (depends on
each GPU's precision for large sine arguments, so the noise would differ between Metal and DX12, against R10).

**[Learn] How do uses get their dimension numbers?** A fixed map, `dim = base(stage) + depth * K + slot`. The answer
given was to chain the hashes, which is a stateful generator in disguise: the last number would have to be stored
between kernels, one extra draw (say, Russian roulette) would shift every later number and change the whole image, and
it rules out low-discrepancy samplers, which are defined per dimension index. A running counter has the same first two
problems.

**[Decide] Which pixel feeds the hash?** The output-frame pixel under the render pixel's center. At render scale 0.5 the
noise is a downsampled copy of the full-resolution noise, so changing scale doesn't reshuffle it.

**[Decide] Where does the sample index come from?** A counter in `PathTracerPass`. Rejected: `Time.frameCount`
(advances whenever anything renders, irregularly in Edit Mode, and can't be reset when accumulation restarts) and a
random seed per frame (not reproducible, and seeds can collide).

**[Decide] How do we prove it works?** A noise AOV for the eye and a statistics test for what the eye misses.

**[Learn] How close to 0.5 must the mean be?** Within 5 standard errors, $5\sqrt{1/12/N} \approx 0.0056$ for $N = 65{,}536$; a correct
sampler fails less than once in a million runs. Rejected: exactly 0.5 (a sample mean is never exact) and a fixed 0.01
(not tied to $N$: 9 standard errors here, too lax, and too strict at small $N$).

**[Learn] Which checks catch a sampler that always returns 0.5?** The variance (0 instead of $1/12$) and the histogram (all
values in one bin). The mean passes, which is why one check is never enough.

**[Learn] How do we check two dimensions are independent?** Their correlation must be within $5/\sqrt{N}$ of zero. Rejected:
comparing means (identical dimensions have equal means) and checking that values differ ($v_1 = 1 - v_0$ passes while
being perfectly anti-correlated).

**[Decide] How does the test reach the GPU sampler?** A test-only kernel that includes `Sampler.hlsl`. Rejected: a C#
port (tests the copy, not the shader that ships) and reading back the noise AOV (8-bit, sRGB-encoded values).

## The math

$$u = \lfloor h / 2^8 \rfloor \cdot 2^{-24} \in [0, 1 - 2^{-24}], \qquad h = \text{pcg4d}(p_x, p_y, s, d)_x$$

$$\text{mean bound } 5\sqrt{\tfrac{1}{12N}}, \qquad \text{variance bound } 5\sqrt{\tfrac{\mu_4 - \sigma^4}{N}} = 5\sqrt{\tfrac{1/80 - 1/144}{N}}, \qquad
\chi^2 = \sum_{k=1}^{16} \frac{(n_k - N/16)^2}{N/16} < 37.70$$

37.70 is the 99.9th percentile of $\chi^2$ with 15 degrees of freedom.

## Problems hit and fixes

- `Sampler.hlsl` was missing its `#endif`, the same slip as `Quad.hlsl` in cp-10. Writing `#endif` right after the
  `#define` prevents it.
- Captures came out blank after regenerating the includes: URP's "Blitter is already initialized" state again;
  `Blitter.Cleanup()` reset it.
- The depth AOV looked inside out: the range had been left at Near 9.15, Far 0.44 (reversed, and far wider than the box's
  0.8 to 1.36 m), so every depth mapped to 0.894 to 0.959 with near surfaces brighter. Checked against hand-computed
  values at eight spots (all within 1 of 255) with a 0.8 to 1.4 m range, now the asset's range. Even correct, the linear
  ramp looks bright and glowing because the display's sRGB encoding lifts dark values (linear 0.1 shows as 89 of 255).
- Mutation testing the tests: ignoring the dimension failed only the correlation test (correlation 1), and a constant
  0.5 failed the variance and histogram tests while the mean passed, as predicted. It also showed the correlation tests
  passing on the constant sampler: the correlation of a constant is $0/0$ = NaN, and NUnit's `Is.LessThan` didn't reject
  it. Both correlation tests now assert the value isn't NaN first.

## Shots to capture

- [ ] The Noise AOV flickering frame to frame, then switching dimension, then pausing so it freezes.
- [ ] The test runner going green, then a deliberately broken sampler turning tests red.
- [ ] The depth AOV before (the inside-out range) and after.

## Explainers to build (Manim)

- A stream of numbers vs a hash of (pixel, sample, dimension): what happens when one path draws an extra number.
- Why 24 bits: the float number line near 1, and $2^{32} - 1$ rounding up to exactly 1.0.
- sRGB encoding lifting a linear ramp.

## References

[JarzynskiOlano20] Mark Jarzynski, Marc Olano. 2020. Hash Functions for GPU Rendering. *Journal of Computer Graphics Techniques* 9, 3, 20-38. https://jcgt.org/published/0009/03/02/
