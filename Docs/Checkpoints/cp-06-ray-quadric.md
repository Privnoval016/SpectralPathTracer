# cp-06-ray-quadric

Goal: the first object in the world. Rays intersect a general quadric surface, tested on one hard-coded
sphere written as its quadric matrix.

Scene: `Assets/SpectralPathTracer/Scenes/Canvas.unity`.

## What changed

The shader gained:

- `CameraRayOrigin()`: the camera's world position, `mul(_CameraToWorld, float4(0, 0, 0, 1))`.
- `SphereQuadric(center, radius)`: a sphere's $4 \times 4$ quadric matrix.
- `IntersectQuadric(q, origin, direction, tMin, out t)`: the nearest hit beyond `tMin` on any quadric
  $\mathbf{x}^\top Q\,\mathbf{x} = 0$, with a single exit at the end.
- A temporary test sphere at $(0, 1, 0)$, radius 1, as `static const` values (replaced by `TracedSphere`
  components in cp-09).

The kernel shades a hit white and a miss with the sky.

## Decisions

**[Learn] Substituting the ray into the sphere equation gives what?** A quadratic in $t$:
$(\mathbf{d}\cdot\mathbf{d})t^2 + 2(\mathbf{d}\cdot\mathbf{m})t + (\mathbf{m}\cdot\mathbf{m} - r^2) = 0$ with
$\mathbf{m} = \mathbf{o} - \mathbf{c}$, and $a = 1$ for a unit direction.

**[Learn] What does a negative discriminant mean?** The line misses: no real $t$. Zero is a tangent graze,
positive is two crossings.

**[Learn] Which root does the camera see?** The smallest positive one: $t_0$ if it's in front, otherwise $t_1$
(the ray starts inside, which refracted rays in glass will rely on), otherwise no hit. Rejected: always
$t_0$ (spheres behind the camera render), always $t_1$ (the back faces), the average (inside the sphere).

**[Decide] Spheres in general, and how?** The `TracedSphere` component, the GPU buffer and the closest-hit loop
all wait for cp-09, which keeps cp-06's story about the math. In cp-09 a `TracedSphere` sits on an empty
GameObject with its own radius field and an Editor gizmo that draws the analytic sphere, since Unity's
sphere mesh is triangles and would never match exactly. For now, the one sphere is hard-coded in the shader.

**[Learn] How will many spheres reach the GPU (cp-09)?** A StructuredBuffer of structs. Rejected: a uniform per
sphere (a fixed maximum, an ID per sphere), packing into a texture (structured data hidden behind pixel
formats and filtering).

**[Decide] Sphere first, or general quadrics?** A general quadric intersector from the start, with the sphere
as the first $Q$. The video explains the discriminant on a sphere, then quickly generalizes.

**[Learn] What are the ray's homogeneous $w$ components?** $O = (\mathbf{o}, 1)$, a point; $D = (\mathbf{d}, 0)$, a
direction. $Q$'s last row and column hold the translation, which must act on the origin but not on the
direction.

**[Learn] Why is the middle coefficient $2 D^\top Q O$?** $Q$ is symmetric, so $D^\top Q O = (O^\top Q D)^\top = O^\top Q D$.
Any $Q$ can be symmetrized as $\frac{1}{2}(Q + Q^\top)$ without changing the surface.

**[Learn] What if $a = D^\top Q D = 0$?** The equation turns linear, $2ht + c = 0$, with one crossing (e.g. along a
cone's side). It's tested as $|a| < \varepsilon$, since a nearly zero $a$ produces huge, meaningless roots.

**[Learn] How are the roots ordered?** Sorted with `min` and `max`: dividing by a negative $a$ swaps them.

## The math

A quadric is $\mathbf{x}^\top Q\,\mathbf{x} = 0$ for homogeneous $\mathbf{x} = (x, y, z, 1)$ and symmetric $Q$. A sphere:

$$Q = \begin{pmatrix} I & -\mathbf{c} \\ -\mathbf{c}^\top & \mathbf{c}\cdot\mathbf{c} - r^2 \end{pmatrix}$$

The ray $X(t) = O + tD$ gives $a t^2 + 2h t + c = 0$ with

$$a = D^\top Q D, \qquad h = D^\top Q O, \qquad c = O^\top Q O, \qquad t = \frac{-h \pm \sqrt{h^2 - ac}}{a}$$

Check against the render: the camera at distance 4.16 from a radius-1 sphere sees it at an angular radius
of $\arcsin(1/4.16) \approx 13.9^\circ$. With a $30^\circ$ half field of view, the disk's radius is
$\tan 13.9^\circ / \tan 30^\circ \approx 0.43$ of the image's half height: 115.8 pixels in a 540-pixel-tall
image, matching the render.

## Problems hit and fixes

- **No sphere at all, with an "implicit truncation of vector type" warning.** `float qo = mul(q, o);` stored
  a `float4` into a `float`. HLSL silently keeps `.x`, then promotes the scalar back to
  `(qo, qo, qo, qo)` inside `dot`, so $h$ and $c$ were garbage and every ray missed. Fixed with `float4 qo`.
  A truncation warning in HLSL is almost always a bug; C# would refuse to compile it.
- **"use of potentially uninitialized variable (IntersectQuadric)" on the lines after early returns.** The
  name in parentheses is the function, not a variable: the shader compiler couldn't prove the function's
  return value was set on every path when it returned from inside `if` blocks. The logic was correct.
  Restructuring to a single exit (a `hit` variable, one `return` at the end) removed both warnings.
- The linear case returned `t >= tMin` while the quadratic case used `t > tMin`. They now match (`>`), so a
  hit exactly at the ray origin is never counted; that matters for bounce rays in cp-14.

## Shots to capture

- [ ] The white disk on the sky, then the camera moving toward it.
- [ ] The camera inside the sphere: everything white (every ray hits the far wall, $t_1$).

## Explainers to build (Manim)

- A line meeting a circle 0, 1 or 2 times, with the discriminant's sign beside it.
- The sphere's equation becoming its $4 \times 4$ matrix, then other $Q$s morphing it into an ellipsoid,
  cylinder, cone and paraboloid.
- Why the ray's origin gets $w = 1$ and its direction $w = 0$.
