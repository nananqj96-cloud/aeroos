# OPEN ROAD — Phase 0 engine spike

OPEN ROAD is a planned free-roam motorcycle simulator. This repository currently contains **Phase 0 source only**: a Unity/URP scaffold for a deliberately small performance spike, not the finished game. The editor bootstrap is intended to generate a procedural bike fixture, a 1 km road strip, 50 kinematic traffic vehicles, a two-target mirror rig, a horizontal-fisheye URP pass, and a deterministic benchmark. Unity has not been run here, so no generated scene or working feature is verified yet. The bike and environment meshes are disposable test geometry; they are **not** being represented as the production motorcycle or final art.

There is no Windows executable checked into this repository. GitHub Actions is configured to run Unity tests, build a Windows x64 Mono player, smoke-launch it on Windows, publish a zip artifact, and create a GitHub Release on `v*` tags. The workflow cannot run until the repository owner adds the Unity license secrets described below. GPU performance and feel have not been measured in this environment.

## Engine and license check (2026-11-27)

- **Unity 6.3 LTS `6000.3.29f1`**, the latest patch of the current LTS available on the check date. The project pins that editor version in `ProjectSettings/ProjectVersion.txt`.
- **URP 17.3.0**, Direct3D 11 as the Windows player graphics API, and the built-in URP FSR 1 upscaler for sub-native rendering.
- **Mono** for Windows x64: selected to keep CI builds straightforward and avoid IL2CPP's Windows toolchain/time costs at this stage.
- Unity Personal is free for eligible customers with up to **$200,000 USD in annual revenue and funding**. Confirm the current terms for the account and project before public distribution; Unity's official pricing page says the splash screen is optional with Unity 6.

References checked on 2026-11-27: [Unity 6 release support](https://unity.com/releases/unity-6/support), [Unity 6000.3.29f1 release](https://unity.com/releases/editor/whats-new/6000.3.29f1), [Unity plan changes](https://unity.com/products/pricing-updates), [GameCI activation](https://game.ci/docs/github/activation/), [GameCI Builder](https://github.com/game-ci/unity-builder/releases), and [GameCI Test Runner](https://github.com/game-ci/unity-test-runner/releases).

## Open the spike

1. Install Unity Hub and Unity Editor **6000.3.29f1** with Windows Build Support (Mono) if building locally.
2. Open this repository as a Unity project. The editor bootstrap is intended to create `Assets/OpenRoad/Generated/OpenRoadPhase0.unity`, the URP assets, and the required build-scene entry on first import. Generated scene/settings are intentionally ignored by Git and are recreated from source; this first-import path has not yet been verified.
3. Enter Play Mode. Controls: **W/S** accelerate/brake, **A/D** steer, **C** switches cockpit/chase camera, **M** toggles mirrors, **F1** cycles Normal/Wide/HyperView-inspired lens, **H** hides/shows the spike panel, and **F2** starts a benchmark.
4. For a deterministic run, build the player and launch:

   ```text
   OpenRoad.exe -benchmark -benchmark-seconds 40
   ```

   The UI offers the same **Run Benchmark** button. It warms up, sets 1920×1080 output, runs the fixed 80 km/h route with seed `20261127`, and writes a frame CSV and text summary beneath `Application.persistentDataPath/Benchmark/`.

See [`docs/BENCHMARKING.md`](docs/BENCHMARKING.md) for result interpretation and the one-click test procedure. See [`docs/PHASE0_REPORT.md`](docs/PHASE0_REPORT.md) for the current evidence/status.

## GitHub Actions setup (owner action required)

GameCI's Personal-license activation currently needs the `.ulf` file plus Unity account credentials. Do **not** put credentials in chat or commit them.

1. In Unity Hub, sign in and go to **Preferences → Licenses → Add → Get a free Personal license**. Ensure the license file is created. On Windows it is normally `C:\ProgramData\Unity\Unity_lic.ulf`.
2. In the GitHub repository, open **Settings → Secrets and variables → Actions → New repository secret** and add:
   - `UNITY_LICENSE` — the complete contents of `Unity_lic.ulf`.
   - `UNITY_EMAIL` — the email for the Unity account.
   - `UNITY_PASSWORD` — the Unity account password.
3. Push/merge to `main`, open a PR targeting `main`, or use the workflow's **Run workflow** button. The Actions workflow checks tests, builds Windows x64, smoke-launches the `.exe`, and uploads a zip. Tagging `vX.Y.Z` creates a GitHub Release with that zip.

GameCI currently uses `unity-builder@v6.0.0` with CLI `v0.1.77`, and `unity-test-runner@v4.4.0` (versions checked 2026-11-27). If a Personal license seat is held by a local Unity Editor, follow GameCI's current activation guidance to return/sign out of that seat. CI activation or build failure is not evidence of a game-performance failure; it must be resolved before the milestone can be accepted.

## Scope and honest status

This is **not yet a playable motorcycle simulator**. The Phase 0 ride controller is kinematic, there is no real bike/rider model, no motorcycle tire/lean physics, no licensed content, no crash system, no real traffic behavior, and no final settings/UI/audio/weather/world systems. The benchmark's FPS and timings are only valid after it is run on the owner's GTX 1050 2 GB / Ryzen 3 1300X PC. A CI runner has no representative GPU, so CI cannot certify the 40 FPS / 30 FPS 1% low contract.

Assets and attribution decisions are tracked in [`docs/ASSET_LICENSES.md`](docs/ASSET_LICENSES.md). The current candidate Sketchfab bike is recorded as **reviewed but not imported**; milestone 2 still requires a topology/part-separation review before acceptance.
