# cp-23-white-furnace

Goal. A check with an answer known from the math. Path tracer bugs are quiet: a missing factor or a pdf that doesn't
match its sampler makes the image a little too dark or too bright, and the Cornell box still looks like a Cornell box.
In a white furnace a correct renderer reads an exact number, so every later chapter (spectral, NEE, MIS, BDPT, glass)
can be checked against it.

Scenes `Assets/SpectralPathTracer/Scenes/Furnaces/Furnace.unity` and `FurnacePlanes.unity`. The Cornell box scene is
now `Scenes/CornellBox.unity` (it was `Canvas.unity`), and from here every demo gets its own scene. The check is
Tools > Spectral Path Tracer > Furnace Check, described in `Assets/SpectralPathTracer/Tools/README.md`.

## What changed

- `AccumulationCapture` records a one-shot readback of the accumulation sums when a tool asks and divides each pixel by
  its own sample count, so the reading is radiance as floats with no exposure, clamp or 8-bit rounding in the way.
- `FurnaceCheck` requests it, repaints the Game view so a converged image still renders one more frame, and compares the
  mean, min and max with the scene's `FurnaceExpectation` (expected value, tolerance, every pixel or only the mean). A
  FAIL is logged as an error, so the console's error count shows it.
- `Furnace.unity` holds an albedo-1 sphere in a sky of 1 in every direction. `FurnacePlanes.unity` holds two facing
  planes with emission 1 and linear albedo 0.5 under a black sky, the cp-18 furnace made permanent.

## Decisions

**[Learn] Why does an albedo-1 Lambert sphere vanish in a uniform sky of 1?** Every bounce's weight
$f \cos\theta / p$ is exactly 1, and a ray leaving a convex sphere can't hit it again, so it reads the sky's 1. The
answer given was that it acts like a mirror. A Lambert surface averages the whole hemisphere rather than reflecting
one direction; a mirror would also vanish here, which is the point, since any material that loses no energy vanishes.
Infinite bounces don't happen on a convex object, and in uniform light there is no shading to average out.

**[Learn] Which bug would the albedo-1 furnace not catch?** Wrong sample directions with the weight still at the
albedo, because every direction sees radiance 1. The warp statistics tests from cp-13 catch distribution bugs. The
answer given was a missing $1/\pi$, which is the loudest bug the furnace shows (the sphere reads $\pi$). Our
`SampleBsdf` already writes the simplified weight, so the $1/\pi$ only lives in `EvalBsdf`, first used by NEE at cp-37.
Dividing by $w + 1$ reads 0.5 after one sample, and paths trapped inside the sphere read dark.

**[Decide] How is the furnace measured?** A float readback of the sums. Rejected were an 8-bit capture at exposure 0.5
(about 0.5% per level, and anything above 2 clamps) and looking at it (fine for the video, but no number).

**[Decide] Where does the furnace live?** Its own scene. The Cornell box keeps its scene, the furnaces are fixtures to
open and check, and later variants are new scenes beside them.

**[Learn] Why did a picker albedo of 0.5 read 0.21404?** The picker shows sRGB values, and `SceneCompiler` converts them
to linear before the GPU sees them. Right. A linear albedo of 0.5 is 0.735357 in the picker.

**[Learn] Why does the two-plane furnace keep its mean of 1.99998 when roulette ends most paths early?** Each survivor
is divided by its survival probability, so its expected contribution is unchanged. Right.

**[Decide] How does the check decide pass or fail?** A `FurnaceExpectation` in each furnace scene, so the check stays
generic and a new variant is just a new scene. Rejected were numbers only (judged by eye) and a table of expected
values keyed by scene name inside the check.

**[Decide] Where does the two-plane furnace go?** Its own scene, `FurnacePlanes.unity`. Rejected were a disabled
object inside the sphere's scene and skipping it because cp-18 and cp-19 had checked it once by hand.

## The math

With $L_i = 1$ in every direction, the rendering equation for a non-emitting surface collapses to its directional
albedo:

$$L_o = \int_{H^2} f(\omega_o, \omega_i) \cos\theta_i \, d\omega_i = \rho \quad\text{for Lambert, } f = \rho / \pi.$$

One sample's estimate is $\frac{f \cos\theta}{p} \cdot 1 = \frac{(\rho/\pi)\cos\theta}{\cos\theta/\pi} = \rho$, the
same for every sample, so the albedo-1 furnace has zero variance and must read exactly 1 after a single sample.

Between two facing planes with emission 1 and albedo 0.5, every bounce hits a plane, so a pixel reads
$\sum_{k=0}^{16} 0.5^k = 2 - 2^{-16} = 1.99998$. Roulette keeps the mean because a survivor of probability $p$ is
divided by $p$: $p \cdot \frac{x}{p} + (1 - p) \cdot 0 = x$. One sample spreads about 0.37 around it, so the mean over
518,400 pixels has a standard error of about $0.37 / \sqrt{518400} \approx 5 \times 10^{-4}$ at one sample per pixel,
and the tolerance $2 \times 10^{-3}$ is four of those.

Picker values are sRGB: $c_{\text{linear}} = \left(\frac{c + 0.055}{1.055}\right)^{2.4}$, so 0.5 becomes 0.21404.

## Verification

| Scene | Reading | Samples |
|---|---|---|
| Furnace | PASS, mean, min and max all 1.00000 | 41 |
| Furnace, picker albedo 0.5 for a moment | min 0.21404, mean 0.82 (the sphere is there, at its linear albedo) | 27 |
| FurnacePlanes | PASS, mean 1.99994 against 1.99998, single pixels 1.5 to 3.075 | 40 |
| FurnacePlanes, Expected set to 1.5 in memory | FAIL, off by 0.50009, console error count 1 | 66 |

## Problems hit and fixes

- An empty sky reads 1 too, so the first PASS proved nothing about the sphere. Dropping its albedo showed it.
- That reading was 0.214, not 0.5, until the sRGB picker was accounted for.
- A converged image stops repainting in Edit Mode, so a capture request would wait forever. The check repaints the
  Game view itself, and the Game view has to be open.

## Shots to capture

- [ ] The albedo-1 sphere fading into the sky as the albedo slider goes from 0 to 1 (movie mode).
- [ ] The check's PASS line, then a deliberate bug (the $1/\pi$ dropped) and the sphere glowing at 3.14.
- [ ] The two-plane furnace converging to 1.99998 with roulette on.

## Explainers to build (Manim)

- A hemisphere of sky rays all returning 1, with the weight $f\cos/p$ collapsing to $\rho$.
- The two-plane series $1 + 0.5 + 0.25 + \dots$ stacking up bounce by bounce, with roulette's boosted survivors
  keeping the same height on average.
