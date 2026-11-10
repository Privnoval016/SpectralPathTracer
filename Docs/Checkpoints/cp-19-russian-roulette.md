# cp-19-russian-roulette

Goal. End paths at random instead of only at a fixed bounce count, and boost the survivors so the average stays exact. A
high cap then costs little, and the darkening bias of a low cap goes away.

Scene `Assets/SpectralPathTracer/Scenes/Canvas.unity`, settings `Settings/PathTracerSettings.asset` (Russian Roulette, under
Max Bounces, now 16).

## What changed

- Shade plays Russian roulette after the albedo multiply, from depth 1 on. A path survives with probability equal to its
  throughput's largest lane, and survivors divide their throughput by it [PBRT4 Sec. 13.4]. The same test absorbs cp-17's
  zero-throughput exit.
- The draw uses its own sampler slot, `SLOT_RUSSIAN_ROULETTE = 2`, inside each bounce's block of 16 dimensions.
- `RussianRoulette` (on by default) sits under Max Bounces in `PathTracerSettings` and is part of the accumulation signature.
  Max Bounces defaults to 16.
- The Runtime/Rendering README's Shade box now mentions roulette.

## Decisions

**[Learn] What happens to a survivor's throughput?** It is divided by $q$. The expectation is $q \cdot L/q + (1 - q) \cdot 0 = L$, so the
average is exact at any depth while individual pixels get noisier. Rejected were leaving it alone (the average becomes $qL$,
darkening every bounce) and multiplying by $q$ (gives $q^2 L$).

**[Learn] What sets the survival probability?** The path's throughput. Bright paths almost always continue, so roulette rarely
discards a large contribution, and nearly dark paths usually stop, where a survivor's boost only scales a small number.
Rejected were a constant (kills bright paths and keeps doomed ones alike) and depth alone (a path between white walls stays
bright for many bounces).

**[Decide] Which lane sets it?** The largest lane, as PBRT does. Luminance would kill a path that just left the red wall
(throughput about (0.58, 0.08, 0.06)) even though its red lane carries the color bleeding, and with spectral lanes any one
bright wavelength should keep a path alive.

**[Learn] Why keep a Max Bounces cap?** The CPU records the render graph before any path runs, so the number of Intersect,
Shade and Args rounds is fixed at record time; looping until the queue empties would mean waiting on the GPU. The answer
given was that the cap keeps the result unbiased. It is the opposite, since the cap is the only bias left and roulette alone
is exact. Removing the cap is not possible for the same recording reason.

**[Learn] Why does roulette start at the second interaction?** First hits carry the most light. Stopping a path there discards
a large contribution and boosts survivors by a large factor, which is noise exactly where the image is brightest, to save
little work. The answer given was that it would be biased, but roulette is unbiased at any depth; starting later is a
noise-versus-cost choice.

**[Decide] What is the default cap?** 16. Few paths survive that long, so the cap's bias is negligible, and empty rounds cost
only small dispatches. Rejected were 8 and keeping 4.

**[Decide] Can roulette be switched off?** Yes, on by default, in `PathTracerSettings` under Max Bounces, for comparisons and
footage.

## Sampler slots

Each bounce owns dimensions $2 + 16 \cdot \text{depth}$ to $17 + 16 \cdot \text{depth}$, and a slot is a position in that block. Slots
are kinds of random decisions, not materials. Slots 0 and 1 choose the bounce direction for every material, slot 2 is the
roulette draw, and later slots will pick a light, a point on it, glass's reflect-or-refract choice and fog distances. Adding
a slot moves no other number, which is why roulette switched off reproduces cp-18's image byte for byte.

## The math

$$q = \min\!\left(1, \max_\lambda \beta_\lambda\right), \qquad \beta' = \frac{\beta}{q} \ \text{if } u < q, \ \text{otherwise the path ends}, \qquad
E[\beta'] = q \cdot \frac{\beta}{q} = \beta$$

Checks between two emissive planes (emission 1, albedo 0.5, exact $\sum 0.5^k$):

| Setup | Measured | Exact |
|---|---|---|
| no roulette, cap 4 | 1.9406 | 1.9375 |
| no roulette, cap 16 | 1.9877 | 1.99998 |
| roulette, cap 16, 256 samples | 1.9997 | 1.99998 |

The cap-16 row without roulette is one 8-bit level off because its true display value falls between two sRGB levels;
roulette's noise dithers that rounding away. Roulette's pixel spread at 256 samples was 0.023, the noise it trades for work.

Timing at 960 by 540 and 16 bounces. In the Cornell box roulette made no measurable difference (about 7 to 8 ms either
way), because paths already escape through the open front and the cp-18 queues make dead paths free. In the closed
furnace, where nothing escapes, it fell from about 18 ms to about 5.5 ms.

## Problems hit and fixes

- Shade used `_RussianRoulette` before declaring it; the uniform was added.
- Roulette's speedup doesn't show in the open Cornell box. The closed furnace shows it, and that is the scene to film.

## Shots to capture

- [ ] The furnace at 16 bounces with roulette off and on, side by side with the frame time.
- [ ] The Cornell box at cap 4 against cap 16 with roulette, showing the darkening a low cap leaves.
- [ ] A one-sample frame with roulette on, where a few boosted survivors sparkle brighter.

## Explainers to build (Manim)

- The one-line unbiasedness argument: $q \cdot L/q + (1 - q) \cdot 0 = L$, with paths stopping and survivors growing.
- Why first hits are spared: a large contribution lost against a small saving.

## References

[PBRT4] Matt Pharr, Wenzel Jakob, Greg Humphreys. 2023. *Physically Based Rendering: From Theory to Implementation*, 4th ed. MIT Press. Sec. 13.4, A Better Path Tracer. https://pbr-book.org/4ed/Light_Transport_I_Surface_Reflection/A_Better_Path_Tracer
