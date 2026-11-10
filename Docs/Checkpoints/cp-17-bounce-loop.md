# cp-17-bounce-loop

Goal. Let light arrive over several bounces, so the ceiling and the shadowed sides of the blocks light up and the
colored walls bleed onto their neighbors. The loop has existed since cp-14; this checkpoint raises its count, ends paths
that can no longer add light, and jitters camera rays inside their pixels.

Scene `Assets/SpectralPathTracer/Scenes/Canvas.unity`, settings `Settings/PathTracerSettings.asset` (Max Bounces 4, Pixel
Jitter on, Exposure -3).

## What changed

- Generate places each camera ray at a random spot inside its pixel, using the jitter dimensions reserved in cp-12.
  `PixelJitter` (on by default) is a setting and part of the accumulation signature.
- Shade ends a path as soon as its throughput is exactly zero.
- Max Bounces defaults to 4.
- `CameraRayMirrorTests` checks pixel centers with jitter off, and a new test checks that every jittered ray lands inside
  its own pixel and that most land off center.

## Decisions

**[Learn] What is the fewest bounces that lights the ceiling?** Two. The light faces down and sits just below the ceiling,
so no ray from the ceiling can see its front. A ceiling path bounces once to a wall or the floor, which can see the light,
and reaches it on the second bounce. Rejected were "1, but noisier" (the probability is exactly zero, and no number of
samples changes that) and "never" (true only for direct light).

**[Learn] How much indirect light is missing at three bounces with about 70% reflectance?** About 24%. Light after exactly $k$
bounces scales as $\rho^k$, all bounces sum to $1/(1 - \rho)$, and stopping after $n$ leaves out $\rho^{n+1} = 0.7^4 \approx 0.24$. The open
front loses some light, so the real gap is a little smaller. Rejected were 1% (that needs $\rho \approx 0.3$) and 50% (that is the
gap after one bounce). A fixed cutoff always biases the image dark, which Russian roulette fixes at cp-19.

**[Learn] What happens to a path whose throughput is exactly zero?** It finishes. Everything it could find later is
multiplied by zero, so the image is identical with less work. Rejected were bouncing on (correct, but every later bounce
wastes an Intersect and a Shade) and clamping to a small value (invents light, so the image is biased bright). This is the
deterministic special case of Russian roulette.

**[Decide] Jitter camera rays now?** Yes. With many samples per pixel, random positions inside each pixel average over its
whole area, so edges come out anti-aliased at no extra cost.

## The math

$$\text{bounce } k \text{ carries } \approx \rho^k, \qquad \frac{\sum_{k > n} \rho^k}{\sum_{k \ge 0} \rho^k} = \rho^{n+1}, \qquad
u = \frac{p + \xi}{\text{resolution}}, \ \xi \sim U[0, 1)^2$$

Rough check on a back wall patch at 256 samples each (linear). Each bounce added 0.0170, 0.0031, 0.0022 and 0.0007. The
first is direct light from the lamp. The bounce 3 to bounce 2 ratio of 0.71 matches the average reflectance; the last
increment is only a couple of 8-bit levels and too coarse to read. The white furnace test (cp-23) is the rigorous version.

## Problems hit and fixes

- Doubling the jitter range made the new test report a ray leaving its pixel, so the test can fail (the mutation was
  reverted).
- Bounce renders need the Game view visible, since a hidden tab never renders (cp-16).

## Shots to capture

- [ ] Max Bounces 1, 2, 3, 4 at the same sample count, with the ceiling lighting up at 2.
- [ ] Color bleeding, red on the tall block's left face and the floor near each wall.
- [ ] Pixel Jitter off and on, zoomed into the light panel's edge.

## Explainers to build (Manim)

- Paths of one, two and three bounces in the box, and the ceiling's light arriving only by the second.
- The geometric series $\rho^k$ and the share of light a fixed bounce count leaves out.
