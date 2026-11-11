# cp-25-cie-xyz

Goal. The color science that turns wavelengths into pixels. The CIE 1931 matching functions and the XYZ to sRGB matrix
go into C# and onto the GPU, a numeric check proves them against known values the way the furnace proves light
transport, and a spectrum strip shows every wavelength's color through the same code paths will use at cp-26.

Scene `Assets/SpectralPathTracer/Scenes/CornellBox.unity` with Show Spectrum Strip ticked under Show Gray Ramp in
`Settings/PathTracerSettings.asset` (exposure around -2 shows it unclipped). The check is Tools > Spectral Path Tracer >
Spectrum Check, and the GPU table is rebuilt with Tools > Spectral Path Tracer > Generate CIE Shader Table.

## What changed

1. `Runtime/Spectra/CieTables.cs` holds the CIE 1931 $2^\circ$ matching functions at 1 nm from 360 to 830 nm and D65 at
   5 nm from 300 to 830 nm, generated once from the CVRL table and pbrt-v4. `Cie.cs` interpolates them and converts XYZ
   to linear sRGB with a matrix written out row by row.
2. `SpectrumCheck` compares the $\bar y$ integral, flat light's chromaticity, D65's sRGB white and a Monte Carlo
   estimate against their known values.
3. `CieShaderTables` writes `Shaders/Include/Spectrum/CieTables.hlsl` from the C# tables, so there is one source.
   `Cie.hlsl` interpolates it and holds the same matrix as a `float3x3`.
4. Display's spectrum strip replaces the image with one wavelength per column, 380 to 780 nm at equal power, using the
   spare `_Padding0` word of `FrameConstants` for its switch.

## Decisions

**[Learn] What do X, Y and Z represent, and why are three enough?** How strongly the light drives three response
curves derived from color matching experiments; the eye has three kinds of cones, so two spectra with equal XYZ look
the same (metamers, cp-36). Right. $\bar y$ is the eye's brightness sensitivity, so $Y$ is luminance, and XYZ is lossy:
infinitely many spectra share one XYZ, which is why cp-33 fits a spectrum to a color instead of inverting anything.

**[Learn] Why divide by $p(\lambda)$?** So the estimate's average equals the integral:
$E[L\bar x/p] = \int L\bar x\,d\lambda$. The answer given was to normalize brightness, which is a different, second
division, by $\int \bar y\,d\lambda$. Without the $1/p$, a uniform choice over 470 nm would make every estimate 470 times
too small, and an uneven choice (cp-30) would also tint the image.

**[Learn] Why is flat light pink in sRGB?** sRGB's white is D65, which is bluer than equal energy, so flat light reads
warm. Right. It means the furnace's sky and every RGB-mode white light must be D65-shaped in spectral mode.

**[Decide] How does the GPU look up the matching functions?** The 1 nm table, interpolated. Rejected were an analytic
Gaussian fit (no data, about 1% off) and a 5 nm table (interpolation error in the sharp parts).

**[Decide] How does the GPU hold the table?** A `static const float3` array in a generated include. Nothing to create,
bind or declare per pass, and generating it from the C# table means the two can't drift. A structured buffer would
have needed binding in every stage that uses it.

**[Decide] How does the strip show colors outside sRGB?** By adding white until no channel is negative, which keeps
each hue and gives up saturation. Clipping negatives keeps brightness but shifts hues. Asked during the round: is the
lost saturation ever fixed? Not on an sRGB screen, which can't show a pure wavelength at all. Nothing is lost in the
sums, ordinary scenes land inside the gamut, and the showcase scenes (prisms, lasers, sodium, CDs) get proper gamut
compression with AgX at cp-55. A Display P3 output would show more, but P3 is inside the horseshoe too.

**[Learn] Why do the strip's ends fade when every column has the same power?** $\bar y$ is tiny there: 0.0004 at
400 nm and 0.0041 at 700 nm against 1 at 555 nm. The answer given was less energy, but the columns carry equal power
by construction, and a short-wavelength photon carries more energy, not less.

**[Decide] How bright is each column?** Equal power, with exposure applied: what flat light split by a prism looks
like, dim ends included. Rejected were equal brightness per column and showing both.

## The math

A spectral renderer computes radiance per nanometre, $L(\lambda)$. Color comes from three integrals:

$$X = \int \bar x(\lambda) L(\lambda)\, d\lambda,\qquad Y = \int \bar y(\lambda) L(\lambda)\, d\lambda,\qquad
Z = \int \bar z(\lambda) L(\lambda)\, d\lambda,$$

