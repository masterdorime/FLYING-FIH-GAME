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
            sm.Momentum.TargetSpeed = 73f;
            // Travel/bounds pin, not timing: freeze the spawner (see Setup note).
            Object.FindFirstObjectByType<TimingPromptSpawner>().enabled = false;
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
            sm.Momentum.CurrentSpeed = 23f; sm.Momentum.TargetSpeed = 23f;
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
            // Climb pin, not timing: freeze the spawner (see Setup note).
            Object.FindFirstObjectByType<TimingPromptSpawner>().enabled = false;
            sm.Momentum.CurrentSpeed = 73f; sm.Momentum.TargetSpeed = 73f;
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
            mover.enabled = false; // single-step AFTER SetTier (see Setup note)
            sm.SetTier(FlightTier.Max);
            // Slalom pins movement/collision, not timing: freeze the spawner so
            // autonomous batchmode Misses cannot disturb the run.
            Object.FindFirstObjectByType<TimingPromptSpawner>().enabled = false;
            sm.Momentum.CurrentSpeed = 73f; sm.Momentum.TargetSpeed = 73f;
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
            sm.Momentum.CurrentSpeed = 73f; sm.Momentum.TargetSpeed = 73f;
            // Turn-bleed pin, not timing: freeze the spawner (see Setup note).
            Object.FindFirstObjectByType<TimingPromptSpawner>().enabled = false;
            float t = 0f;
            while (t < 3f)
            {
                t += Time.deltaTime;
                mover.TickMove(new Vector2(1f, 0f), Time.deltaTime);
                yield return null;
            }
            Assert.AreEqual(60f, mover.Yaw, 0.5f, "full stick did not pin at cone edge");
            Assert.Less(sm.Momentum.CurrentSpeed, 72f, "cone-edge flight did not bleed speed");
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
            sm.Momentum.CurrentSpeed = 73f; sm.Momentum.TargetSpeed = 73f;
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
            sm.Momentum.CurrentSpeed = 73f; sm.Momentum.TargetSpeed = 73f;
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
        public IEnumerator PromptOpensInGame()
        {
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            var spawner = Object.FindFirstObjectByType<TimingPromptSpawner>();
            Assert.IsNotNull(spawner, "spawner not wired in scene");
            sm.SetTier(FlightTier.Medium); sm.Momentum.TargetSpeed = 20f;
            float t = 0f;
            while (!spawner.Active.Open && t < 10f) { t += Time.deltaTime; yield return null; }
            Assert.IsTrue(spawner.Active.Open, "no prompt after 60m of travel");
        }

        [UnityTest]
        public IEnumerator SpawnStartsAtTen()
        {
            // Gearless climb: every run starts at speed 10, target pinned 10.
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            yield return null;
            Assert.AreEqual(10f, sm.Momentum.CurrentSpeed, 0.5f);
            Assert.AreEqual(10f, sm.Momentum.TargetSpeed, 0.5f);
        }

        [UnityTest]
        public IEnumerator PerfectPressGainsSpeedInGame()
        {
            // Manual drive, honest stepping: spawner.Tick with an explicit clock.
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            var mover = sm.GetComponent<PlayerMovementController>();
            var spawner = Object.FindFirstObjectByType<TimingPromptSpawner>();
            sm.SetTier(FlightTier.Medium);
            mover.enabled = false; // single-step AFTER SetTier (see Setup note)
            spawner.enabled = false;
            sm.Momentum.CurrentSpeed = 10f; sm.Momentum.TargetSpeed = 10f;
            const float step = 1f / 60f;
            float now = 0f;
            int guard = 0;
            while (!spawner.Active.Open && guard++ < 200)
            {
                now += step;
                spawner.Tick(now, step, 50f, FlightTier.Medium, false);
            }
            Assert.IsTrue(spawner.Active.Open, "prompt never opened");
            yield return null;
            spawner.Tick(spawner.Active.TargetTime, 0f, 10f, FlightTier.Medium, true);
            Assert.IsTrue(spawner.HasResolved);
            Assert.AreEqual(TimingResult.Perfect, spawner.LastResult);
            Assert.AreEqual(15f, sm.Momentum.CurrentSpeed, 0.5f);
            yield break;
        }

        [UnityTest]
        public IEnumerator ExpiredPromptLosesSpeedInGame()
        {
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            var mover = sm.GetComponent<PlayerMovementController>();
            var spawner = Object.FindFirstObjectByType<TimingPromptSpawner>();
            sm.SetTier(FlightTier.Medium);
            mover.enabled = false; // single-step AFTER SetTier (see Setup note)
            spawner.enabled = false;
            sm.Momentum.CurrentSpeed = 10f; sm.Momentum.TargetSpeed = 10f;
            const float step = 1f / 60f;
            float now = 0f;
            int guard = 0;
            while (!spawner.Active.Open && guard++ < 200)
            {
                now += step;
                spawner.Tick(now, step, 50f, FlightTier.Medium, false);
            }
            Assert.IsTrue(spawner.Active.Open, "prompt never opened");
            yield return null;
            spawner.Tick(spawner.Active.TargetTime + 0.3f, 0f, 10f, FlightTier.Medium, false);
            Assert.IsTrue(spawner.HasResolved);
            Assert.AreEqual(TimingResult.Miss, spawner.LastResult);
            Assert.IsFalse(spawner.Active.Open);
            // MinSpeed floor lives in Apply now: 10 - 28 bottoms at 5.
            Assert.AreEqual(5f, sm.Momentum.CurrentSpeed, 0.5f);
            yield break;
        }

        [UnityTest]
        public IEnumerator PromptSurvivesBreachMidOpen()
        {
            // Spawner ignores locomotion: breach while open, then resolve Perfect.
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            var mover = sm.GetComponent<PlayerMovementController>();
            var spawner = Object.FindFirstObjectByType<TimingPromptSpawner>();
            sm.SetTier(FlightTier.Medium);
            mover.enabled = false; // single-step AFTER SetTier (see Setup note)
            spawner.enabled = false;
            sm.Momentum.CurrentSpeed = 25f; sm.Momentum.TargetSpeed = 25f;
            const float step = 1f / 60f;
            float now = 0f;
            int guard = 0;
            while (!spawner.Active.Open && guard++ < 200)
            {
                now += step;
                spawner.Tick(now, step, 50f, FlightTier.Medium, false);
            }
            Assert.IsTrue(spawner.Active.Open, "prompt never opened");
            yield return null;
            guard = 0;
            while (sm.Locomotion != PlayerLocomotionState.Flying && guard++ < 600)
                mover.TickMove(new Vector2(0f, 1f), step);
            Assert.AreEqual(PlayerLocomotionState.Flying, sm.Locomotion, "breach failed mid-prompt");
            Assert.IsTrue(spawner.Active.Open, "prompt died on breach");
            spawner.Tick(spawner.Active.TargetTime, 0f, sm.Momentum.CurrentSpeed, FlightTier.Medium, true);
            Assert.AreEqual(TimingResult.Perfect, spawner.LastResult);
            yield break;
        }

        [UnityTest]
        public IEnumerator DialVisibleOnlyWhileOpen()
        {
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            var mover = sm.GetComponent<PlayerMovementController>();
            var spawner = Object.FindFirstObjectByType<TimingPromptSpawner>();
            var dial = Object.FindFirstObjectByType<TimingPromptDial>();
            Assert.IsNotNull(dial, "dial not wired in scene");
            sm.SetTier(FlightTier.Medium);
            mover.enabled = false; // single-step AFTER SetTier (see Setup note)
            spawner.enabled = false;
            sm.Momentum.CurrentSpeed = 33f; sm.Momentum.TargetSpeed = 33f;
            Assert.IsFalse(dial.Visible, "dial visible with no prompt");
            const float step = 1f / 60f;
            float now = 0f;
            int guard = 0;
            while (!spawner.Active.Open && guard++ < 200)
            {
                now += step;
                spawner.Tick(now, step, 50f, FlightTier.Medium, false);
            }
            Assert.IsTrue(spawner.Active.Open, "prompt never opened");
            yield return null;
            yield return null; // let dial.Update observe the open prompt
            Assert.IsTrue(dial.Visible, "dial hidden while prompt open");
            spawner.Tick(spawner.Active.TargetTime, 0f, 50f, FlightTier.Medium, true);
            yield return null;
            Assert.IsFalse(dial.Visible, "dial stuck visible after resolve");
        }

        [UnityTest]
        public IEnumerator NeedleLandsOnGreenAtTarget()
        {
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            var mover = sm.GetComponent<PlayerMovementController>();
            var spawner = Object.FindFirstObjectByType<TimingPromptSpawner>();
            var dial = Object.FindFirstObjectByType<TimingPromptDial>();
            sm.SetTier(FlightTier.Medium);
            mover.enabled = false; // single-step AFTER SetTier (see Setup note)
            spawner.enabled = false;
            sm.Momentum.CurrentSpeed = 33f; sm.Momentum.TargetSpeed = 33f;
            const float step = 1f / 60f;
            float now = 0f;
            int guard = 0;
            while (!spawner.Active.Open && guard++ < 200)
            {
                now += step;
                spawner.Tick(now, step, 50f, FlightTier.Medium, false);
            }
            float target = spawner.Active.TargetTime;
            while (now < target && guard++ < 400)
            {
                now += step;
                spawner.Tick(now, step, 50f, FlightTier.Medium, false);
                yield return null;
            }
            Assert.AreEqual(1f, spawner.Progress01, 0.02f); // fixed-step overshoot ≤ 1 step
            Assert.Less(Mathf.DeltaAngle(dial.NeedleAngleZ, -spawner.HitAngleDeg), 15f, "needle missed red at target");
        }

        [UnityTest]
        public IEnumerator LongRunStaysOverWater()
        {
            // Runway proof: 12s at Max must stay over the seabed (old water ended at z=700).
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            sm.SetTier(FlightTier.Max);
            sm.Momentum.TargetSpeed = 73f;
            Object.FindFirstObjectByType<TimingPromptSpawner>().enabled = false;
            yield return new WaitForSeconds(12f);
            var p = sm.transform.position;
            Assert.Greater(p.z, 750f, "did not reach the new water zone");
            Assert.Less(Mathf.Abs(p.y + 3f), 1f, "left level swim on the long run");
            Assert.IsTrue(Physics.Raycast(p, Vector3.down, 50f), "no seabed under the fish");
        }

        [UnityTest]
        public IEnumerator DialArcsSaneWhileOpen()
        {
            // Probe for the missing-red-slice report: while open, the perfect
            // arc must have real width, sit inside the good arc, and be red.
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            var mover = sm.GetComponent<PlayerMovementController>();
            var spawner = Object.FindFirstObjectByType<TimingPromptSpawner>();
            var dial = Object.FindFirstObjectByType<TimingPromptDial>();
            sm.SetTier(FlightTier.Medium);
            mover.enabled = false; // single-step AFTER SetTier (see Setup note)
            spawner.enabled = false;
            sm.Momentum.CurrentSpeed = 33f; sm.Momentum.TargetSpeed = 33f;
            const float step = 1f / 60f;
            float now = 0f;
            int guard = 0;
            while (!spawner.Active.Open && guard++ < 200)
            {
                now += step;
                spawner.Tick(now, step, 33f, FlightTier.Medium, false);
            }
            Assert.IsTrue(spawner.Active.Open, "prompt never opened");
            yield return null;
            yield return null;
            Assert.IsTrue(dial.Visible);
            Assert.Greater(dial.PerfectHalfWidthDeg, 1f, "perfect slice has no width");
            Assert.Greater(dial.GoodHalfWidthDeg, dial.PerfectHalfWidthDeg, "good not wider than perfect");
            var red = dial.PerfectArcColor;
            Assert.Less(Mathf.Abs(red.r - 1f) + Mathf.Abs(red.g - 0.25f) + Mathf.Abs(red.b - 0.25f),
                0.05f, "perfect slice is not red");
            Assert.AreEqual(3002, dial.PerfectRenderQueue, "perfect must paint last");
        }

        [UnityTest]
        public IEnumerator ChargeBarVisibleDuringCharge()
        {
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            var mover = sm.GetComponent<PlayerMovementController>();
            var spawner = Object.FindFirstObjectByType<TimingPromptSpawner>();
            var bar = Object.FindFirstObjectByType<ChargeBar>();
            Assert.IsNotNull(bar, "charge bar not wired in scene");
            sm.SetTier(FlightTier.Medium);
            mover.enabled = false; // single-step AFTER SetTier (see Setup note)
            spawner.enabled = false;
            sm.Momentum.CurrentSpeed = 10f; sm.Momentum.TargetSpeed = 10f;
            Assert.IsFalse(bar.Visible, "bar visible with no charge");
            mover.transform.position = spawner.Rings[0].transform.position;
            spawner.CheckRingTrigger(0f, mover.transform.position);
            Assert.IsTrue(spawner.ChargeActive, "ring did not trigger");
            yield return null;
            yield return null;
            Assert.IsTrue(bar.Visible, "bar hidden during charge");
            float now = 0f;
            int guard = 0;
            while (spawner.ChargeActive && guard++ < 200)
            {
                now += 0.5f;
                spawner.Tick(now, 0.5f, 10f, FlightTier.Medium, false, false);
            }
            yield return null;
            yield return null;
            Assert.IsFalse(bar.Visible, "bar stuck visible after charge");
        }

        [UnityTest]
        public IEnumerator RingSwimThroughTriggersLive()
        {
            // Live Update path: park the fish inside ring 1, charge must start.
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            var mover = sm.GetComponent<PlayerMovementController>();
            var spawner = Object.FindFirstObjectByType<TimingPromptSpawner>();
            sm.SetTier(FlightTier.Medium);
            sm.Momentum.CurrentSpeed = 10f; sm.Momentum.TargetSpeed = 10f;
            mover.transform.position = spawner.Rings[0].transform.position;
            // Freeze the fish on the ring: first-frame hitches in batchmode
            // can fling it past the 2m trigger window before spawner.Update
            // runs. Spawner keeps updating while mover is off.
            mover.enabled = false;
            float t = 0f;
            while (!spawner.ChargeActive && t < 3f) { t += Time.deltaTime; yield return null; }
            Assert.IsTrue(spawner.ChargeActive, "swimming through a ring did not start charge");
            Assert.AreEqual(3, spawner.ChargeOrder.Length);
        }

        [UnityTest]
        public IEnumerator ChargeStepsLookDifferent()
        {
            // Hold shows the fill bar, Tap shows the shrinking pulse — never
            // the same visual. Adapts to whatever order the seed drew.
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            var mover = sm.GetComponent<PlayerMovementController>();
            var spawner = Object.FindFirstObjectByType<TimingPromptSpawner>();
            var bar = Object.FindFirstObjectByType<ChargeBar>();
            sm.SetTier(FlightTier.Medium);
            mover.enabled = false; // single-step AFTER SetTier (see Setup note)
            spawner.enabled = false;
            sm.Momentum.CurrentSpeed = 10f; sm.Momentum.TargetSpeed = 10f;
            mover.transform.position = spawner.Rings[0].transform.position;
            spawner.CheckRingTrigger(0f, mover.transform.position);
            Assert.IsTrue(spawner.ChargeActive, "ring did not trigger");
            yield return null;
            Assert.AreEqual(spawner.ChargeOrder[0].ToString(), bar.ShownKind,
                "bar visual does not match step 0 kind");
            // Finish step 0 blindly (hold past limit AND tap target both pass
            // harmlessly for the other kind), then check step 1 matches.
            float now = 0f;
            int guard = 0;
            while (spawner.Charge.StepIndex < 1 && guard++ < 100)
            {
                now += 0.1f;
                spawner.Tick(now, 0.1f, 10f, FlightTier.Medium, true, true);
            }
            yield return null;
            Assert.IsTrue(spawner.ChargeActive, "charge ended after one step");
            yield return null;
            yield return null; // let bar.Update observe the new step
            Assert.AreEqual(spawner.ChargeOrder[1].ToString(), bar.ShownKind,
                "bar visual does not match step 1 kind");
        }

        [UnityTest]
        public IEnumerator DialZonesFollowStreak()
        {
            // The drawn red slice must match the judged windows: after one
            // Perfect (streak 1), the perfect half-width shrinks ×0.97.
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            var mover = sm.GetComponent<PlayerMovementController>();
            var spawner = Object.FindFirstObjectByType<TimingPromptSpawner>();
            var dial = Object.FindFirstObjectByType<TimingPromptDial>();
            sm.SetTier(FlightTier.Medium);
            mover.enabled = false; // single-step AFTER SetTier (see Setup note)
            spawner.enabled = false;
            sm.Momentum.CurrentSpeed = 10f; sm.Momentum.TargetSpeed = 10f;
            const float step = 1f / 60f;
            float now = 0f;
            int guard = 0;
            while (!spawner.Active.Open && guard++ < 600)
            {
                now += step;
                spawner.Tick(now, step, 10f, FlightTier.Medium, false);
            }
            yield return null;
            yield return null;
            float fresh = dial.PerfectHalfWidthDeg;
            Assert.Greater(fresh, 1f, "no red slice on fresh prompt");
            spawner.Tick(spawner.Active.TargetTime, 0f, 10f, FlightTier.Medium, true);
            Assert.AreEqual(TimingResult.Perfect, spawner.LastResult);
            sm.Momentum.CurrentSpeed = 10f; // hold speed fixed: isolate the streak effect
            guard = 0;
            while (!spawner.Active.Open && guard++ < 600)
            {
                now += step;
                spawner.Tick(now, step, 10f, FlightTier.Medium, false);
            }
            yield return null;
            yield return null;
            float expected = fresh * 0.97f;
            Assert.AreEqual(expected, dial.PerfectHalfWidthDeg, 0.05f);
        }

        [UnityTest]
        public IEnumerator NeedleCorrectUnderPitchedCamera()
        {
            // Tip world direction must equal the hit ray in dial space.
            // True only when the needle angle is dial-local, even with the
            // dial billboarded steeply. Camera restored in finally.
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            var mover = sm.GetComponent<PlayerMovementController>();
            var spawner = Object.FindFirstObjectByType<TimingPromptSpawner>();
            var dial = Object.FindFirstObjectByType<TimingPromptDial>();
            sm.SetTier(FlightTier.Medium);
            mover.enabled = false;
            spawner.enabled = false;
            sm.Momentum.CurrentSpeed = 10f;
            sm.Momentum.TargetSpeed = 10f;
            var cam = Camera.main;
            var oldPos = cam.transform.position;
            cam.transform.position = mover.transform.position + new Vector3(0f, 25f, -6f);
            try
            {
                const float step = 1f / 60f;
                float now = 0f;
                int guard = 0;
                while (!spawner.Active.Open && guard++ < 600)
                {
                    now += step;
                    spawner.Tick(now, step, 10f, FlightTier.Medium, false);
                }
                Assert.IsTrue(spawner.Active.Open, "prompt never opened");
                float target = spawner.Active.TargetTime;
                while (now < target && guard++ < 1200)
                {
                    now += step;
                    spawner.Tick(now, step, 10f, FlightTier.Medium, false);
                    yield return null;
                }
                yield return null;
                float hRad = spawner.HitAngleDeg * Mathf.Deg2Rad;
                Vector3 tip = dial.NeedlePivot.TransformPoint(0f, dial.NeedleLength, 0f);
                Vector3 want = dial.transform.TransformDirection(new Vector3(Mathf.Sin(hRad), Mathf.Cos(hRad), 0f));
                float off = Vector3.Angle(tip - dial.NeedlePivot.position, want);
                Assert.Less(off, 3f, "needle tip off the hit ray");
            }
            finally
            {
                cam.transform.position = oldPos;
            }
        }

        [UnityTest]
        public IEnumerator PauseFreezesSimulation()
        {
            var mover = Object.FindFirstObjectByType<PlayerMovementController>();
            mover.StateMachine.SetTier(FlightTier.Low);
            mover.StateMachine.Momentum.TargetSpeed = 23f;
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
