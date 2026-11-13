# cp-29-illuminants

Goal. Lights described the way physics describes them. Most real light is thermal: a candle, a tungsten bulb, the
sun, hot metal. Planck's law gives each one's spectrum from a single number, its temperature, so one slider takes a
lamp from candlelight to blue sky. The film is a Cornell box whose lamp sweeps from 1800 to 10,000 K while the room's
color follows it.

Scene `BlackbodySweep.unity`, a copy of the Cornell box. Its lamp has an `Emitter` and a `TemperatureSweep`, and the
Movie object holds the Timeline (`BlackbodySweep.playable`, 6 s) and a disabled `MovieMode`. Play Mode loops the sweep in
real time; enabling `MovieMode` films it to `Recordings/BlackbodySweep/`. Any traced shape can take an `Emitter`.

## What changed

1. `Blackbody` evaluates Planck's law in doubles, resamples it onto the 5 nm GPU grid scaled to a chosen luminance, and
   integrates its color at 1 nm for RGB mode.
2. `Emitter`, a component next to a `TracedShape`, replaces its material's emission with a blackbody, a measured
   spectrum or a color. Strength is luminance $Y$ for all three, so a new temperature or color never changes brightness.
3. The scene compiler gives each emitting shape its own material entry, and compares every emitter's `EmitterState`
   each frame, since Timeline writes fields without calling `OnValidate`. `EmitterState` is a readonly, equatable struct,
   so the comparison allocates nothing.
4. `TemperatureSweep` (Tools) maps a 0 to 1 progress, which the Timeline animates, to a temperature stepping evenly in
   mireds, and writes it to the emitter in `LateUpdate`.
5. Spectrum Check adds the 6504 K color, Wien's peak at 1800 K and the luminance of the GPU table at three temperatures.

## Decisions

**[Learn] An 1800 K blackbody peaks near 1600 nm, in the infrared. Why does it look orange rather than dark?** Its
visible tail rises to red: from 400 to 700 nm Planck's curve is still climbing toward its peak, so there is far more red
than blue (37.6 times more at 650 nm than at 450), and enough light to see. Right. The eye never sees the infrared peak,
the display adds no tint, and the color is the light's own, not reflected.

**[Learn] D65 is "daylight at about 6504 K", yet it isn't a 6504 K blackbody. Why?** It is measured daylight: sunlight
through the atmosphere plus scattered sky light, with absorption dips, and 6504 K is only the temperature of the
blackbody whose color comes closest. The answer given was different units. D65 is indeed relative and Planck's law
absolute, but units only scale a spectrum and can't change its shape; rounding to 5 nm wouldn't change it either, and
D65 isn't defined by Planck's law at all.

**[Decide] How does brightness behave as the temperature changes?** Hold luminance fixed: the light is scaled so its
$Y$ equals Strength at every temperature, and the slider changes only color. Physical $T^4$ brightness would take the
sweep from nearly black to about 950 times brighter. Dividing by the curve's peak (pbrt's choice) still changes the
visible brightness, because the peak leaves the visible range at both ends.

**[Decide] Where does a light's temperature live?** On an emitter component on the light's shape. Timeline can only
animate components, and a component lets two lamps sharing one material glow at different temperatures. A blackbody
option on `SpectralMaterial` would have needed a separate override hook for the sweep.

**Why a struct for the emitter's state?** (asked in review) The first version compared a five-field tuple; a named,
equatable struct says what each value is, in the same style as `RenderParams`.

**[Learn] Why is a 6504 K blackbody at $Y = 1$ slightly pink, (1.043, 0.984, 1.036), and not sRGB's white?** D65 isn't
a blackbody. Right. Its chromaticity is (0.3127, 0.3290), the blackbody's (0.3135, 0.3236): D65 sits slightly above the
Planckian locus, toward green, so the blackbody has a little less green. The 5 nm table doesn't touch RGB mode's
color, 4 K moves $x$ by only about 0.0003, and the sums run in doubles. The odd 4 K is history: D65 was 6500 K until
the constant $c_2 = hc/k$ was revised from 1.4380 to 1.4388 cm K, which renamed the same light
$6500 \times 1.4388 / 1.4380 = 6504$ K.

**[Learn] With the lamp at 1800 K in split screen, where do the halves differ most?** On the lit walls. Right. The lamp
matches by construction, since RGB mode's lamp color is the spectrum's integral. After a bounce, RGB multiplies the
averages, $\boldsymbol\rho_{\text{rgb}} \odot \mathbf L_{\text{rgb}}$, where spectral mode averages the products,
$\int \rho\,L\,\bar{\mathbf x}\,d\lambda$, and the two differ wherever a wall's reflectance and the light rise
together. RGB mode also carries the light's negative blue, -0.019, and subtracts it at every bounce. Shadows compound
the difference but are dim, and they are not the only place it shows.

