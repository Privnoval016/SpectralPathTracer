# cp-10-quads-and-cornell

Goal: a second shape type, the quad, plus infinite planes, and the Cornell box built from its published
measurements.

Scene: `Assets/SpectralPathTracer/Scenes/Canvas.unity`. Prefab: `Assets/SpectralPathTracer/Prefabs/CornellBox.prefab`.
Materials: `Assets/SpectralPathTracer/Materials/Cornell/`.

## What changed

- `ShapeType` enum (`[GenerateHLSL]`, `SHAPETYPE_QUADRIC`, `SHAPETYPE_QUAD`). `GpuObject` gains `WorldToLocal` and
  `Type` (136 bytes).
- `Shaders/Include/Shapes/Quad.hlsl`: `IntersectQuad` (the unit square on the local z = 0 plane, intersected in local
  space) and `QuadNormal` (local $-z$ through the inverse transpose).
- `SceneData.hlsl` gains `IntersectObject` and `ObjectNormal`, which branch on the shape type; the kernels call only these.
- Components: an abstract `TracedShape` (material, registration, change tracking, and the virtual properties
  `WorldQuadric` and `WorldToShape`), an abstract `TracedQuadric` (local $Q$ to world $Q$), and the concrete
  `TracedSphere`, `TracedPlane` and `TracedQuad` (with a `Size`). The registry keeps one shape list, and the compiler
  fills every record the same way.
- Gizmos for every shape in `TracedShapeGizmos`: wire sphere, quad outline with a front marker, and a 10 by 10 square
  with a normal arrow for planes.
- The Cornell box as a prefab, built once by a one-off Editor tool (not kept in the repository), with the camera at the
  measured viewpoint.
- Folder layout: `Runtime/Rendering/{GpuData, Settings, Stages}`, `Runtime/Scene/{Components, Compilation}`,
  `Shaders/Include/Shapes`, still one `SpectralPathTracer` namespace.

## Decisions

