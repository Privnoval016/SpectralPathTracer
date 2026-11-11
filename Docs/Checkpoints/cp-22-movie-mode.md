# cp-22-movie-mode

Goal. The footage engine for the rest of the video. A Timeline animates the scene, and movie mode renders it one
movie frame at a time, accumulating each to a fixed sample count and saving it as a PNG, so slow converging shots and
parameter sweeps can be filmed while Unity sits in the background.

Scene `Assets/SpectralPathTracer/Scenes/Canvas.unity`. Movie mode is a `Tools` component, `MovieMode`, used in Play
Mode. The steps for making a movie, with a sequence diagram, are in `Assets/SpectralPathTracer/Tools/README.md`.

## What changed

- `MovieMode` steps a PlayableDirector by hand. Each movie frame sets the Timeline's time to frame / fps, evaluates
  it, asks for an accumulation restart, and waits until a fresh progress report says the frame has N samples. Then it
  asks for a capture, writes `Recordings/<timeline>/frame_NNNN.png`, and steps to the next frame from the capture
  callback. It turns `runInBackground` on while it runs and restores it after.
- `PathTracerOverrides` lets a tool override the target samples and the frame budget, and request a restart, without
  writing the settings asset. The renderer reads the target and budget through two properties that prefer the override.
- `AccumulationProgress` counts its reports, so a reader can tell a report made after its change from one made before.
- `DisplayCapture` records a one-shot unsafe pass after Display when a tool asks. It reads the display texture back
  asynchronously and converts the linear halves to 8-bit sRGB on the CPU.
- `Recordings/` is ignored by git.

## Decisions

**[Learn] Why can't movie mode just use Unity Recorder?** It saves one image per Unity frame at a fixed rate, and one
of our movie frames spans many Unity frames (128 at 1080p and 256 samples). It would save 128 noisy partial images per
frame. Its sub-frame accumulation only works through HDRP's path tracer. The answer given was that encoding is too
slow, but one PNG takes tens of milliseconds next to seconds of rendering.

**[Learn] What should movie mode do with Frame Budget?** Raise it. Focused, 33 ms already hits the 16-wave cap at
1080p, so raising changes nothing there. In the background the Editor paces frames about 100 ms apart, and the
scheduler only grows past 1 wave per frame once the budget is at least 200 ms (see the math). The cap still bounds the
worst frame, and the movie default is 250 ms so a heavy scene's frames stay near a quarter second. Right. Budget 0 was
the weakest option (one sample per frame, or per 100 ms in the background).

