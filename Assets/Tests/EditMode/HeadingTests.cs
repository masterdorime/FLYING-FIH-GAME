using NUnit.Framework;
using UnityEngine;

namespace FlyingFishMomentum.Tests.EditMode
{
    public class HeadingTests
    {
        [Test]
        public void ZeroInputHoldsHeadingWithoutNaN()
        {
            var h = HeadingMath.Step(45f, 10f, Vector2.zero, 200f, 1f, 1f);
            Assert.IsFalse(float.IsNaN(h.yaw) || float.IsNaN(h.pitch));
            Assert.AreEqual(45f, h.yaw, 0.001f);
            Assert.AreEqual(10f, h.pitch, 0.001f);
        }

        [Test]
        public void RightInputTurnsRight_UpInputClimbs()
        {
            var h = HeadingMath.Step(0f, 0f, new Vector2(1f, 1f), 200f, 1f, 0.25f);
            Assert.AreEqual(50f, h.yaw, 0.001f);
            Assert.AreEqual(50f, h.pitch, 0.001f);
        }

        [Test]
        public void PitchClampsAndYawClampsToCone()
        {
            var h = HeadingMath.Step(170f, 0f, new Vector2(1f, 1f), 200f, 1f, 1f);
            Assert.AreEqual(60f, h.pitch, 0.001f);
            Assert.AreEqual(60f, h.yaw, 0.001f); // 370 clamps to cone edge
        }

        [Test]
        public void YawClampsToForwardConeBothDirections()
        {
            var right = HeadingMath.Step(0f, 0f, new Vector2(1f, 0f), 200f, 1f, 1f);
            Assert.AreEqual(60f, right.yaw, 0.001f);
            var left = HeadingMath.Step(0f, 0f, new Vector2(-1f, 0f), 200f, 1f, 1f);
            Assert.AreEqual(-60f, left.yaw, 0.001f);
        }

        [Test]
        public void SustainedFullStickNeverExceedsCone()
        {
            float yaw = 0f;
            for (int i = 0; i < 60; i++)
            {
                yaw = HeadingMath.Step(yaw, 0f, new Vector2(1f, 0f), 200f, 1f, 0.1f).yaw;
                Assert.LessOrEqual(Mathf.Abs(yaw), 60.0001f);
            }
            Assert.AreEqual(60f, yaw, 0.001f);
        }

        [Test]
        public void OrientationMatchesForwardVector()
        {
            // The visual orientation must agree exactly with the motion
            // direction, or the fish strafes sideways instead of turning.
            foreach (var (yaw, pitch) in new[] { (0f, 0f), (90f, 0f), (0f, 60f), (-45f, -30f), (170f, 20f) })
            {
                Vector3 nose = HeadingMath.Orientation(yaw, pitch) * Vector3.forward;
                Assert.Less((nose - HeadingMath.Forward(yaw, pitch)).magnitude, 0.001f,
                    $"orientation disagrees with Forward at yaw={yaw} pitch={pitch}");
            }
        }

        [Test]
        public void OrientationPitchUpTiltsNoseUp()
        {
            Vector3 nose = HeadingMath.Orientation(0f, 60f) * Vector3.forward;
            Assert.Greater(nose.y, 0.8f); // sin60 ≈ 0.866
        }
    }
}
