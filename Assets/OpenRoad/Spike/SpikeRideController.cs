using UnityEngine;

namespace OpenRoad.Spike
{
    /// <summary>
    /// Kinematic Phase 0 input/benchmark driver. It deliberately does not claim motorcycle
    /// dynamics, collisions, suspension, countersteer, or PhysX handling.
    /// </summary>
    public sealed class SpikeRideController : MonoBehaviour
    {
        private const float BenchmarkStartZ = -420f;
        private float _speedMetresPerSecond;
        private float _benchmarkSpeedMetresPerSecond;
        private float _benchmarkStartTime;
        private Vector3 _benchmarkStartPosition;
        private bool _benchmarkRideActive;

        public float SpeedKph => _speedMetresPerSecond * 3.6f;
        public bool IsBenchmarkRideActive => _benchmarkRideActive;

        public void BeginBenchmarkRide(float speedKph)
        {
            _benchmarkSpeedMetresPerSecond = Mathf.Max(0f, speedKph / 3.6f);
            _speedMetresPerSecond = _benchmarkSpeedMetresPerSecond;
            _benchmarkStartPosition = new Vector3(0f, transform.position.y, BenchmarkStartZ);
            _benchmarkStartTime = Time.unscaledTime;
            _benchmarkRideActive = true;
            transform.SetPositionAndRotation(_benchmarkStartPosition, Quaternion.identity);
        }

        public void EndBenchmarkRide()
        {
            _benchmarkRideActive = false;
            _speedMetresPerSecond = 0f;
        }

        public void ResetRideFixture()
        {
            _speedMetresPerSecond = 0f;
            _benchmarkRideActive = false;
            transform.SetPositionAndRotation(new Vector3(0f, transform.position.y, BenchmarkStartZ), Quaternion.identity);
        }

        private void Update()
        {
            if (_benchmarkRideActive)
            {
                float elapsed = Time.unscaledTime - _benchmarkStartTime;
                Vector3 position = _benchmarkStartPosition + Vector3.forward * (_benchmarkSpeedMetresPerSecond * elapsed);
                transform.SetPositionAndRotation(position, Quaternion.identity);
                _speedMetresPerSecond = _benchmarkSpeedMetresPerSecond;
                return;
            }

            float throttle = Input.GetKey(KeyCode.W) ? 1f : 0f;
            bool braking = Input.GetKey(KeyCode.S);
            float targetSpeed = throttle * (105f / 3.6f);
            if (braking)
            {
                targetSpeed = 0f;
            }

            float acceleration = braking ? 28f : throttle > 0f ? 8f : 1.8f;
            _speedMetresPerSecond = Mathf.MoveTowards(_speedMetresPerSecond, targetSpeed, acceleration * Time.deltaTime);

            float steer = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            Vector3 positionNow = transform.position;
            float lateralRate = Mathf.Lerp(0f, 2.2f, Mathf.Clamp01(_speedMetresPerSecond / 16f));
            positionNow.x = Mathf.Clamp(positionNow.x + steer * lateralRate * Time.deltaTime, -4.2f, 4.2f);
            positionNow.z += _speedMetresPerSecond * Time.deltaTime;
            transform.position = positionNow;

            Quaternion targetRotation = Quaternion.Euler(0f, steer * 4f, -steer * 6f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-5f * Time.deltaTime));
        }
    }
}
