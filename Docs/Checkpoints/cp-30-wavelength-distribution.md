# cp-30-wavelength-distribution

Goal. Less color noise for the same samples. Every spectral sample traces one wavelength, and until now that wavelength
was drawn uniformly from 360 to 830 nm: a third of the draws landed where the eye sees almost nothing, and under an
1800 K lamp most of the rest landed where there was almost no light. Drawing wavelengths where the eye and the scene's
light are, and spreading each pixel's draws evenly over its samples, gives the same image with a fraction of the color
noise. The estimator never changes; only the pdf it divides by does.

Scenes: any spectral scene; `BlackbodySweep.unity` shows it best at its starting 1800 K. Light Share and Stratify
Wavelengths sit under Locked Wavelength in `Settings/PathTracerSettings.asset`, visible while a spectral image is drawn
and the wavelength isn't locked. Tools > Spectral Path Tracer > Noise Check > Save Reference stores a high-sample image
of the open scene in `Recordings/NoiseReferences/`, and Compare reports how far the current image is from it.

## What changed

1. `WavelengthDistribution` builds a piecewise-constant pdf on the 1 nm grid and its CDF: the eye's weights
   $\bar x + \bar y + \bar z$, the scene's light seen through the same curves, a mix of the two, and a 1% uniform
   floor. Generate finds the bin by binary search (`SampleWavelengthCdf`) and divides by the bin's own probability.
2. `WavelengthTable` sums every shape's emission and the sky at each nm, evaluated the way Shade evaluates them,
   mixes that with the eye by Light Share, and uploads the CDF as `_WavelengthCdf` whenever the light or the share
   changes. Generate now takes the scene buffers.
3. Stratify Wavelengths draws each pixel's $u$ from a golden ratio sequence with a random start per pixel
   (`SampleStratified`), in the spare frame constant at byte 236.
4. `SceneCompiler` was split into one class per job: `SceneChanges`, `MaterialTable`, `SceneSky` and
   `WavelengthTable`, with the compiler running them in order. Transforms and sky colors are now compared exactly.
5. Noise Check measures RMSE and relative MSE against a saved reference.
6. Also: `Cie.D65Scale` moved to the runtime, and `TemperatureSweep` applies its temperature in `OnValidate`, so the
   sweep scene opens at 1800 K in Edit Mode.

## Decisions

**Why not keep sampling uniformly?** (asked before building) Uniform is unbiased but slow. To estimate
$\int \bar y = 106.9$, a uniform sample returns $\bar y(\lambda) \times 470$: 468 at 550 nm, 17.9 at 450, nearly 0 at
800, so single samples swing from 0 to 468 around the answer. Drawing in proportion to $\bar y$ makes every sample
return exactly 106.9. That is importance sampling: draw often where the function is large and divide by the pdf.

