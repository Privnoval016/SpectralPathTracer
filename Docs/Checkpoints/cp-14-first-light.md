# cp-14-first-light

Goal: the first image made of light. The Cornell box's ceiling panel emits, walls reflect it with one
cosine-sampled bounce, and each pixel holds a one-sample Monte Carlo estimate of the rendering equation [Kajiya86].

Scene: `Assets/SpectralPathTracer/Scenes/Canvas.unity` (sky set to black). Settings: `Settings/PathTracerSettings.asset`
(Max Bounces 1).

## What changed

- `SpectralMaterial` gains `EmissionColor` and `EmissionStrength`; `GpuMaterial` gets their product as linear radiance.
  The Cornell light emits (17.0, 11.8, 4.0), the band averages of its measured emission spectrum [Cornell].
- Path state: `GpuPathState` {Radiance, Throughput, Active} in its own buffer with `PathState.hlsl` accessors.
  Generate starts every path (radiance 0, throughput 1, active); Intersect skips finished paths.
- A bounce loop in `PathTracerPass`: Generate, then Intersect and Shade `MaxBounces + 1` times, then a new Resolve stage
  that writes each path's radiance to its pixel. `MaxBounces` is a setting (0 to 16, default 1).
- Shade adds emission (front faces only) and sky radiance weighted by the throughput, then samples a cosine-weighted
  bounce, offsets its origin [WaechterBinder19] and multiplies the throughput by the albedo. AOVs still show the first hit.
- `RayOffset.hlsl`: an HLSL port of Waechter and Binder's Listing 6-1.
- Fixes: `[Header]` on auto-properties became `[field: Header]` (the Inspector draws the backing field, so the headers
  never showed).

## Decisions

**[Learn] What does the Lambertian estimator simplify to?** $a \cdot L_\text{in}$. The answer given was $a\,L_\text{in}\cos\theta$. With
$f = a/\pi$ and $p = \cos\theta/\pi$, $f\,L_\text{in}\cos\theta/p = a\,L_\text{in}$: the rendering equation's cosine cancels the one we chose to
sample with, and the $\pi$'s cancel. Keeping the extra cosine would count it twice (about a third of the energy lost per
bounce, since $E[\cos\theta] = 2/3$); dividing by $\pi$ would forget that the pdf's $\pi$ cancels the BRDF's.

**[Learn] How do we stop a bounce ray from re-hitting its own surface?** Offset the origin along the normal by an amount
that scales with the coordinates' magnitude [WaechterBinder19]. Rejected: a fixed tMin (right at one scale only: too
small far from the origin, too large in corners, where it leaks light along edges) and skipping the object just hit
(breaks surfaces that see themselves, like the inside of a sphere).

**[Decide] Where does emission live?** On `SpectralMaterial`, so any shape can glow; the light list for direct light
sampling is built later from emissive objects. Rejected: a separate light component.

**[Decide] How does the bounce run in the wavefront?** Loop the stages: Shade writes the next ray into the ray buffer and
the pass runs Intersect and Shade again. cp-17 only raises the count. Rejected: tracing the bounce inline in Shade.

**[Decide] Where does path state live?** A separate buffer indexed like the rays: rays get rewritten each bounce, paths
persist, and cp-18's queues can reorder rays while paths stay put. Rejected: growing `GpuRay`.

**[Decide] When does radiance reach the output?** A Resolve stage after the loop, the one place where cp-15's display
transform, cp-16's accumulation and cp-25's spectrum-to-RGB conversion will go. Rejected: Shade writing on the last bounce.

