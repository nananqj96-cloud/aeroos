# Phase 0 milestone report — Godot prototype

**Date:** 2026-11-27
**Status:** Godot 4.7.2 project import, project checks, Windows x64 export, and Windows executable smoke test have passed in GitHub Actions. Target-PC performance acceptance is still not measured.

## Verified in CI

The Godot workflow run [46068369120](https://github.com/nananqj96-cloud/aeroos/actions/runs/46068369120) completed successfully on commit `2fa5e00`. A second run for the pull request, [46068374802](https://github.com/nananqj96-cloud/aeroos/actions/runs/46068374802), also passed.

- Godot 4.7.2 and matching export templates installed on the Linux runner.
- Headless Godot editor import completed.
- Project math/scene checks passed.
- Startup smoke mode passed.
- Windows x64 single-file executable exported and uploaded.
- The exported Windows executable launched on a Windows runner with `--headless -- --smoke`, exited successfully, and passed the smoke-success check.

The full Windows package from the first successful run is available as [Actions artifact `openroad-windows-1`](https://github.com/nananqj96-cloud/aeroos/actions/runs/46068369120/artifacts/15232126166). A player-only artifact is also available as [Actions artifact `openroad-windows-player`](https://github.com/nananqj96-cloud/aeroos/actions/runs/46068369120/artifacts/15231966428). GitHub Actions artifacts are temporary (14-day retention); use the newest successful workflow run for later builds. No tagged GitHub Release was created.

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
