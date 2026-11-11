# cp-21-fly-camera-and-stats

Goal. Tooling for the rest of the project. A camera you can fly in Play Mode and frame shots with from the Scene view,
numbers that show how many paths survive each bounce and how long a frame takes, and a per-frame time budget so heavy
scenes stay responsive and never trip the macOS watchdog.

Scene `Assets/SpectralPathTracer/Scenes/Canvas.unity` (FlyCamera on the Main Camera), settings
`Settings/PathTracerSettings.asset` (Frame Budget 33 ms, under Target Samples). The stats sit under Samples in the
settings inspector.

## What changed

- Tools live in their own assemblies under `Tools/`, so the core runtime never depends on the Input System.
  `FlyCamera` flies the camera in Play Mode from `FlyCamera.inputactions` (WASD or left stick, Q and E, mouse or right
  stick look while Space or the right mouse button is held, Shift to boost, scroll for speed). A Scene view overlay,
  "Follow Scene View", makes the Main Camera copy the Scene view in Edit Mode, writing only when something changed.
- Args adds each bounce's survivor count to a small raw buffer. `LivePathStats` clears it, and an unsafe render graph
  pass reads it back asynchronously into `RenderStats`. The inspector lists live paths per depth with their share of
  depth 0.
- Each path carries its own sample index in `GpuPathState` (the spare uint, still 128 bytes), so one frame can hold waves
  of two samples. `FrameConstants` dropped `_SampleIndex` and notes every field's byte offset.
- `WaveScheduler` keeps a cursor into the current sample and runs as many waves as fit in Frame Budget (ms), from a
  smoothed milliseconds-per-wave estimate. A sample can finish across frames, or several can finish in one. Budget 0
  keeps one full sample per frame. The inspector shows the frame time and wave count.

## Decisions

**[Learn] Why can't a C# Stopwatch around `RecordRenderGraph` measure the GPU?** The GPU runs later. Recording only
describes the work, and the GPU executes it after the CPU has moved on.

**[Learn] How should C# read the GPU's counts every frame?** AsyncGPUReadback. `GetData` makes the CPU wait for the GPU,
and a shader can't hand numbers to C#.

**[Decide] What gives when a frame would run past the budget?** Spread waves across frames. Lower resolution and fewer
bounces were rejected because they change the image; spreading waves only changes how long it takes.

**[Decide] Where does the fly camera work?** Play Mode, plus Edit Mode following the Scene view. Edit Mode already has
good navigation in the Scene view, so the Game camera copies it instead of reinventing it.

**[Decide] How does the fly camera read input?** An `.inputactions` asset, so bindings for keyboard, mouse and gamepad
live in one editable place. Rejected were action fields on the component and polling devices directly.

**[Decide] Where does the follow switch live?** A Scene view overlay, stored per user in `UserSettings`. A field on
`PathTracerSettings` would put an editing preference into render settings.

**[Learn] Which of movement and mouse look are multiplied by `Time.deltaTime`?** Movement only. A mouse delta is already
this frame's distance, so scaling it by frame time makes looking slower at high frame rates. A stick, like movement, is a
rate. The answer given was both.

**[Learn] Why clamp pitch to about $\pm 89^\circ$?** At $90^\circ$ forward lines up with world up, and yaw no longer has
a defined direction, so the view flips. Right.

**[Decide] When is mouse look active?** While a button is held (Space or right mouse), since a trackpad can't hold a
right click while moving comfortably.

**[Decide] Where do camera tools and movie mode live?** In separate `Tools` assemblies. The renderer shouldn't know how
a camera gets moved, and keeping the Input System out of the core keeps the core testable without it.

**[Learn] Where should the per-bounce counts come from?** Args, which already loads the survivor count to size the next
dispatch, so recording it costs one atomic add per bounce per wave. The answer given was Shade, counting every
survivor. That is about 150,000 atomics on one address per wave at depth 0, all to recompute a number Args holds.

**[Learn] When can the CPU use frame N's readback?** A frame or more later, once the GPU has finished frame N. Right.

**[Decide] What does the budget measure?** Smoothed frame time, which every platform has and which is what the watchdog
and responsiveness care about. Rejected were a readback round trip (quantized to when callbacks are polled) and a cost
model (its calibration drifts with the scene).

**[Decide] Where are the stats shown?** The settings inspector, which already repaints while accumulating. A Game view
overlay for filming can come later in `Tools`.

**[Learn] Why is a wave the smallest unit the budget schedules?** Its rays, paths and queues are transient render graph
buffers that only exist for one frame's graph. The answer given was that GPUs can't pause, which is true of one dispatch,
but a wave is about 50 dispatches and the budget only chooses how many to record.

