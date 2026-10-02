using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace FlyingFishMomentum.Tests.EditMode
{
    public class FlightGaugeTests
    {
        GameObject _go;
        readonly List<Object> _transient = new List<Object>();

        FlightTierProfile Profile(FlightTier tier)
        {
            var p = ScriptableObject.CreateInstance<FlightTierProfile>();
            p.Tier = tier;
            p.MaxSpeed = 10f + (float)tier * 10f;
            p.Acceleration = 45f;
            _transient.Add(p);
            return p;
        }

        FlightGaugeSystem NewGauge(out FlightStateMachine sm)
        {
            _go = new GameObject("gauge");
            var momentum = _go.AddComponent<PlayerMomentumController>();
            var momSettings = ScriptableObject.CreateInstance<MomentumSettings>();
            _transient.Add(momSettings);
            momSettings.MinSpeed = 5f;
            momSettings.DragSwimming = 1.5f;
            momSettings.DragFlying = 0.6f;
            momSettings.BreachSpeedThreshold = 20f;
            var timing = ScriptableObject.CreateInstance<TimingSettings>();
            _transient.Add(timing);
            momentum.Configure(momSettings, timing);
            sm = _go.AddComponent<FlightStateMachine>();
            sm.Configure(
                new List<FlightTierProfile>
                {
                    Profile(FlightTier.None),
                    Profile(FlightTier.Low),
                    Profile(FlightTier.Medium),
                    Profile(FlightTier.High),
                    Profile(FlightTier.Max),
                },
                momentum, momSettings);
            var settings = ScriptableObject.CreateInstance<FlightGaugeSettings>();
            _transient.Add(settings);
            settings.MaxGauge = 120f;
            settings.StartGauge = 0f;
            settings.FlyDrainPerSecond = 3.5f;
            settings.MissDrain = 10f;
            settings.TierThresholds = new float[] { 20f, 40f, 70f, 100f };
            var gauge = _go.AddComponent<FlightGaugeSystem>();
            gauge.Configure(sm, settings, null);
            return gauge;
        }

        [TearDown]
        public void Cleanup()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            _go = null;
            foreach (var o in _transient) Object.DestroyImmediate(o);
            _transient.Clear();
        }

        [Test]
        public void FillCrossingUpShiftsTier()
        {
            var gauge = NewGauge(out var sm);
            Assert.AreEqual(FlightTier.None, sm.ActiveTier);
            gauge.AddFill(20f);
            Assert.AreEqual(FlightTier.Low, sm.ActiveTier);
            gauge.AddFill(20f);
            Assert.AreEqual(FlightTier.Medium, sm.ActiveTier);
            gauge.AddFill(30f);
            Assert.AreEqual(FlightTier.High, sm.ActiveTier);
            gauge.AddFill(30f);
            Assert.AreEqual(FlightTier.Max, sm.ActiveTier);
        }

        [Test]
        public void FullGaugeLaunchesSwimmerSkyward()
        {
            // Rising edge to full while swimming: locomotion flips to fly,
            // tank stays full (flight drains it), exactly one launch fires.
            var gauge = NewGaugeWithMover(out var sm, out var mover);
            gauge.AddFill(120f);
            Assert.AreEqual(PlayerLocomotionState.Flying, sm.Locomotion);
            Assert.AreEqual(120f, gauge.CurrentGauge, 0.001f);
            Assert.AreEqual(1, mover.LaunchCount);
        }

        [Test]
        public void FullTankDoesNotChainLaunch()
        {
            // Landing (or sitting) with a full tank fires nothing: only the
            // rising edge launches. Gauge must dip and refill first.
            var gauge = NewGaugeWithMover(out var sm, out var mover);
            gauge.AddFill(120f);
            Assert.AreEqual(1, mover.LaunchCount);
            gauge.AddFill(10f);
            gauge.AddFill(10f);
            Assert.AreEqual(1, mover.LaunchCount, "re-filled full tank re-launched");
        }

        FlightGaugeSystem NewGaugeWithMover(out FlightStateMachine sm, out PlayerMovementController mover)
        {
            // Fresh rig (not shared with NewGauge): gauge needs a movement
            // ref for launch, so build momentum + mover together here.
            var go = new GameObject("gauge");
            var momentum = go.AddComponent<PlayerMomentumController>();
            var momSettings = ScriptableObject.CreateInstance<MomentumSettings>();
            _transient.Add(momSettings);
            momSettings.MinSpeed = 5f;
            var timing = ScriptableObject.CreateInstance<TimingSettings>();
            _transient.Add(timing);
            momentum.Configure(momSettings, timing);
            sm = go.AddComponent<FlightStateMachine>();
            sm.Configure(
                new List<FlightTierProfile>
                {
                    Profile(FlightTier.None),
                    Profile(FlightTier.Low),
                    Profile(FlightTier.Medium),
                    Profile(FlightTier.High),
                    Profile(FlightTier.Max),
                },
                momentum, momSettings);
            var settings = ScriptableObject.CreateInstance<FlightGaugeSettings>();
            _transient.Add(settings);
            settings.MaxGauge = 120f;
            settings.StartGauge = 0f;
            settings.FlyDrainPerSecond = 3.5f;
            settings.MissDrain = 10f;
            settings.TierThresholds = new float[] { 20f, 40f, 70f, 100f };
            var gauge = go.AddComponent<FlightGaugeSystem>();
            var player = new GameObject("player");
            player.AddComponent<CharacterController>();
            mover = player.AddComponent<PlayerMovementController>();
            mover.Configure(sm, momentum, momSettings);
            gauge.Configure(sm, settings, null, mover);
            _transient.Add(go);
            _transient.Add(player);
            return gauge;
        }

        [Test]
        public void ExactThresholdReadsUpper()
        {
            var gauge = NewGauge(out var sm);
            gauge.AddFill(70f);
            Assert.AreEqual(FlightTier.High, sm.ActiveTier);
        }

        [Test]
        public void FlyingDrainDropsTier()
        {
            var gauge = NewGauge(out var sm);
            gauge.AddFill(25f);
            Assert.AreEqual(FlightTier.Low, sm.ActiveTier);
            gauge.Tick(2f, flying: true);
            gauge.Tick(2f, flying: true);
            Assert.AreEqual(FlightTier.None, sm.ActiveTier);
        }

        [Test]
        public void MissAtZeroStaysPut()
        {
            var gauge = NewGauge(out var sm);
            int fires = 0;
            sm.OnTierChanged += (_, _) => fires++;
            gauge.DrainMiss();
            Assert.AreEqual(0f, gauge.CurrentGauge, 0.001f);
            Assert.AreEqual(0, fires);
        }

        [Test]
        public void JackpotOverfillCaps()
        {
            var gauge = NewGauge(out var sm);
            gauge.AddFill(200f);
            Assert.AreEqual(120f, gauge.CurrentGauge, 0.001f);
            Assert.AreEqual(FlightTier.Max, sm.ActiveTier);
        }

        [Test]
        public void SwimDoesNotDrain()
        {
            var gauge = NewGauge(out var sm);
            gauge.AddFill(50f);
            gauge.Tick(10f, flying: false);
            Assert.AreEqual(50f, gauge.CurrentGauge, 0.001f);
        }
    }
}
