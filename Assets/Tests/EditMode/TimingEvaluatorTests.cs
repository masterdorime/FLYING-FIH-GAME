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
            // at speed 33, squared curve: perfect ≈ 0.0607, good ≈ 0.1495
            Assert.AreEqual(TimingResult.Perfect, TimingEvaluator.Evaluate(0.05f, 33f, s, 5f, 73f));
            Assert.AreEqual(TimingResult.Perfect, TimingEvaluator.Evaluate(-0.05f, 33f, s, 5f, 73f));
            Assert.AreEqual(TimingResult.Good, TimingEvaluator.Evaluate(0.062f, 33f, s, 5f, 73f));
            Assert.AreEqual(TimingResult.Miss, TimingEvaluator.Evaluate(-0.5f, 33f, s, 5f, 73f));
            Assert.AreEqual(0.07f, TimingEvaluator.PerfectWindowAt(5f, s, 5f, 73f, 0), 0.0001f);
            Assert.AreEqual(0.015f, TimingEvaluator.PerfectWindowAt(73f, s, 5f, 73f, 0), 0.0001f);
        }

        [Test]
        public void LateGraceAndPastIt()
        {
            var s = NewSettings();
            // good at 33u/s ≈ 0.1495; grace +0.06 → 0.19 Good, 0.23 Miss
            Assert.AreEqual(TimingResult.Good, TimingEvaluator.Evaluate(0.19f, 33f, s, 5f, 73f));
            Assert.AreEqual(TimingResult.Miss, TimingEvaluator.Evaluate(0.23f, 33f, s, 5f, 73f));
        }

        [Test]
        public void WindowsShrinkAtSpeedAndClampOutOfRange()
        {
            var s = NewSettings();
            Assert.AreEqual(TimingResult.Perfect, TimingEvaluator.Evaluate(0.05f, 5f, s, 5f, 73f));
            Assert.AreEqual(TimingResult.Miss, TimingEvaluator.Evaluate(0.05f, 73f, s, 5f, 73f));
            Assert.AreEqual(TimingResult.Miss, TimingEvaluator.Evaluate(0.05f, 500f, s, 5f, 73f));
            Assert.AreEqual(TimingResult.Perfect, TimingEvaluator.Evaluate(0.05f, 0f, s, 5f, 73f));
        }

        [Test]
        public void GoodGoneAtTopSpeed()
        {
            // MinGoodWindow is 0: at max speed only Perfect-or-Miss exists.
            var s = NewSettings();
            Assert.AreEqual(TimingResult.Perfect, TimingEvaluator.Evaluate(0.01f, 73f, s, 5f, 73f, 0));
            Assert.AreEqual(TimingResult.Miss, TimingEvaluator.Evaluate(0.05f, 73f, s, 5f, 73f, 0));
        }

        [Test]
        public void StreakNarrowsWindowsWithFloor()
        {
            var s = NewSettings();
            // 0.058 at speed 33 is Perfect unstreaked, Good after 3 straight hits
            Assert.AreEqual(TimingResult.Perfect, TimingEvaluator.Evaluate(0.058f, 33f, s, 5f, 73f, 0));
            Assert.AreEqual(TimingResult.Good, TimingEvaluator.Evaluate(0.058f, 33f, s, 5f, 73f, 3));
            // floor: 100 straight hits still leave a hittable perfect (x0.5)
            Assert.AreEqual(TimingResult.Perfect, TimingEvaluator.Evaluate(0.03f, 33f, s, 5f, 73f, 100));
            Assert.AreEqual(TimingResult.Good, TimingEvaluator.Evaluate(0.031f, 33f, s, 5f, 73f, 100));
        }
    }
}
