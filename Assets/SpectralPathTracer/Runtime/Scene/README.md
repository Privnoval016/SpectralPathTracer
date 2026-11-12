# Scene system

Traced objects are components on ordinary GameObjects. They never render through Unity's meshes. Instead they register
themselves, and once per frame `SceneCompiler` turns them into flat GPU buffers that Intersect and Shade read.

```mermaid
flowchart LR
    subgraph EDIT["In the scene"]
        shapes["TracedSphere, TracedPlane, TracedQuad<br/>components on GameObjects"]
        mats["SpectralMaterial assets<br/>albedo, emission,<br/>optional measured spectra"]
        sky["Sky component<br/>zenith, horizon, ground colors"]
    end

    reg["TracedRegistry<br/>enabled shapes and skies,<br/>Version bumps on shape add, remove or edit"]

    subgraph FRAME["Every frame"]
        comp["SceneCompiler.TryUpdate<br/>Version or any transform changed?<br/>then rebuild and upload"]
        pick["SceneCompiler<br/>picks the one enabled sky"]
    end

    subgraph GPU["GPU buffers"]
        objs["objects<br/>Quadric, WorldToLocal, Type, MaterialIndex,<br/>LightId, MediumIn, MediumOut"]
        mtab["materials<br/>sigmoid coefficients, Albedo, Emission, Type,<br/>measured table indices"]
        stab["spectra<br/>measured spectra, 95 values each"]
    end

    shapes -- "OnEnable, OnDisable, OnValidate" --> reg
    mats -- "OnValidate" --> reg
    sky --> reg
    reg --> comp
    reg --> pick
    comp --> objs
    comp --> mtab
    comp -- "resampled to 5 nm" --> stab
    objs -- "SceneBuffers.Bind" --> kernels["Intersect and Shade"]
    mtab -- "SceneBuffers.Bind" --> kernels
    stab -- "SceneBuffers.Bind" --> kernels
    pick -- "sky colors and spectra in the frame constants" --> kernels
    comp -- "rebuilt" --> reset["accumulation restarts"]
    pick -- "colors changed" --> reset
```

| Piece | Role |
|---|---|
| `TracedShape` | Base component. Registers itself, holds the material, and answers `WorldQuadric` and `WorldToShape`, so the compiler never checks shape types. |
| `TracedQuadric` | Spheres and planes. Turns a local $Q$ into world space with $M^{-\top} Q M^{-1}$. |
| `TracedQuad` | Divides by its `Size` after `worldToLocal`, so the GPU always tests the unit square. |
| `TracedRegistry` | Static, data only. Moving a transform doesn't bump `Version`, which is why the compiler also compares matrices. |
| `SceneCompiler` | Rebuilds only when something changed, deduplicates materials into one table, and picks the sky, comparing its colors every frame since a color edit bumps no version. Every color also becomes sigmoid coefficients through `SigmoidTable`, and measured spectra are resampled into the spectrum buffer. |
| `GrowableBuffer<T>` | A persistent structured buffer that doubles its capacity when an upload outgrows it. |
| `SceneBuffers` | The tables imported for one frame. `Declare` adds the reads to a pass and `Bind` sets every table that `SceneData.hlsl` declares. |
| `GpuData/` | The GPU records. `LightId` and the medium ids are -1 until the light table and media arrive; a material's table indices are -1 unless it has a measured spectrum. |

Emission is front-face only, and a sky with black colors turns the open Cornell box into the classic closed look.
