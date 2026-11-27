# Performance budget — Phase 0 and target contract

**Target hardware:** GTX 1050 2 GB, Ryzen 3 1300X (4C/4T, no SMT), 16 GB RAM, Windows 10/11 x64. **No target-PC measurements exist yet.** CI's lack of a representative GPU means no FPS budget can be marked passed until the owner runs the benchmark locally.

## Contract carried forward

| Metric | Low acceptance target | Phase 0 measurement/status |
| --- | ---: | --- |
| Output | 1920×1080 | Benchmark requests 1920×1080 windowed output. |
| Average FPS | ≥ 40 | Captured by benchmark; owner run required. |
| 1% low | ≥ 30 FPS | Calculated from p99 frame time; owner run required. |
| 0.1% low | Report | Calculated from p99.9 frame time. |
| GPU frame time | ≤ 22 ms | `FrameTimingManager` average when supported; otherwise reported unavailable. |
| CPU main-thread time | ≤ 18 ms | `FrameTimingManager` average when supported; otherwise reported unavailable. |
| VRAM allocated | ≤ 1,536 MB | Driver allocation API when supported; adapter capacity is reported separately. |
| Texture memory | ≤ 700 MB | Not reliably exposed by the Phase 0 runtime benchmark; not measured yet. |
| Render targets | ≤ 250 MB | Phase 0 targets are deliberately small. Exact runtime allocation is not a Unity-independent counter; no pass claim yet. |
| Draw calls | ≤ 200 per Low frame | `ProfilerRecorder` reads Unity's `Draw Calls Count` counter if the player exposes it. Counter may be unavailable in non-development builds; then result is explicitly unverified. |
| Traffic | 50 active vehicles on Low | Hard-capped at 50 in the spike. The final Low cap remains 50 unless benchmark evidence supports another value. |

The **Ultra Low** acceptance target is ≥ 60 FPS; it is not implemented in this spike.

## Provisional draw-call ceilings by preset

These ceilings are **planning limits**, not measured results. Phase 0 only implements the Low fixture and exposes a draw-call counter when Unity provides one.

| Preset | Hard draw-call ceiling (design target) | Traffic ceiling | Mirrors | Notes |
| --- | ---: | ---: | --- | --- |
| Ultra Low | 120 | 25 | Off | Minimal shadows, lower render scale. |
| Low (default) | 200 | 50 | 256×128, half-rate | GTX 1050 acceptance preset. |
| Medium | 350 | 75 | 512×256, half-rate | Optional higher setting. |
| High | 550 | 100 | 768×384, every frame only if budget allows | Optional. |
| Ultra | 800 | 150 | 1024×512, adaptive update | Optional. |

The spike enforces the only implemented content ceiling (50 traffic vehicles) and reports live draw calls when available. It does not yet implement the full multi-preset watchdog or exact VRAM/texture/render-target allocator accounting; those are explicit follow-up tasks, not claimed as done.

## Phase 0 workload

- Deterministic 1 km straight road route, 80 km/h, traffic seed `20261127`, clear weather, 50 moving vehicle fixtures.
- Wide main camera and horizontal remap enabled; two 256×128 mirror renders, each updated every second primary frame.
- 1920×1080 output. Benchmark begins at 0.80 dynamic-buffer scale and adjusts between 0.65 and 1.00 based on frame time. URP FSR 1 is configured for upscaling.
- No physics simulation, final bike/rider asset, runtime city streamer, rain, or high-cost effects are included in this spike.

## Benchmark decision rule

Use the generated `benchmark_summary.txt` as the evidence record. A local run that reports at least 40 average FPS and at least 30 FPS 1% low is **one machine/run result**, not a guarantee for all content. If a comparable later milestone regresses by more than 10%, stop adding features and profile/fix it before proceeding. Record GPU driver, output resolution, average render scale, traffic count, and mirror/lens settings with each pasted report.
