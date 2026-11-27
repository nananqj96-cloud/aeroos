using System.Collections;
using OpenRoad.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenRoad.Spike
{
    /// <summary>Runs only for CI's -smoke player launch; it checks scene bootstrap and exits.</summary>
    public sealed class StartupSmokeTest : MonoBehaviour
    {
        private IEnumerator Start()
        {
            if (!HasSmokeArgument()) yield break;

            // Let all scene components finish Awake/Start and create their pooled fixtures.
            yield return null;
            yield return null;

            TrafficWorld traffic = FindFirstObjectByType<TrafficWorld>();
            SpikeRideController bike = FindFirstObjectByType<SpikeRideController>();
            SpikeCameraRig cameraRig = FindFirstObjectByType<SpikeCameraRig>();
            MirrorCameraRig mirrors = FindFirstObjectByType<MirrorCameraRig>();
            BenchmarkRunner benchmark = FindFirstObjectByType<BenchmarkRunner>();
            Camera camera = cameraRig != null ? cameraRig.PlayerCamera : null;
            bool hasLensMarker = camera != null && camera.GetComponent<LensCameraMarker>() != null;
            bool trafficReady = traffic != null && traffic.ActiveVehicleCount == TrafficWorld.LowPresetVehicleCap;
            bool passed = trafficReady && bike != null && camera != null && hasLensMarker && mirrors != null && mirrors.IsReady && benchmark != null;

            if (passed)
            {
                Debug.Log("OPENROAD_SMOKE_OK: scene, player camera/lens marker, mirror rig, benchmark, and 50 traffic fixtures initialized. Graphics device: " + SystemInfo.graphicsDeviceType);
            }
            else
            {
                Debug.LogError("OPENROAD_SMOKE_FAIL: traffic=" + (traffic != null ? traffic.ActiveVehicleCount.ToString() : "missing")
                    + ", bike=" + (bike != null) + ", camera=" + (camera != null) + ", lens=" + hasLensMarker
                    + ", mirrors=" + (mirrors != null && mirrors.IsReady) + ", benchmark=" + (benchmark != null));
            }

            if (Application.isBatchMode)
            {
                Application.Quit(passed ? 0 : 1);
            }
        }

        private static bool HasSmokeArgument()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "-smoke", System.StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }
}
