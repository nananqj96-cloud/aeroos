# OPEN ROAD — Godot Phase 0

OPEN ROAD is a planned free-roam motorcycle simulator. This repository is a small **Phase 0 prototype**, not the finished game. It contains a procedural 1 km road, a simple unbranded bike fixture, 50 seeded traffic fixtures, two rear-view mirror renders, a horizontal fisheye-style lens effect, chase/cockpit camera modes, basic keyboard controls, and a repeatable frame-time benchmark.

## $0 engine path

The project uses **Godot 4.7.2**, a free and open-source engine. It does not require a Unity account, Unity activation, a paid engine subscription, or engine-license secrets. The project currently contains no downloaded third-party art, vehicle model, music, or sound. The bike and world are deliberately simple test geometry, not production assets.

The source repository does not commit the Windows executable. The GitHub Actions workflow builds a **single-file Windows x64 `OpenRoad.exe`**, launches it in headless smoke-test mode on a Windows runner, and uploads it as an artifact when all checks pass. A `v*` tag publishes the tested ZIP as a GitHub Release. Artifacts from ordinary runs are retained for 14 days. No Godot activation or signing secret is needed.

> **Honest status:** passing CI only proves the project imported, tests passed, the Windows executable exported, and the executable started in the CI smoke test. It does not prove that the visuals are polished or that the target-PC FPS goal is met. There is no target-PC benchmark result in this source tree yet.

## Play

Keyboard controls:

- **W / S** — accelerate / brake
- **A / D** — steer
- **C** — switch chase/cockpit camera
- **M** — toggle mirrors
- **F1** — cycle the lens effect
- **F2** — run the benchmark
- **H** — hide/show the information panel

To run a headless startup check or math tests from a Godot 4.7.2 editor install:

```sh
godot --headless --path . -- --test
godot --headless --path . -- --smoke
```

For the local editor, install the matching Godot 4.7.2 export templates using the editor's template manager. The Windows export preset embeds the project data, so the exported game is one `.exe` file.

## Get the Windows executable

1. A verified package is available as [Actions artifact `openroad-windows-3`](https://github.com/nananqj96-cloud/aeroos/actions/runs/46068747923/artifacts/15231294423). For newer builds, open the repository's **Actions** tab and choose the latest successful **Godot CI and Windows build** run.
2. Download the artifact and unzip it once. It contains `OpenRoad.exe` plus the readme, benchmark guide, attribution notes, and Godot Engine license notice. A [player-only artifact](https://github.com/nananqj96-cloud/aeroos/actions/runs/46068747923/artifacts/15232445123) is also available.
3. Run `OpenRoad.exe`. The executable is unsigned and Windows may show a SmartScreen warning.

Artifacts are retained for 14 days. A version tag such as `v0.1.0` creates a GitHub Release only after both the Linux build/tests and Windows smoke test pass. Do not use a failed or in-progress run as a build.

## Benchmark on the target PC

Start a 5-second warm-up and 40-second measured run from a command prompt:

```text
OpenRoad.exe -- --benchmark --benchmark-seconds 40
```

The `--` separates Godot options from the game's own options. The test requests a 1920×1080 window, drives at a fixed 80 km/h, uses seed `20261127`, keeps 50 traffic fixtures and the lens/mirrors enabled, and adjusts render scale between 0.65 and 1.00. Press **F2** for the same benchmark without auto-exiting.

The per-frame CSV and `benchmark_summary.txt` are saved in the Godot user-data folder, under `Benchmark`. The summary prints the full path. Read [`docs/BENCHMARKING.md`](docs/BENCHMARKING.md) before comparing runs. The target remains **at least 40 average FPS and at least 30 FPS 1% low** on the actual target PC. CI does not measure this performance gate.

## Scope and limitations

This is not yet a complete motorcycle simulator. The ride controller is kinematic; there is no production bike/rider model, tire or lean physics, crash system, traffic AI, streamed world, sound, weather, save system, or finished UI. The 50 vehicles and all scene models are low-detail fixtures. No public release should imply otherwise.

See [`docs/PHASE0_REPORT.md`](docs/PHASE0_REPORT.md) for the latest verification status, [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) for the current structure, [`docs/PERF_BUDGET.md`](docs/PERF_BUDGET.md) for the carried-forward performance goals, and [`docs/ASSET_LICENSES.md`](docs/ASSET_LICENSES.md) for the asset/license ledger.
