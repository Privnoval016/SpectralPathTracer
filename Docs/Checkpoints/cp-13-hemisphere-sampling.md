# cp-13-hemisphere-sampling

Goal: turn two uniform numbers into a direction in the hemisphere around any surface normal, cosine-weighted for
diffuse bounces, with the uniform warp alongside for comparison, and prove both on the GPU.

Scene: `Assets/SpectralPathTracer/Scenes/Canvas.unity`. Settings: `Settings/AovSettings.asset` (AOV Bounce Direction).

## What changed

- `Shaders/Include/Sampling.hlsl`: `SampleDiscConcentric` (concentric square to disk map) [ShirleyChiu97],
  `SampleCosineHemisphere` (Malley's method) with `CosineHemispherePdf` [PBRT4 Sec. A.5.3], `SampleUniformHemisphere` with
  `UniformHemispherePdf` [PBRT4 Sec. A.5.2], `BuildOrthonormalBasis` (branchless, no singular normal) [Duff17], and
  `LocalToWorld`.
- The dimension map grows per-bounce slots: `BounceDimension(depth, slot) = 2 + 16 * depth + slot`, with the BSDF's
  u and v as slots 0 and 1.
- A Bounce Direction AOV: at each hit, one cosine-sampled direction around the normal on the side the ray arrived from,
  shown as a color.
- `WarpStatisticsTests`: a test-only kernel (`Tests/EditMode/WarpTest.compute`) warps 65,536 samples around seven normals
  ($\pm y$, $\pm z$, $+x$ and two oblique ones); the tests check unit length, the hemisphere side, the distribution of the
  angle, and the mean direction.

## Decisions

**[Learn] How are $\theta = 90^\circ u_1$, $\phi = 360^\circ u_2$ directions spread?** Clumped at the pole. A band at polar angle $\theta$ is
a ring whose length is proportional to $\sin\theta$, so equal numbers of directions per $\theta$ crowd the small rings near the
normal and thin out the wide rings near the horizon: $d\omega = \sin\theta\,d\theta\,d\phi$. Uniform sampling needs $\cos\theta = u_1$.

**[Learn] Why sample diffuse bounces in proportion to $\cos\theta$?** Importance sampling. Light arriving at angle $\theta$ spreads
over a patch $1/\cos\theta$ larger, so it counts $\cos\theta$ less; with $p \propto \cos\theta$ the cosine cancels in $f/p$ and every
diffuse sample carries the same weight, which lowers noise. It doesn't change the converged answer. Rejected: "it's
cheaper" (about the same cost) and "it makes diffuse look right" (uniform converges to the same image, more slowly).

**[Learn] What's wrong with tangents from `cross(n, (0, 1, 0))`?** It is zero when $n = \pm y$, and normalizing it gives NaN.
The Cornell box's floor and ceiling are exactly $\pm y$, so every bounce there would poison its pixel. We use Duff et al.'s
branchless basis, which has no singular normal.

**[Decide] How does the square map onto the disk?** The concentric map: square rings become circles with little
distortion, so evenly spread (stratified) inputs stay evenly spread once smarter samplers arrive. Rejected: the polar
map ($r = \sqrt{u_1}$, $\phi = 2\pi u_2$), also exactly uniform but stretching some regions of the square and squeezing others.

**[Learn] Why does the concentric map special-case the exact center?** Both ratios would be $0/0$ = NaN. Only the exact center
fails: with one coordinate zero, the branch divides by the other.

**[Learn] Why clamp before the square root when lifting a disk point?** Rounding can put rim points slightly outside radius 1,
and the square root of a negative is NaN.

**[Learn] Which quantity is uniform for cosine-sampled directions?** $\cos^2\theta$. The answer given was $\cos\theta$. Disk points
are uniform by area, so $r^2$ is uniform and $\cos^2\theta = z^2 = 1 - r^2$ is uniform; $\cos\theta$ has density $2\cos\theta$. For the
uniform warp it is $\cos\theta$ that is uniform, so the test tells the two warps apart.

**[Decide] Where do the warps live?** In their own `Sampling.hlsl`, apart from `Sampler.hlsl`, which only produces random numbers.
This mirrors PBRT's split between samplers and sampling functions.

**[Learn] What do cosine-sampled directions average to?** $\tfrac{2}{3}\,n$: sideways components cancel by symmetry, and
$E[\cos\theta] = \int_0^1 c \cdot 2c\,dc = 2/3$. Uniform sampling averages to $\tfrac{1}{2}\,n$. So once accumulation exists, the Bounce
Direction AOV converges to $\tfrac{1}{3}n + \tfrac{1}{2}$, a washed-out normals AOV.

**[Decide] Is there a Bounce Direction AOV?** Yes, as footage of noisy directions that lean toward the normal.

## The math

$$\text{concentric: } (u_1, u_2) \mapsto r\,(\cos\theta, \sin\theta), \quad r = a, \ \theta = \tfrac{\pi}{4}\tfrac{b}{a} \text{ if } |a| > |b|, \text{ else } r = b, \ \theta = \tfrac{\pi}{2} - \tfrac{\pi}{4}\tfrac{a}{b}, \quad (a, b) = 2u - 1$$

$$\text{cosine: } \omega = \big(d_x, d_y, \sqrt{1 - d_x^2 - d_y^2}\big), \quad p(\omega) = \frac{\cos\theta}{\pi}; \qquad
\text{uniform: } \cos\theta = u_1, \ \phi = 2\pi u_2, \quad p(\omega) = \frac{1}{2\pi}$$

$$s = \operatorname{sgn}(n_z), \ a = \frac{-1}{s + n_z}, \ b = n_x n_y a: \quad
b_1 = (1 + s n_x^2 a, \ s b, \ -s n_x), \quad b_2 = (b, \ s + n_y^2 a, \ -n_y)$$

## Problems hit and fixes

- HLSL has no `copysignf`, and `sign(0)` is 0, which makes the basis divide by zero exactly at $n_z = 0$: the floor, the
  ceiling and the side walls. The basis uses `n.z >= 0 ? 1 : -1`, which the paper also suggests. A deliberate `sign()`
  mutation failed exactly those three normals in the tests.
- The first Bounce Direction AOV was missing the `Sampling.hlsl` include and sat before the hit check, so on misses it
  drew a basis around a zero normal, which quietly returns $x$ and $y$ and paints the sky with sideways directions instead
  of black. It moved inside the hit branch.
- The include generator ran before Unity had compiled the new enum value and wrote the old list; recompiling first fixed it.
- Mutation testing found the NaN hole again: a NaN mean direction passed the mean test. Its tests now assert the mean
  isn't NaN. A first attempt at the swapped-warp mutation called a function before its declaration, so the kernel failed to
  compile and every test failed, proving nothing; inlining the uniform math made exactly the 14 cosine tests fail.

## Shots to capture

- [ ] Clumped vs uniform points on a hemisphere (the explainer below).
- [ ] The Bounce Direction AOV: noise centered on each surface's normal color, clean on the floor and ceiling.
- [ ] The `sign()` mutation turning exactly the floor, ceiling and wall normals red in the test runner.

## Explainers to build (Manim)

- Clumped vs uniform: points from uniform $\theta$ piling up at the pole, then $\cos\theta = u_1$ spreading them evenly.
- Malley's method: uniform points on a disk lifted onto the hemisphere, thinning toward the rim.
- The concentric map: square rings becoming circles, compared with the polar map's stretching.

## References

[Duff17] Tom Duff, James Burgess, Per Christensen, Christophe Hery, Andrew Kensler, Max Liani, Ryusuke Villemin. 2017. Building an Orthonormal Basis, Revisited. *Journal of Computer Graphics Techniques* 6, 1, 1-8. https://jcgt.org/published/0006/01/01/ (Listing 3)

[PBRT4] Matt Pharr, Wenzel Jakob, Greg Humphreys. 2023. *Physically Based Rendering: From Theory to Implementation*, 4th ed. MIT Press. Sec. A.5.1 to A.5.3. https://pbr-book.org/4ed/Sampling_Algorithms/Sampling_Multidimensional_Functions

[ShirleyChiu97] Peter Shirley, Kenneth Chiu. 1997. A Low Distortion Map Between Disk and Square. *Journal of Graphics Tools* 2, 3, 45-52. https://doi.org/10.1080/10867651.1997.10487479
