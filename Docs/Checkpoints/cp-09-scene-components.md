# cp-09-scene-components

Goal: replace the hard-coded test sphere with real scene objects. Spheres are components on GameObjects,
materials are assets, and moving, scaling or editing either updates the render.

Scene: `Assets/SpectralPathTracer/Scenes/Canvas.unity`. Materials: `Assets/SpectralPathTracer/Materials/`.

## What changed

- `SpectralMaterial` asset (Create > Spectral Path Tracer > Spectral Material) with an RGB `Albedo` from 0 to 1.
- `TracedSphere` component (`[ExecuteAlways]`) with a `Radius` and a `Material`. It goes on an empty
  GameObject; the transform positions, rotates and scales it, so non-uniform scale gives an ellipsoid.
- `TracedRegistry` keeps the enabled spheres and a global `Version`, bumped on any register, unregister or
  Inspector edit.
- `SceneCompiler` (plain C#, owned by the pass) rebuilds and uploads the scene only when it changed: it
  transforms each sphere's quadric to world space, builds a deduplicated material table, and fills two
  persistent buffers that it imports into the render graph each frame.
- New `[GenerateHLSL]` structs `GpuObject` (world-space quadric, material index) and `GpuMaterial` (linear
  albedo). `GpuHit` gains `ObjectIndex`.
- Intersect loops over every object and keeps the nearest hit. Shade rebuilds the normal from the hit
  object's quadric and shows its material's albedo, or the sky on a miss.
- `TestScene.hlsl` is gone. `SceneData.hlsl` holds the object and material accessors.
- An Editor gizmo draws each traced sphere as a wire sphere in its material's color.
- This absorbs the old cp-11 (flat albedo): hits show their albedo from here on.

## Decisions

**[Learn] What is a quadric's matrix in world space?** $Q_\text{world} = M^{-\top} Q_\text{local} M^{-1}$. Substitute
$\mathbf{x}_\text{local} = M^{-1}\mathbf{x}_\text{world}$ into the local equation and regroup. The result stays symmetric, and one local
shape, $\text{diag}(1, 1, 1, -r^2)$, covers every position, rotation and scale. Normals come out right for free, since
`QuadricNormal` takes the gradient of the world-space $Q$; it is the rasterizer's inverse-transpose normal rule
with nothing extra to compute [Hartley04 Ch. 3]. Rejected: $MQ$ (treats the matrix as a point and breaks
symmetry), $MQM^\top$ (transforms the dual quadric, a different object), and $Q$ unchanged (a moved sphere is a
different set of points).

**[Decide] What does the GPU store per object?** The world-space $Q$ (64 bytes). C# applies the transform
once per change instead of every thread doing it every frame. Rejected: center and radius, which can't
express rotation or non-uniform scale and would need a different path for every quadric.

**[Decide] How do materials reach the GPU?** A material table plus a per-object index, the standard layout
in production renderers (PBRT, Mitsuba, Cycles and HDRP all keep materials apart from geometry). Shared
materials are stored once; Intersect never reads materials, so hit records stay lean; editing a material
never touches geometry; the table can later hold several material types side by side; and the wavefront
material queues need a material ID per hit anyway. Rejected: copying parameters into each object, which only
wins for tiny fixed scenes.

**[Decide] When does scene data upload?** Only when something changed, which needs change detection.

**[Learn] How does the scene notice a moved object?** Compare each sphere's `localToWorldMatrix` against the
one cached at the last build. Unity raises no event when a transform changes. Rejected:
`transform.hasChanged` (a single shared flag that something has to reset, so whichever reader clears it hides
the change from every other reader) and `OnValidate` (runs on Inspector edits, not on
dragging a gizmo or on parent motion).

**[Learn] Who owns the buffers?** `SceneCompiler` owns plain `GraphicsBuffer`s and imports them each frame.
Render graph buffers are transient and pooled, so their contents don't survive to the next frame, which
defeats uploading only on change. The output texture follows the same pattern since cp-02.

**[Learn] How does a buffer's size follow the object count?** It doubles when it has to grow. Adding $n$
objects one by one costs $O(n)$ copies in total instead of $O(n^2)$, and removing objects never shrinks the
buffer, since the count uniform says how many entries are live. Rejected: reallocating to the exact size on
every change (churn while building a scene), and a fixed maximum (an arbitrary cap that wastes memory below it).

**[Decide] How does the hit record name the object?** Add `ObjectIndex` and keep the `Hit` flag. The index is
$-1$ on a miss, but the flag stays the one thing every reader checks.

**[Learn] Is albedo HDR, like the sky?** No, 0 to 1. Albedo is the fraction of incoming light a surface reflects,
and a surface can't reflect more than arrives. Above 1, every bounce would add energy, which compounds
once paths bounce (cp-17). Like every Inspector color, it goes through `.linear` before upload.

**[Learn] What happens with no spheres?** Keep the buffers' capacity at least 1, send a count of 0, and run
every pass as usual: Intersect's loop runs zero times, every ray records a clean miss, and Shade shows the
sky. Skipping the Intersect dispatch (the answer given) would leave Shade reading a pooled hit buffer that
nothing wrote this frame; leftover entries could look like hits with garbage object indices, and an
out-of-bounds GPU read is undefined behavior, not an exception. A buffer of count 0 is not an option, since
Unity rejects creating an empty `GraphicsBuffer`.

**[Decide] What does Shade show on a hit?** The material's albedo, from this checkpoint on.

**[Decide] What does the gizmo look like?** A wire sphere in the material's color, drawn with the GameObject's
full transform so a scaled sphere shows the same ellipsoid the tracer renders. Unity's triangle-mesh sphere is
never used, so it can't be confused with the traced one.

## The math

$$\mathbf{x}_\text{local}^\top Q\,\mathbf{x}_\text{local} = (M^{-1}\mathbf{x}_\text{world})^\top Q\,(M^{-1}\mathbf{x}_\text{world})
= \mathbf{x}_\text{world}^\top \underbrace{M^{-\top} Q\,M^{-1}}_{Q_\text{world}} \mathbf{x}_\text{world},
\qquad Q_\text{sphere} = \text{diag}(1, 1, 1, -r^2)$$

Scaling by $(1, 2, 1)$ turns $x^2 + y^2 + z^2 = r^2$ into $x^2 + (y/2)^2 + z^2 = r^2$, an ellipsoid twice as tall, with no new
shape code.

## Problems hit and fixes

- The new structs' `.cs.hlsl` files don't exist until Edit > Rendering > Generate Shader Includes runs, so
  the kernels were reimported after generating them.
- With an AOV selected, an empty scene renders black: `AovColor` paints misses black. Setting the AOV to
  None shows the sky.
- Reimporting kernels while URP rebuilt its pipeline logged "Blitter is already initialized" twice. It comes
  from URP's own initialization, not this project's code.

## Shots to capture

- [ ] Adding spheres one by one, each appearing in the render with its material color.
- [ ] Dragging a sphere with the move gizmo: the render follows.
- [ ] Scaling one axis: the sphere becomes an ellipsoid, and the wire gizmo matches it.
- [ ] Editing a material's albedo: every sphere sharing it changes together.
- [ ] Deleting every sphere: only the sky remains.

## Explainers to build (Manim)

- Substituting $M^{-1}\mathbf{x}$ into $\mathbf{x}^\top Q\,\mathbf{x}$: a unit sphere stretching into an ellipsoid as $M$ changes.
- The material table: objects pointing at shared entries by index.

## References

[Hartley04] Richard Hartley, Andrew Zisserman. 2004. *Multiple View Geometry in Computer Vision*, 2nd ed. Cambridge University Press. Chapter 3, Projective Geometry and Transformations of 3D. https://doi.org/10.1017/CBO9780511811685
