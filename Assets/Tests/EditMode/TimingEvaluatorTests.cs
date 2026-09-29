using NUnit.Framework;
using UnityEngine;

namespace FlyingFishMomentum.Tests.EditMode
{
    public class TimingEvaluatorTests
    {
        TimingSettings NewSettings() => ScriptableObject.CreateInstance<TimingSettings>();

        [Test]
        public void WindowEdges()
        {
            var s = NewSettings();
            // at speed 50: perfect ≈ 0.0556, good ≈ 0.143 (margins are float-safe)
            Assert.AreEqual(TimingResult.Perfect, TimingEvaluator.Evaluate(0.05f, 50f, s, 8f, 110f));
            Assert.AreEqual(TimingResult.Perfect, TimingEvaluator.Evaluate(-0.05f, 50f, s, 8f, 110f));
            Assert.AreEqual(TimingResult.Good, TimingEvaluator.Evaluate(0.06f, 50f, s, 8f, 110f));
            Assert.AreEqual(TimingResult.Miss, TimingEvaluator.Evaluate(-0.5f, 50f, s, 8f, 110f));
            Assert.AreEqual(0.07f, TimingEvaluator.PerfectWindowAt(8f, s, 8f, 110f), 0.0001f);
            Assert.AreEqual(0.035f, TimingEvaluator.PerfectWindowAt(110f, s, 8f, 110f), 0.0001f);
        }

        [Test]
        public void LateGraceAndPastIt()
        {
            var s = NewSettings();
            // good at 50u/s ≈ 0.143; grace +0.06 → 0.19 Good, 0.21 Miss
            Assert.AreEqual(TimingResult.Good, TimingEvaluator.Evaluate(0.19f, 50f, s, 8f, 110f));
            Assert.AreEqual(TimingResult.Miss, TimingEvaluator.Evaluate(0.21f, 50f, s, 8f, 110f));
        }

        [Test]
        public void WindowsShrinkAtSpeedAndClampOutOfRange()
        {
            var s = NewSettings();
            Assert.AreEqual(TimingResult.Perfect, TimingEvaluator.Evaluate(0.05f, 8f, s, 8f, 110f));
            Assert.AreEqual(TimingResult.Good, TimingEvaluator.Evaluate(0.05f, 110f, s, 8f, 110f));
            Assert.AreEqual(TimingResult.Good, TimingEvaluator.Evaluate(0.05f, 500f, s, 8f, 110f));
            Assert.AreEqual(TimingResult.Perfect, TimingEvaluator.Evaluate(0.05f, 0f, s, 8f, 110f));
        }
    }
}