**[Decide] How is a quad represented?** Options: its own shape intersected in local space, a plane quadric clipped to
local bounds, or two triangles. Two triangles was the first pick; after the trade-offs it became an **analytic quad**.
Triangles would have pulled cp-82 forward by about 70 checkpoints, need a watertight test [Woop13] to avoid cracks
along the shared diagonal (the standard M\"{o}ller-Trumbore test lets rays on the edge miss both triangles), and make
light sampling pick a triangle by area and then warp into it. An analytic quad samples as corner $+\,u\,\mathbf{e}_1 + v\,\mathbf{e}_2$
with pdf $1/\text{area}$, which the spectral scenes need: the prism (cp-50) depends on sampling a small emitter
directly for every wavelength. cp-82 still adds watertight triangles for meshes.

**[Learn] What happens to the ray's direction in local space?** Transform it with $w = 0$ and keep its length. The
answer given was to normalize it. Because $M^{-1}(\mathbf{o} + t\,\mathbf{d}) = M^{-1}\mathbf{o} + t\,M^{-1}\mathbf{d}$, the unnormalized local
ray reaches the hit at the same $t$ as the world ray. Normalizing rescales $t$ by $\lVert M^{-1}\mathbf{d}\rVert$: a quad scaled by
4 and hit at world $t = 8$ reports $t = 2$, which beats a sphere in front of it at $t = 5$. Transforming with $w = 1$
adds the translation to a direction.

**[Decide] How does Intersect loop over several shape types?** One object list with a type field, the standard
instance-table layout that a BVH later sits over. With no BVH, every thread tests object $i$ at the same time, so
the branch on its type is uniform and costs no divergence. Rejected: one buffer per type, which adds a buffer, a
count and a loop per shape and has to be merged again for the BVH.

**[Decide] Infinite planes?** Yes, as plane quadrics: they intersect through the existing linear case with no new
GPU code. They can't be lights (no finite area to sample), and with a BVH they will sit in a small side list every
ray tests, since their bounds are infinite.

**[Learn] Which local Q is the plane y = 0?** $Q_{13} = Q_{31} = \tfrac{1}{2}$, so $\mathbf{x}^\top Q\,\mathbf{x} = y$, $a = 0$ for every ray, and
$t = -o_y/d_y$. Rejected: $\text{diag}(0, 1, 0, 0)$ ($y^2 = 0$ is a double root that float error turns into random
misses, and its gradient is zero on the surface, so the normal is NaN), and $Q_{13} = 1$ alone (the surface is right,
but the intersector assumes symmetry and computes $h = d_y$, so every hit lands at half the distance, and the
gradient of $\mathbf{x}^\top Q\,\mathbf{x}$ is really $(Q + Q^\top)\mathbf{x}$).

**[Decide] What does a GpuObject hold?** Both matrices as separate fields (136 bytes). Each field means one thing,
spheres will want `WorldToLocal` later (texture coordinates, light sampling), and all threads read the same object
at once, so the GPU fetches it once per warp. Rejected: one shared matrix whose meaning depends on the type.

**[Learn] What is the quad's world normal?** $\text{worldToLocal}^\top (0, 0, -1, 0)$, the inverse transpose of localToWorld.
That vector is the negated third row of `worldToLocal`, which is the quad's world plane equation: planes transform by
$M^{-\top}$, the same matrix as quadrics [Hartley04 Ch. 3]. Rejected: $M\,\mathbf{n}$ (correct for plain rotation and scale,
but tilted off the surface under a non-uniformly scaled, rotated parent) and keeping $(0, 0, -1)$ (ignores rotation).

**[Learn] What if the ray is parallel to the quad?** Guard it: if $|d_z|$ is below an epsilon, it misses. The answer
given was that no guard is needed. In exact IEEE arithmetic the infinity would fail the bounds check, but shader
compilers commonly assume infinities and NaN never occur, so code that relies on them can be optimized into something
else. Rejected: clamping $d_z$, which computes $t$ for a different ray and can flip its sign.

**[Decide] Which side is the quad's front?** Local $-z$, like Unity's built-in Quad (normal $(0, 0, -1)$, spanning $\pm 0.5$).
Quad lights from cp-14 emit from the front, so this decides which way a light shines.

**[Decide] How are the files organized?** By role: `GpuData`, `Settings` and `Stages` under rendering, `Components`
and `Compilation` under scene, and `Shapes` under the shader includes, all in one namespace. Folders are for
browsing; matching namespaces would add `using` lines everywhere for no gain.

**[Decide] How do the shape components share code?** An abstract `TracedShape` base with registration, the material
and change tracking. The first plan had the compiler `switch` on the component type; instead each shape answers two
virtual properties: `WorldQuadric` (zero by default; `TracedQuadric` returns $M^{-\top} Q M^{-1}$) and `WorldToShape`
(`worldToLocal` by default; `TracedQuad` divides by its size). The compiler fills every record the same way, so a
new shape is one class and the compiler never changes (the open/closed principle). Components expose math, not the
GPU layout. The GPU keeps its branch on the type tag, since compute shaders have no virtual functions.

**[Decide] How is a quad sized?** A `Size` field like the sphere's `Radius`, so walls are sized in the Inspector with
transform scale 1.

**[Learn] Which matrix becomes the quad's WorldToLocal?** $S^{-1}W$: apply `worldToLocal` first, then divide by the
size along the quad's own axes. The answer given was $W S^{-1}$, which divides along the world axes before
rotating: a 4 by 1 quad turned $90^\circ$ about $y$ would render 1 unit long. $S\,W$ multiplies instead and shrinks a 4 unit
quad to a quarter of a unit. In general $(LS)^{-1} = S^{-1}L^{-1}$.

**[Decide] What does the plane gizmo show?** A 10 by 10 wire square and an arrow along the normal.

**[Decide] Where do the box's dimensions come from?** The published measurements of the real box [Cornell], squared
up: walls of 556 by 548.8 by 559.2 mm, and each block fitted from its four measured top corners (opposite edges
averaged) into a box about 166 mm square, 165 or 330 mm tall, turned about $\pm 17^\circ$. Rejected: the exact measured
polygons (not rectangles, so they need a general quad) and a made-up unit box.

**[Decide] What colors do the walls get?** Placeholder linear RGB taken from the measured reflectance spectra by
averaging each over three bands (600 to 700, 500 to 596 and 400 to 496 nm): white $(0.734, 0.736, 0.698)$, green
$(0.133, 0.351, 0.114)$, red $(0.576, 0.083, 0.056)$. The light quad gets the listed reflectance 0.78. The spectral
checkpoints replace these with the full 4 nm tables. Rejected: pure red, green and white, where a white of 1 reflects
all light.

**[Decide] How is the box built?** An Editor menu built it from the data and saved a prefab. The tool did its job once
and is not kept in the repository; the prefab is the artifact.

**[Decide] How are the blocks made?** A parent per block at its center, turned about $y$, with five child quads (the
bottom never shows). Rejected: a new box shape with a slab test, which can wait for the BVH.

**[Learn] How does right-handed data become left-handed?** Negate x (and subtract the camera's 278 mm so the box is
centered). Changing handedness needs a reflection; rotating $180^\circ$ about $y$ puts red on the left but looks at the box
from behind, and copying the numbers renders a mirror image with the blocks swapped.

**[Learn] What happens where the light and ceiling overlap?** A speckled mix: both hit at the same $t$ in exact
math, but each computes it through its own matrix, so rounding decides per pixel, the ray tracing version of
z-fighting. The real data cuts a hole in the ceiling; we lower the light by 0.1 mm, invisible at this scale and far
above float rounding near 0.5 m.

**[Decide] What scale?** Meters, true size (the box is 0.55 m). Time of flight (R9) and medium densities use real
distances.

**[Decide] Does the tool place the camera?** Yes, undoably, at the measured position with the vertical field of view
of the 35 mm lens on 25 mm film: $2\arctan(12.5/35) = 39.3^\circ$.

## The math

$$\text{quad: } t = -\frac{o_z}{d_z}, \quad |o_x + t\,d_x| \le \tfrac{1}{2}, \quad |o_y + t\,d_y| \le \tfrac{1}{2}, \qquad
\mathbf{n} = \text{normalize}\big(W^\top (0, 0, -1, 0)\big)$$

$$\text{plane: } Q = \begin{pmatrix} 0 & 0 & 0 & 0 \\ 0 & 0 & 0 & \tfrac{1}{2} \\ 0 & 0 & 0 & 0 \\ 0 & \tfrac{1}{2} & 0 & 0 \end{pmatrix}, \qquad
\text{sized quad: } W_\text{shape} = S^{-1} W$$

## Problems hit and fixes

- First compile of the quad code: `Quad.hlsl` was missing its `#endif` ("Unterminated conditional expression"), a
  parameter named `point` collided with an HLSL keyword (a geometry shader primitive type), `QuadNormal` was declared
  with two parameters and called with one, and the walkthrough gave the wrong define names: the generator writes
  `SHAPETYPE_QUAD` (no underscore inside the type name), not `SHAPE_TYPE_QUAD`.
- "Blitter is already initialized" on every render, even with the renderer feature disabled: URP left its blit helper
  initialized after a pipeline teardown during the asset moves. `Blitter.Cleanup()` once reset it.
- The Cornell data page moved from graphics.cornell.edu; the current copy is on bowers.cornell.edu.
- The box was first described as 5.5 m; the data is in millimeters (the lens is listed as 0.035), so it is 0.55 m.
- The first block fit took its forward axis from the first measured edge, so the short block's "Front" face pointed
  sideways; the fit now picks the edge running closer to $+z$.

## Shots to capture

- [ ] A TracedQuad appearing, then rotating and resizing it with the gizmo following.
- [ ] A TracedPlane appearing as an endless floor.
- [ ] The wrong matrix order ($W S^{-1}$) on a rotated 4 by 1 quad shrinking to a sliver, then the fix.
- [ ] The Cornell box appearing from the menu, the camera snapping to the measured viewpoint.
- [ ] The flat box: white blocks vanish against white walls until the normals AOV reveals them, the motivation for light.

## Explainers to build (Manim)

- A plane as a degenerate quadric: $a = 0$, the linear case.
- The third row of worldToLocal as the quad's plane equation.
- Matrix order: dividing by the size before vs after rotating.
- Handedness: why a mirror, not a rotation, converts the data.

## References

[Cornell] Cornell University Program of Computer Graphics. Cornell Box Data (geometry, camera, reflectance and emission spectra). https://bowers.cornell.edu/computer-graphics/data (accessed 2026-11-10).

[Hartley04] Richard Hartley, Andrew Zisserman. 2004. *Multiple View Geometry in Computer Vision*, 2nd ed. Cambridge University Press. Chapter 3, Projective Geometry and Transformations of 3D. https://doi.org/10.1017/CBO9780511811685

[Woop13] Sven Woop, Carsten Benthin, Ingo Wald. 2013. Watertight Ray/Triangle Intersection. *Journal of Computer Graphics Techniques* 2, 1, 65-82. https://jcgt.org/published/0002/01/05/
