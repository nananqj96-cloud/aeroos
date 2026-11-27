# Phase 0 milestone report — source draft and status

**Date:** 2026-11-27

**Status:** source/configuration draft in progress; Unity has not been run, no scene/player has been generated, and acceptance is **not passed**.

## Source and configuration present (not yet runtime-verified)

- Unity 6.3 LTS / URP project scaffold, an Editor bootstrap intended to generate the spike scene/settings, direct D3D11 preference, and Mono Windows build configuration.
- Source for a procedural Phase 0 bike test fixture, 1 km road, 50 seeded kinematic traffic vehicles, custom horizontal-only fisheye render feature, two low-resolution half-rate mirror views, simple controls, smoke mode, and benchmark UI/data output.
- GameCI workflow source for tests, Windows x64 build, Windows smoke launch, zipped artifact, and tag release.
- Architecture, performance budget, benchmark instructions, changelog, and asset ledger.

These bullets describe files and intended behavior only. They are not evidence that Unity imported, compiled, generated, rendered, or executed the project.

## Evidence collected here

- Repository inspection: clean initial commit contained only a 9-byte README; no existing Unity project or assets. The current source/config files are now confirmed inside the active repository branch, not a sibling `/home/user` directory.
- Static validation: all project JSON/assembly-definition files present parsed; the GitHub Actions workflow parsed as YAML with the expected four jobs; `git diff --check` passed. This does not validate Unity APIs, shader compilation, or workflow execution.
- Tool availability check found no Unity Editor, .NET SDK, Mono, or C# compiler in this environment.
- Official Unity release page checked: `6000.3.29f1` released 2026-11-19; Unity identifies 6.3 as the current LTS.
- Official Unity plan page checked: Unity Personal is listed as free for eligible customers up to $200,000 in annual revenue and funding.
- GameCI activation, Builder, and Test Runner guidance checked; workflow pins Builder `v6.0.0`, CLI `v0.1.77`, and Test Runner `v4.4.0`.
- Candidate Sketchfab H2 listing reports CC BY 4.0, 226.8k triangles, and AI generation. It has **not** been downloaded, inspected, or imported.

## Not tested / not passed

- No Unity Editor is installed in this coding environment, so scripts, URP feature, generated scene, and Unity tests have not been compiled or executed here.
- No GameCI run has occurred. Owner-managed Unity license secrets are not available to this environment.
- No Windows executable has been built or launched here.
- No GTX 1050 / Ryzen 3 benchmark, FPS, 1% low, CPU/GPU timing, VRAM, draw-call, rendering screenshot, or play-feel result exists yet.
- There is no production superbike, rider, city, realistic traffic AI, motorcycle physics, crash system, sound, weather, persistent settings, or final UI.

## Owner test required before Phase 0 acceptance

1. Add `UNITY_LICENSE`, `UNITY_EMAIL`, and `UNITY_PASSWORD` under GitHub **Settings → Secrets and variables → Actions** (see the README). Do not send them in chat.
2. Run the workflow from Actions, or push/PR to `main`, and confirm test, Windows build, and Windows smoke jobs pass.
3. Download the zip and run `OpenRoad.exe -benchmark -benchmark-seconds 40` on the target PC.
4. Paste `benchmark_summary.txt` here. Phase 0 only passes its performance gate if the target-PC average is at least 40 FPS and 1% low is at least 30 FPS, with the worst-case 50-vehicle workload and default lens/mirrors settings. If it misses, revise the render/traffic plan before advancing.
