using OpenRoad.Core;
using UnityEngine;

namespace OpenRoad.Spike
{
    /// <summary>One camera with simple chase/cockpit framing for the engine/rendering spike.</summary>
    public sealed class SpikeCameraRig : MonoBehaviour
    {
        public Camera playerCamera;
        public Transform bikeRoot;
        public LensCameraMarker lensMarker;

        private bool _cockpitView;

        public Camera PlayerCamera => playerCamera;
        public bool IsCockpitView => _cockpitView;

        public void ToggleView()
        {
            _cockpitView = !_cockpitView;
        }

        public void SetCockpitView(bool cockpit)
        {
            _cockpitView = cockpit;
        }

        /// <summary>Places the camera directly in the selected view, useful after deterministic resets.</summary>
        public void SnapToCurrentView()
        {
            if (playerCamera == null || bikeRoot == null)
            {
                return;
            }

            Transform cameraTransform = playerCamera.transform;
            if (_cockpitView)
            {
                cameraTransform.position = bikeRoot.TransformPoint(new Vector3(0f, 1.43f, 0.18f));
                cameraTransform.rotation = bikeRoot.rotation;
                return;
            }

            Vector3 desiredPosition = bikeRoot.position - bikeRoot.forward * 7.2f + Vector3.up * 2.65f;
            Vector3 lookPoint = bikeRoot.position + bikeRoot.forward * 9f + Vector3.up * 1.15f;
            Vector3 lookDirection = lookPoint - desiredPosition;
            cameraTransform.position = desiredPosition;
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                cameraTransform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            }
        }

        private void LateUpdate()
        {
            if (playerCamera == null || bikeRoot == null)
            {
                return;
            }

            Transform cameraTransform = playerCamera.transform;
            if (_cockpitView)
            {
                cameraTransform.position = bikeRoot.TransformPoint(new Vector3(0f, 1.43f, 0.18f));
                cameraTransform.rotation = bikeRoot.rotation;
            }
            else
            {
                Vector3 desiredPosition = bikeRoot.position - bikeRoot.forward * 7.2f + Vector3.up * 2.65f;
                Vector3 lookPoint = bikeRoot.position + bikeRoot.forward * 9f + Vector3.up * 1.15f;
                cameraTransform.position = Vector3.Lerp(cameraTransform.position, desiredPosition, 1f - Mathf.Exp(-5f * Time.deltaTime));
                Vector3 lookDirection = lookPoint - cameraTransform.position;
                if (lookDirection.sqrMagnitude > 0.001f)
                {
                    Quaternion desiredRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
                    cameraTransform.rotation = Quaternion.Slerp(cameraTransform.rotation, desiredRotation, 1f - Mathf.Exp(-6f * Time.deltaTime));
                }
            }
        }
    }
}
