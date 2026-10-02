using NUnit.Framework;

namespace FlyingFishMomentum.Tests.EditMode
{
    public class CameraMathTests
    {
        [Test]
        public void FovIsBaseAtMin_TierAtMax_NeverOvershoots()
        {
            Assert.AreEqual(60f, CameraMath.TargetFov(8f, 8f, 110f, 60f, 95f), 0.01f);
            Assert.AreEqual(95f, CameraMath.TargetFov(110f, 8f, 110f, 60f, 95f), 0.01f);
            Assert.AreEqual(77.5f, CameraMath.TargetFov(59f, 8f, 110f, 60f, 95f), 0.01f);
            Assert.AreEqual(60f, CameraMath.TargetFov(-50f, 8f, 110f, 60f, 95f), 0.01f, "below min clamps");
        }

        [Test]
        public void RollProportionalAndClamped()
        {
            Assert.AreEqual(-4f, CameraMath.RollTarget(100f, 200f), 0.01f);
            Assert.AreEqual(-8f, CameraMath.RollTarget(400f, 200f), 0.01f, "clamps at -8");
            Assert.AreEqual(0f, CameraMath.RollTarget(0f, 200f), 0.01f);
        }

        [Test]
        public void FovNeverExceedsMaxFov()
        {
            Assert.LessOrEqual(CameraMath.ClampFov(150f, 100f), 100f);
            Assert.AreEqual(95f, CameraMath.ClampFov(95f, 100f), 0.01f);
        }

        [Test]
        public void FinalFovCapsKickAtMaxFov()
        {
            // Live bug: the tier-up kick (+7 at full envelope) stacked on
            // top of an already-max display FOV, blowing past MaxFOV.
            Assert.AreEqual(100f, CameraMath.FinalFov(100f, 7f, 100f), 0.01f);
            Assert.AreEqual(87f, CameraMath.FinalFov(80f, 7f, 100f), 0.01f);
            Assert.AreEqual(80f, CameraMath.FinalFov(80f, 0f, 100f), 0.01f);
        }

        [Test]
        public void EnvelopesDecayToZero()
        {
            Assert.AreEqual(0f, CameraMath.Decay(1f, 2.5f, 1f), 0.01f);
            Assert.AreEqual(0.5f, CameraMath.Decay(1f, 2.5f, 0.2f), 0.01f);
        }
    }
}
