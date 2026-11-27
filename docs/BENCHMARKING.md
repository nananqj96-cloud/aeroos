# Phase 0 benchmark guide

## Run it on the target PC

1. Download/unzip the Windows x64 build produced by GitHub Actions (or build locally from Unity 6000.3.29f1).
2. Close GPU-heavy applications. Use the owner's GTX 1050 2 GB / Ryzen 3 1300X / 16 GB PC, 1920×1080 output, current NVIDIA driver, and the default Low spike workload. Do not run through a remote desktop session if it changes GPU selection.
3. Start `OpenRoad.exe` and click **Run Benchmark**, or launch from Explorer's address bar / a shortcut with:

   ```text
   OpenRoad.exe -benchmark -benchmark-seconds 40
   ```

4. Let the 5-second warm-up finish. Avoid alt-tabbing during the measured interval. Default route: 40 seconds, 80 km/h, seed `20261127`, clear weather, 50 traffic vehicles, lens enabled, mirrors enabled, 1920×1080 output. The render scale starts at 0.80 and the experimental controller adjusts from 0.65 to 1.00.
5. The app writes a per-frame `benchmark_*.csv` and `benchmark_summary.txt` in the displayed `Application.persistentDataPath/Benchmark/` directory. On Windows this is generally under `%USERPROFILE%\AppData\LocalLow\<Company>\OpenRoad\Benchmark\`.
6. Paste the complete text summary here. Include the GPU/CPU lines and whether the run used mirrors/lens. Do not report a pass without the target-PC output.

The same benchmark is available from the in-game button; **F2** starts it. `-benchmark-seconds N` accepts 10–180 seconds. A 40-second default run remains inside the 1 km route at the fixed 80 km/h speed; longer runs wrap the fixture route and are less representative of streaming.

## Contents of the report

- Average FPS, p99/p99.9 frame time converted to 1%/0.1% low FPS, and a frame-time histogram.
- CPU frame and main-thread time plus GPU frame time from Unity `FrameTimingManager`, if the driver/player reports them.
- Peak draw calls from `ProfilerRecorder` if the named Unity counter is available; otherwise the report says `unavailable`.
- GPU driver allocation (where exposed), adapter VRAM capacity, process working set, traffic count, mirror/lens status, and dynamic-resolution scale range.
- The 40 FPS / 30 FPS 1% low threshold result for this run. It is not a cross-machine or final-game certification.

## Smoke test vs performance test

CI runs the Windows player with `-smoke -batchmode -nographics`. That confirms that the packaged player starts, its Phase 0 scene initializes, the 50-vehicle pool exists, and the camera/mirror systems are present. Because this has no representative GPU and no visible display, it is **not** a performance or visual-quality test. A successful smoke test does not substitute for the owner benchmark.
