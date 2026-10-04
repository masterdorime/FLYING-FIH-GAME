using NUnit.Framework;
using UnityEngine;

namespace FlyingFishMomentum.Tests.EditMode
{
    public class ChargeSequencerTests
    {
        TimingSettings NewSettings() => ScriptableObject.CreateInstance<TimingSettings>();

        [Test]
        public void ArrowEdgesFromStick()
        {
            Assert.AreEqual(ChargeArrow.Right, ChargeSequencer.ArrowFromStick(new Vector2(0f, 0f), new Vector2(1f, 0f)));
            Assert.AreEqual(ChargeArrow.Left, ChargeSequencer.ArrowFromStick(new Vector2(0f, 0f), new Vector2(-1f, 0f)));
            Assert.AreEqual(ChargeArrow.Up, ChargeSequencer.ArrowFromStick(new Vector2(0f, 0f), new Vector2(0f, 1f)));
            Assert.AreEqual(ChargeArrow.Down, ChargeSequencer.ArrowFromStick(new Vector2(0f, 0f), new Vector2(0f, -1f)));
            Assert.IsNull(ChargeSequencer.ArrowFromStick(new Vector2(1f, 0f), new Vector2(1f, 0f)));
            Assert.IsNull(ChargeSequencer.ArrowFromStick(new Vector2(0f, 0f), new Vector2(0.2f, 0f)));
            // Near-cardinal tilts still count; true diagonals never guess
            // (a wrong guess lights red — ignoring is cheaper than missing).
            Assert.AreEqual(ChargeArrow.Right, ChargeSequencer.ArrowFromStick(new Vector2(0f, 0f), new Vector2(1f, 0.4f)));
            Assert.AreEqual(ChargeArrow.Up, ChargeSequencer.ArrowFromStick(new Vector2(0f, 0f), new Vector2(0.4f, 1f)));
            Assert.IsNull(ChargeSequencer.ArrowFromStick(new Vector2(0f, 0f), new Vector2(0.7f, 0.7f)));
            Assert.IsNull(ChargeSequencer.ArrowFromStick(new Vector2(0f, 0f), new Vector2(1f, 1f)));
        }

        [Test]
        public void CorrectKeyBanksPlusJackpot()
        {
            var s = NewSettings();
            var q = new ChargeSequencer();
            q.Begin(new[] { ChargeArrow.Up }, 0f);
            float gain = q.Tick(0.5f, ChargeArrow.Up, s);
            Assert.AreEqual(3f, gain, 0.001f); // +1 step +2 single-step jackpot
            Assert.IsFalse(q.IsActive);
        }

        [Test]
        public void WrongKeyFailsStepAndContinues()
        {
            var s = NewSettings();
            var q = new ChargeSequencer();
            q.Begin(new[] { ChargeArrow.Up, ChargeArrow.Down }, 0f);
            float first = q.Tick(0.5f, ChargeArrow.Left, s);
            Assert.AreEqual(0f, first, 0.001f);
            Assert.IsTrue(q.IsActive);
            Assert.AreEqual(ChargeArrow.Down, q.CurrentArrow);
            float second = q.Tick(1f, ChargeArrow.Down, s);
            Assert.AreEqual(1f, second, 0.001f); // step gain only, no jackpot
            Assert.IsFalse(q.IsActive);
        }

        [Test]
        public void TimeoutAbortsSequence()
        {
            var s = NewSettings();
            var q = new ChargeSequencer();
            q.Begin(new[] { ChargeArrow.Up }, 0f);
            float gain = q.Tick(9f, null, s);
            Assert.AreEqual(0f, gain, 0.001f);
            Assert.IsFalse(q.IsActive);
        }

        [Test]
        public void NoInputStallsNothing()
        {
            var s = NewSettings();
            var q = new ChargeSequencer();
            q.Begin(new[] { ChargeArrow.Up }, 0f);
            Assert.AreEqual(0f, q.Tick(0.5f, null, s), 0.001f);
            Assert.IsTrue(q.IsActive);
        }

        [Test]
        public void StepResultsTrackPerStep()
        {
            var s = NewSettings();
            var q = new ChargeSequencer();
            q.Begin(new[] { ChargeArrow.Up, ChargeArrow.Down }, 0f);
            q.Tick(0.5f, ChargeArrow.Up, s);
            q.Tick(1f, ChargeArrow.Left, s);
            Assert.AreEqual(new bool?[] { true, false }, q.StepResults);
        }
    }
}
