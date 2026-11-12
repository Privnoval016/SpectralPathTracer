# cp-28-wavelength-lock

Goal. See one wavelength at a time. A spectral path computes how much light at exactly one wavelength reaches the camera,
and the color image is the blend of countless such monochrome answers. Locking the wavelength shows a single answer:
the room as it looks at 450 nm, or 650. A contact sheet from 400 to 700 nm turns the cp-26 spectra into images, sets up
dispersion, sodium and thin films, and gives the furnace an exact per-pixel check in spectral mode.

Scenes `CornellBox.unity` and `CornellBoxMeasured.unity`. Lock Wavelength and Locked Wavelength sit under the split screen
settings, visible while a spectral image is drawn. For a contact sheet, add `ContactSheet` to any GameObject in Play
Mode; it writes `Recordings/ContactSheets/<scene>.png`.

## What changed

1. Lock Wavelength (or `PathTracerOverrides.LockedWavelength`) replaces the random draw in Generate with a fixed
   wavelength, carried in the spare `_LockedWavelength` word of `FrameConstants`. It is part of `RenderParams`, so a new
   wavelength restarts the sums, and it only touches the spectral image.
2. Resolve accumulates the locked radiance itself, and Display multiplies it by `WavelengthTint`, the wavelength's
   gamut-mapped color scaled so its brightest channel is 1.
3. The furnace check expects white light's radiance at the locked wavelength, $D65(\lambda_0)$ times `CIE_D65_SCALE`,
   per pixel. On its first run it showed that the fitted white reflectance is 0.99986, so exact white and black now snap
   to flat spectra.
4. `PathTracerOverrides.Spectral` lets a tool force one mode, which also turns split screen off.
5. `ContactSheet` renders 16 locked tiles from 400 to 700 nm in Play Mode, box-filters each capture into its cell, stamps
   a pixel-font label in each corner, and writes one 1920 by 1080 image.

## Decisions

**What is the lock for?** (asked before building it) Seeing one wavelength at a time: the contact sheet, the setup for
dispersion (a prism makes one sharp image per wavelength, and the rainbow is their stack), sodium (only its line exists
at 589 nm) and thin films (one fringe pattern instead of a smear), and exact checks, since a locked image has no color
noise. The tinted image is a label on physics, not what an eye behind a filter would see.

**[Learn] Why can't Resolve use $L\,\bar{\mathbf x}/p$ when locked?** There is no integral to estimate: a fixed wavelength
isn't a random draw, its density is a spike, and the image is $L(\lambda_0)$ itself. The answer given was that it
divides by zero. The probability of drawing exactly $\lambda_0$ from a continuous distribution is 0, but the density of
the locked choice is a spike, not 0; keeping the old code with $p = 1/470$ wouldn't crash, it would silently multiply by
470 and weight by $\bar{\mathbf x}(\lambda_0)$.

**[Learn] Why does a locked render have no color noise?** Every sample is the same wavelength, so only path noise in
brightness remains. Right. It converges as fast as RGB, which is what makes a sheet of 16 tiles practical.

**[Decide] How is a locked image shown?** Brightness as radiance, tinted by the wavelength's hue: tiles stay comparable
and each reads as its color. Plain gray loses the cue; true color goes nearly black at the ends, where $\bar y$ is tiny,
and shows the eye instead of the scene.

**[Decide] How is the contact sheet made?** A Tools component reusing movie mode's loop. Movie mode can't keyframe a
ScriptableObject setting, and doing it by hand is tedious for 16 tiles.

**[Decide] Should a wavelength be selectable by hand?** (asked during the round) Yes: Lock Wavelength plus a 360 to 830 nm
slider, which the sheet tool drives through the override.

**[Decide] What happens to the fitted white at 0.99986?** Snap exact white and black to flat spectra, a sigmoid with
$c_2 = \pm 10^4$, which is 1 to within $2.5 \times 10^{-9}$ and exactly 1 as a float. Loosening the locked check would
have accepted a real energy loss to pass a test, and fitting harder only reaches 0.99993, since 1 sits at infinity on
the sigmoid.

