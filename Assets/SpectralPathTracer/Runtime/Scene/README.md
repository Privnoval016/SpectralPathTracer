# Scene system

Traced objects are components on ordinary GameObjects. They never render through Unity's meshes. Instead they register
themselves, and once per frame `SceneCompiler` turns them into flat GPU buffers that Generate, Intersect and Shade read.

```mermaid
flowchart LR
    subgraph EDIT["In the scene"]
        shapes["TracedSphere, TracedPlane, TracedQuad<br/>components on GameObjects"]
        mats["SpectralMaterial assets<br/>albedo, emission,<br/>optional measured spectra"]
        sky["Sky component<br/>zenith, horizon, ground colors"]
        emit["Emitter component<br/>on a shape: blackbody, measured<br/>or color light at a luminance"]
    end

    reg["TracedRegistry<br/>enabled shapes and skies,<br/>Version bumps on shape add, remove or edit"]

    subgraph FRAME["SceneCompiler.TryUpdate, every frame, in order"]
        skyp["SceneSky<br/>picks the one enabled sky,<br/>refits its spectra when its colors change"]
        changes["SceneChanges<br/>Version, any transform or<br/>any EmitterState changed?"]
        build["SceneCompiler.Rebuild<br/>one record per shape"]
        mattab["MaterialTable<br/>deduplicates materials, an emitter<br/>gets its own entry, resamples spectra"]
        wave["WavelengthTable<br/>eye weights mixed with the scene's light<br/>by Light Share, as a CDF"]
    end

    subgraph GPU["GPU buffers"]
        objs["objects<br/>Quadric, WorldToLocal, Type, MaterialIndex,<br/>LightId, MediumIn, MediumOut"]
        mtab["materials<br/>sigmoid coefficients, Albedo, Emission, Type,<br/>measured table indices"]
        stab["spectra<br/>measured and blackbody spectra,<br/>95 values each"]
        cdf["wavelength CDF<br/>471 values from 0 to 1"]
    end

    shapes -- "OnEnable, OnDisable, OnValidate" --> reg
    mats -- "OnValidate" --> reg
    sky --> reg
    emit -- "OnEnable, OnDisable, OnValidate" --> reg
    emit -- "values compared every frame" --> changes
    reg --> skyp
    reg --> changes
    changes -- "changed" --> build
    build -- "IndexFor(shape)" --> mattab
    build --> objs
    mattab --> mtab
    mattab -- "resampled to 5 nm" --> stab
    mattab -- "emission at each nm" --> wave
    skyp -- "radiance at each nm" --> wave
    wave --> cdf
    objs -- "SceneBuffers.Bind" --> kernels["Generate, Intersect and Shade"]
    mtab -- "SceneBuffers.Bind" --> kernels
    stab -- "SceneBuffers.Bind" --> kernels
    cdf -- "SceneBuffers.Bind" --> kernels
    skyp -- "sky colors and spectra in the frame constants" --> kernels
    build -- "rebuilt" --> reset["accumulation restarts"]
    skyp -- "colors changed" --> reset
    wave -- "new distribution" --> reset
```

| Piece | Role |
|---|---|
| `TracedShape` | Base component. Registers itself, holds the material, and answers `WorldQuadric` and `WorldToShape`, so the compiler never checks shape types. |
| `TracedQuadric` | Spheres and planes. Turns a local $Q$ into world space with $M^{-\top} Q M^{-1}$. |
| `TracedQuad` | Divides by its `Size` after `worldToLocal`, so the GPU always tests the unit square. |
| `Emitter` | Replaces its shape's material emission with a blackbody at a temperature, a measured spectrum or a color, all at luminance $Y$ = Strength. A component, so Timeline can animate it. |
| `EmitterState` | Every value an emitter's light depends on, as an equatable struct. Timeline writes fields without `OnValidate`, so the compiler compares these every frame, like transforms. |
| `TracedRegistry` | Static, data only. Moving a transform doesn't bump `Version`, which is why the compiler also compares matrices. |
| `SceneCompiler` | Runs the parts below in order each frame, builds one object record per shape, and imports every table for the render graph. |
| `SceneChanges` | Remembers the last build's version, transforms and emitter states, compared exactly, so a slow animation never skips a rebuild. |
| `MaterialTable` | Deduplicates materials into one table (a shape with an emitter gets its own entry). Every color becomes sigmoid coefficients through `SigmoidTable`, measured and blackbody spectra are resampled into the spectrum buffer, and `EmissionAt` evaluates any entry's light on the CPU the way Shade does. |
| `SceneSky` | Picks the one enabled sky, comparing its colors every frame since a color edit bumps no version, and refits their spectra when they change. |
| `WavelengthTable` | The wavelength distribution spectral paths draw from: the eye's weights mixed with every shape's emission and the sky by Light Share, uploaded as a CDF. Rebuilt only when the light or the share changes. |
| `GrowableBuffer<T>` | A persistent structured buffer that doubles its capacity when an upload outgrows it. |
| `SceneBuffers` | The tables imported for one frame. `Declare` adds the reads to a pass and `Bind` sets every table that `SceneData.hlsl` declares. |
| `GpuData/` | The GPU records. `LightId` and the medium ids are -1 until the light table and media arrive; a material's table indices are -1 unless it has a measured spectrum. |

Emission is front-face only, and a sky with black colors turns the open Cornell box into the classic closed look.
