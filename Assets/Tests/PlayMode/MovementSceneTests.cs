using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
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
            // No devices, no Press(), no manual Update() pumping: synthetic
            // input stalls the player loop in batchmode (see ledger). Steering
            // is driven via TickMove directly; device→value plumbing is engine
            // behavior covered by InputAssetTests plus the human feel pass.
            // Tests that call TickMove manually MUST call SetTier BEFORE
            // mover.enabled = false: disabling unsubscribes the tier profile
            // (turn/gravity would freeze at the spawn tier), and the scene
            // Update would otherwise call TickMove again each frame
            // (double-stepped simulation).
        }

        [UnitySetUp]
        public IEnumerator LoadM1Scene()
        {
            yield return SceneManager.LoadSceneAsync(
                "Assets/Scenes/M1_MovementProof.unity", LoadSceneMode.Single);
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
            // Steering is driven via TickMove directly, not Press(): synthetic
            // input events stall the player loop in batchmode (see Setup note
            // and ledger). Device→value plumbing is engine behavior covered by
            // InputAssetTests (bindings) plus the human feel pass (real keys).
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            var mover = sm.GetComponent<PlayerMovementController>();
            Assert.AreEqual(PlayerLocomotionState.Swimming, sm.Locomotion, "spawn must be underwater Swimming");
            sm.SetTier(FlightTier.Low);
            mover.enabled = false; // single-step AFTER SetTier (see Setup note)
            sm.Momentum.CurrentSpeed = 35f; sm.Momentum.TargetSpeed = 35f;
            float t = 0f;
            while (sm.Locomotion != PlayerLocomotionState.Flying && t < 5f)
            {
                t += Time.deltaTime;
                mover.TickMove(new Vector2(0f, 1f), Time.deltaTime);
                yield return null;
            }
            Assert.AreEqual(PlayerLocomotionState.Flying, sm.Locomotion, "fast breach did not enter Fly");
            t = 0f;
            while (sm.Locomotion != PlayerLocomotionState.Swimming && t < 5f)
            {
                t += Time.deltaTime;
                mover.TickMove(new Vector2(0f, -1f), Time.deltaTime);
                yield return null;
            }
            Assert.AreEqual(PlayerLocomotionState.Swimming, sm.Locomotion, "downward crossing did not re-enter Swim");
        }
        [UnityTest]
        public IEnumerator FlyingAtSpeedWithFullUpInputClimbs()
        {
            // Glide fantasy check: flight with speed + full climb input must
            // gain altitude (no speed = glide/sink is by design, untested here).
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            var mover = sm.GetComponent<PlayerMovementController>();
            sm.SetTier(FlightTier.Max);
            mover.enabled = false; // single-step AFTER SetTier (see Setup note)
            sm.Momentum.CurrentSpeed = 110f; sm.Momentum.TargetSpeed = 110f;
            float t = 0f;
            while (sm.Locomotion != PlayerLocomotionState.Flying && t < 5f)
            {
                t += Time.deltaTime;
                mover.TickMove(new Vector2(0f, 1f), Time.deltaTime);
                yield return null;
            }
            Assert.AreEqual(PlayerLocomotionState.Flying, sm.Locomotion, "setup did not reach Fly");
            float y0 = mover.transform.position.y;
            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime;
                mover.TickMove(new Vector2(0f, 1f), Time.deltaTime);
                yield return null;
            }
            Assert.Greater(mover.transform.position.y - y0, 10f, "full climb input at Max speed did not climb in Fly");
        }
        [UnityTest]
        public IEnumerator MaxSpeedSlalomDoesNotTunnelThroughIslands()
        {
            // Spec §3 anti-tunneling evidence: home onto Island_West_1
            // (x -20..-10, z 25..35, y -7.5..17.5) at Max speed, then push
            // into its face. Swept CharacterController.Move must stop/slide,
            // never enter the volume.
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            var mover = sm.GetComponent<PlayerMovementController>();
            sm.SetTier(FlightTier.Max);
            mover.enabled = false; // single-step AFTER SetTier (see Setup note)
            sm.Momentum.CurrentSpeed = 110f; sm.Momentum.TargetSpeed = 110f;
            float t = 0f;
            while (mover.transform.position.z < 24f && t < 5f)
            {
                t += Time.deltaTime;
                float steer = Mathf.Clamp((-15f - mover.transform.position.x) * 0.2f, -1f, 1f);
                mover.TickMove(new Vector2(steer, 0f), Time.deltaTime);
                AssertNotInsideWestIsland(mover.transform.position);
                yield return null;
            }
            Assert.GreaterOrEqual(mover.transform.position.z, 24f, "homing never reached the island face");
            t = 0f;
            while (t < 2f)
            {
                t += Time.deltaTime;
                mover.TickMove(Vector2.zero, Time.deltaTime);
                var p = mover.transform.position;
                Assert.IsFalse(float.IsNaN(p.x + p.y + p.z), "position went NaN on island contact");
                AssertNotInsideWestIsland(p);
                yield return null;
            }
            Assert.Less(Mathf.Abs(mover.transform.position.x), 150f);
        }

        static void AssertNotInsideWestIsland(Vector3 p)
        {
            bool inside = p.x > -19.9f && p.x < -10.1f && p.z > 25.1f && p.z < 34.9f
                && p.y > -7.4f && p.y < 17.4f;
            Assert.IsFalse(inside, $"tunneled into Island_West_1 at {p}");
        }

        [UnityTest]
        public IEnumerator HoldingConeEdgeBleedsSpeedInGame()
        {
            // In-game pin for forward pressure: full lateral stick pins yaw
            // at the cone edge, which must cost top speed even at tier max.
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            var mover = sm.GetComponent<PlayerMovementController>();
            sm.SetTier(FlightTier.Max);
            mover.enabled = false; // single-step AFTER SetTier (see Setup note)
            sm.Momentum.CurrentSpeed = 110f; sm.Momentum.TargetSpeed = 110f;
            float t = 0f;
            while (t < 3f)
            {
                t += Time.deltaTime;
                mover.TickMove(new Vector2(1f, 0f), Time.deltaTime);
                yield return null;
            }
            Assert.AreEqual(60f, mover.Yaw, 0.5f, "full stick did not pin at cone edge");
            Assert.Less(sm.Momentum.CurrentSpeed, 109f, "cone-edge flight did not bleed speed");
        }

        [UnityTest]
        public IEnumerator AirTurnsWiderThanWaterTurns()
        {
            // Fixed-step so the comparison is frame-rate independent: 10
            // sixtieths of full stick. Water at 200 deg/s gains ~33 deg;
            // air must gain clearly less (wide swoops, not tight arcs).
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            var mover = sm.GetComponent<PlayerMovementController>();
            sm.SetTier(FlightTier.Max);
            mover.enabled = false; // single-step AFTER SetTier (see Setup note)
            sm.Momentum.CurrentSpeed = 110f; sm.Momentum.TargetSpeed = 110f;
            const float step = 1f / 60f;
            for (int i = 0; i < 10; i++) mover.TickMove(new Vector2(1f, 0f), step);
            float swimYaw = mover.Yaw;
            int guard = 0;
            while (sm.Locomotion != PlayerLocomotionState.Flying && guard++ < 600)
            {
                mover.TickMove(new Vector2(0f, 1f), step);
                yield return null;
            }
            Assert.AreEqual(PlayerLocomotionState.Flying, sm.Locomotion, "setup did not reach Fly");
            float yawBefore = mover.Yaw;
            for (int i = 0; i < 10; i++) mover.TickMove(new Vector2(1f, 0f), step);
            Assert.Greater(swimYaw, 30f, "water turn weaker than the tier promises");
            Assert.Less(mover.Yaw - yawBefore, 25f, "air turn as tight as water turn");
        }

        [UnityTest]
        public IEnumerator LevelFlightEventuallyGlidesOut()
        {
            // Structural pin for rise-then-glide: unpowered level flight must
            // always end (gravity debt is unbounded), never cruise forever.
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            var mover = sm.GetComponent<PlayerMovementController>();
            sm.SetTier(FlightTier.Max);
            mover.enabled = false; // single-step AFTER SetTier (see Setup note)
            sm.Momentum.CurrentSpeed = 110f; sm.Momentum.TargetSpeed = 110f;
            float t = 0f;
            while (sm.Locomotion != PlayerLocomotionState.Flying && t < 5f)
            {
                t += Time.deltaTime;
                mover.TickMove(new Vector2(0f, 1f), Time.deltaTime);
                yield return null;
            }
            Assert.AreEqual(PlayerLocomotionState.Flying, sm.Locomotion, "setup did not reach Fly");
            const float step = 1f / 60f;
            int guard = 0;
            while (mover.Pitch > 0f && guard++ < 120)
                mover.TickMove(new Vector2(0f, -1f), step);
            guard = 0;
            while (sm.Locomotion == PlayerLocomotionState.Flying && guard++ < 3600)
                mover.TickMove(Vector2.zero, step);
            Assert.AreEqual(PlayerLocomotionState.Swimming, sm.Locomotion, "level flight never glided out");
        }

        [UnityTest]
        public IEnumerator PauseFreezesSimulation()
        {
            var mover = Object.FindFirstObjectByType<PlayerMovementController>();
            mover.StateMachine.SetTier(FlightTier.Low);
            mover.StateMachine.Momentum.TargetSpeed = 35f;
            yield return new WaitForSeconds(0.5f);
            var before = mover.transform.position;
            Time.timeScale = 0f;
            try
            {
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.AreEqual(before, mover.transform.position, "moved while paused");
            }
            finally { Time.timeScale = 1f; }
        }
    }
}
