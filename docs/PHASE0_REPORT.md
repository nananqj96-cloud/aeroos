# Phase 0 milestone report — Godot prototype

**Date:** 2026-11-27
**Status:** Godot 4.7.2 project import, project checks, Windows x64 export, and Windows executable smoke test have passed in GitHub Actions. Target-PC performance acceptance is still not measured.

## Verified in CI

The direct-package build run, [46068747923](https://github.com/nananqj96-cloud/aeroos/actions/runs/46068747923), completed successfully on commit `ba6ded9`. Its pull-request check, [46068754619](https://github.com/nananqj96-cloud/aeroos/actions/runs/46068754619), also passed. The earlier run [46068369120](https://github.com/nananqj96-cloud/aeroos/actions/runs/46068369120) passed the same project import, test, export, and Windows smoke stages.

- Godot 4.7.2 and matching export templates installed on the Linux runner.
- Headless Godot editor import completed.
- Project math/scene checks passed.
- Startup smoke mode passed.
- Windows x64 single-file executable exported and uploaded.
- The exported Windows executable launched on a Windows runner with `--headless -- --smoke`, exited successfully, and passed the smoke-success check.
- The CI workflow now uploads the Windows package contents directly as an artifact, so one unzip gives you `OpenRoad.exe` and its documents.

Download the verified [Windows package artifact `openroad-windows-3`](https://github.com/nananqj96-cloud/aeroos/actions/runs/46068747923/artifacts/15231294423), or the [player-only artifact](https://github.com/nananqj96-cloud/aeroos/actions/runs/46068747923/artifacts/15232445123). Artifacts are temporary (14-day retention); use the newest successful workflow run for later builds. No tagged GitHub Release was created.

Local static checks also passed: `gdparse`, `gdformat --check`, `gdlint`, workflow YAML parsing, and `git diff --check`.

## What this proves—and what it does not

CI confirms that Godot imported the project, its checks ran, a Windows executable was produced, and that executable started in headless smoke mode. It does **not** certify visuals, gameplay feel, or performance on the owner's computer.

No target-PC benchmark has been collected. The required target remains **at least 40 average FPS and at least 30 FPS 1% low** with the default workload. A comparable regression greater than 10% blocks progression until fixed.

## Remaining scope and unverified items

- No target-PC FPS, 1% low, GPU timing, CPU timing, VRAM, visual-quality, or controls-feel result exists.
- The bike and road are procedural placeholders, not production art.
- No motorcycle tire/lean physics, rider, traffic AI, crash system, streamed world, weather, audio, settings persistence, or final UI is implemented.
- The Windows executable is unsigned; Windows may show a SmartScreen warning.

Run the benchmark from a command prompt with `OpenRoad.exe -- --benchmark --benchmark-seconds 40`. See [`BENCHMARKING.md`](BENCHMARKING.md) for the target-PC procedure.