**[Decide] How does the sweep space its temperatures?** Evenly in mireds, $10^6/T$. Planck's exponent depends on $T$
only through $1/T$, so equal mired steps look like equal color changes; half the clip covers 1800 to 3050 K, where the
color moves fastest. Even kelvin would race through the warm colors in the first 15% and hold near-identical
blue-whites for the last half. Hand-placed keys follow no rule anyone could state.

**[Decide] What does the demo show?** The Cornell box with its lamp sweeping, so the whole room answers one moving
light. A row of fixed lamps would mix their colors on every wall, and a still chart loses the sweep.

## The math

Planck's law, with $x = hc/(\lambda k T) = 1.4388 \times 10^7\ \text{nm K}/(\lambda T)$:

$$B(\lambda, T) = \frac{2hc^2}{\lambda^5}\,\frac{1}{e^{x} - 1}$$

The first factor, the number of ways light can oscillate times each photon's energy, grows toward short wavelengths.
The second, the average number of photons per way, dies exponentially once a photon costs much more than $kT$. At
1800 K the first gives blue a lead of $(650/450)^5 = 6.3$ and the second takes back $e^{17.76 - 12.30} \approx 235$, so
red wins 37.6 to 1. The ratio is 0.74 at 6504 K and 0.46 at 10,000 K.

Wien's law puts the peak at $2.898 \times 10^6 / T$ nm: 1610 nm at 1800 K, 446 at 6504 K, 290 at 10,000 K.

Holding luminance fixed scales the curve by

$$s = \frac{\text{Strength}\cdot\int\bar y\,d\lambda}{\int B\,\bar y\,d\lambda}, \qquad \text{so} \qquad Y = \frac{\int s\,B\,\bar y\,d\lambda}{\int \bar y\,d\lambda} = \text{Strength}.$$

The color at $Y = 1$ is $M_{\mathrm{XYZ}\to\mathrm{sRGB}}\,(X/Y,\ 1,\ Z/Y)$:

| T | XYZ at Y = 1 | $(x, y)$ | linear sRGB |
|---|---|---|---|
| 1800 K | (1.346, 1, 0.104) | (0.549, 0.408) | (2.771, 0.576, -0.019) |
| 6504 K | (0.969, 1, 1.122) | (0.314, 0.324) | (1.043, 0.984, 1.036) |
| 10000 K | (0.973, 1, 1.493) | (0.281, 0.289) | (0.872, 0.995, 1.429) |

The sweep's temperature at progress $t$ is $T(t) = 10^6 / \big((1 - t)\,10^6/T_0 + t\,10^6/T_1\big)$, 555.6 to 100
mireds; $t = \frac{1}{2}$ gives 3051 K.

## Verification

| Check | Result |
|---|---|
| Spectrum Check | 29 of 29 PASS; 6504 K is (1.04289, 0.98363, 1.03585); Wien peak at 1800 K 1610 nm; table $Y$ 1.00033, 0.99999, 1.00003 at 1800, 6504, 10,000 K |
| Scenes without an emitter: RGB beauty, normals, 8 samples; spectral and split, 8 samples | byte-identical |
| Cornell lamp at Strength 12.343, RGB, mean $Y$ over its 6,872 pixels | 12.386, 12.383, 12.382 at 1800, 6504, 10,000 K |
| The same, spectral at 64 samples | 12.338, 12.366, 12.370; colors within about 0.01 of RGB |
| Timeline at 0, 3, 5.967 and 6 s | 1800, 3051, 9753, 10,000 K; 181 frames |
| Movie at 2 samples, frames 0, 90, 180 against renders made after the temperature settled | identical byte for byte |

## Problems hit and fixes

- The first spectral lamp measurement read $Y = 13.8$ and a greenish 6504 K. It picked the lamp's pixels by their own
  noisy $Y$, which keeps the pixels whose wavelengths happened to land near the peak of $\bar y$: brighter, and greener.
  Measuring the same pixels in both modes fixed it.
- The Timeline's automatic length came out 5.999999999999 s, so movie mode would have saved 180 frames ending at
  9753 K. It is now fixed at exactly 6 s.
- My test scripts left your settings asset at their pinned values (4 bounces, exposure -3), which made the first sweep
  test nearly black. Your saved values were restored, and the scripts now restore them in the same run.
- At 1800 K the lamp shows flat yellow: its red and green both clip at 1. The display clamp again, which cp-55 replaces.

## Shots to capture

- [ ] The full sweep, 1800 to 10,000 K, at a high sample count.
- [ ] The sweep in split screen, the walls drifting apart between the halves as the lamp cools.
- [ ] A 6504 K blackbody lamp beside a D65 lamp, the blackbody faintly pink.

## Explainers to build (Manim)

- Planck's curves for rising temperatures, their peaks sliding left through the visible window, the two factors drawn
  separately.
- The same sweep spaced in kelvin and in mireds, side by side.
- The chromaticity diagram with the Planckian locus and D65 just above it.
