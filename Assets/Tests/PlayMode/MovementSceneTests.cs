using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace FlyingFishMomentum.Tests.PlayMode
{
    // Requires the M1_MovementProof scene (Task 7): red until the scene lands.
    public class MovementSceneTests : InputTestFixture
    {
        [SetUp]
        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Gamepad>();
        }

        static IEnumerator WaitForState(FlightStateMachine sm, PlayerLocomotionState want, float timeout = 5f)
        {
            float t = 0f;
            while (sm.Locomotion != want && t < timeout) { t += Time.deltaTime; yield return null; }
        }

        [UnityTest]
        public IEnumerator MaxSpeedRunTravelsForwardInBounds()
        {
            // No steering input: heading holds at spawn yaw/pitch (0, 0), fish porpoises level toward +Z.
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            sm.SetTier(FlightTier.Max);
            sm.Momentum.TargetSpeed = 110f;
            yield return new WaitForSeconds(2f);
            var p = sm.transform.position;
            Assert.IsFalse(float.IsNaN(p.x + p.y + p.z), "position went NaN at max speed");
            Assert.Greater(p.z, 100f, "fish did not travel forward at speed");
            Assert.Less(Mathf.Abs(p.x), 150f);
            Assert.GreaterOrEqual(p.y, -12f); Assert.Less(p.y, 120f);
        }

        [UnityTest]
        public IEnumerator SwimFlySwimRoundTripViaBreach()
        {
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            Assert.AreEqual(PlayerLocomotionState.Swimming, sm.Locomotion, "spawn must be underwater Swimming");
            sm.SetTier(FlightTier.Low);
            sm.Momentum.CurrentSpeed = 35f; sm.Momentum.TargetSpeed = 35f;
            Press(Keyboard.current.wKey); // climb at 35 >= threshold 30
            yield return WaitForState(sm, PlayerLocomotionState.Flying);
            Release(Keyboard.current.wKey);
            Assert.AreEqual(PlayerLocomotionState.Flying, sm.Locomotion, "fast breach did not enter Fly");
            Press(Keyboard.current.sKey); // dive back down
            yield return WaitForState(sm, PlayerLocomotionState.Swimming);
            Release(Keyboard.current.sKey);
            Assert.AreEqual(PlayerLocomotionState.Swimming, sm.Locomotion, "downward crossing did not re-enter Swim");
        }
    }
}
