using NUnit.Framework;
using OpenRoad.Core;

namespace OpenRoad.Tests.EditMode
{
    public sealed class BenchmarkMathTests
    {
        [Test]
        public void Percentile_UsesNearestRankAndHonorsLogicalCount()
        {
            float[] samples = { 10f, 20f, 30f, 40f, 999f };

            Assert.That(BenchmarkMath.Percentile(samples, 4, 0.5f), Is.EqualTo(20f));
            Assert.That(BenchmarkMath.Percentile(samples, 4, 0.99f), Is.EqualTo(40f));
        }

        [Test]
        public void Percentile_ClampsRequestedQuantile()
        {
            float[] samples = { 5f, 15f, 25f };

            Assert.That(BenchmarkMath.Percentile(samples, 3, -1f), Is.EqualTo(5f));
            Assert.That(BenchmarkMath.Percentile(samples, 3, 2f), Is.EqualTo(25f));
        }

        [Test]
        public void LowFps_IsInverseOfFrameTimePercentile()
        {
            Assert.That(BenchmarkMath.FpsForFrameTime(25f), Is.EqualTo(40f).Within(0.001f));
            Assert.That(BenchmarkMath.FpsForFrameTime(0f), Is.Zero);
        }

        [TestCase(8.32f, 0)]
        [TestCase(8.33f, 1)]
        [TestCase(16.67f, 3)]
        [TestCase(50f, 6)]
        public void HistogramBucket_UsesDocumentedThresholds(float frameMs, int expectedBucket)
        {
            Assert.That(BenchmarkMath.HistogramBucket(frameMs), Is.EqualTo(expectedBucket));
        }

        [Test]
        public void HorizontalFisheye_LeavesCenterAndVerticalCoordinateUntouched()
        {
            float centerU = FisheyeProjection.MapOutputU(0.5f, 1f, 0.8f, 0.93f);
            float lineU = FisheyeProjection.MapOutputU(0.25f, 1f, 0.8f, 0.93f);

            Assert.That(centerU, Is.EqualTo(0.5f).Within(0.0001f));
            // The projection API maps U only; V is not accepted, so a vertical source line keeps
            // the same U at every row in the shader's per-pixel remap.
            Assert.That(lineU, Is.GreaterThan(0f).And.LessThan(0.5f));
        }

        [Test]
        public void HorizontalFisheye_EdgeCoverageKeepsSampleInsideSource()
        {
            float left = FisheyeProjection.MapOutputU(0f, 1f, 0.8f, 0.93f);
            float right = FisheyeProjection.MapOutputU(1f, 1f, 0.8f, 0.93f);

            Assert.That(left, Is.GreaterThan(0f));
            Assert.That(right, Is.LessThan(1f));
            Assert.That(left + right, Is.EqualTo(1f).Within(0.0001f));
        }
    }
}