**[Decide] How does a path learn its sample index when a frame spans two samples?** The path carries it. BDPT will pair
subpaths by sample anyway. Rejected were per-wave constant buffer slices (256-byte offset alignment, a bigger upload)
and never crossing a sample boundary (a cheap scene could never do more than one sample per frame).

**[Learn] Why does the image stay correct while early waves' pixels have one more sample?** Display divides each pixel
by its own count in w. Right. Dividing by one global count would show a band at $8/7$ of the true brightness, or twice
it right after a reset.

**[Decide] What sets the target?** One Frame Budget (ms) setting, default 33, with 0 meaning one full sample per frame.

**[Learn] What should the scheduler smooth?** Milliseconds per wave, which makes frames with different wave counts
comparable. The answer given was waves per frame, a controller with no model of cost that has to creep toward the
right count. Smoothing frame time and dividing by the latest count mixes frames, for example $25/4 = 6.25$ ms against a
true 10.

**[Learn] Why cap growth at twice the previous frame?** The frame time read now belongs to work from one to three frames
ago, so an uncapped jump overshoots before the slow frames show up. Right.

**[Decide] What happens when Unity isn't focused?** The scheduler stays as it is. A "grow until it costs" hill climb was
designed and dropped, because at the wave cap it can't tell a throttled Editor from real cost and ends up cycling.
Budget 0 is the way to accumulate in the background.

## The math

$$\bar m_{t} = \bar m_{t-1} + 0.1\,\left(\frac{T_t}{W_{t-1}} - \bar m_{t-1}\right), \qquad
W_{t} = \min\!\left(16,\ 2 W_{t-1},\ \max\!\left(1, \left\lfloor \frac{B}{\bar m_{t}} \right\rfloor\right)\right)$$

with $T_t$ the time since the last traced frame, $W$ the waves per frame and $B$ the budget.

Live paths per depth in the Cornell box at 960 by 540 and 16 bounces: 518,400, 272,493, 79,259, 41,222, 21,753, and so on
down to 20 at depth 16. Depth 0 is exactly $960 \times 540$. Depth 1 matches the 272,479 hit pixels of the normals AOV
(52.56% either way; the gap is jitter). From depth 2 on, roughly 53% survive each bounce, about the walls' albedo minus
what escapes through the open front.

| Mode | Frame time | Waves per frame |
|---|---|---|
| Edit Mode, focused, 960 by 540 | 31 to 32 ms | 15 to 16 (about 250 samples per second) |
| Edit Mode, Unity in the background | about 100 ms (the Editor's own pace) | 1 |
| Play Mode, focused, 1920 by 1080 | 28 to 29 ms | 16, the cap |
| Play Mode, Unity in the background | doesn't run (`runInBackground` is off) | none |

A 16-bounce frame of two waves costs about 7 ms of GPU time when timed directly.

## Problems hit and fixes

- Unity reports no GPU recorder on this Mac (`SystemInfo.supportsGpuRecorder` is false), so GPU time can't be read
  directly and frame time stands in for it.
- With Unity in the background the Editor paces frames about 100 ms apart whatever the work, so the budget reads 100 ms
  per wave and runs 1 wave per frame, half of budget 0's throughput. Measured focused, it settles at the budget.
- Moving the sample index into the path had to stay byte-identical. It does at 1 sample (beauty, normals and four AOVs)
  and at 8 samples, and 8 samples at a 33 ms budget match 8 samples at budget 0 byte for byte, since each pixel still
  gets samples 0 to 7 in the same order. A single Game view capture is not deterministic past one sample, so the check
  renders until the count reaches 8 before capturing.
- The stats files first lacked usings and a namespace, `RecordClear` returned the raw buffer instead of the graph
  handle, and `LivePathStats` had no `Dispose`. The scheduler declared a `double` it assigned to a `float` property.
- The readback pass writes nothing the graph can see, so it needs `AllowPassCulling(false)` or it is silently culled.

## Shots to capture

- [ ] Flying through the Cornell box in Play Mode as samples climb.
- [ ] The inspector's live paths per depth while accumulating, next to the image.
- [ ] Frame Budget 0 against 33 in a heavy scene, with the Editor staying responsive.

## Explainers to build (Manim)

- The queue shrinking bounce by bounce, with the real counts from the Cornell box.
- The wave cursor sweeping over the image, wrapping into the next sample, several samples per frame when cheap.
- A delayed measurement making an uncapped controller overshoot, and the twofold cap taming it.