**[Learn] What must a new pdf satisfy to stay unbiased?** It must be nonzero wherever $L\,\bar{\mathbf x}$ can be,
and be the density actually sampled from, since $E[f/p] = \int_{p>0} f$. The answer given was that it must integrate
to 470 like the uniform pdf; a pdf integrates to 1 (the uniform's $1/470$ per nm over 470 nm), and a total of 470
would make a white wall read $Y = 0.0021$. Proportional to $L$ is the zero-variance ideal, not a requirement.

**[Learn] Which pdf shape gives the least color noise for a white wall under D65?** Light times
$\bar x + \bar y + \bar z$. The answer given was light times $\bar y$, which makes $Y$ noiseless but weights each blue
draw by $\bar z/\bar y$, 47 at 450 nm and 161 at 420, so Z's scatter per sample rises from 2.23 (uniform) to 6.88.
The sum bounds all three weights by 1: 0.72, 0.85, 1.09 for X, Y, Z.

**[Decide] How is the distribution represented?** A tabulated CDF built on the CPU and searched on the GPU in 9 steps.
pbrt-v4's analytic visible fit can't follow a lamp, and fitting now and tabulating later would mean two samplers.

**[Decide] Who owns it?** The scene compiler, rebuilt with the scene, since round 2 made it depend on the lights.

**Your search, and the floor.** (found in review) The first `Sample` searched for the first index with
`cdf[index] >= u`, which is the bin after the one holding $u$, and read past the end in the last bin; it now finds the
first index above $u$ and takes the bin before it. The first check also found 21 far-red bins with zero width: their
probability, about $10^{-8}$, vanishes in a float CDF near 1, so those wavelengths could never be drawn. A 1% uniform
floor (defensive importance sampling) gives every bin at least $2.1 \times 10^{-5}$.

**[Learn] What does a pdf built from the emitted light get wrong about the light reaching the camera?** What surfaces
reflect. The answer given was the camera's exposure, but any constant scale cancels when the CDF normalizes. Under the
1800 K lamp the light peaks at 607 nm with 27% in 500 to 580 nm; after the measured green wall it peaks at 580 with
46% there. That is why the eye's half stays in the mix.

**[Learn] Why does blue noise double at 1800 K when the pdf follows the light?** Blue is drawn less often: 4% of draws
instead of 37.5%, each counted 9 times larger. Right. Importance sampling moves noise to where it matters less; the
measurement confirmed it, blue RMSE 0.026 at share 0 against 0.049 at share 1 while red fell from 0.630 to 0.336.

**[Decide] How are several lights weighted?** By luminance as authored: each light's spectrum is summed at its own
radiance, size ignored. Weighting by power needs areas that infinite planes and the sky don't have.

**[Decide] How much follows the light?** Chosen as a fixed half, then changed during the round to a Light Share
setting (default 0.5), so the trade-off can be filmed. A change restarts accumulation for clean comparisons, though
samples drawn under two pdfs, each divided by its own, would still average to the same image.

**What stratification does.** (asked during the round) Eight random $u$ clumped six of themselves between 0.45 and
0.59; eight golden ratio steps landed every 0.09 or 0.146. Out of 8 samples, 2.3% of pixels get no blue at all with
random draws and 11% get one, while with the golden ratio 91% get exactly three and none fewer than two.

**[Learn] Why does stratifying remove color speckle?** No pixel can miss a band of the CDF. Right. It doesn't make
samples brighter, touch the bounce directions or change the pdf.

**[Learn] Why a random start per pixel?** Without it every pixel draws the same wavelength at each sample, so the
whole image shares one drifting tint, a pattern instead of noise. Right. The random start also keeps every single
$u$ uniform, so each sample stays unbiased.

**[Decide] Which sequence?** The golden ratio in integer form, progressive at any sample count. Van der Corput is
better only at powers of two, and strata sized to Target Samples break when accumulation continues past it.

**[Decide] How is the noise measured?** A reusable Noise Check tool, which cp-34's hero wavelengths will need too.

**[Decide] Split `SceneCompiler`?** (asked in review) It had reached 482 lines doing five jobs; one class per job
leaves it at 124. Partial class files would only have hidden the coupling.

**[Decide] Exact comparisons?** Unity's `==` on matrices and colors treats differences under $10^{-5}$ as equal, so a
slow Timeline animation could skip rebuilds; emitters already compared exactly, and now everything does.

**[Decide] How should Noise Check report?** The first version divided RMSE by the image's mean and read about 900%,
since the lamp's pixels dominate squared error. Relative MSE, $\frac{(x - r)^2}{r^2 + 0.01}$ per pixel, weighs every
pixel by its own brightness.

## The math

Each sample estimates $\int L\,\bar{\mathbf x}\,d\lambda$ as $L(\lambda)\,\bar{\mathbf x}(\lambda)/p(\lambda)$. The
weights per 1 nm bin $i$ are trapezoid averages; with $s = \bar x + \bar y + \bar z$, the eye's are
$v_i = \frac{1}{2}(s_i + s_{i+1})$ and the light's $\ell_i = \frac{1}{2}(L_i s_i + L_{i+1} s_{i+1})$. The mix with
share $\alpha$ and the floor:

$$w_i = 0.99\left[(1 - \alpha)\frac{v_i}{\sum v} + \alpha\frac{\ell_i}{\sum \ell}\right] + \frac{0.01}{470}, \qquad F_0 = 0,\ F_{i+1} = \frac{\sum_{j \le i} w_j}{\sum_j w_j}.$$

A draw finds the bin with $F_i \le u < F_{i+1}$, returns $\lambda = 360 + i + (u - F_i)/(F_{i+1} - F_i)$ and the pdf
$p = F_{i+1} - F_i$ per nm. The stratified $u$ for sample $n$ is
$\big(h + n \lfloor 2^{32}/\varphi \rfloor\big) \bmod 2^{32}$, divided by $2^{32}$, with $h$ a hash of the pixel.

## Verification

| Check | Result |
|---|---|
| Spectrum Check | 30 of 30 PASS; flat Y through the eye's pdf with evenly spaced $u$ exactly 1.00000 |
| Tables read back from the GPU, flat X, Y, Z | (1.00008, 1.00000, 1.00033), the areas of $\bar x$ and $\bar z$ over $\bar y$, for every share |
| RGB beauty, normals, 8 samples, every round | byte-identical |
| Stratification off | spectral, split and measured box byte-identical to round 2 |
| `SceneCompiler` split | RGB, spectral, split and measured box byte-identical before and after |
| Change detection after the split | unchanged scene keeps accumulating, a temperature change and a $10^{-6}$ move restart |
| Spectral furnaces, stratified, mean | off by 0.00024 and 0.00056; per-pixel range 0.81 to 1.25 against -1.13 to 3.20 before |
| Image means, uniform against the new pdf, 64 samples | XYZ within 0.6% |
| Draws in 400 to 490 nm, share 0 and 1 | 1800 K lamp 37.4% and 4.2%; measured box 37.4% and 15.4% |

Noise against a 2048-sample reference, 960 by 540, 16 bounces, at 64 samples (RMSE, then relative MSE):

| pdf | Cornell, independent | Cornell, stratified | 1800 K, independent | 1800 K, stratified |
|---|---|---|---|---|
| Uniform | 0.310, 1.92 | 0.209, 1.88 | 0.433, 1.97 | 0.304, 1.91 |
| Eye only | 0.224, 1.09 | 0.156, 0.93 | 0.372, 1.50 | 0.274, 1.23 |
| Light Share 0.5 | 0.209, 0.98 | 0.146, 0.83 | 0.259, 0.89 | 0.198, 0.73 |
| Light Share 1 | 0.198, 0.91 | 0.139, 0.80 | 0.211, 0.72 | 0.170, 0.65 |

RMSE is ruled by bright pixels, where the remaining noise is color noise; the defaults cut its variance 4.5 times in
the Cornell box and 4.8 times at 1800 K. Relative MSE weighs dark pixels equally, and there the noise is from bounce
directions, which no wavelength choice touches: stratification barely moves it. That is light sampling's job (cp-37).

## Problems hit and fixes

- The search in `Sample` took the bin after the one holding $u$; fixed as described above.
- 21 tail bins had zero width in a float CDF; the uniform floor fixed it.
- The first Spectrum Check line used random $u$ and read 0.990 with its seed, three standard errors off by chance; a
  million draws gave 1.000. Evenly spaced $u$ makes the check exact, with a $10^{-3}$ tolerance.
- An 8-bit capture's total brightness changed 31% with the new pdf: sRGB encoding of a nearly black, noisy image.
  Linear float means agree within 0.6%.
- The sweep scene was first measured at 6504 K, since `TemperatureSweep` only wrote in `LateUpdate`; `OnValidate`
  now applies it.
- Noise Check's first percentage was dominated by the lamp; relative MSE replaced it.

## Shots to capture

- [ ] The 1800 K box at 16 samples in split screen: uniform against Light Share 1, stratified.
- [ ] Light Share dragged from 0 to 1 at a low sample count, the noise moving from red to blue.
- [ ] The spectral furnace at 8 samples with Stratify Wavelengths toggled: speckle to nearly flat.

## Explainers to build (Manim)

- Eight random dots clumping on a line against eight golden ratio dots spreading evenly, then mapped through the CDF
  to wavelengths.
- A uniform pdf reshaped into the eye's curve and then pulled toward a red lamp, with blue samples thinning out.
- A CDF as a staircase, $u$ dropping onto it and sliding down to a wavelength.
