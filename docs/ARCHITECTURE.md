# OPEN ROAD architecture — Phase 0

**Status:** small Godot prototype. The scene and systems below are source-level intent until the Godot CI import/test/export and Windows smoke job pass. The target-PC performance gate is separate and remains unmeasured.

## Engine and cost

- **Godot 4.7.2**, standard (non-Mono) build, GDScript, Windows x64 export.
- Renderer: **GL Compatibility**, chosen for broad older-GPU support. The Windows export uses the official matching Godot export template and embeds the project data in one executable.
- Godot Engine is MIT-licensed. The project has no Unity dependency, Unity account, activation credential, paid engine plan, or paid content dependency. See `GODOT_ENGINE_LICENSE.txt` and `docs/ASSET_LICENSES.md`.

## Scene and systems

`scenes/main.tscn` contains a full-window `Control` with the Phase 0 script. The script creates its test world at runtime:

- A 1 km straight road, ground plane, simple roadside buildings, lamps, and dashed markings.
- An unbranded procedural bike fixture and a kinematic W/S/A/D ride controller.
- Exactly 50 deterministically seeded moving car fixtures. The body and canopy meshes are batched with `MultiMeshInstance3D`; this is not traffic AI.
- One player camera with chase/cockpit positions.
- Two low-resolution mirror `SubViewport`s sharing the world. Their cameras reflect the player camera across each mirror plane; the mirrors update on alternate frames and can be disabled.
- A dedicated world viewport, drawn through a canvas-item shader that remaps horizontal image coordinates for the fisheye-style lens. The HUD is drawn above the processed image and is not warped.
- A text HUD and an opt-in benchmark that records frame samples, frame-time percentiles, a histogram, and render-scale range.

The bike is explicitly a visual placeholder. This spike does not implement motorcycle physics, a rider, production art, or a finished game loop.

## Rendering and frame budget

The main camera renders to a `SubViewport`. Its texture is scaled to the window and sampled through `scripts/horizontal_fisheye.gdshader`; only horizontal UVs are warped. At the start of a benchmark, the program requests a 1920×1080 output and a 0.80 world-viewport scale. Every half-second, a simple frame-time rule adjusts that scale within 0.65–1.00. This controller is experimental; it has not been shown to meet the performance target on the owner's GPU.

Each mirror is 256×128. The mirror camera cull mask excludes the mirror display surfaces to prevent recursive mirror rendering. Disabling mirrors stops mirror updates. No claim is made about exact GPU memory allocation or actual mirror cost until measured on the target computer.

## Tests and build pipeline

`.github/workflows/godot-ci.yml` is intended to:

1. Download Godot 4.7.2 and the matching official export templates.
2. Import the project headlessly, run the project's math/scene checks, and run a startup smoke check.
3. Export a standalone Windows x64 executable with embedded project data.
4. Run that executable with `--headless -- --smoke` on a Windows runner and upload the verified executable.
5. Publish a GitHub Release for `v*` tags only after the build and Windows smoke test succeed.

This CI requires no project-specific secret. A CI smoke test is not a visual review, real GPU performance measurement, or target-PC certification. See `docs/PHASE0_REPORT.md` for the actual latest run status.

## Planned boundaries after Phase 0

If this prototype passes its initial checks, later work should separate bike dynamics, rider animation, traffic behavior, lens/camera, crash response, world streaming, audio, weather, settings, and UI into independently tested systems. Any later third-party asset needs a verified license and attribution before it is added. Keep the existing acceptance target visible: 40 average FPS and 30 FPS 1% low on the specified target PC, with a regression greater than 10% blocking further progression.
