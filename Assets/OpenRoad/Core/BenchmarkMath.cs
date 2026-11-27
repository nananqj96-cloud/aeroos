using System;

namespace OpenRoad.Core
{
    /// <summary>Small allocation-free helpers shared by benchmark reporting and tests.</summary>
    public static class BenchmarkMath
    {
        /// <summary>
        /// Returns the nearest-rank percentile from an ascending array. The caller owns sorting
        /// and may pass a larger backing array than <paramref name="count"/>.
        /// </summary>
        public static float Percentile(float[] sortedValues, int count, float percentile)
        {
            if (sortedValues == null || count <= 0)
            {
                return 0f;
            }

            count = Math.Min(count, sortedValues.Length);
            percentile = Clamp01(percentile);
            int rank = (int)Math.Ceiling(percentile * count);
            int index = Math.Max(0, Math.Min(count - 1, rank - 1));
            return sortedValues[index];
        }

        /// <summary>Converts a frame-time percentile in milliseconds to its corresponding FPS.</summary>
        public static float FpsForFrameTime(float frameTimeMs)
        {
            return frameTimeMs > 0f && !float.IsNaN(frameTimeMs) && !float.IsInfinity(frameTimeMs)
                ? 1000f / frameTimeMs
                : 0f;
        }

        /// <summary>Seven fixed histogram buckets: &lt;8.33, &lt;12.5, &lt;16.67, &lt;22, &lt;33.33, &lt;50, and ≥50 ms.</summary>
        public static int HistogramBucket(float frameTimeMs)
        {
            if (frameTimeMs < 8.33f) return 0;
            if (frameTimeMs < 12.5f) return 1;
            if (frameTimeMs < 16.67f) return 2;
            if (frameTimeMs < 22f) return 3;
            if (frameTimeMs < 33.33f) return 4;
            if (frameTimeMs < 50f) return 5;
            return 6;
        }

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value)) return 0f;
            if (value < 0f) return 0f;
            return value > 1f ? 1f : value;
        }
    }
}