**[Learn] What happens to a path that has finished?** Shade marks it inactive and later stages return immediately for it.
Rejected: tracing it anyway (a wasted intersection loop per escaped ray) and compaction (right idea, needs cp-18's queues).

**[Decide] Is the sky a light?** Yes, at every bounce, so the open box is lit consistently through its opening. The Cornell
scene sets the sky to black for the classic look. Rejected: sky only for camera rays (inconsistent energy).

**[Decide] How is emission edited?** A 0 to 1 color plus a strength, which becomes "spectrum shape times scale" in the
spectral chapters. Rejected: one HDR color.

**[Learn] Does the back of an area light emit?** No, front only (cp-10's front, local $-z$). A real panel has an opaque housing;
two-sided emission would double its power and shine into the ceiling.

**[Decide] Is the bounce count a setting already?** Yes, Max Bounces (default 1).

**[Learn] Which access does Shade declare on the ray buffer?** ReadWrite. Write alone would tell render graph the rays
Generate wrote are unused (it may cull Generate); Read alone hides the write from the next Intersect.

**[Learn] How many Intersect dispatches per frame with one bounce?** Two: the camera rays and the bounce rays. Nothing
searches for the light; a bounce counts emission only when it happens to hit the panel.

**[Decide] One hit buffer or one per bounce?** One per bounce; render graph pools the memory and each pass stays explicit.

**[Decide] Restructure and light at once?** No: the path-state and Resolve restructure first had to reproduce the old image
byte for byte (it did, plain and with the normals AOV), then lighting went in.

**[Learn] In which order does Shade update a path at an emissive hit?** Add emission with the current throughput, then
multiply the throughput by the albedo. The answer given was the reverse. Throughput weights light arriving at this
surface; emission leaves it unreflected. Scaling first would dim a directly seen light to 0.78 of its emission and make a
light with black albedo emit nothing.

**[Learn] Which normal does the offset use?** The normal flipped to the side the ray arrived on, which is where the
bounce leaves. The raw normal pushes back-face hits through the surface (light leaks through walls); offsetting along
the new direction barely moves away from the plane at grazing angles.

**[Learn] What happens at the last allowed hit?** Add its emission and finish, without sampling a bounce nobody traces.

## The math

$$L_o(x, \omega_o) = L_e(x, \omega_o) + \int_{H^2} f(x, \omega_i, \omega_o)\, L_i(x, \omega_i) \cos\theta_i \, d\omega_i \qquad \text{[Kajiya86]}$$

$$\hat{L} = L_e + \frac{f\,L_i\cos\theta}{p(\omega_i)} = L_e + \frac{\frac{a}{\pi}\,L_i\cos\theta}{\frac{\cos\theta}{\pi}} = L_e + a\,L_i, \qquad
\text{radiance} \mathrel{+}= \beta\,L_e, \quad \beta \mathrel{*}= a$$

Check: the chance that a cosine-sampled bounce from the back wall hits the light is its projected solid angle over $\pi$,
$\frac{1}{\pi}\int_A \frac{\cos\theta\cos\theta'}{r^2}\,dA \approx 1.66\%$ over a patch of the wall. Two frames had 118 lit pixels of
7504 on that patch against an expected $124 \pm 11$.

## Problems hit and fixes

- `[Header]` on auto-properties compiles but lands on the property, not the serialized backing field, so the "Depth",
  "Noise" and "Renderer Settings" headers never appeared. Fixed with `[field: Header]`.
- Calling `Blitter.Cleanup()` on a healthy Blitter (as a precaution) nulled URP's blit materials: "ArgumentNullException:
  material" and blank captures. `Blitter.Initialize` with URP's runtime shaders restored it; the cleanup is only for the
  "already initialized" error.
- The renderer feature built its pass before the new Resolve shader was assigned; `ScriptableRendererData.SetDirty()`
  rebuilt it.
- Older HLSL compilers don't guarantee `||` short-circuits, so the active-path test is a separate early return after the
  bounds check rather than `id.x >= _RayCount || LoadPath(...)`, which could read past the buffer.
- Lit pixels look yellow-white even on the red and green walls: one lit red-wall pixel is $0.576 \times 17 \approx 9.8$ red and
  $0.083 \times 11.8 \approx 0.98$ green, and both clip to 1 without tone mapping (cp-15).

## Shots to capture

- [ ] Max Bounces 0: black box, only the glowing panel.
- [ ] Max Bounces 1: the first noisy render, sparse speckles on the walls, black ceiling, shadows under the blocks.
- [ ] The normals AOV unchanged, proving AOVs still show the first hit.

## Explainers to build (Manim)

- The rendering equation term by term, and the one-sample estimator with the cosines and $\pi$'s cancelling.
- Throughput as a running product along a path: emission added, then the albedo applied.
- Self-intersection: a hit point rounded to either side of the surface, and the scaled offset along the normal.
- Why the ceiling stays black: a downward light can't be seen from the ceiling.

## References

[Cornell] Cornell University Program of Computer Graphics. Cornell Box Data. https://bowers.cornell.edu/computer-graphics/data (accessed 2026-11-10).

[Kajiya86] James T. Kajiya. 1986. The Rendering Equation. *ACM SIGGRAPH Computer Graphics* 20, 4 (SIGGRAPH '86), 143-150. https://doi.org/10.1145/15922.15902

[WaechterBinder19] Carsten W\"{a}chter, Nikolaus Binder. 2019. A Fast and Robust Method for Avoiding Self-Intersection. In *Ray Tracing Gems*, Eric Haines and Tomas Akenine-M\"{o}ller (eds.), Apress, chapter 6, 77-85. https://doi.org/10.1007/978-1-4842-4427-2_6 (Listing 6-1)
