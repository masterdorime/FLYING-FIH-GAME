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
            // at speed 50, squared curve: perfect ≈ 0.0607, good ≈ 0.1647
            Assert.AreEqual(TimingResult.Perfect, TimingEvaluator.Evaluate(0.05f, 50f, s, 8f, 110f));
            Assert.AreEqual(TimingResult.Perfect, TimingEvaluator.Evaluate(-0.05f, 50f, s, 8f, 110f));
            Assert.AreEqual(TimingResult.Good, TimingEvaluator.Evaluate(0.062f, 50f, s, 8f, 110f));
            Assert.AreEqual(TimingResult.Miss, TimingEvaluator.Evaluate(-0.5f, 50f, s, 8f, 110f));
            Assert.AreEqual(0.07f, TimingEvaluator.PerfectWindowAt(8f, s, 8f, 110f, 0), 0.0001f);
            Assert.AreEqual(0.015f, TimingEvaluator.PerfectWindowAt(110f, s, 8f, 110f, 0), 0.0001f);
        }

        [Test]
        public void LateGraceAndPastIt()
        {
            var s = NewSettings();
            // good at 50u/s ≈ 0.1647; grace +0.06 → 0.19 Good, 0.23 Miss
            Assert.AreEqual(TimingResult.Good, TimingEvaluator.Evaluate(0.19f, 50f, s, 8f, 110f));
            Assert.AreEqual(TimingResult.Miss, TimingEvaluator.Evaluate(0.23f, 50f, s, 8f, 110f));
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

        [Test]
        public void StreakNarrowsWindowsWithFloor()
        {
            var s = NewSettings();
            // 0.058 at speed 50 is Perfect unstreaked, Good after 3 straight hits
            Assert.AreEqual(TimingResult.Perfect, TimingEvaluator.Evaluate(0.058f, 50f, s, 8f, 110f, 0));
            Assert.AreEqual(TimingResult.Good, TimingEvaluator.Evaluate(0.058f, 50f, s, 8f, 110f, 3));
            // floor: 100 straight hits still leave a hittable perfect (x0.5)
            Assert.AreEqual(TimingResult.Perfect, TimingEvaluator.Evaluate(0.03f, 50f, s, 8f, 110f, 100));
            Assert.AreEqual(TimingResult.Good, TimingEvaluator.Evaluate(0.031f, 50f, s, 8f, 110f, 100));
        }
    }
}
