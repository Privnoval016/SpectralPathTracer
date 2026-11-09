# cp-04-camera-rays

Goal: turn every pixel into a ray from the camera through that pixel, and show each ray's direction as a
color. Plus a Scene view overlay that draws the same rays in 3D.

Scene: `Assets/SpectralPathTracer/Scenes/Canvas.unity`.

## What changed

The kernel maps each pixel center to normalized device coordinates, builds the ray direction with
`CameraRayDirection`, and writes `direction * 0.5 + 0.5`: red is world $+x$, green $+y$, blue $+z$.

`Runtime/Rendering/PinholeCamera.cs` is a small `readonly struct` (camera-to-world matrix, $\tan\frac{\theta}{2}$,
output aspect) with `From(Camera, PathTracerSettings)` and `RayDirection(ndc)`, the CPU mirror of the HLSL
function. The pass sends its three values to the kernel.

`PathTracerPass.RecordRenderGraph` is now a short sequence of named helpers: `ImportOutput`,
`RecordRenderStage`, `RecordPresentStage`, plus `GroupCount` for the round-up division. Pass builders use
`using var` declarations.

`Editor/RayFanOverlay.cs` is a Scene view overlay (off by default, in the Overlays menu) that draws a
configurable grid of camera rays from `Camera.main`, optional frustum edges, and the image plane at
distance 1. Its settings (`Editor/RayFanSettings.cs`) are per user, saved under `UserSettings/`.

## Decisions

**How does the 0 to 1 coordinate become a centered one?** $2u - 1$, normalized device coordinates from
$-1$ to $1$. Rejected: $u - 0.5$ (only reaches $\pm 0.5$, every later formula needs a factor of 2),
$1 - 2u$ (mirrors the image).

**How tall is the image plane 1 unit away for a vertical FOV $\theta$?** $2\tan\frac{\theta}{2}$, from the right
triangle formed by the view axis and half the angle; about 1.155 at 60 degrees. Rejected: $\tan\theta$
(tangent isn't linear), $\theta / 90$ (an angle isn't a length), exactly 1 (only true at about 53.1 degrees).

**Which aspect ratio do the rays use?** The output resolution's, $1920 / 1080$. The rays must match the pixel
grid they fill. Rejected: `Camera.aspect` (follows the Game view window, so an 806x599 window stretched
everything $1.778 / 1.346 \approx 1.32\times$ and the framing changed with the window size), a square aspect
(squashes the image). This is also why Unity's projection matrix isn't reused.

**How does the kernel build a ray?** The pinhole formula: the point $(x\,a\tan\frac{\theta}{2},\ y\tan\frac{\theta}{2},\ 1)$
rotated by the camera's transform and normalized. Rejected: inverting the projection matrix (inherits the
window aspect plus clip-space and reversed-depth conventions), building rays on the CPU (2 million
calculations in sequence and a 24 MB upload per frame).

**How is `RecordRenderGraph` broken up?** Private helper methods for now, one job each. At cp-07, when the
wavefront split adds more kernels, the pass becomes an orchestrator with one small class per stage.
Rejected for now: a URP pass per stage (shared textures and ordering get harder), partial classes (spread
one class over files without separating responsibilities).

**How are render functions written?** Inline `static` lambdas. The compiler caches them once; passing a
named method instead would allocate a new delegate every call in C# 9.

**Where does the camera math live?** Inline at first, then moved into the `PinholeCamera` struct the moment
a second consumer (the overlay) needed the same rays.

**How is the HLSL organized?** One `.compute` file for now, split into topic includes once a second topic
appears.

**Where does the ray visualization live?** A Scene view overlay in the Editor assembly, with per-user
settings. Nothing is added to the scene and nothing ships in builds. Rejected: a MonoBehaviour with
`OnDrawGizmos` (debug data on a scene object), a data component plus a drawer (still a component).

**How do the CPU and GPU rays stay identical?** One C# struct used by both the pass and the overlay, whose
`RayDirection` is documented as the CPU mirror of the HLSL `CameraRayDirection`. Rejected: duplicating the formula in the overlay (two copies
drift), reading rays back from the GPU (heavy for a gizmo).

## The math

$$\text{ndc} = 2\,\frac{\text{id} + 0.5}{\text{res}} - 1, \qquad
\mathbf{d}_{\text{cam}} = \left(\text{ndc}_x\, a \tan\tfrac{\theta}{2},\ \text{ndc}_y \tan\tfrac{\theta}{2},\ 1\right), \qquad
\mathbf{d} = \frac{M\,(\mathbf{d}_{\text{cam}}, 0)}{\lVert M\,(\mathbf{d}_{\text{cam}}, 0) \rVert}$$

$w = 0$ makes the camera matrix $M$ rotate the direction without translating it, so moving the camera leaves
the image unchanged and only rotation affects it.

## Problems hit and fixes

- `passData` was used before its `using` declared it, with lowercase field names (CS0103).
- The camera parameters were set after `DispatchCompute`. A command buffer runs in order, so the dispatch
  saw zeros on the first frame (NaN directions) and the previous frame's camera afterward. Every `Set`
  call now comes before the dispatch.
- `camera.cameraToWorldMatrix` looks down $-z$ (OpenGL convention); `transform.localToWorldMatrix` looks
  down $+z$ and is the one used.

## Shots to capture

- [ ] The direction image at the default camera: lavender center, pink right, cyan top-left, blue bottom-left.
- [ ] Rotating the camera: the colors follow. Moving it: nothing changes.
- [ ] The Ray Fan overlay: the grid growing from 2x2 to 64x36, the image plane, then lengthening the rays.

## Explainers to build (Manim)

- The right triangle behind $\tan\frac{\theta}{2}$: camera, view axis, image plane at distance 1.
- uv to NDC to a point on the image plane to a world direction, one pixel followed through every step.
- Aspect mismatch: the same scene rendered with the window's aspect vs the output's.
