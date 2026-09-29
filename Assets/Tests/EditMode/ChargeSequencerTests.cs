using NUnit.Framework;
using UnityEngine;

namespace FlyingFishMomentum.Tests.EditMode
{
    public class ChargeSequencerTests
    {
        TimingSettings NewSettings() => ScriptableObject.CreateInstance<TimingSettings>();

        [Test]
        public void HoldFullDurationThenReleaseBanks()
        {
            var s = NewSettings();
            var q = new ChargeSequencer();
            q.Begin(new[] { ChargeStepKind.Hold }, 0f);
            float gain = 0f;
            for (int i = 0; i < 8; i++) gain = q.Tick(0.1f * (i + 1), 0.1f, true, false, 33f, s, 5f, 73f, 0);
            Assert.AreEqual(0f, gain, 0.001f); // still holding: nothing banked yet
            Assert.IsTrue(q.IsActive);
            gain = q.Tick(0.9f, 0.1f, false, false, 33f, s, 5f, 73f, 0);
            Assert.AreEqual(6f, gain, 0.001f); // +2 step +4 single-step jackpot
            Assert.IsFalse(q.IsActive);
        }

        [Test]
        public void EarlyReleaseFailsStep()
        {
            var s = NewSettings();
            var q = new ChargeSequencer();
            q.Begin(new[] { ChargeStepKind.Hold }, 0f);
            q.Tick(0.1f, 0.1f, true, false, 33f, s, 5f, 73f, 0);
            q.Tick(0.2f, 0.1f, true, false, 33f, s, 5f, 73f, 0);
            float gain = q.Tick(0.3f, 0.1f, false, false, 33f, s, 5f, 73f, 0);
            Assert.AreEqual(0f, gain, 0.001f);
            Assert.IsFalse(q.IsActive);
        }

        [Test]
        public void OverholdFizzles()
        {
            var s = NewSettings();
            var q = new ChargeSequencer();
            q.Begin(new[] { ChargeStepKind.Hold }, 0f);
            float gain = 0f;
            for (int i = 0; i < 14; i++) gain = q.Tick(0.1f * (i + 1), 0.1f, true, false, 33f, s, 5f, 73f, 0);
            Assert.AreEqual(0f, gain, 0.001f); // held past the 1.3s limit: greed gets nothing
            Assert.IsFalse(q.IsActive);
        }

        [Test]
        public void TapPerfectBanks()
        {
            var s = NewSettings();
            var q = new ChargeSequencer();
            q.Begin(new[] { ChargeStepKind.Tap }, 0f);
            float gain = q.Tick(0.6f, 0.1f, false, true, 33f, s, 5f, 73f, 0);
            Assert.AreEqual(6f, gain, 0.001f); // +2 step +4 jackpot
            Assert.IsFalse(q.IsActive);
        }

        [Test]
        public void TapExpiryFailsQuietly()
        {
            var s = NewSettings();
            var q = new ChargeSequencer();
            q.Begin(new[] { ChargeStepKind.Tap }, 0f);
            float gain = q.Tick(1.0f, 0.1f, false, false, 33f, s, 5f, 73f, 0);
            Assert.AreEqual(0f, gain, 0.001f);
            Assert.IsFalse(q.IsActive);
        }

        [Test]
        public void JackpotNeedsEveryStep()
        {
            var s = NewSettings();
            var q = new ChargeSequencer();
            q.Begin(new[] { ChargeStepKind.Tap, ChargeStepKind.Tap }, 0f);
            float first = q.Tick(0.6f, 0.1f, false, true, 33f, s, 5f, 73f, 0);
            Assert.AreEqual(2f, first, 0.001f); // step gain only: sequence continues
            Assert.IsTrue(q.IsActive);
            float second = q.Tick(2.0f, 0.1f, false, false, 33f, s, 5f, 73f, 0);
            Assert.AreEqual(0f, second, 0.001f); // expiry, no jackpot
            Assert.IsFalse(q.IsActive);
        }
    }
}
