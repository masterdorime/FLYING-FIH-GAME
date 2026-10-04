using NUnit.Framework;
using UnityEngine;

namespace FlyingFishMomentum.Tests.EditMode
{
    public class MomentumTests
    {
        PlayerMomentumController NewMomentum(float max = 35f, float accel = 45f)
        {
            var go = new GameObject("m");
            var m = go.AddComponent<PlayerMomentumController>();
            var s = ScriptableObject.CreateInstance<MomentumSettings>();
            s.MinSpeed = 5f;
            s.DragSwimming = 1.5f;
            s.DragFlying = 0.6f;
            s.BreachSpeedThreshold = 20f;
            s.DeviationDragGain = 3f;
            s.DeviationSpeedPenalty = 0.5f;
            var t = ScriptableObject.CreateInstance<TimingSettings>();
            m.Configure(s, t);
            m.SetLimits(max, accel);
            return m;
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (go.name == "m") Object.DestroyImmediate(go);
        }

        [Test]
        public void AddSpeedClampsToActiveMax()
        {
            var m = NewMomentum();
            m.CurrentSpeed = 30f;
            m.AddSpeed(12f);
            Assert.AreEqual(35f, m.CurrentSpeed, 0.001f);
        }

        [Test]
        public void DragFallsTowardTargetButNeverBelowMin()
        {
            var m = NewMomentum();
            m.CurrentSpeed = 60f;
            m.TargetSpeed = 8f;
            m.Tick(1f, 1.5f);
            Assert.Less(m.CurrentSpeed, 60f);
            for (int i = 0; i < 120; i++) m.Tick(1f, 1.5f);
            Assert.AreEqual(8f, m.CurrentSpeed, 0.01f);
        }

        [Test]
        public void AccelRisesTowardTargetAtProfileRate()
        {
            var m = NewMomentum();
            m.CurrentSpeed = 8f;
            m.TargetSpeed = 35f;
            m.Tick(0.5f, 1.5f); // 45 * 0.5 = 22.5 -> 30.5
            Assert.AreEqual(30.5f, m.CurrentSpeed, 0.01f);
        }

        [Test]
        public void ZeroDeviationBehavesExactlyAsBefore()
        {
            var m = NewMomentum(max: 120f);
            m.CurrentSpeed = 60f; m.TargetSpeed = 8f;
            m.Tick(1f, 1.5f, 0f);
            Assert.AreEqual(58.5f, m.CurrentSpeed, 0.01f); // 60 - 1.5
        }

        [Test]
        public void FullBackwardDeviationMultipliesDrag()
        {
            var m = NewMomentum(max: 120f);
            m.CurrentSpeed = 60f; m.TargetSpeed = 8f;
            m.Tick(1f, 1.5f, 180f); // gain 3 -> x4 -> 6.0 drag
            Assert.AreEqual(54f, m.CurrentSpeed, 0.01f);
        }

        [Test]
        public void DeviationDragNeverBreachesMinSpeed()
        {
            var m = NewMomentum();
            m.CurrentSpeed = 9f; m.TargetSpeed = 8f;
            for (int i = 0; i < 200; i++) m.Tick(1f, 1.5f, 180f);
            Assert.AreEqual(5f, m.CurrentSpeed, 0.01f);
        }

        [Test]
        public void AtTargetSpeedWithDeviationBleedsBelowMax()
        {
            // Review fix: off-forward flight must cost speed even when the
            // target chase has nothing to do (Current == Target == max).
            var m = NewMomentum(max: 110f, accel: 120f);
            m.CurrentSpeed = 110f; m.TargetSpeed = 110f;
            m.Tick(1f, 0.6f, 60f); // cone edge: effective target ~91.7, drag 1.2
            Assert.Less(m.CurrentSpeed, 110f);
            Assert.Greater(m.CurrentSpeed, 90f);
        }

