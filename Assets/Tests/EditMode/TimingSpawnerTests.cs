using NUnit.Framework;
using UnityEngine;

namespace FlyingFishMomentum.Tests.EditMode
{
    public class TimingSpawnerTests
    {
        TimingPromptSpawner NewSpawner(out PlayerMomentumController momentum)
        {
            var go = new GameObject("spawner");
            var spawner = go.AddComponent<TimingPromptSpawner>();
            var mgo = new GameObject("m");
            momentum = mgo.AddComponent<PlayerMomentumController>();
            var mom = ScriptableObject.CreateInstance<MomentumSettings>();
            mom.MinSpeed = 8f;
            var timing = ScriptableObject.CreateInstance<TimingSettings>();
            momentum.Configure(mom, timing);
            momentum.SetLimits(100f, 120f);
            spawner.Configure(null, momentum, null, timing, mom, null);
            return spawner;
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (go.name == "spawner" || go.name == "m") Object.DestroyImmediate(go);
        }

        [Test]
        public void PromptOpensAfter60mNotAt59()
        {
            var spawner = NewSpawner(out _);
            float now = 0f;
            for (int i = 0; i < 11; i++) { now += 0.1f; spawner.Tick(now, 0.1f, 50f, FlightTier.Medium, false); }
            Assert.IsFalse(spawner.Active.Open, "opened before 60m");
            now += 0.1f; spawner.Tick(now, 0.1f, 50f, FlightTier.Medium, false);
            Assert.IsTrue(spawner.Active.Open);
            Assert.AreEqual(now + 1f, spawner.Active.TargetTime, 0.001f);
        }

        [Test]
        public void SecondPromptSuppressedWhileOpen()
        {
            var spawner = NewSpawner(out _);
            float now = 0f;
            for (int i = 0; i < 12; i++) { now += 0.1f; spawner.Tick(now, 0.1f, 50f, FlightTier.Medium, false); }
            float first = spawner.Active.TargetTime;
            for (int i = 0; i < 12; i++) { now += 0.1f; spawner.Tick(now, 0.1f, 50f, FlightTier.Medium, false); }
            Assert.IsTrue(spawner.Active.Open);
            Assert.AreEqual(first, spawner.Active.TargetTime, 0.001f);
        }

        [Test]
        public void SameScheduleIsDeterministic()
        {
            var a = NewSpawner(out _);
            float now = 0f;
            for (int i = 0; i < 12; i++) { now += 0.1f; a.Tick(now, 0.1f, 50f, FlightTier.Medium, false); }
            Cleanup();
            var b = NewSpawner(out _);
            now = 0f;
            for (int i = 0; i < 12; i++) { now += 0.1f; b.Tick(now, 0.1f, 50f, FlightTier.Medium, false); }
            Assert.AreEqual(a.Active.TargetTime, b.Active.TargetTime, 0.001f);
        }

        [Test]
        public void PressWithNothingOpenIsIgnored()
        {
            var spawner = NewSpawner(out var momentum);
            momentum.CurrentSpeed = 50f; momentum.TargetSpeed = 50f;
            spawner.Tick(1f, 0f, 50f, FlightTier.Medium, true);
            Assert.IsFalse(spawner.HasResolved);
            Assert.AreEqual(50f, momentum.CurrentSpeed, 0.001f);
        }

        [Test]
        public void FrozenClockNeverResolves()
        {
            var spawner = NewSpawner(out _);
            float now = 0f;
            for (int i = 0; i < 12; i++) { now += 0.1f; spawner.Tick(now, 0.1f, 50f, FlightTier.Medium, false); }
            Assert.IsTrue(spawner.Active.Open);
            for (int i = 0; i < 10; i++) spawner.Tick(now, 0.1f, 50f, FlightTier.Medium, false);
            Assert.IsTrue(spawner.Active.Open, "resolved with frozen clock");
            Assert.IsFalse(spawner.HasResolved);
        }

        [Test]
        public void PerfectPressAppliesBoost()
        {
            var spawner = NewSpawner(out var momentum);
            momentum.CurrentSpeed = 50f; momentum.TargetSpeed = 50f;
            float now = 0f;
            for (int i = 0; i < 12; i++) { now += 0.1f; spawner.Tick(now, 0.1f, 50f, FlightTier.Medium, false); }
            spawner.Tick(spawner.Active.TargetTime, 0f, 50f, FlightTier.Medium, true);
            Assert.IsTrue(spawner.HasResolved);
            Assert.AreEqual(TimingResult.Perfect, spawner.LastResult);
            Assert.AreEqual(66f, momentum.CurrentSpeed, 0.5f);
        }

        [Test]
        public void ExpiryResolvesMiss()
        {
            var spawner = NewSpawner(out var momentum);
            momentum.CurrentSpeed = 50f; momentum.TargetSpeed = 50f;
            float now = 0f;
            for (int i = 0; i < 12; i++) { now += 0.1f; spawner.Tick(now, 0.1f, 50f, FlightTier.Medium, false); }
            float target = spawner.Active.TargetTime;
            spawner.Tick(target + 0.3f, 0f, 50f, FlightTier.Medium, false);
            Assert.IsTrue(spawner.HasResolved);
            Assert.AreEqual(TimingResult.Miss, spawner.LastResult);
            Assert.IsFalse(spawner.Active.Open);
            Assert.Less(momentum.CurrentSpeed, 50f);
        }
    }
}
