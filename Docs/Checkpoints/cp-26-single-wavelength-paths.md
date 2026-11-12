# cp-26-single-wavelength-paths

Goal. Spectral mode stops being a stub. Every path carries a real wavelength, every material, light and the sky is a
spectrum at that wavelength, and the image is built in CIE XYZ. Turning RGB colors into spectra needed the Jakob-Hanika
fit, planned for cp-32 and cp-33 and pulled forward here, and a fifth round added measured spectra, starting with the
real Cornell box.

Scenes `Assets/SpectralPathTracer/Scenes/CornellBox.unity` (fitted spectra) and `CornellBoxMeasured.unity` (the Cornell
group's measured walls and light). Spectral is the switch under Pixel Jitter in `Settings/PathTracerSettings.asset`.
`Runtime/Spectra/README.md` diagrams the whole color pipeline.

## What changed

1. The sigmoid model and its fit. `SigmoidSpectrum` evaluates $s(\lambda) = S(c_0\lambda^2 + c_1\lambda + c_2)$, and
   `SigmoidFit` finds the coefficients for a color by Levenberg-Marquardt on the CIELAB error under D65, with the normal
   equations, the Jacobian and the Lab derivative written as matrices.
2. The table. Build Sigmoid Table fits 786,432 grid colors in parallel and writes `Data/SigmoidTable.bytes` (9.4 MB).
   `SigmoidTable` reads it and finds any color's coefficients by trilinear interpolation.
3. Spectra on the GPU. The scene compiler converts every material and sky color to coefficients once per change (a
   reflectance directly, a light as rgb / 2 max with the scale kept), `GpuMaterial` and the frame constants carry them,
   `CieTables.hlsl` gains D65 and its scale, and `EvaluateSigmoid` works on all four lanes at once.
4. Single-wavelength paths. Generate draws one wavelength into lane 0 (slot 3 at depth 0), Resolve turns each sample into
   XYZ, Display converts the averaged XYZ to sRGB, AOVs skip the conversion, and the furnace check judges spectral mode
   on the mean.
5. Measured spectra. `MeasuredSpectrum` holds a spectrum as an `AnimationCurve` over nm, imported from CSV or drawn by
   hand. A material can take a measured reflectance or emission, which replaces its picker color in both modes. The
   compiler resamples them onto a 5 nm grid in one buffer, and `MaterialSpectra.hlsl` prefers a table over the sigmoid.

## Decisions

### Round 1: the plan

**[Learn] Why is the spectral white furnace noisy when the RGB one reads exactly 1?** Each sample sees one wavelength,
so it contributes one hue weighted by $1/p$, and only the average over hues is white. Right. That is color noise, the
main cost of spectral rendering; cp-30 and cp-34 shrink it. Precision loss, a broken furnace and roulette were the other
options: the conversion is exact in float to about $10^{-6}$, the furnace's math still holds on average, and an albedo of
1 keeps roulette from ever firing.

**[Learn] Why does a light's white become D65 but a wall's white a flat 1?** Albedo is the fraction of light kept at
each wavelength, so white keeps all of it, while a light's color is what you see, and seeing white means D65. Right. A
D65-shaped wall would multiply the light by D65 again at every bounce, turning a white room blue after 16 bounces.

**[Decide] What turns an RGB color into a spectrum?** Jakob-Hanika, pulled forward. It round-trips every sRGB color, stays
in $[0, 1]$, is smooth and costs one square root per wavelength. Smits 1999 would have been quicker and good to a few
percent; three box bands visibly shift colors. The cost was cp-26 growing to five rounds.

**[Decide] What does the accumulation buffer hold in spectral mode?** XYZ, which is never negative, converted to sRGB
once per pixel in Display. Converting each sample to sRGB would sum negative values and fix the gamut mapping in the
wrong place.

### Round 2: the table

**[Learn] Why index by the largest channel and two ratios rather than plain RGB?** It separates brightness from hue:
colors with the same ratios differ only in $z$, along which coefficients change smoothly, and the ratios always lie in
$[0, 1]$. Three sub-tables cover the cube. The answer given was that the fit needs it, but the fit works at any scale
(the Cornell red, largest channel 0.58, fits to delta E 0.00000). A plain grid would squeeze every dark color into the
cells near black and blend dark reds with dark blues.

**[Learn] Why warm-start each fit from its neighbor?** Saturated colors need large coefficients that 15 iterations from
zero may not reach, and neighbors that start close end close, so interpolating between cells stays valid. Right.

**[Decide] What resolution?** 64 per axis, the reference and PBRT default, 9.4 MB. 32 would be 1.2 MB with more error in
saturated colors.

**[Decide] How is the table stored?** A binary `.bytes` file read as a `TextAsset`, one copy that the checks and the
compiler both read. Three `Texture3D` assets would be awkward for C# to read back, and a ScriptableObject array of 2.4
million floats as YAML would be slow and huge.

### Round 3: spectra on the GPU

**[Decide] Where does RGB become coefficients?** On the CPU at scene compile, since every color is fixed per scene; the
GPU only evaluates sigmoids, and the table stays on the CPU. A per-bounce GPU lookup would cost eight loads and a blend
at every hit, and only textures would need it. Raised during the round: textures can be converted offline into
coefficient textures later, filtering after evaluation because the sigmoid is nonlinear.

**[Learn] Why divide a light's color by twice its largest channel before the lookup?** So the fitted color's largest
channel is 0.5 and its spectrum never approaches the sigmoid's ceiling, where coefficients get extreme and the table's
worst errors live. Right.

**[Learn] Is blending the sky's two fitted spectra the same as fitting the blended color?** The same color, a different
spectrum: XYZ is linear in the spectrum, so the blend of spectra converts to exactly the blend of colors, but the
spectrum in between is a metamer of a fresh fit. Right.

### Round 4: real wavelengths

**[Decide] Where does the one wavelength sit?** Lane 0, with the mask switching the other three off, so Resolve's mask
division and the $\frac{1}{4}$ average cancel, and cp-34 only has to fill lanes 1 to 3. Copying it into all four lanes
would have worked now and changed again at cp-34.

**[Learn] How does a spectral Cornell box compare with the RGB one at the same sample count?** Nearly the same average
with color noise. Right. Darker was the trap: dim wavelengths count less but the $1/p$ weighting keeps the average.

**[Decide] How does the furnace judge spectral mode?** On the mean, with its own tolerance (0.01), since one wavelength
per sample makes every pixel noisy.

### Round 5: measured spectra

**[Decide] How is a measured spectrum authored?** A ScriptableObject whose data is an `AnimationCurve` over nm: the
inspector plots it, a CSV import fills it with linear keys, and keys can be dragged to draw a notch or a spike. Sample
arrays would have no plot; fields on the material couldn't be shared.

**[Learn] What should a measured reflectance be outside its data?** Hold the edge values: a surface keeps doing what it
did at the last measurement. The answer given was a sigmoid fit, whose quadratic tails follow the shape of the whole
curve: the measured green rises from 0.114 at 640 nm to 0.159 at 700 nm, but its hump would send a fit toward 0, and
the fit would also smooth away the real bumps. Zero would darken and tint, and an extended slope can leave $[0, 1]$.
Under D65 only 0.18%, 0.04% and 0.29% of X, Y and Z lie outside 400 to 700 nm, so for walls it barely matters.

**[Decide] How does the GPU store measured spectra?** Resampled to one value every 5 nm from 360 to 830 nm, 95 floats
each, in one buffer, so every lookup costs the same. Raw tables would need a search whose length varies per spectrum.
5 nm flattens features narrower than about 10 nm, which is why sodium lines (cp-31) will be analytic.

**[Decide] What does RGB mode show for a measured material?** The spectrum's own color, so both modes show the same
material and differences are pure light transport. The picker color would have been ΔE 16 off for the green wall.

### Asked along the way

**Do we limit ourselves to visible light?** Yes, 360 to 830 nm, where the matching functions are nonzero. Everything
this renderer simulates keeps a photon's wavelength, so invisible light can never become visible here. Fluorescence,
phosphorescence and incandescence would; a cheaper variant is swapping the observer for a camera's or a bee's curves.

**Why will four wavelengths fix the color noise, and why four?** Four lanes a quarter-turn apart in $\lambda$ put four
spread hues in every sample. In the white furnace's sky the per-sample spread falls from (3.29, 2.24, 2.64) to (0.78,
0.42, 0.82), 10 to 29 times less variance, more than four independent samples would give because the spacing covers
every lobe of the matching functions. The expensive parts of a path are shared, the path state is already `float4`
wide, eight lanes would roughly double the memory traffic for less extra gain, and Wilkie et al. and PBRT use four.
Wavelengths are picked at the camera, not from the light; the light only weights them, and cp-30 will pick them more
often where lights and the eye make them count.