        [Test]
        public void FlyBonusRaisesChasedTarget()
        {
            // Air must sustain a higher top speed than swimming at the same tier.
            var m = NewMomentum(max: 100f, accel: 120f);
            m.CurrentSpeed = 50f; m.TargetSpeed = 50f;
            m.Tick(1f, 0.6f, 0f, 1.2f); // air bonus: effective target 60
            Assert.Greater(m.CurrentSpeed, 50f);
        }

        [Test]
        public void ApplyTimingResultFollowsTable()
        {
            var m = NewMomentum(max: 100f, accel: 120f);
            m.CurrentSpeed = 50f; m.TargetSpeed = 50f;
            m.ApplyTimingResult(TimingResult.Good, FlightTier.Medium);
            Assert.AreEqual(51f, m.CurrentSpeed, 0.001f);
            m.ApplyTimingResult(TimingResult.Perfect, FlightTier.Max);
            Assert.AreEqual(53f, m.CurrentSpeed, 0.001f);
            m.ApplyTimingResult(TimingResult.Miss, FlightTier.Medium);
            Assert.AreEqual(47f, m.CurrentSpeed, 0.001f);
        }

        [Test]
        public void MissNeverBreaksMinSpeed()
        {
            var m = NewMomentum();
            m.CurrentSpeed = 10f; m.TargetSpeed = 8f;
            m.ApplyTimingResult(TimingResult.Miss, FlightTier.Max); // -16
            m.Tick(1f, 1.5f);
            Assert.AreEqual(5f, m.CurrentSpeed, 0.01f);
        }

        [Test]
        public void PerfectOverflowsTierMaxThenDecays()
        {
            var m = NewMomentum(max: 50f, accel: 120f);
            m.CurrentSpeed = 50f; m.TargetSpeed = 50f;
            m.ApplyTimingResult(TimingResult.Perfect, FlightTier.Medium); // +1 → 51, above max
            Assert.AreEqual(51f, m.CurrentSpeed, 0.001f);
            m.Tick(1f, 0.6f); // gain sticks: target ratcheted to 51, no bleed
            Assert.AreEqual(51f, m.CurrentSpeed, 0.001f);
            Assert.AreEqual(51f, m.TargetSpeed, 0.001f);
        }

        [Test]
        public void PerfectRespectsSpeedCap()
        {
            var m = NewMomentum(max: 50f, accel: 120f);
            m.CurrentSpeed = 69f; m.TargetSpeed = 50f;
            m.ApplyTimingResult(TimingResult.Perfect, FlightTier.Medium); // +1 → capped 70
            Assert.AreEqual(70f, m.CurrentSpeed, 0.001f);
        }

        [Test]
        public void PerfectGainSurvivesSwimDragToNextPrompt()
        {
            // Bug repro: a Perfect from cruise must still be there after a
            // typical beat gap, not bled away by drag toward the old target.
            var m = NewMomentum(max: 100f, accel: 120f);
            m.CurrentSpeed = 10f; m.TargetSpeed = 10f;
            m.ApplyTimingResult(TimingResult.Perfect, FlightTier.Medium); // +1 → 11
            for (int i = 0; i < 240; i++) m.Tick(1f / 60f, 1.5f); // 4s of swim
            Assert.Greater(m.CurrentSpeed, 10f, "Perfect gain bled away before the next prompt");
        }

        [Test]
        public void ChargeGainRatchetsAndCaps()
        {
            // Charge gains stick (target ratchets too) and obey the cap.
            var m = NewMomentum(max: 100f, accel: 120f);
            m.CurrentSpeed = 10f; m.TargetSpeed = 10f;
            m.AddChargeGain(2f);
            Assert.AreEqual(12f, m.CurrentSpeed, 0.001f);
            Assert.AreEqual(12f, m.TargetSpeed, 0.001f);
            m.AddChargeGain(100f);
            Assert.AreEqual(70f, m.CurrentSpeed, 0.001f);
            Assert.AreEqual(70f, m.TargetSpeed, 0.001f);
        }
    }
}