**Why not set the budget as low as possible?** (asked during the round) Every frame pays fixed costs that don't shrink
with the budget (recording the graph, the scene check, Display, Present, the Editor's frame loop), so one wave per
frame leaves the GPU idle. At a 16 ms floor that would be about 7.5 samples per second at 1080p against the 70 the
budget reaches. The budget trades responsiveness for throughput, and a movie only wants throughput.

**[Decide] How does a movie describe what changes over time?** A Timeline stepped by hand, so camera paths and
component fields are keyframed in Unity's own editor and every frame lands on exact Timeline time. Rejected were
keyframes on the component (new code for every kind of parameter, no curve editor) and a C# callback per frame
(every movie becomes a script).

**[Decide] Play Mode or Edit Mode?** Play Mode, which renders at the full 1920 by 1080 instead of 960 by 540 and ticks
every frame. `runInBackground` keeps it going unfocused. Scene edits made during a movie are lost, which is fine for
filming.

**[Decide] How does every saved frame get exactly N samples?** Restart on every step. The first test inherited 93
samples from before the movie began, because a step that leaves the camera where it was changes nothing the renderer
watches. Restarting only at the start would be cheaper on still stretches but would keep stale samples whenever a
change goes unnoticed, and accepting only a count of exactly N would wait forever at 93.

**[Learn] What does a PNG of the raw display values look like?** Darker midtones. The display holds linear light, and
Present writes it into an sRGB target that applies the sRGB curve on write, so linear 0.214 shows as 128. Stored raw
it becomes 55, and viewers read PNG bytes as sRGB. Black and white stay put. The answer given was washed out, which is
the opposite mistake of encoding twice (128 becoming 187).

**[Learn] Request a capture and step in the same Update. What gets saved?** The next frame, barely started. The render
that records the readback is the same one that takes the restart, and the readback copies the texture right after
that frame's Display. The answer given was whatever the texture holds when the callback fires, but `AsyncGPUReadback`
copies into its own staging memory when the GPU reaches the request, and the callback only delivers that copy. So the
movie steps from the callback.

**[Decide] Which file format?** PNG, 8-bit sRGB, exactly what the Game view shows, read by ffmpeg and every editor.
EXR of the display would hold no extra range because Display clamps to [0, 1]; EXR of the pre-clamp sums fits with
tone mapping at cp-55.

**[Decide] Who records the readback?** The renderer, on request, since the display is a transient render graph texture
only code inside the graph can read. A frame without a request adds no pass. Rejected were a persistent copy every
frame (16 MB and a copy per frame for a capture every few seconds) and `ScreenCapture` (window size, letterbox
included).

## The math

Movie frame $i$ shows Timeline time $t_i = i / f$ for $i = 0, \dots, \lfloor D f \rfloor$, so both ends of a sweep are
rendered. The Timeline reported $D = 0.999999999999$ for a 1 second clip; the cast to float rounds it to 1 before the
floor.

In the background each frame takes about 100 ms whatever the work, so the smoothed cost per wave reads
$\bar m \approx 100 / W$. The scheduler can double only when

$$\left\lfloor \frac{B}{100 / W} \right\rfloor \ge 2W \quad\Longleftrightarrow\quad B \ge 200 \text{ ms}.$$

The display is linear and a PNG holds sRGB:

$$c_{\text{sRGB}} = \begin{cases} 12.92\,c & c \le 0.0031308 \\ 1.055\,c^{1/2.4} - 0.055 & \text{otherwise} \end{cases}$$

The sampler has no per-frame seed, so every movie frame draws the same random numbers. Noise stays put between frames
of a parameter sweep instead of boiling, and only the change being filmed moves.

## Verification

- A 1 second camera slide at 4 frames per second and 16 samples per frame saved 5 frames. Started on top of 212
  already accumulated samples, every frame still had exactly 16. Afterwards the overrides and `runInBackground` were
  cleared, the renderer went on to its own 1024 target at the 33 ms budget, and the settings asset was untouched.
- The capture path and a Game view capture of the same 16-sample image agree in all but 804 of 6.2 million channels,
  and those differ by 1 (rounding). Rows are the right way up.
- Restarting at the last pose reproduced movie frame 5 byte for byte.
- In the background with the 250 ms budget, the movie's 640 waves finished in under about 90 frames. At 33 ms the
  same setup runs 1 wave per frame.

## Problems hit and fixes

- Frame 1 inherited 93 samples from before the movie. Every step now requests a restart.
- In the frame that steps, `AccumulationProgress` still describes the previous movie frame, and in the first Play
  Mode `Update` nothing has rendered yet. The movie remembers the report count at each step and only trusts newer
  reports.
- A test clip that animated the Animator's own transform did nothing, because Unity treats that as root motion. Bound
  to a parent of the camera it worked. Keyframes recorded in the Timeline window don't have this problem.
- Later frames of the test were smaller files because the camera slid past the box's open side into black.

## Shots to capture

- [ ] A Cornell box camera move rendered by movie mode at 256 samples, played back as a video.
- [ ] A raw linear PNG next to the sRGB one, the midtones visibly sinking.
- [ ] Frames appearing in the Recordings folder while Unity sits in the background.

## Explainers to build (Manim)

- One Unity frame as a strip (Update, render, readback), showing why a step in the same Update saves the wrong frame.
- The sRGB curve, with 0.214 mapping to 0.5 and the raw value landing at 55.
- The background pace: $100 / W$ per wave, and the budget line at 200 ms that lets the wave count climb.
