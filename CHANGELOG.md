# Changelog

## Unreleased — Godot Phase 0 prototype

- Replaced the Unity-only project and Unity-activation workflow with a Godot 4.7.2 project that does not require a Unity account, paid engine subscription, or activation secrets.
- Added a procedural 1 km road, placeholder bike, 50 deterministically seeded traffic fixtures, chase/cockpit cameras, two mirror views, and a horizontal lens shader.
- Added GDScript math and scene checks, headless startup smoke mode, frame-time CSV/report output, percentile-based low-FPS estimates, and a frame-time histogram.
- Added a GitHub Actions pipeline intended to import/test the project, export a single-file Windows x64 executable, launch it on Windows in smoke mode, and publish tagged builds after verification.
- Replaced Unity-specific setup and performance notes with Godot project, licensing, benchmark, and performance-budget documentation.
- **Not yet verified by the Godot engine:** engine import, shader compilation, runtime tests, Windows export, Windows executable launch. Target-PC FPS and game acceptance remain unmeasured.
