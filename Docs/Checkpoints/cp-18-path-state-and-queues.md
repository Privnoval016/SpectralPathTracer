# cp-18-path-state-and-queues

Goal. A structural checkpoint with no video and an identical image as its check. The path state layout is frozen with
every field the later chapters need, the frame's pixels run in fixed-size waves of path slots, and each bounce dispatches
only the paths still alive, sized on the GPU by indirect dispatch.

Windows (DX12) check is pending; it will be run later on the user's PC.

## What changed

- `GpuPathState` is frozen at 128 bytes. Radiance and throughput became float4 lanes (RGB mode uses r, g, b, 0), and
  wavelengths, wavelength pdfs, time, two MIS accumulators, the pixel index, flags and the medium id are reserved.
  `PathFlags` (`Active` so far) replaces the old active field, read through `IsActive` and `Finish`.
- Waves of $2^{18}$ path slots. Each frame runs as many waves as the image needs (two at Edit Mode's 960 by 540). Generate is
  one-dimensional, mapping slot i to pixel `WaveStart + i`, and Resolve finds its pixel through `path.PixelIndex`.
- Two queues of live path slots per wave, read and appended in turn. Generate seeds queue A with every slot, Shade appends
  survivors with an atomic add, and a new one-thread `Args` kernel turns the appended count into the next dispatch size and
  empties the queue just read. Intersect and Shade use indirect dispatch and check the live count on the GPU. Resolve still
  runs over every slot.
- Counters and dispatch arguments live in raw buffers (`RWByteAddressBuffer`), which DX12 accepts for indirect arguments and
  atomics.

## Decisions

**[Learn] Why do dead paths waste time even with an early return?** Threads run in lockstep groups of 32. A group with one
live path runs the full intersection loop with its dead lanes idle, and every dead thread still gets launched, so at 30%
alive the GPU pays nearly full cost for a third of the work. Rejected were "it doesn't" (the return is cheap, the idle lanes
aren't) and "returns are slow" (branching isn't the cost).

**[Learn] How does each appending thread get its own slot?** An atomic add, which returns the counter's value from before the
add, unique to that thread, so survivors pack densely. Rejected were using the thread ID (leaves holes, so nothing is
compacted) and reading then writing plus one (a race in which two threads take the same slot and a path is lost).

**[Learn] Why not read the count back on the CPU?** It stalls the pipeline. The CPU would wait for the GPU after every bounce,
so they stop overlapping. Indirect dispatch reads the count on the GPU. Rejected were "the CPU can't read GPU buffers" (it
can, slowly) and "it would be fine" (the wait for all prior work is the cost, four or more times a frame).

**[Decide] When do waves arrive?** Now, with the layout, so the queues and the frozen struct never need restructuring.
Rejected was waiting for BDPT.

**[Learn] Why float4 lanes even in RGB mode?** One struct serves both modes (R2). Spectral mode carries four wavelengths per
path, and RGB mode uses the same lanes as (r, g, b, 0), so cp-24 changes types, not layouts. Rejected were alignment only and
an alpha channel.

**[Learn] Which layout wastes less bandwidth when a kernel reads only one field?** Struct of arrays. The answer given was array
of structs. Memory moves in blocks of 32 to 128 bytes, so 32 flags stored together cost one 128-byte block, while flags 128
bytes apart cost 32 blocks (4096 bytes moved for 128 used). "Same either way" assumes the GPU fetches single fields.

**[Decide] How is path state stored?** As an array of structs. With queues, Intersect no longer reads path state, and Shade
reads nearly every field, which whole structs serve well. The accessors keep a later switch to struct of arrays possible.

**[Decide] How are waves scheduled?** All waves every frame, so each frame adds one sample to every pixel. cp-21's time budget
can spread them later.

**[Learn] What bound do indirect kernels check?** The live count, read from the GPU counter buffer. Without a check, threads
past the count read stale queue entries; checking the wave size reads the same stale entries further out and shades some
paths twice.

**[Learn] Why two queues?** Reading and appending one queue in the same dispatch races. Groups run in no fixed order, so a
survivor can overwrite an entry that hasn't been read yet, skipping one path and shading another twice. The answer given was
speed only. It is a correctness problem, and an intermittent one.

**[Learn] Which paths does Resolve process?** Every slot in the wave. Every sample has to reach its pixel, and the first sample
after a reset overwrites the old sum, so a skipped pixel would keep stale light. The answer given was only finished paths,
which matches today only because the last bounce finishes every path, and would silently break if Russian roulette or a
time budget ever left one unfinished. The last alive queue is empty after the final bounce.

**[Decide] Who resets the counters?** The Args kernel, so everything stays on the GPU.

## The math

Exact check without noise. Between two emissive infinite planes facing each other (emission 1, albedo 0.5), every path hits a
plane on every bounce, so every pixel equals $\sum_{k=0}^{N} 0.5^k$. Measured with the queue pipeline at one sample: 1.0006, 1.5050,
1.7586, 1.8711 and 1.9406 against 1, 1.5, 1.75, 1.875 and 1.9375 for $N = 0$ to 4 (8-bit capture rounding).

Timing at 960 by 540 and four bounces fell from about 5.6 to 6.3 ms to about 3.1 to 3.4 ms per frame.

## Problems hit and fixes

- The first image comparison showed 6158 brighter pixels and no darker ones. Runs repeated exactly, one bounce matched the
  expected hit rate, and the furnace matched to three decimals, so the pipeline was right. The cause was the exposure,
  which had been changed between the baseline (-3) and the second capture (0.4). At one sample most pixels are black, so an
  exposure change only shows on the lit ones. Comparisons now pin every setting that affects pixels.
- Shade's append first declared `uint position` inside the block that also used the outer `float3 position` hit point.
  It is renamed `queuePosition`; reusing an outer name invites the wrong variable.
- The camera ray test dispatches Generate directly, so it now binds the queue, counter and argument buffers too (the cp-15
  lesson that unbound writable buffers can make a dispatch do nothing).
- "Blitter is already initialized" came back after the renderer rebuild and blanked captures; the reset fixed it.

## Shots to capture

None; this checkpoint is structural. A frame-time overlay before and after compaction could be B-roll for cp-21.

## Explainers to build (Manim)

- Warps in lockstep, idle lanes, and compaction packing live paths together.
- An atomic append, and the race a single queue would have.
- Two queues swapping roles each bounce, with the args kernel sizing the next dispatch.
