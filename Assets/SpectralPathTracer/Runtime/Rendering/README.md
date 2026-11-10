# Runtime control flow

`PathTracerPass` records one frame into URP's render graph. The CPU only decides what runs. Every stage below is a
compute kernel on the GPU, and each kernel runs one thread per path (or per pixel).

```mermaid
flowchart TD
    subgraph CPU["CPU, once per frame (PathTracerPass)"]
        scene["SceneCompiler.Update<br/>rebuild GPU scene buffers if anything changed"]
        reset["Accumulation check<br/>restart the sums if the image would change"]
        decide{"accumulating?<br/>not paused, below the sample target"}
        scene --> reset --> decide
    end

    subgraph WAVE["GPU, for each wave of up to 2^18 paths"]
        gen["Generate<br/>camera ray and path state per slot,<br/>queue A = every slot"]
        isect["Intersect<br/>nearest hit for each queued path"]
        shade["Shade<br/>add light, sample the bounce,<br/>Russian roulette from depth 1,<br/>append survivors to the other queue"]
        more{"another bounce?"}
        args["Args<br/>survivor count becomes the next dispatch size"]
        resolve["Resolve<br/>add every path's radiance to its pixel's sum"]
        gen --> isect --> shade --> more
        more -- yes --> args --> isect
        more -- no --> resolve
    end

    decide -- yes --> gen
    resolve --> display
    decide -- no --> display
    display["Display<br/>sum / samples, exposure, clamp"] --> present["Present<br/>letterboxed blit to the camera"]
```

| Stage | Threads | Reads | Writes |
|---|---|---|---|
| Generate | one per slot in the wave | camera, sampler | rays, path state, queue A, counters, dispatch args |
| Intersect | one per queued path (indirect) | rays, queue, scene objects | hits |
| Shade | one per queued path (indirect) | hits, rays, path state, scene, materials | path state, rays, the other queue |
| Args | one | counters | dispatch args, counters |
| Resolve | one per slot in the wave | path state | accumulation sums |
| Display | one per pixel | accumulation sums | display texture |
| Present | raster blit | display texture | camera target |

## How the parallel parts fit

- **Waves.** Path memory is sized for $2^{18}$ slots, not for the image. A frame runs as many waves as it needs to give every
  pixel one sample (two at Edit Mode's 960 by 540).
- **Queues.** Paths die at different bounces (escaping, or stopped by Russian roulette). Shade appends the survivors to a fresh queue with an atomic add, so the next
  Intersect and Shade only launch threads for live paths. Two queues swap roles every bounce so no kernel reads and appends
  the same list.
- **Indirect dispatch.** The survivor count only exists on the GPU. The one-thread Args kernel turns it into the next
  dispatch size, so the CPU never waits for the GPU between bounces.
- **Accumulation.** Resolve adds each sample to a per-pixel sum, and Display divides by the sample count. Anything that
  changes the image (camera, scene, bounces, AOV) restarts the sums; exposure doesn't, because it applies after averaging.

The checkpoint devlogs in `Docs/Checkpoints` explain why each piece is built this way.
