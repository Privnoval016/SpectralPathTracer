# cp-08-aovs

Goal: show what the first hit sees instead of the rendered image: surface normals, planar depth, and which
side of a surface a ray hit. These are AOVs (arbitrary output variables), selected at runtime.

Scene: `Assets/SpectralPathTracer/Scenes/Canvas.unity`. Settings: `Settings/AovSettings.asset`.

## What changed

- `Aov` enum (`None`, `Normals`, `Depth`, `FrontFace`), marked `[GenerateHLSL]`, so `Aov.cs.hlsl` defines
  `AOV_NONE` through `AOV_FRONT_FACE` from the same source.
- `AovSettings` asset: the selected AOV and the depth range (near, far), referenced by the renderer feature.
  The pass copies them into `FrameContext` each frame.
- `ComputeStage.SetCamera` sets the camera uniforms for any stage; Generate and Shade both use it.
- HLSL: `CameraForward()`, `QuadricNormal()` (the gradient), `TestScene.hlsl` (the temporary test sphere,
  now shared by Intersect and Shade) and `Aov.hlsl` (`AovColor`, with a single exit).
- Shade rebuilds the hit position, normal and front-face flag from the ray, then either shows the selected
  AOV or the normal output.

## Decisions

**[Learn] How do we get a quadric's normal?** The gradient: $\nabla(\mathbf{x}^\top Q\,\mathbf{x}) = 2Q\mathbf{x}$, so
$\mathbf{n} = \text{normalize}\big((Q\,(\mathbf{p}, 1)).xyz\big)$. For a sphere this reduces to $\text{normalize}(\mathbf{p} - \mathbf{c})$. $Q$ and $-Q$
describe the same surface with opposite normals; writing $f > 0$ outside makes them point outward.
Rejected: center to hit point (spheres only), the negated ray direction (where the ray came from, not
the surface), crossing two tangents (needs tangents first).

**[Learn] What does $\mathbf{n}\cdot\mathbf{d} > 0$ mean?** The ray hit the back side: it travels with the outward normal,
as when the camera is inside the sphere. A grazing ray gives $\approx 0$, not a positive value. The normal stays
the geometric one, and a front-face flag records the side; refraction in cp-43 uses it to pick
$\eta = n_\text{air}/n_\text{glass}$ or its inverse.

**[Learn] How does the selection reach the GPU?** An int uniform and a branch. Every thread reads the same
value, so it's a uniform branch with no divergence. Rejected: a shader keyword per view (a variant per
view per kernel, doubled again by `SPECTRAL` later), a kernel per view (duplicated setup).

**[Decide] Where does the selector live?** Its own `AovSettings` asset, separate from render controls.

**[Decide] What are these called?** AOVs (arbitrary output variables), the production term. Normal and depth
images are real outputs in production renderers (compositing, and denoisers such as Open Image Denoise take
normal and albedo images), not only debugging aids.

**[Decide] Where does the normal come from?** Recomputed in Shade from the ray and the object. Intersect runs
every bounce for every ray, and memory traffic usually limits GPU path tracers, so hit records stay lean
(which object, and how far); from cp-09 the hit record carries an object ID that Shade uses to rebuild
normals and materials.

**[Decide] What does depth measure?** Planar depth along the camera's forward axis, $z = t\,(\mathbf{d}\cdot\mathbf{f})$, like a
raster depth buffer, so it compares directly with Unity's.

**[Decide] How does depth map to brightness?** Linearly between a near and a far value, the way graphics
debuggers such as RenderDoc show depth.

**[Decide] Which AOVs exist now?** Normals, depth and front face; later checkpoints add their own.

## The math

$$\mathbf{n} = \frac{(Q\,(\mathbf{p}, 1))_{xyz}}{\lVert (Q\,(\mathbf{p}, 1))_{xyz} \rVert}, \qquad
\text{front face} \iff \mathbf{n}\cdot\mathbf{d} < 0, \qquad
\text{brightness} = \text{saturate}\!\left(\frac{t\,(\mathbf{d}\cdot\mathbf{f}) - z_\text{near}}{z_\text{far} - z_\text{near}}\right)$$

The normals AOV shows $\mathbf{n}\cdot 0.5 + 0.5$: the sphere's center faces the camera with $\mathbf{n} = (0, 0, -1)$, so it is
$(0.5, 0.5, 0)$, olive, displayed brighter (yellow) by the sRGB encoding. The edges show pink ($+x$), green
($+y$), cyan ($-x$) and magenta ($-y$). Normals are world space, so the colors stay attached to the world as
the camera moves.

## Problems hit and fixes

- Shade first compiled before `Aov.cs.hlsl` was generated ("Couldn't open include file"); regenerating the
  include and reimporting cleared it.
- The default depth range (0 to 20) squeezes a sphere 3.2 to 4.2 units away into a sliver of gray; a range
  around the object (e.g. 3 to 5) shows its shading.
- The renderer feature's new `aovSettings` assignment lived only in memory until the assets were saved.

## Shots to capture

- [ ] Cycling the AOVs: normal output, normals, depth, front face.
- [ ] Depth with a tight range, then widening it.
- [ ] The camera moving inside the sphere: the front-face AOV turns red.
- [ ] Orbiting the camera with the normals AOV: colors stay fixed to world directions.

## Explainers to build (Manim)

- The gradient of $\mathbf{x}^\top Q\,\mathbf{x}$ as a field of arrows, perpendicular to the surface where $f = 0$.
- Front vs back hits: the sign of $\mathbf{n}\cdot\mathbf{d}$, setting up refraction.
- Planar depth vs distance along the ray.
