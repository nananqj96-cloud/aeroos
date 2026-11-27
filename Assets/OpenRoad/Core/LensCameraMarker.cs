using UnityEngine;

namespace OpenRoad.Core
{
    /// <summary>Marks the one player camera that receives the horizontal lens remap.</summary>
    [DisallowMultipleComponent]
    public sealed class LensCameraMarker : MonoBehaviour
    {
        [Range(0f, 1f)] public float distortionStrength = 0.92f;
        [Range(0.65f, 1f)] public float edgeCoverage = 0.93f;
        [Range(0.75f, 1.25f)] public float verticalScale = 1f;

        public string ModeName
        {
            get
            {
                if (distortionStrength < 0.01f) return "Normal";
                return distortionStrength < 0.75f ? "Wide" : "HyperView-inspired";
            }
        }
    }
}
