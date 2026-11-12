# cp-27-split-screen

Goal. The comparison tool for the rest of Chapter 4: RGB on one side of a divider, spectral on the other, both
rendered from the same random numbers, so whatever differs between the halves is how color is carried and nothing else.
Every spectral feature from cp-28 to cp-36 gets filmed through it.

Scenes `CornellBox.unity` and `CornellBoxMeasured.unity`, which both have `SplitDivider` on the Main Camera. Split Screen
and Split Position sit under Spectral in `Settings/PathTracerSettings.asset`; in Play Mode, drag the white line with the
left mouse button.

## What changed

1. Split Screen runs every wave twice, once per color mode, into two accumulation buffers. `FrameContext.InMode` carries
   which mode a pass renders, so one frame sets the `SPECTRAL` keyword both ways, which is what per-dispatch keywords
   were chosen for at cp-24. The second buffer exists only while split screen is on.
2. Display picks the RGB or the spectral image per pixel by the divider, converts only the spectral side from XYZ, and
   draws a one pixel white line in the divider's column. `_SplitPosition` and `_SplitScreen` fill a new register of
   `FrameConstants`, now 240 bytes.
3. `SplitDivider` in `Tools` drags the divider in Play Mode through `PathTracerOverrides.SplitPosition`, mapping the
   pointer through the same letterbox as Present, whose `FitInside` became public for it.
4. The settings inspector hides what doesn't apply: Split Position without split screen, Spectral with it, and each
   render scale while its mode is off.

## Decisions

**[Decide] How are the two halves rendered?** Both modes everywhere, into two buffers, with Display choosing per pixel.
The divider is then just a number Display reads, so it moves across converged images without restarting anything,
which a filmed wipe needs. It costs twice the work per sample and a second buffer (about 33 MB at 1920 by 1080); the
time budget absorbs the extra cost because it measures milliseconds per wave. Rendering each side only in its own mode
would cost nothing extra, but a moved divider would leave RGB sums where XYZ sums belong, so every move would restart.

**[Learn] What do identical random numbers buy the comparison?** Only color math differs. The sampler hashes pixel,
sample index and dimension, both passes share all three, and the one dimension only spectral mode draws (the
wavelength, slot 3 at depth 0) shifts nothing else, so both paths through a pixel jitter, bounce and survive roulette
identically. Right. They don't converge faster, they don't make the halves identical (the color differences are the
point, and spectral adds its own color noise), and nothing is stored, since the numbers are recomputed by hashing.

**[Decide] How is the divider moved?** A Split Position setting that works in both modes, plus a Play Mode drag that
writes an override, so a drag never changes the asset and movie mode can animate a wipe. A setting alone would have no
hands-on drag; a drag alone would have no way to place it in Edit Mode.

**[Decide] Which settings does the inspector hide?** Split Position without split screen, Spectral with it (both modes
render, so it does nothing), and each render scale while its mode doesn't render. Hiding Show Spectrum Strip under the
gray ramp was offered and left out.

**[Learn] Why can't the divider be the mouse's fraction of the window?** The image isn't the window: Present fits the
16:9 image inside the Game view with bars. In a 2000 by 1000 window the image starts at x = 111 and is 1778 wide, so a
cursor at x = 500 is 25% of the window but 21.9% of the image. Right. The vertical divider doesn't care about y, and the
pointer and `Screen` use the same pixel units, so neither is the problem.

**[Learn] Position or movement?** Position: every frame the divider goes where the cursor is in the image, so it stays
under the pointer. The answer given was movement, which adds deltas to wherever the divider was. Its errors accumulate:
a sensitivity can't match an image whose width changes with the window, and dragging past an edge clamps the divider
while the mouse keeps going, after which they never line up again. Movement is right where nothing is under the pointer,
which is why FlyCamera's look uses it.

**[Decide] How does a drag start?** Pressing the left button within 12 pixels of the line, then following until release.
Clicks elsewhere do nothing, so focusing the Game view or flying with FlyCamera (right button, Space) never moves it.
Jumping the divider on any left click, or dragging with a held key, were the alternatives.

**[Decide] Where does the drag live?** Its own component, `SplitDivider`, with two serialized `InputAction`s so the
bindings stay editable without code. Folding it into FlyCamera would tie dragging to flying.

## The math

The divider's position $t \in [0, 1]$ is a fraction of the image width. A pixel shows the spectral image when its
center $(x + \frac{1}{2})/w \ge t$, and the line is drawn in column $\min(\lfloor t\,w \rfloor, w - 1)$, exactly one
column at any $t$.

The letterbox: with output size $(W_o, H_o)$ and window $(W, H)$, $s = \min(W/W_o, H/H_o)$, the image is
$\operatorname{round}(W_o s)$ wide and starts at $\lfloor (W - \operatorname{round}(W_o s))/2 \rfloor$. A pointer at $x$
drags the divider to $t = \operatorname{clamp}\big((x - \text{left})/\text{width}, 0, 1\big)$.

## Verification

| Check | Result |
|---|---|
| Split off: beauty, normals, 8 samples | byte-identical to the baselines |
| Split on, 1 and 8 samples, left half against an RGB-only render | 0 pixels differ |
| Split on, 1 and 8 samples, right half against a spectral-only render | 0 pixels differ |
| Divider | white in its column on all 540 rows |
| Divider moved three times during accumulation | samples kept rising, 32, 37, 52, 64 |
| Simulated drag: press 0.3 widths away, press 6 px away, drag to 0.6, past the edge, to 0.75, release, move | none, none, 0.1967, 0.6, 0, 0.75, 0.75, 0.75 |
| Leaving Play Mode | override cleared, scene unchanged |
| EditMode tests | 48 of 48 |

## Problems hit and fixes

- The first divider test, $|\text{center} - t|\,w < \frac{1}{2}$, drew nothing at the exact midpoint of an even width,
  where the divider falls between two columns that are each exactly half a pixel away. It now draws the column the
  divider falls in.
- The capture scripts now pin Split Screen and Split Position as well as Spectral, so settings left on in the editor
  can't leak into a byte comparison.
- The first simulated drag assumed the divider was at the middle; the settings had it at 0.19, so the "away from the
  line" press grabbed it. The rerun placed every press relative to the real line.

## Shots to capture

- [ ] A slow wipe across the converged measured box, the green wall turning warmer on the spectral side.
- [ ] The same wipe at 16 samples, the geometry noise lining up across the divider while only the color noise differs.
- [ ] Dragging the divider by hand in Play Mode.

## Explainers to build (Manim)

- One wave split into two passes over the same paths, each into its own buffer, and Display reading both.
- A letterboxed window with the cursor, the window fraction and the image fraction drifting apart toward the edges.
