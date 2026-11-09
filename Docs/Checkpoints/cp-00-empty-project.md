# cp-00-empty-project

Goal: an empty Unity project to build the spectral path tracer from.

Scene: `Assets/SpectralPathTracer/Scenes/Canvas.unity`. Environment: Unity 6000.5.3f1, URP 17.5, linear
color space, macOS (Metal).

## What changed

The URP template's tutorial content (`TutorialInfo/`, `Readme.asset`, the Hub editor script) and the sample
scene are gone. So are four packages the project doesn't use: AI Navigation, Visual Scripting, Multiplayer
Center and Collab Proxy.

The Mobile URP asset and its renderer were deleted, which leaves `PC` as the only quality level and the
default on every platform.

The code lives under `Assets/SpectralPathTracer/` in one namespace, `SpectralPathTracer`, split into three
assemblies: `Runtime/SpectralPathTracer.asmdef`, `Editor/SpectralPathTracer.Editor.asmdef` (Editor only)
and `Tests/EditMode/SpectralPathTracer.Tests.asmdef` (Editor only, NUnit).

`Scenes/Canvas.unity` holds a camera and a directional light and is the only scene in Build Settings.

## Shots to capture

- [ ] The empty Project window and an empty Game view.
