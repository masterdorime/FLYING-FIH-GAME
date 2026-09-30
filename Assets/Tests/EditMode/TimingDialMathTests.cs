using NUnit.Framework;

namespace FlyingFishMomentum.Tests.EditMode
{
    public class TimingDialMathTests
    {
        [Test]
        public void NeedleSweepsFullCircleClockwise()
        {
            // Hit at top: one full turn behind at open, on top at target.
            Assert.AreEqual(360f, TimingDialMath.NeedleAngle(0f), 0.001f);
            Assert.AreEqual(180f, TimingDialMath.NeedleAngle(0.5f), 0.001f);
            Assert.AreEqual(0f, TimingDialMath.NeedleAngle(1f), 0.001f);
        }

        [Test]
        public void NeedleEndsOnHitAngle()
        {
            Assert.AreEqual(-90f, TimingDialMath.NeedleAngle(1f, 90f), 0.001f);
            Assert.AreEqual(270f, TimingDialMath.NeedleAngle(0f, 90f), 0.001f);
            Assert.AreEqual(-200f, TimingDialMath.NeedleAngle(1f, 200f), 0.001f);
            Assert.AreEqual(-270f, TimingDialMath.NeedleAngle(1.5f, 90f), 0.001f);
        }

        [Test]
        public void ZoneWidthsScaleWithWindows()
        {
            // Half-width degrees = 360 * window / lead (lead 1s).
            Assert.AreEqual(20f, TimingDialMath.HalfWidthDeg(0.0556f, 1f), 0.5f);
            Assert.AreEqual(51.5f, TimingDialMath.HalfWidthDeg(0.143f, 1f), 0.5f);
        }

        [Test]
        public void ProgressRunsPastTarget()
        {
            // No parking: past the hit moment progress exceeds 1 (drift).
            Assert.AreEqual(0f, TimingDialMath.Progress01(targetTime: 10f, now: 9f, lead: 1f), 0.001f);
            Assert.AreEqual(1f, TimingDialMath.Progress01(targetTime: 10f, now: 10f, lead: 1f), 0.001f);
            Assert.AreEqual(1.5f, TimingDialMath.Progress01(targetTime: 10f, now: 10.5f, lead: 1f), 0.001f);
        }
    }
}