**Why did we build the table with least squares ourselves?** (asked during the round) There is no formula from a color
back to coefficients, so each one is found by improving a guess, which is Gauss-Newton; least squares with damping adds
robustness and stops at the closest color where no exact one exists. The table, built with warm starts, makes every
lookup reliable, fast and repeatable, and fitting it with our own CIE data keeps it consistent with the renderer.

**[Learn] Why is the measured box's 400 nm tile black?** The measured lamp emits nothing there (0 at 400 nm, 18.4 at
700). The answer given was the eye's insensitivity, which dimmed the strip's ends in cp-25, but a locked tile shows
radiance, not what the eye weighs it by. The fitted box, lit by D65-shaped light, keeps a lit 400 nm tile.

**[Decide] Which wavelengths?** 400 to 700 nm every 20, 16 tiles that fill a 4 by 4 grid at 480 by 270 each.

**[Decide] How are tiles labeled?** Stamped into the image with a 3 by 5 pixel font at twice its size, white over a black
outline, so the image explains itself.

**[Decide] Where does the tool run?** Play Mode, like movie mode: full resolution, its proven loop, and it keeps running
in the background.

## The math

Unlocked, a sample estimates $\int L\,\bar{\mathbf x}\,d\lambda$ as $L(\lambda)\,\bar{\mathbf x}(\lambda)/p(\lambda)$.
Locked at $\lambda_0$, the sample is $L(\lambda_0)$, and Display shows $L(\lambda_0)\cdot t(\lambda_0)$ with

$$t(\lambda) = \frac{c - \min(0, c_r, c_g, c_b)}{\max(\ldots)}, \qquad c = M_{\mathrm{XYZ}\to\mathrm{sRGB}}\,\bar{\mathbf x}(\lambda).$$

White light at $\lambda_0$ is $D65(\lambda_0)\cdot\int\bar y / \int D65\,\bar y$: 1.18321 at 450 nm, 1.05214 at 550,
0.80925 at 650. The snapped white is $S(10^4) = \frac{1}{2} + \frac{10^4}{2\sqrt{1 + 10^8}} \approx 1 - 2.5\times10^{-9}$.

A tile pixel is the plain average of a $4 \times 4$ block of the 1920 by 1080 capture.

## Verification

| Check | Result |
|---|---|
| Unlocked RGB beauty, normals, 8 samples | byte-identical |
| Unlocked spectral and split, 8 samples | byte-identical to cp-27 |
| White furnace locked at 450, 550, 650 nm, before the snap | sky exact, sphere at 0.99986 of it, FAIL by 1.1e-4 to 1.7e-4 |
| The same, after the snap | PASS, every pixel exactly 1.18322, 1.05214, 0.80925 |
| Contact sheets of both boxes | 1920 by 1080, 16 tiles; overrides and runInBackground cleared after |
| The 560 nm tile against a separate locked render | 0 of 115,200 pixels differ |

## Problems hit and fixes

- An edit expected the old wording of a comment you had shortened in `SampledWavelengths.hlsl`; your comment stayed and
  only the lock line was added.
- My restores of your settings assumed older values; they now come from your saved settings file (at the time, Spectral
  off and split on at 0.19).
- The lamp turns magenta in the deep red and violet tiles: it is far brighter than the walls, its strongest channel clips
  at 1 while the tint's small second channel keeps rising. That is the display clamp, which cp-55 replaces.

## Shots to capture

- [ ] The two contact sheets side by side: the measured lamp leaves 400 nm black, D65 doesn't.
- [ ] Dragging Locked Wavelength from 400 to 700 nm in the measured box, the red wall lighting up near 590 nm.
- [ ] The locked furnace failing by 0.00014 before the snap and passing after.

## Explainers to build (Manim)

- A spectrum as a stack of monochrome images, sliding the lock through it.
- A uniform density over 360 to 830 nm collapsing into a spike at one wavelength, and the $1/p$ losing its meaning.
- The sigmoid creeping toward 1 and never reaching it, with the snapped point far out on the curve.
