using System.Collections;
using NUnit.Framework;
using OpenRoad.Core;
using OpenRoad.Spike;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenRoad.Tests.PlayMode
{
    public sealed class Phase0RuntimeTests
    {
        [UnityTest]
        public IEnumerator BenchmarkRide_ResetsToDeterministicCenterAndSpeed()
        {
            var bikeObject = new GameObject("Benchmark ride test");
            bikeObject.transform.position = new Vector3(2.5f, 0.2f, 100f);
            var ride = bikeObject.AddComponent<SpikeRideController>();

            ride.BeginBenchmarkRide(BenchmarkRunner.FixedBenchmarkSpeedKph);
            Assert.That(bikeObject.transform.position.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(bikeObject.transform.position.z, Is.EqualTo(-420f).Within(0.0001f));
            Assert.That(ride.SpeedKph, Is.EqualTo(BenchmarkRunner.FixedBenchmarkSpeedKph).Within(0.0001f));

            ride.EndBenchmarkRide();
            Assert.That(ride.SpeedKph, Is.Zero);
            Object.Destroy(bikeObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LensCameraMarker_ExposesWideAndNormalModes()
        {
            var cameraObject = new GameObject("Lens marker test camera");
            var marker = cameraObject.AddComponent<LensCameraMarker>();

            Assert.That(marker.ModeName, Is.EqualTo("HyperView-inspired"));
            marker.distortionStrength = 0f;
            Assert.That(marker.ModeName, Is.EqualTo("Normal"));
            marker.distortionStrength = 0.5f;
            Assert.That(marker.ModeName, Is.EqualTo("Wide"));

            yield return null;
            Object.Destroy(cameraObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TrafficFixture_ClampsToLowPresetCeiling()
        {
            var worldObject = new GameObject("Traffic fixture test");
            var world = worldObject.AddComponent<TrafficWorld>();
            world.requestedVehicleCount = 99;

            // Allow Awake/Start to create the pooled fixture.
            yield return null;

            Assert.That(world.ActiveVehicleCount, Is.EqualTo(TrafficWorld.LowPresetVehicleCap));
            Object.Destroy(worldObject);
            yield return null;
        }
    }
}
