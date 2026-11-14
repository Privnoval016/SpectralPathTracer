# cp-31-line-spectra

Goal. Lamps that emit only a few sharp wavelengths. A sodium street lamp puts nearly all its light into two lines
0.6 nm apart, a mercury lamp into a handful, a laser into one. Under sodium a spectral renderer shows what RGB can't:
every surface reflects the same 589 nm light, so the room loses its hues and keeps only brightness. Sampling such
narrow light well also pushed the wavelength table from 1 nm bins to 0.02 nm.

Scene `SodiumBox.unity`, the Cornell box with a sodium `Emitter` on its lamp. Film it at Light Share 1 in split
screen. A lamp of lines is an `Emitter` of the Lines kind pointing to a `LineSpectrum` asset; Sodium, Mercury and a
532 nm laser are in `Data/Spectra/Lines/`.

## What changed

1. `LineSpectrum` (asset) holds lines as center, full width at half maximum and relative strength. An `Emitter` of
   the Lines kind turns them into Gaussians at its luminance, uploaded to a `_Lines` buffer; `GpuMaterial` gained
   `LineStart` and `LineCount`, and Shade sums the lamp's Gaussians at the path's wavelength (`LinesAt`). RGB mode
   uses the lines' integrated color.
2. `ResolvedEmission` replaced the `(Vector3, float[])` tuple now that an emission can also carry lines.
3. The wavelength table has 23,500 bins of 0.02 nm. Each line's area is integrated into its bins exactly with `erf`;
   smooth lights are still read at whole nanometres and interpolated. The count lives in a generated
   `WavelengthBins` enum that C# and the shader both read.
4. Limits that were typed twice are now defined once: the bounce limit (which also sizes the live path stats), the
   blackbody temperature range, the 6 sigma reach of a line, and Locked Wavelength's range.

## Decisions

**[Learn] What goes wrong if sodium goes into the 5 nm table?** It smears the lines: interpolating between 585 and
590 nm turns a 0.3 nm line into a triangle about 10 nm wide, with light at wavelengths sodium doesn't emit. Right.
Normalizing fixes the total, not the shape, and the shape is what a locked view or a prism will show.

**[Learn] Under sodium in split screen, which half loses its hue?** The spectral half: every wall reflects only
589 nm light, so all of them share its color, an R to G share of 0.83 to 0.17, and differ only in brightness. The
answer given was the RGB half, but RGB mode multiplies the lamp's three numbers by each wall's, and sodium's color
(2.77, 0.59, -0.13) still has green: the green wall comes out 0.45 to 0.55 and the red wall 0.98 to 0.02. Measured in
the render: RGB walls 0.97, 0.65 and 0.93 red, spectral walls 0.83, 0.82 and 0.83. The green wall is even brighter
than the red one in spectral mode, since its paint reflects more at 589 nm.

**[Decide] How are lines represented?** Gaussians on the GPU (center, sigma, peak), exact at any width. A 0.1 nm
table would need 4,701 values per lamp and still be a staircase; true zero-width lines would need a pdf mixing
probability masses with densities in every estimator.

**[Decide] Where are lines authored?** A `LineSpectrum` asset, like `MeasuredSpectrum`, shared by every lamp that
uses it. Inline lists would repeat the numbers; built-in presets couldn't hold a custom lamp.

**Why the sodium room was speckled, and the fixes.** (asked to discuss) Per-sample scatter of the lamp's brightness:

| Setup | Sodium | Laser at 532.5 nm |
|---|---|---|
| Share 0.5, 1 nm bins read at whole nm | 1.99 | 5.07 |
| Share 1 | 1.23 | 3.52 |
| Share 1, lines integrated into 1 nm bins | 1.28 | 2.39 |
| Share 1, 0.02 nm bins, integrated | 0.11 | 0.17 |
| 90% of draws taken from the lines themselves | 0.18 | 0.28 |

Three problems: half the draws followed the eye and saw no sodium, the table could miss a line sitting between whole
nanometres, and draws within a 1 nm bin landed beside a 0.3 nm line.

**[Learn] Why does Light Share 1 cut the scatter from 1.99 to 1.23?** Half the draws were wasted: at 0.5, 49% land
where sodium emits nothing and return 0, so the hits must count double, and
$\mathrm{CV}^2 = \frac{1 + \mathrm{CV}_{\text{hit}}^2}{q} - 1$ with $q = 0.506$ gives exactly 1.99. The answer given
was that the lines get brighter, but the share only moves the draws; the lamp's radiance never changes, and scaling
it wouldn't change relative scatter anyway.

