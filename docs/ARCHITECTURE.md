# OPEN ROAD architecture — Phase 0 baseline

**Status:** architecture baseline and test spike only; systems described as future work are not implemented. Last reviewed 2026-11-27.

## Engine choice

- **Unity 6.3 LTS `6000.3.29f1` + URP 17.3.0**, Windows x64, Direct3D 11 primary, Mono scripting backend.
- Unity's release support page names 6.3 LTS as the current LTS and says it is supported through December 2027. The project's editor version is pinned in `ProjectSettings/ProjectVersion.txt`.
- URP is selected for controlled render cost on the GTX 1050 2 GB. The Phase 0 project disables HDR/MSAA by default and uses the URP FSR 1 spatial upscaler at sub-native render scale. The fisheye pass is a custom `ScriptableRendererFeature` written against URP's RenderGraph path and runs after post-processing. HUD/debug UI is drawn after the camera image and is therefore not distorted.
- Mono is deliberate for iteration and cross-build simplicity. An IL2CPP switch is a later release decision, not required to test the rendering spike.
- Unity Personal is currently free to eligible customers with up to $200,000 in annual revenue and funding. The owner must verify account/project eligibility and the current Unity terms; no license secrets are stored in the repository.

## Rejected alternatives for this target

- **Unreal Engine 5:** Nanite, Lumen, and Virtual Shadow Maps are not appropriate assumptions for a GTX 1050 2 GB / DirectX 11 floor. Disabling them removes much of the headline advantage while increasing CI/build and project burden.
- **Godot 4:** documented fallback if Unity licensing or CI activation blocks progress. Godot is light and permissively licensed, but has a less mature out-of-the-box path for vehicle/ragdoll authoring and large-world streaming.
- **Bevy/Rust:** capable runtime performance, but weaker editor, animation-rigging, asset-pipeline, and shader iteration support for this project. The immediate bottleneck is not managed-language throughput.
- **HDRP:** explicitly out of scope for the target GPU.

## Phase 0 project boundaries

Current assemblies:

- `OpenRoad.Core` — lens mapping helper and camera marker.
- `OpenRoad.Rendering` — URP horizontal lens feature.
- `OpenRoad.Spike` — deterministic traffic fixture, simple kinematic ride, mirror rig, benchmark UI/recorder, and smoke entry point.
- `OpenRoad.Editor` — generates the spike scene/settings on editor load and provides the GameCI/local build entry point.

When run in Unity, the generator is intended to create one 1,000 m road strip, one unbranded procedural bike fixture, exactly 50 pooled kinematic car meshes with a fixed seed, low-poly roadside geometry, a chase/cockpit camera, and two 256×128 mirror targets updated every other frame. The generator and runtime have not yet been compiled or run, so these are source-level targets, not verified scene behavior. The procedural bike is **test geometry only** and is not a production motorcycle asset. The traffic is not a traffic-AI system. The ride controller is not PhysX bike handling.

## Rendering and camera path

1. Main scene camera renders a wide rectilinear image (vertical FOV 84°).
2. URP FSR 1 is enabled for sub-native scene rendering and scaling back to output.
3. The horizontal-only remap samples the scene using an equidistant-style horizontal angle curve; vertical UV is kept linear. `edgeCoverage` crops source coverage slightly so the remap does not sample outside the source image.
4. The pass executes after post-processing only for the explicitly marked primary camera. Mirror cameras do not recurse through the lens effect. HUD remains undistorted.
5. The mirror rig reflects the eye-camera pose across each mirror plane, renders only into small targets, and updates at half the primary-camera rate. Turning mirrors off releases the targets and stops manual camera renders.

This is Tier A only. There is no stitched multi-camera Tier B mode, motion blur, mirror calibration UI, temporal anti-aliasing, or runtime FSR/dynamic-resolution quality menu yet. The benchmark has an experimental bounded dynamic-resolution controller; owner GPU testing is required to confirm that the selected Unity/driver combination actually scales and upscales as expected.

## Data and future system boundaries

The next implementation should split into Vehicle, Rider, Camera/Lens, Crash, Traffic, World/Streaming, Weather, Audio, UI, Settings, and Core assemblies. Bike physics must use a single chassis body with custom ray/sphere wheel contacts and a fixed-step substepped single-track model; do not substitute `WheelCollider`. Traffic data should be isolated from presentation and updated from pooled data-oriented state. Settings should be versioned JSON under `Application.persistentDataPath`.

## CI and release path

GitHub Actions uses GameCI to run Unity EditMode/PlayMode tests and build `StandaloneWindows64` with Mono. A Windows runner smoke-launches the generated executable with `-smoke`; the build is then zipped with the README, benchmark instructions, and license ledger. A `v*` tag creates a GitHub Release. The `.exe` is unsigned and may trigger Windows SmartScreen; no code-signing claim is made.

Unity Personal CI needs `UNITY_LICENSE`, `UNITY_EMAIL`, and `UNITY_PASSWORD` repository Actions secrets. These are owner-managed and must never be committed or sent in chat. CI can validate compilation/tests/player startup; it cannot measure GTX 1050 performance or riding feel.