**How do CIE XYZ, CIELAB, sRGB and D65 relate?** See `Runtime/Spectra/README.md`: XYZ is the linear hub every spectrum
converts through, sRGB a real screen defined by three primaries and the D65 white, CIELAB a perceptual ruler built from
XYZ with cube roots, and D65 both the light called white and the white point.

## The math

The model and its derivative:

$$s(\lambda) = S(x), \quad x = c_0\lambda^2 + c_1\lambda + c_2, \quad S(x) = \frac{1}{2} + \frac{x}{2\sqrt{1+x^2}}, \quad
S'(x) = \frac{1}{2(1+x^2)^{3/2}}$$

The fit minimizes $\|r\|^2$, $r(c) = \mathrm{Lab}(\mathrm{XYZ}(c)) - \mathrm{Lab}(\text{target})$, with
$\mathrm{XYZ}(c) = \sum_\lambda s(\lambda)\, D65(\lambda)\, \bar{\mathbf x}(\lambda) / \sum_\lambda D65\,\bar y$. Each step
solves $(J^\top J + \mu I)\,\delta = J^\top r$ and takes $c \leftarrow c - \delta$, halving $\mu$ after a step that lowers
the cost and multiplying it by 10 after one that doesn't. By the chain rule
$J = \frac{\partial\,\mathrm{Lab}}{\partial\,\mathrm{XYZ}} \cdot \frac{\partial\,\mathrm{XYZ}}{\partial c}$ with
$\frac{\partial s}{\partial c} = S'(x)\,(\hat\lambda^2, \hat\lambda, 1)$. The fit runs on
$\hat\lambda = (\lambda - 360)/470$, then converts:

