using System;

namespace OpenRoad.Core
{
    /// <summary>
    /// CPU reference for the horizontal remap used by the URP shader. Only U changes; V is
    /// intentionally linear so vertical lines remain vertical instead of being stretched.
    /// </summary>
    public static class FisheyeProjection
    {
        public static float MapOutputU(float outputU, float strength, float sourceHalfAngleRadians, float edgeCoverage)
        {
            if (sourceHalfAngleRadians <= 0f || edgeCoverage <= 0f)
            {
                return outputU;
            }

            strength = Clamp01(strength);
            edgeCoverage = Math.Min(1f, Math.Max(0.01f, edgeCoverage));

            float sourceHalfTangent = (float)Math.Tan(sourceHalfAngleRadians);
            if (Math.Abs(sourceHalfTangent) < 0.0001f)
            {
                return outputU;
            }

            float targetHalfAngle = (float)Math.Atan(sourceHalfTangent * edgeCoverage);
            float centered = (outputU - 0.5f) * 2f;
            float warped = (float)Math.Tan(centered * targetHalfAngle) / sourceHalfTangent;
            float remapped = centered + (warped - centered) * strength;
            return 0.5f + 0.5f * remapped;
        }

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value)) return 0f;
            if (value < 0f) return 0f;
            return value > 1f ? 1f : value;
        }
    }
}