divided by $\int \bar y\, d\lambda = 106.857$ so a flat spectrum of 1 has $Y = 1$. Chromaticity divides out brightness:
$x = X/(X+Y+Z)$, $y = Y/(X+Y+Z)$.

A path traces four wavelengths drawn with pdfs $p(\lambda_i)$ (one at cp-26, four at cp-34), and Resolve will estimate

$$X \approx \frac{1}{4}\sum_{i=1}^{4} \frac{L(\lambda_i)\,\bar x(\lambda_i)}{p(\lambda_i)} \cdot \frac{1}{\int \bar y\, d\lambda}$$

and likewise for $Y$ and $Z$. $L(\lambda_i)$ is itself a path-sampled estimate, and the two random choices are
independent, so one sample estimates both integrals at once. Then

$$\begin{pmatrix} R \\ G \\ B \end{pmatrix} = \begin{pmatrix} 3.2404542 & -1.5371385 & -0.4985314 \\ -0.9692660 &
1.8760108 & 0.0415560 \\ 0.0556434 & -0.2040259 & 1.0572252 \end{pmatrix} \begin{pmatrix} X \\ Y \\ Z \end{pmatrix}.$$

Numbers from the tables:

| Quantity | Value |
|---|---|
| 1 nm sums of $\bar x$, $\bar y$, $\bar z$ | 106.865469, 106.856917, 106.892251 (pbrt uses 106.856895 for $\bar y$) |
| Flat light chromaticity | (0.33331, 0.33329) |
| D65 chromaticity | (0.31273, 0.32902) |
| D65 at $Y = 1$ in linear sRGB | (1.00000, 1.00000, 1.00000) |
| Flat light at $Y = 1$ in linear sRGB | (1.20488, 0.94824, 0.90920) |
| 450 nm, 555 nm, 600 nm in linear sRGB | (0.148, -0.181, 1.884), (0.119, 1.380, -0.169), (2.472, 0.154, -0.069) |

Uniform $\lambda$ over 360 to 830 nm, flat $L = 1$, estimate of $Y$ over its true value: 0.026 after 1 sample, 1.068
after 16, 1.003 after 1024.

The strip adds white with $\text{rgb} \leftarrow \text{rgb} - \min(0, \min(r, g, b))$: the same amount added to all
three channels moves a color straight toward white, so its hue holds.

Pure wavelengths are negative in sRGB, and a white wall's samples average back into the gamut only because their
negatives cancel. Sums must be accumulated signed; only Display clamps.

## Verification

- Spectrum Check: all seven lines PASS. $\bar y$ sums to 106.85680 (float sum), flat light is (0.33331, 0.33329), D65
  gives 1.00000 in every channel, and the Monte Carlo estimate reads 1.00040.
- The CVRL table and pbrt-v4's copy agree to $5 \times 10^{-13}$.
- Nine strip columns from 390 to 768 nm match the same conversion done in Python from the original CSV after sRGB
  encoding, all exactly except one near-black column off by one 8-bit level.
- With the strip off, the 1-sample beauty and normals captures are byte-identical to the baseline.

## Problems hit and fixes

- The CIE's own CSV links now return 404; the data came from CVRL and was cross-checked against pbrt-v4.
- The generator first wrote zero as `0f`, which HLSL rejects; literals without a decimal point or exponent get `.0`.
- Metal warned about a signed and unsigned comparison in the range check; the bound is cast to `float`.
- Reimporting a kernel before Unity noticed an edited include failed with an asset version error; refreshing first
  fixed it.
- Resetting a healthy Blitter left URP without its blit material and blank captures; reinitializing it fixed them.

## Shots to capture

- [ ] The spectrum strip at exposure -2, then raised until the green middle clips.
- [ ] The strip with add-white against clipped negatives, the cyan region changing hue.
- [ ] The Spectrum Check's seven PASS lines, then the Monte Carlo line failing with the $1/p$ removed.

## Explainers to build (Manim)

- The horseshoe: every pure wavelength's chromaticity, the sRGB triangle inside it, D65 and equal-energy white as two
  nearby dots.
- $\bar x$, $\bar y$, $\bar z$ drawn over the spectrum, and a flat spectrum multiplied by each into three areas.
- Random wavelengths falling under $\bar y$, each weighted by $1/p$, the running average settling on the area.
- Adding white: a negative channel lifted to zero, the point sliding toward white along a straight line.
