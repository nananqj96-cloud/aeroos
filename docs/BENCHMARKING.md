# Phase 0 benchmark guide

## Run it on the target PC

1. Download and unzip the `OpenRoad.exe` artifact from a successful **Godot CI and Windows build** run. The executable is a single file with the project data embedded.
2. Use the actual target PC: GTX 1050 2 GB, Ryzen 3 1300X, 16 GB RAM, Windows x64, 1920×1080. Close GPU-heavy applications. Avoid remote desktop if it changes GPU selection.
3. From a command prompt, run:

   ```text
   OpenRoad.exe -- --benchmark --benchmark-seconds 40
   ```

   You can also press **F2** in-game; that run returns to the game rather than automatically exiting. The CLI run exits after saving its report.
4. The benchmark warms up for 5 seconds and measures for 40 seconds. It requests a 1920×1080 window, fixes speed at 80 km/h, uses seed `20261127`, enables 50 traffic fixtures, mirrors, and the horizontal lens effect, and starts at 0.80 render scale. The experimental controller adjusts scale between 0.65 and 1.00.
5. When the run finishes, find `benchmark_*.csv` and `benchmark_summary.txt` under the Godot user-data folder's `Benchmark` directory. The full CSV path is printed in the summary and to the console.
6. Share the complete summary along with confirmation that the run was on the target PC. Do not call CI performance numbers a target-PC pass.

The default run advances about 889 m at 80 km/h during the 40 measured seconds, within the 1 km fixture road. If you change duration, the measured road traversal is no longer directly comparable.

## What is measured

- Per-frame `_process` delta in milliseconds and its reciprocal FPS estimate.
- Average FPS and average frame time.
- Nearest-rank p99 and p99.9 frame times, converted to 1% and 0.1% low FPS.
- Frame-time histogram counts in seven buckets: `<8.33`, `<12.5`, `<16.67`, `<22`, `<33.33`, `<50`, and `>=50` milliseconds.
- Minimum, average, and maximum dynamic render scale.
- Workload metadata: engine version, traffic seed/count, requested output resolution, lens/mirror state, run duration, and CSV path.

This Phase 0 build does **not** report CPU/GPU frame-time breakdown, VRAM usage, draw calls, or power. The FPS values are application frame-loop measurements and are not a hardware-independent profiler result.

## Decision rule

The carried-forward target-PC gate is **at least 40 average FPS and at least 30 FPS 1% low** with the default workload. A `PASS for this run` line only describes one run; it does not certify other computers or the completed game. Compare runs with the same driver, output resolution, mirror/lens settings, and workload. A greater than 10% regression against a comparable accepted result blocks progression until it is understood and fixed.

## Smoke test versus performance test

CI starts the exported Windows executable in headless smoke mode. This checks that the packaged executable can start, create the Phase 0 world objects, and exit successfully. It does not measure graphical rendering, show a visual window, test riding feel, or certify performance. The target-PC benchmark is still required.
