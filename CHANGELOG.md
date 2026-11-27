# Changelog

## Unreleased — Godot Phase 0 prototype

- Replaced the Unity-only project and Unity-activation workflow with a Godot 4.7.2 project that does not require a Unity account, paid engine subscription, or activation secrets.
- Added a procedural 1 km road, placeholder bike, 50 deterministically seeded traffic fixtures, chase/cockpit cameras, two mirror views, and a horizontal lens shader.
- Added GDScript math and scene checks, headless startup smoke mode, frame-time CSV/report output, percentile-based low-FPS estimates, and a frame-time histogram.
- Added a GitHub Actions pipeline for engine import/tests, single-file Windows x64 export, Windows smoke launch, artifacts, and tag releases. The first CI run passed project import/checks, export, and Windows executable smoke launch.
- Replaced Unity-specific setup and performance notes with Godot project, licensing, benchmark, and performance-budget documentation.
- **Still unmeasured:** target-PC FPS, visuals, and game acceptance. The existing ≥40 average FPS / ≥30 FPS 1% low contract requires a real run on the target PC.
