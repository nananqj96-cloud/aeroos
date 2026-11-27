# Phase 0 milestone report — Godot prototype

**Date:** 2026-11-27
**Status:** Godot source/configuration has been drafted and passed GDScript parser/linter checks in this environment. Engine import, runtime tests, Windows export, and Windows launch still require a successful GitHub Actions run. Performance acceptance is not passed.

## Included in source

- Godot 4.7.2 project using GDScript and the GL Compatibility renderer.
- Runtime-built 1 km road fixture, simple bike geometry, 50 deterministic moving traffic fixtures, two mirror viewports, a horizontal lens shader, basic controls, and a text HUD.
- Benchmark command, per-frame CSV, summary statistics, percentile-based low-FPS estimates, frame-time histogram, and bounded render-scale controller.
- Godot CI workflow intended to import and test the project, export one Windows x64 `.exe`, launch it headlessly on Windows, and upload the verified artifact.
- Readme, architecture notes, benchmark instructions, performance goals, and a separate Godot Engine MIT license notice.

These are source-level features until the engine workflow validates them. The bike and environment are placeholders, not production art or simulation.

## Evidence currently available

- `gdparse` successfully parsed the GDScript files and `gdlint` reported no problems.
- `gdformat` formatting check completed without further changes.
- No Godot editor/runtime execution has occurred in this environment yet. The first authoritative engine parser/import, test, export, and Windows executable smoke result must come from the GitHub Actions workflow.
- No Windows executable or release has been verified at the time this report was written.

## Not tested / not passed

- Godot scene import and shader compilation by Godot itself.
- Project test mode, startup smoke mode, Windows export, and exported `.exe` launch.
- Visual quality, controls feel, or performance on any real target hardware.
- The ≥40 average FPS / ≥30 FPS 1% low target-PC contract.
- Production motorcycle/rider, motorcycle handling physics, traffic AI, crash behavior, a streamed environment, audio, weather, persistence, or final UI.

Update this report with the actual GitHub Actions run URL and its test/build/smoke result after CI completes. Do not state the Windows executable is ready until that job passes. Even then, target-PC benchmark acceptance remains a separate owner-run step.
