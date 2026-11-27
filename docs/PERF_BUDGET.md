# Performance budget — Phase 0 and target contract

**Target hardware:** GTX 1050 2 GB, Ryzen 3 1300X, 16 GB RAM, Windows x64. No target-PC performance result has been collected. A headless CI run cannot satisfy the performance gate.

## Carried-forward acceptance targets

| Metric | Target | Phase 0 status |
| --- | ---: | --- |
| Output | 1920×1080 | Requested by the benchmark; verify actual display mode on the target PC. |
| Average FPS | ≥ 40 | Not measured. |
| 1% low FPS | ≥ 30 | Not measured; estimated from nearest-rank p99 frame time. |
| 0.1% low FPS | Report | Not measured; estimated from p99.9 frame time. |
| Frame-time histogram | Report seven bins | Implemented in the benchmark source; runtime output awaits a real run. |
| Traffic fixtures | 50 active | Hard-coded fixture count; CI/startup test checks initialization. |
| Mirrors and lens | On for default benchmark | Source default; no target GPU cost measurement yet. |

A later comparable milestone that regresses by more than 10% blocks progression until the regression is understood and fixed. A local report that meets the threshold is one machine/run result, not a guarantee for all game content.

## Phase 0 workload

- A 1 km straight test road; fixed 80 km/h benchmark speed; seed `20261127`; 50 simple moving vehicle fixtures.
- A chase camera, horizontal fisheye-style remap, and two 256×128 mirror views updated on alternate frames.
- A requested 1920×1080 window. The world viewport starts at 0.80 scale and adjusts between 0.65 and 1.00 using a simple frame-time threshold.
- Simple procedural meshes only. No motorcycle physics, rider, streamed city, weather, or final content is in this test.

The benchmark records app frame deltas, average FPS/frame time, p99/p99.9 low-FPS estimates, a frame-time histogram, render-scale range, and workload details. It does **not** record GPU time, CPU main-thread time, draw calls, VRAM, texture memory, or power. Do not claim those budgets are met from the current report.

The target is intentionally conservative about what it claims: the project passes the numeric FPS gate only after the owner runs it on the specified GPU/CPU and shares the report. CI can validate project startup and Windows packaging, not this performance contract.
