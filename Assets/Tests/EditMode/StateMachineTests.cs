using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace FlyingFishMomentum.Tests.EditMode
{
    public class StateMachineTests
    {
        readonly List<Object> _transient = new List<Object>();

        FlightTierProfile Profile(FlightTier tier, float max, float accel)
        {
            var p = ScriptableObject.CreateInstance<FlightTierProfile>();
            p.Tier = tier; p.MaxSpeed = max; p.Acceleration = accel;
            _transient.Add(p);
            return p;
        }

        FlightStateMachine NewStateMachineWithLowAndHigh(out PlayerMomentumController momentum)
        {
            var go = new GameObject("sm");
            momentum = go.AddComponent<PlayerMomentumController>();
            var settings = ScriptableObject.CreateInstance<MomentumSettings>();
            _transient.Add(settings);
            settings.MinSpeed = 8f;
            settings.DragSwimming = 1.5f;
            settings.DragFlying = 0.6f;
            settings.BreachSpeedThreshold = 20f;
            momentum.Configure(settings);
            var sm = go.AddComponent<FlightStateMachine>();
            sm.Configure(
                new List<FlightTierProfile>
                {
                    Profile(FlightTier.Low, 23f, 45f),
                    Profile(FlightTier.High, 47f, 80f),
                },
                momentum, settings);
            return sm;
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (go.name == "sm") Object.DestroyImmediate(go);
            foreach (var o in _transient) Object.DestroyImmediate(o);
            _transient.Clear();
        }

        [Test]
        public void FastUpwardBreachEntersFly()
        {
            var next = SurfaceCrossing.Evaluate(-1f, 1f, 32f, 30f, PlayerLocomotionState.Swimming);
            Assert.AreEqual(PlayerLocomotionState.Flying, next);
        }

        [Test]
        public void SlowUpwardBreachStaysSwimming()
        {
            var next = SurfaceCrossing.Evaluate(-1f, 1f, 20f, 30f, PlayerLocomotionState.Swimming);
            Assert.AreEqual(PlayerLocomotionState.Swimming, next);
        }

        [Test]
        public void AnyDownwardCrossingSwims()
        {
            var next = SurfaceCrossing.Evaluate(1f, -1f, 110f, 30f, PlayerLocomotionState.Flying);
            Assert.AreEqual(PlayerLocomotionState.Swimming, next);
        }

        [Test]
        public void NoCrossingKeepsState()
        {
            Assert.AreEqual(PlayerLocomotionState.Swimming,
                SurfaceCrossing.Evaluate(-3f, -2f, 35f, 30f, PlayerLocomotionState.Swimming));
            Assert.AreEqual(PlayerLocomotionState.Flying,
                SurfaceCrossing.Evaluate(5f, 6f, 35f, 30f, PlayerLocomotionState.Flying));
        }

        [Test]
        public void SetTierPushesLimitsAndFiresEventOnce()
        {
            var sm = NewStateMachineWithLowAndHigh(out var momentum);
            int fires = 0;
            sm.OnTierChanged += (_, _) => fires++;
            sm.SetTier(FlightTier.High);
            Assert.AreEqual(1, fires);
            Assert.AreEqual(47f, momentum.CurrentMaxSpeed);
            Assert.AreEqual(FlightTier.High, sm.ActiveTier);
            sm.SetTier(FlightTier.High);
            Assert.AreEqual(1, fires, "same-tier set must not re-fire");
        }

        [Test]
        public void SetTierUnknownToListIsNoOp()
        {
            var sm = NewStateMachineWithLowAndHigh(out _);
            int fires = 0;
            sm.OnTierChanged += (_, _) => fires++;
            sm.SetTier(FlightTier.Max); // not in the 2-profile list
            Assert.AreEqual(0, fires);
        }

        [Test]
        public void EvaluateSurfaceDrivesLocomotion()
        {
            var sm = NewStateMachineWithLowAndHigh(out var momentum);
            momentum.CurrentSpeed = 23f;
            sm.EvaluateSurface(-1f, 1f);
            Assert.AreEqual(PlayerLocomotionState.Flying, sm.Locomotion);
            sm.EvaluateSurface(1f, -1f);
            Assert.AreEqual(PlayerLocomotionState.Swimming, sm.Locomotion);
        }
    }
}
