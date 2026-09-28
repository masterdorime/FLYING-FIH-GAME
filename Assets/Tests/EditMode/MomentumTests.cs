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
            s.MinSpeed = 8f;
            s.DragSwimming = 1.5f;
            s.DragFlying = 0.6f;
            s.BreachSpeedThreshold = 30f;
            s.DeviationDragGain = 3f;
            m.Configure(s);
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
            Assert.AreEqual(8f, m.CurrentSpeed, 0.01f);
        }
    }
}