$$c_0' = \frac{c_0}{470^2}, \quad c_1' = \frac{c_1}{470} - \frac{2 \cdot 360\, c_0}{470^2}, \quad
c_2' = c_2 - \frac{360\, c_1}{470} + c_0\left(\frac{360}{470}\right)^2$$

The table holds $3 \times 64^3$ fits at $(\ell, z, x/z, y/z)$ with $z$ on
$\text{smoothstep}(\text{smoothstep}(k/63))$. A light uses the coefficients of $\text{rgb}/(2\max)$ times
$2\max \cdot D65(\lambda) \cdot \texttt{CIE\_D65\_SCALE}$, where
$\texttt{CIE\_D65\_SCALE} = \int\bar y / \int D65\,\bar y = 0.0101122474$.

A sample's XYZ, with one live lane and the mask division:

$$\mathrm{XYZ} = \frac{1}{4}\sum_{i} \frac{L(\lambda_i)\,\bar{\mathbf x}(\lambda_i)}{p(\lambda_i)} \cdot \frac{1}{\int\bar y}
\cdot \frac{1}{\tfrac{1}{4}\sum_i m_i}, \qquad p = \frac{1}{470}$$

A measured light is normalized by $\int \bar y / \int s\,\bar y$ and multiplied by its strength, so it has $Y$ equal to
its strength like an RGB light. The measured Cornell light's strength, 12.343, matches the original light's luminance.

## Verification

| Check | Result |
|---|---|
| Sigmoid fits of gray, the Cornell walls and pure R, G, B | delta E at most 0.00006; rebuilt from nm coefficients within 0.000005 |
| Table, 1,000 random colors | mean delta E 0.026, worst 0.991 (9 above 0.1, all saturated reds at the gamut edge) |
| Table build | 786,432 fits in 63.1 s |
| White furnace, spectral, 64 samples | mean (1.00005, 0.99998, 0.99988); single pixels -0.80 to 3.16 |
| Planes furnace, spectral, 64 samples | (2.00091, 2.00022, 1.99947) against 1.99998 |
| RGB beauty, normals, AOVs 2 to 5, 8 samples | byte-identical after every round |
| EditMode tests | 48 of 48 |

The Cornell box at 256 samples, whole image: RGB (0.1101, 0.0707, 0.0211), spectral (0.1096, 0.0706, 0.0205). The red
wall's red reads 0.164 in RGB and 0.148 in spectral, the green wall's red 0.042 and 0.051: the color of a product of
spectra isn't the product of colors, and the light is warm. In the measured box the green wall's red goes from 0.026 to
0.045, because the real green paint rises again past 640 nm where the lamp is brightest.

The hand-picked Cornell colors against the measured walls: white delta E 2.9, red 8.5, green 16.4.

## Problems hit and fixes

- My first claim that pure sRGB red was beyond a smooth reflectance was wrong, and a check line reused one variable as
  both target and start, which hid it. Sigmoid spectra reach every sRGB color.
- Edit > Rendering > Generate Shader Includes rewrites URP's and Core's generated files too, and their shaders log
  stale errors while it runs; after a reimport they compile clean.
- An eval that returned the table `TextAsset` printed 9.4 MB of binary; evals now return names or counts.
- The RGB capture scripts never pinned Spectral, so a session with Spectral on compared a spectral render to an RGB
  baseline. They now pin it off.
- Splitting `MaterialSpectra.hlsl` out of `SigmoidSpectrum.hlsl` first cut at an `#endif` inside a function; both files
  were rewritten whole, and a comment changed after round 3 was restored.
- A reimport before Unity noticed an edited include failed with an asset version error; refreshing first fixed it.

## Shots to capture

- [ ] The Cornell box in RGB and spectral at the same sample count: nearly the same picture, one covered in colored
  speckle.
- [ ] The measured box's green wall gaining red in spectral mode, with the measured green curve rising past 640 nm.
- [ ] A `MeasuredSpectrum` curve in the inspector, dragged into a notch, and the wall changing color.
- [ ] Build Sigmoid Table running, then the Spectrum Check's lines.

## Explainers to build (Manim)

- A quadratic bending into a sigmoid: an edge for red, a hump for green, a dip for magenta, yellow as one minus blue.
- Levenberg-Marquardt on a 2D slice of the cost: damped steps far away, Gauss-Newton steps near the answer.
- The worked lookup: the Cornell red in its table cell, eight corners blending into one spectrum.
- One wavelength against four rotated ones: the per-sample color scattering, then tightening.
