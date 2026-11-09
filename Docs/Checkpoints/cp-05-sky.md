# cp-05-sky

Goal: give rays something to see when they hit nothing. The sky is the first light source; from cp-14 on,
every path that escapes the scene ends by reading it.

Scene: `Assets/SpectralPathTracer/Scenes/Canvas.unity`.

## What changed

`SkyRadiance(direction)` in the shader returns a ground color below the horizon and, above it, a
horizon-to-zenith blend linear in elevation angle. The kernel writes it for every pixel.

`Runtime/Scene/` is new:

- `Sky.cs`: an `[ExecuteAlways]` component with three HDR colors (zenith, horizon, ground) that registers
  itself on enable and unregisters on disable.
- `TracedRegistry.cs`: a static registry of enabled skies, the start of the scene system.
- `SkyColors.cs`: a value type for the three colors, including the built-in `Default` sky.

The pass resolves the sky each frame (`TryResolveSky`): the scene's `Sky`, the built-in default if there
is none, or nothing with one logged error if several are enabled. Colors go to the kernel converted to
linear.

## Decisions

**[Learn] What does the sky's color depend on?** Direction only. The sky stands for light from infinitely
far away, where parallax is zero: moving the observer doesn't change where it is seen, like the sun. If the
origin mattered, surfaces at different positions would be lit by different skies and mirrors would show a
sky that doesn't match the one behind them. Rejected: pixel position (glues the sky to the screen; a
reflected ray has no screen position).

**[Decide] What controls the horizon-to-zenith blend?** The elevation angle, $\arcsin(y) / \frac{\pi}{2}$, which is
linear in angle and gives a wider horizon band than blending by $y = \sin(\text{elevation})$, where the zenith
color takes over quickly (0.707 at 45 degrees). Both are valid art direction; screen-space `uv.y` is not.

**[Decide] Where do the sky colors live?** In a scene component, so each scene owns its sky. This starts the
scene system early with a static registry.

**[Learn] Should sky colors be limited to 0 to 1?** No. The sky emits radiance, which has no upper limit (a
real sky is many times brighter than white paper indoors); exposure maps it to the screen later. The
colors use the HDR picker (`ColorUsage(false, true)`).

**[Learn] How does the pass find the Sky component?** A static registry the component joins in `OnEnable` and
leaves in `OnDisable`. Rejected: `FindFirstObjectByType` (a scene search every frame, banned at runtime),
a singleton `Instance` (the same static state, but limited to one sky), a serialized reference on the
feature (assets can't reference scene objects).

**[Decide] What if two Sky components are enabled?** Log one error and skip the path tracer until there is
one, so which sky is lit is never ambiguous.

**[Decide] What if there is no Sky component?** Use a built-in default sky, so a new scene still looks
outdoors.

**[Learn] What makes the component register in Edit Mode?** `[ExecuteAlways]`. Without it, `OnEnable` and
`OnDisable` only run in Play Mode and the registry would be empty while editing. After a script recompile
the statics reset, and `[ExecuteAlways]` components re-register themselves.

**[Decide] When does the HLSL split into topic includes?** Together with the C# stage restructure at cp-07.

## The math

$$L_{\text{sky}}(\mathbf{d}) = \begin{cases} C_{\text{ground}} & d_y < 0 \\ \text{lerp}\!\left(C_{\text{horizon}},\ C_{\text{zenith}},\ \dfrac{\arcsin d_y}{\pi / 2}\right) & d_y \ge 0 \end{cases}$$

`saturate` keeps $d_y \le 1$ before `asin`, since rounding can push a normalized vector's component a hair
past 1, where `asin` returns NaN.

## Problems hit and fixes

- The sky colors were resolved at the top of `RecordRenderGraph` and read in the render function, but
  nothing carried them in between: the call site missed the new argument (CS7036), `RenderPassData` had
  no `Sky` field (CS1061), and the field was never assigned (which would have rendered black silently).

## Shots to capture

- [ ] The default sky, then tilting the camera so the horizon moves.
- [ ] Adding a Sky component and dragging the HDR intensity up.
- [ ] Two Sky components: the error and the fallback to Unity's background.

## Explainers to build (Manim)

- Parallax shrinking to zero as a light moves infinitely far away: why the sky depends only on direction.
- Blending by $\sin(\text{elevation})$ vs by elevation angle: the two gradients side by side.