**[Learn] Why does integrating help the laser but not sodium?** Where the centers sit: the laser at 532.5 nm is 11.8
sigmas from both 532 and 533, so a table read at whole nanometres didn't see it, while sodium's main line sits 0.04
sigmas from 589 and was caught by luck. The answer given was that the laser is brighter, but both were at $Y = 1$,
and brightness cancels out of relative scatter.

**[Decide] How do draws stop missing a line?** A 0.02 nm grid with exact integration: one mechanism, cp-30's table,
with 6 more search steps and 94 KB. Drawing from the lines directly works at any width but needs a second sampling
path and a pdf summing every line in the scene; bins hugging the lines need a bin edge buffer. Lines narrower than
0.05 nm would speckle again, so that is the new minimum width.

**[Decide] What does cp-31 film?** `SodiumBox` in split screen. A scene per lamp waits for shots of its own, and a
lamp switcher is cp-35's.

**Why is the bin count typed into the shader?** (asked in review) The shader can't see C# constants, so it was
copied by hand, and a change on one side would have silently broken sampling. A generated enum, like
`SpectrumTables`, now gives both sides one source.

**Which other values were copied?** (asked in review) The bounce limit and the live path stats buffer it sizes, the
blackbody temperature range, the 6 sigma reach, and Locked Wavelength's range; all four are now defined once. The lamp
strength copied into two scenes is data, the spectrum strip's range exists in one place, and thread group sizes are
read from the shaders, so those stayed.

## The math

A line of strength $s_i$ among lines of total $S$, full width at half maximum $w$, has

$$\sigma = \frac{w}{2\sqrt{2\ln 2}} = 0.4247\,w, \qquad \text{peak} = \frac{s_i}{S}\cdot\frac{1}{\sigma\sqrt{2\pi}}\cdot k,$$

so the lines' total area is $k$, and $k$ is chosen so the lamp has $Y = $ Strength. A line's share of bin $[a, b]$ is

$$A\left[\Phi\!\left(\frac{b - c}{\sigma}\right) - \Phi\!\left(\frac{a - c}{\sigma}\right)\right], \qquad \Phi(x) = \tfrac{1}{2}\left(1 + \operatorname{erf}(x/\sqrt 2)\right),$$

weighted by $\bar x + \bar y + \bar z$ at the bin's middle. A draw in bin $i$ returns
$\lambda = 360 + (i + f)\cdot 0.02$ and $p = (F_{i+1} - F_i)/0.02$ per nm.

## Verification

| Check | Result |
|---|---|
| Spectrum Check | 35 of 35 PASS; sodium's color (2.771, 0.587, -0.128) against (2.772, 0.587, -0.128) from its line centers; line area exactly 1; sodium's $Y$ through its own table 1.00033 |
| RGB, every step | byte-identical to cp-30 |
| Scenes without lines, round 1 | spectral, split and measured box byte-identical |
| 0.02 nm grid | spectral images shift slightly, totals within 0.002% |
| Generated enum, tied constants | all four captures byte-identical; live path counts read back, 17 depths |
| Spectral furnaces | off by 0.00024 and 0.00057 |
| Sodium lamp, $Y$ (Strength 12.343) | RGB 12.317, spectral 12.322 |
| `SodiumBox`, 64 samples, relMSE (RMSE) | share 0.5: 1.63 (0.271) before, 0.61 (0.172) after; share 1: 0.66 (0.190) before, 0.35 (0.123) after |
| Cornell box, 1800 K box, share 0.5 | relMSE 0.831 and 0.726, the same as on the 1 nm grid |

## Problems hit and fixes

- `line` is a reserved word in HLSL (a geometry shader primitive), so the loop variable is `spectralLine`.
- A verification run hung: the editor reported 32 samples against a target of 16 and the wait loop wanted exactly
  16. It didn't reproduce; the scripts now restart after every settings change and wait for at least the target.
- One RGB comparison ran with `SodiumBox` open instead of `CornellBox` and looked different; rerun on `CornellBox`,
  it was byte-identical.

## Shots to capture

- [ ] `SodiumBox` in split screen at Light Share 1, the divider wiping the walls' colors away.
- [ ] The same room at 16 samples on the old grid against the new one.
- [ ] Locked Wavelength dragged across 589 nm in `SodiumBox`: the room appears only within a fraction of a nanometre.

## Explainers to build (Manim)

- A Gaussian line sliding across whole-nanometre ticks, visible to the table only when it lands on one; then the
  area between bin edges filling in wherever it sits.
- 1 nm bins against 0.02 nm bins over a 0.3 nm line, with draws falling beside it and then on it.
- White, red and green walls under sodium: three reflectance curves cut at one wavelength, three heights, one hue.
