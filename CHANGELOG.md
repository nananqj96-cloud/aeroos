# Changelog

## Unreleased — Phase 0 spike

- Added a Unity 6.3 LTS / URP project baseline and generated Phase 0 scene setup.
- Added a RenderGraph-backed URP horizontal fisheye pass and a low-resolution, half-rate mirror rig.
- Added a fixed-route benchmark mode that writes per-frame CSV and summary metrics, including frame-time percentiles and memory readings where the runtime exposes them.
- Added GameCI edit/play-mode test, Windows Mono build, Windows smoke-launch, artifact, and tag-release workflow.
- Added architecture, performance-budget, benchmarking, and asset-license documentation.
- **Not yet verified:** Unity compilation, GitHub Actions execution, a Windows player launch, GPU timings, target-PC FPS, or gameplay feel. The repository owner must add Unity CI secrets and run/paste the target-PC benchmark summary.
