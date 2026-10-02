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
            Object.FindFirstObjectByType<FlightGaugeSystem>().enabled = false;
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
        public IEnumerator LaunchRoundTripViaGaugeFull()
        {
            // Spec: ship swim + gauge-full launch + dive re-entry. Full tank
            // rising edge launches sky-high; aiming down returns to swim.
            // Steering is driven via TickMove directly, not Press(): synthetic
            // input events stall the player loop in batchmode (see Setup note
            // and ledger). Device→value plumbing is engine behavior covered by
            // InputAssetTests (bindings) plus the human feel pass (real keys).
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            var mover = sm.GetComponent<PlayerMovementController>();
            var gauge = Object.FindFirstObjectByType<FlightGaugeSystem>();
            Assert.AreEqual(PlayerLocomotionState.Swimming, sm.Locomotion, "spawn must be underwater Swimming");
            sm.SetTier(FlightTier.Medium);
            mover.enabled = false; // single-step AFTER SetTier (see Setup note)
            // Round trip pins movement, not timing: freeze the spawner.
            Object.FindFirstObjectByType<TimingPromptSpawner>().enabled = false;
            sm.Momentum.CurrentSpeed = 33f; sm.Momentum.TargetSpeed = 33f;
            float y0 = mover.transform.position.y;
            gauge.AddFill(120f);
            Assert.AreEqual(PlayerLocomotionState.Flying, sm.Locomotion, "full tank did not launch");
            Assert.AreEqual(1, mover.LaunchCount, "launch fired more than once");
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime;
                mover.TickMove(new Vector2(0f, 1f), Time.deltaTime);
                yield return null;
            }
            Assert.Greater(mover.transform.position.y - y0, 20f, "launch did not go sky-high");
            // Launch arcs far higher than the old breach: allow a long descent.
            t = 0f;
            while (sm.Locomotion != PlayerLocomotionState.Swimming && t < 12f)
            {
                t += Time.deltaTime;
                mover.TickMove(new Vector2(0f, -1f), Time.deltaTime);
                yield return null;
            }
            Assert.AreEqual(PlayerLocomotionState.Swimming, sm.Locomotion, "downward crossing did not re-enter Swim");
        }

        [UnityTest]
        public IEnumerator LaunchPreservesTankAndTier()
        {
            // Tank is fuel, not a fuse: launching keeps the fill and the
            // tier (flight then drains it). The old empty-on-launch reset
            // dropped takeoff to None-tier crawl and flights died fast.
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            var mover = sm.GetComponent<PlayerMovementController>();
            var gauge = Object.FindFirstObjectByType<FlightGaugeSystem>();
            Assert.AreEqual(PlayerLocomotionState.Swimming, sm.Locomotion, "spawn must be underwater Swimming");
            sm.SetTier(FlightTier.Medium);
            mover.enabled = false; // single-step AFTER SetTier (see Setup note)
            Object.FindFirstObjectByType<TimingPromptSpawner>().enabled = false;
            sm.Momentum.CurrentSpeed = 33f; sm.Momentum.TargetSpeed = 33f;
            gauge.AddFill(120f);
            Assert.AreEqual(PlayerLocomotionState.Flying, sm.Locomotion, "full tank did not launch");
            Assert.AreEqual(48f, gauge.CurrentGauge, 0.001f, "launch emptied the tank");
            Assert.AreEqual(FlightTier.Max, sm.ActiveTier, "launch reset the tier");
            yield break;
        }
        [UnityTest]
        public IEnumerator FlyingAtSpeedWithFullUpInputClimbs()
        {
            // Glide fantasy check: flight with speed + full climb input must
            // gain altitude (no speed = glide/sink is by design, untested here).
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            var mover = sm.GetComponent<PlayerMovementController>();
            sm.SetTier(FlightTier.Max);
            Object.FindFirstObjectByType<FlightGaugeSystem>().enabled = false;
            mover.enabled = false; // single-step AFTER SetTier (see Setup note)
            // Climb pin, not timing: freeze the spawner (see Setup note).
            Object.FindFirstObjectByType<TimingPromptSpawner>().enabled = false;
            sm.Momentum.CurrentSpeed = 73f; sm.Momentum.TargetSpeed = 73f;
            // Launch setup (ship swim cannot breach upward anymore): full
            // tank rising edge launches; re-rig speed after the tier reset.
            var gauge = Object.FindFirstObjectByType<FlightGaugeSystem>();
            gauge.AddFill(120f);
            Assert.AreEqual(PlayerLocomotionState.Flying, sm.Locomotion, "setup did not reach Fly");
            sm.Momentum.CurrentSpeed = 73f; sm.Momentum.TargetSpeed = 73f;
            float y0 = mover.transform.position.y;
            float t = 0f;
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
            Object.FindFirstObjectByType<FlightGaugeSystem>().enabled = false;
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
            Object.FindFirstObjectByType<FlightGaugeSystem>().enabled = false;
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
            Object.FindFirstObjectByType<FlightGaugeSystem>().enabled = false;
            mover.enabled = false; // single-step AFTER SetTier (see Setup note)
            sm.Momentum.CurrentSpeed = 73f; sm.Momentum.TargetSpeed = 73f;
            const float step = 1f / 60f;
            for (int i = 0; i < 10; i++) mover.TickMove(new Vector2(1f, 0f), step);
            float swimYaw = mover.Yaw;
            int guard = 0;
            // Launch setup (ship swim cannot breach upward anymore): full
            // tank rising edge launches; re-take Max tier afterward (launch
            // reconcile resets to None, and this test pins Max turn rates).
            Object.FindFirstObjectByType<FlightGaugeSystem>().AddFill(120f);
            Assert.AreEqual(PlayerLocomotionState.Flying, sm.Locomotion, "setup did not reach Fly");
            sm.SetTier(FlightTier.Max);
            yield return null;
            float yawBefore = mover.Yaw;
            for (int i = 0; i < 10; i++) mover.TickMove(new Vector2(1f, 0f), step);
            Assert.Greater(swimYaw, 30f, "water turn weaker than the tier promises");
            Assert.Less(mover.Yaw - yawBefore, 25f, "air turn as tight as water turn");
        }

        [UnityTest]
        public IEnumerator DiveBuildsSpeedClimbBleedsIt()
        {
            // Arcade glide: downhill converts to speed, uphill bleeds it.
            // Fixed-step, mid-tier speed so caps never interfere.
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            var mover = sm.GetComponent<PlayerMovementController>();
            sm.SetTier(FlightTier.Max);
            Object.FindFirstObjectByType<FlightGaugeSystem>().enabled = false;
            mover.enabled = false; // single-step AFTER SetTier (see Setup note)
            Object.FindFirstObjectByType<TimingPromptSpawner>().enabled = false;
            Object.FindFirstObjectByType<FlightGaugeSystem>().AddFill(120f);
            Assert.AreEqual(PlayerLocomotionState.Flying, sm.Locomotion, "setup did not reach Fly");
            sm.SetTier(FlightTier.Max);
            sm.Momentum.CurrentSpeed = 40f; sm.Momentum.TargetSpeed = 40f;
            const float step = 1f / 60f;
            // Climb out of the launch splash first: the dive below starts
            // from altitude, or it ends underwater where pitch locks to 0.
            for (int i = 0; i < 30; i++) mover.TickMove(new Vector2(0f, 1f), step);
            for (int i = 0; i < 300; i++) mover.TickMove(Vector2.zero, step);
            Assert.AreEqual(PlayerLocomotionState.Flying, sm.Locomotion, "climb-out touched water");
            sm.Momentum.CurrentSpeed = 40f; sm.Momentum.TargetSpeed = 40f;
            for (int i = 0; i < 48; i++) mover.TickMove(new Vector2(0f, -1f), step);
            Assert.Less(mover.Pitch, -15f, "setup did not dive");
            // Fly bonus alone cruises 40 → 48: a real dive must push
            // clearly past it (target raised toward the speed cap).
            for (int i = 0; i < 60; i++) mover.TickMove(Vector2.zero, step);
            Assert.Greater(sm.Momentum.CurrentSpeed, 55f, "dive did not build speed");
            for (int i = 0; i < 60; i++) mover.TickMove(new Vector2(0f, 1f), step);
            Assert.Greater(mover.Pitch, 15f, "setup did not climb");
            // Climb drag bleeds slowly: 15s must fall clearly under cruise.
            for (int i = 0; i < 900; i++) mover.TickMove(Vector2.zero, step);
            Assert.AreEqual(PlayerLocomotionState.Flying, sm.Locomotion, "climb touched water");
            Assert.Less(sm.Momentum.CurrentSpeed, 44f, "climb did not bleed speed");
            yield break;
        }

        [UnityTest]
        public IEnumerator GlideSinksGentlyAtCruise()
        {
            // Lift holds most of the weight at cruise: 30s of level Max
            // flight from a launch kick is still hundreds of meters up.
            // Ballistic (no lift) lands in ~27s — so this pins the glide.
            // (Flight must still end; see LevelFlightEventuallyGlidesOut.)
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            var mover = sm.GetComponent<PlayerMovementController>();
            sm.SetTier(FlightTier.Max);
            Object.FindFirstObjectByType<FlightGaugeSystem>().enabled = false;
            mover.enabled = false; // single-step AFTER SetTier (see Setup note)
            Object.FindFirstObjectByType<TimingPromptSpawner>().enabled = false;
            Object.FindFirstObjectByType<FlightGaugeSystem>().AddFill(120f);
            Assert.AreEqual(PlayerLocomotionState.Flying, sm.Locomotion, "setup did not reach Fly");
            sm.SetTier(FlightTier.Max);
            sm.Momentum.CurrentSpeed = 73f; sm.Momentum.TargetSpeed = 73f;
            const float step = 1f / 60f;
            for (int i = 0; i < 1800; i++) mover.TickMove(Vector2.zero, step);
            Assert.AreEqual(PlayerLocomotionState.Flying, sm.Locomotion, "glide did not hold altitude");
            Assert.Greater(mover.transform.position.y, 300f, "level cruise sinks like a stone");
            yield break;
        }

        [UnityTest]
        public IEnumerator CameraDistanceConstantAcrossTiers()
        {
            // Constant-size fish: the chase camera holds the same distance
            // at Low cruise as at Max cruise (no drift-away with speed).
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            var mover = sm.GetComponent<PlayerMovementController>();
            var cam = Object.FindFirstObjectByType<CameraSpeedReactor>();
            Object.FindFirstObjectByType<FlightGaugeSystem>().enabled = false;
            Object.FindFirstObjectByType<TimingPromptSpawner>().enabled = false;
            mover.enabled = false; // single-step AFTER SetTier (see Setup note)
            sm.SetTier(FlightTier.Low);
            sm.Momentum.CurrentSpeed = 25f; sm.Momentum.TargetSpeed = 25f;
            // Wall-clock settle (batchmode frames are tiny; fixed frame
            // counts settle nothing): the guard below proves Low anchor.
            yield return new WaitForSecondsRealtime(1.5f);
            float lowDist = (cam.transform.position - mover.transform.position).magnitude;
            Assert.Less(lowDist, 12f, "camera never reached the Low anchor");
            sm.SetTier(FlightTier.Max);
            sm.Momentum.CurrentSpeed = 73f; sm.Momentum.TargetSpeed = 73f;
            yield return new WaitForSecondsRealtime(1.5f);
            float maxDist = (cam.transform.position - mover.transform.position).magnitude;
            Assert.Greater(lowDist, 10.5f); Assert.Less(lowDist, 13f);
            Assert.Greater(maxDist, 10.5f); Assert.Less(maxDist, 13f);
            Assert.Less(Mathf.Abs(lowDist - maxDist), 1.5f, "camera drifts with tier");
            yield break;
        }

        [UnityTest]
        public IEnumerator LevelFlightEventuallyGlidesOut()
        {
            // Structural pin for rise-then-glide: unpowered level flight must
            // always end (gravity debt is unbounded), never cruise forever.
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            var mover = sm.GetComponent<PlayerMovementController>();
            sm.SetTier(FlightTier.Max);
            Object.FindFirstObjectByType<FlightGaugeSystem>().enabled = false;
            mover.enabled = false; // single-step AFTER SetTier (see Setup note)
            sm.Momentum.CurrentSpeed = 73f; sm.Momentum.TargetSpeed = 73f;
            // Launch setup (ship swim cannot breach upward anymore).
            Object.FindFirstObjectByType<FlightGaugeSystem>().AddFill(120f);
            Assert.AreEqual(PlayerLocomotionState.Flying, sm.Locomotion, "setup did not reach Fly");
            yield return null;
            const float step = 1f / 60f;
            int guard = 0;
            while (mover.Pitch > 0f && guard++ < 120)
                mover.TickMove(new Vector2(0f, -1f), step);
            guard = 0;
            // Generous budget: launch now keeps Max-tier lift, so the arc
            // is minutes, not seconds — but gravity debt still ends it.
            while (sm.Locomotion == PlayerLocomotionState.Flying && guard++ < 12000)
                mover.TickMove(Vector2.zero, step);
            Assert.AreEqual(PlayerLocomotionState.Swimming, sm.Locomotion, "level flight never glided out");
        }

        [UnityTest]
        public IEnumerator PromptOpensInGame()
        {
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            var spawner = Object.FindFirstObjectByType<TimingPromptSpawner>();
            Assert.IsNotNull(spawner, "spawner not wired in scene");
            sm.SetTier(FlightTier.Medium);
            Object.FindFirstObjectByType<FlightGaugeSystem>().enabled = false;
            sm.Momentum.TargetSpeed = 20f;
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
            Object.FindFirstObjectByType<FlightGaugeSystem>().enabled = false;
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
            Object.FindFirstObjectByType<FlightGaugeSystem>().enabled = false;
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
        public IEnumerator PromptSurvivesLaunchMidOpen()
        {
            // Spawner ignores locomotion: launch while open, then resolve Perfect.
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            var mover = sm.GetComponent<PlayerMovementController>();
            var spawner = Object.FindFirstObjectByType<TimingPromptSpawner>();
            sm.SetTier(FlightTier.Medium);
            Object.FindFirstObjectByType<FlightGaugeSystem>().enabled = false;
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
            Object.FindFirstObjectByType<FlightGaugeSystem>().AddFill(120f);
            Assert.AreEqual(PlayerLocomotionState.Flying, sm.Locomotion, "launch failed mid-prompt");
            Assert.IsTrue(spawner.Active.Open, "prompt died on launch");
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
            Object.FindFirstObjectByType<FlightGaugeSystem>().enabled = false;
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
            Object.FindFirstObjectByType<FlightGaugeSystem>().enabled = false;
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
            Object.FindFirstObjectByType<FlightGaugeSystem>().enabled = false;
            sm.Momentum.TargetSpeed = 73f;
            Object.FindFirstObjectByType<TimingPromptSpawner>().enabled = false;
            yield return new WaitForSeconds(12f);
            var p = sm.transform.position;
            Assert.Greater(p.z, 750f, "did not reach the new water zone");
            Assert.Less(Mathf.Abs(p.y + 1.5f), 1f, "left locked swim depth on the long run");
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
            Object.FindFirstObjectByType<FlightGaugeSystem>().enabled = false;
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
            Object.FindFirstObjectByType<FlightGaugeSystem>().enabled = false;
            mover.enabled = false; // single-step AFTER SetTier (see Setup note)
            spawner.enabled = false;
            sm.Momentum.CurrentSpeed = 10f; sm.Momentum.TargetSpeed = 10f;
            // Manual CheckRingTrigger engages ring slow-mo (0.4); the
            // spawner stays disabled so UpdateSlowMo never restores it.
            try
            {
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
            finally { Time.timeScale = 1f; }
        }

        [UnityTest]
        public IEnumerator RingSwimThroughTriggersLive()
        {
            // Live Update path: park the fish inside ring 1, charge must start.
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            var mover = sm.GetComponent<PlayerMovementController>();
            var spawner = Object.FindFirstObjectByType<TimingPromptSpawner>();
            sm.SetTier(FlightTier.Medium);
            Object.FindFirstObjectByType<FlightGaugeSystem>().enabled = false;
            sm.Momentum.CurrentSpeed = 10f; sm.Momentum.TargetSpeed = 10f;
            mover.transform.position = spawner.Rings[0].transform.position;
            // Freeze the fish on the ring: first-frame hitches in batchmode
            // can fling it past the 2m trigger window before spawner.Update
            // runs. Spawner keeps updating while mover is off.
            mover.enabled = false;
            float t = 0f;
            try
            {
                while (!spawner.ChargeActive && t < 3f) { t += Time.deltaTime; yield return null; }
                Assert.IsTrue(spawner.ChargeActive, "swimming through a ring did not start charge");
                Assert.AreEqual(4, spawner.ChargeOrder.Length);
            }
            finally { Time.timeScale = 1f; }
        }

        [UnityTest]
        public IEnumerator ChargeStepsLookDifferent()
        {
            // Marker cubes track step outcomes: a clean first step lights one
            // green, a deliberately wrong second step lights one red.
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            var mover = sm.GetComponent<PlayerMovementController>();
            var spawner = Object.FindFirstObjectByType<TimingPromptSpawner>();
            var bar = Object.FindFirstObjectByType<ChargeBar>();
            sm.SetTier(FlightTier.Medium);
            Object.FindFirstObjectByType<FlightGaugeSystem>().enabled = false;
            mover.enabled = false; // single-step AFTER SetTier (see Setup note)
            spawner.enabled = false;
            sm.Momentum.CurrentSpeed = 10f; sm.Momentum.TargetSpeed = 10f;
            mover.transform.position = spawner.Rings[0].transform.position;
            try
            {
                // Fully manual clock (single timeline for trigger + drive;
                // wall Time.time would instantly trip the charge timeout).
                float now = 100f;
                spawner.CheckRingTrigger(now, mover.transform.position);
                Assert.IsTrue(spawner.ChargeActive, "ring did not trigger");
                spawner.Tick(now + 0.1f, 0.1f, 10f, FlightTier.Medium, false, false, spawner.Charge.CurrentArrow);
                yield return null;
                Assert.AreEqual(1, bar.GreenCount, "clean step did not light green");
                now += 0.2f;
                ChargeArrow wrong = spawner.Charge.CurrentArrow == ChargeArrow.Up ? ChargeArrow.Down : ChargeArrow.Up;
                spawner.Tick(now, 0.1f, 10f, FlightTier.Medium, false, false, wrong);
                yield return null;
                Assert.AreEqual(1, bar.RedCount, "wrong key did not light red");
            }
            finally { Time.timeScale = 1f; }
        }

        [UnityTest]
        public IEnumerator ChargeRowReadsLeftToRightOnScreen()
        {
            // User report: "the sequence actually starts from right" — taps
            // on the leftmost arrow go red on keyboard. Either the glyph row
            // is mirrored on screen (display bug) or the taps arrive wrong
            // (input side). This pins the display half: step index must grow
            // with screen-x through the live chase camera.
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            var mover = sm.GetComponent<PlayerMovementController>();
            var spawner = Object.FindFirstObjectByType<TimingPromptSpawner>();
            var bar = Object.FindFirstObjectByType<ChargeBar>();
            sm.SetTier(FlightTier.Medium);
            Object.FindFirstObjectByType<FlightGaugeSystem>().enabled = false;
            mover.enabled = false; // single-step AFTER SetTier (see Setup note)
            sm.Momentum.CurrentSpeed = 10f; sm.Momentum.TargetSpeed = 10f;
            mover.transform.position = spawner.Rings[0].transform.position;
            try
            {
                // Spawner stays enabled: its Update triggers the charge on
                // the pinned fish and the bar draws through the live camera.
                int guard = 0;
                while (!spawner.ChargeActive && guard++ < 600) yield return null;
                Assert.IsTrue(spawner.ChargeActive, "ring did not trigger");
                // Let the chase camera glide behind the teleported fish.
                for (int f = 0; f < 120; f++) yield return null;
                Assert.IsTrue(spawner.ChargeActive, "charge expired before camera settled");
                float prevX = float.NegativeInfinity;
                for (int i = 0; i < spawner.Charge.StepCount; i++)
                {
                    Vector3 sp = Camera.main.WorldToScreenPoint(bar.GlyphWorldPosition(i));
                    Assert.Greater(sp.z, 0f, "glyph " + i + " is behind the camera");
                    Assert.Greater(sp.x, prevX, "glyph " + i + " is not left-to-right on screen");
                    prevX = sp.x;
                }
            }
            finally { Time.timeScale = 1f; }
        }

        [UnityTest]
        public IEnumerator ChargeCurrentStepStandsOut()
        {
            // Reading order is left-to-right from step 0; the step being
            // judged must look different from the grey steps still ahead:
            // bigger (pulsing) and yellow instead of grey.
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            var mover = sm.GetComponent<PlayerMovementController>();
            var spawner = Object.FindFirstObjectByType<TimingPromptSpawner>();
            var bar = Object.FindFirstObjectByType<ChargeBar>();
            sm.SetTier(FlightTier.Medium);
            Object.FindFirstObjectByType<FlightGaugeSystem>().enabled = false;
            mover.enabled = false; // single-step AFTER SetTier (see Setup note)
            spawner.enabled = false;
            sm.Momentum.CurrentSpeed = 10f; sm.Momentum.TargetSpeed = 10f;
            mover.transform.position = spawner.Rings[0].transform.position;
            try
            {
                float now = 100f;
                spawner.CheckRingTrigger(now, mover.transform.position);
                Assert.IsTrue(spawner.ChargeActive, "ring did not trigger");
                yield return null; // let ChargeBar.Update draw the glyphs
                int cur = spawner.Charge.StepIndex;
                int next = (cur + 1) % spawner.Charge.StepCount;
                Assert.Greater(bar.GlyphScale(cur), bar.GlyphScale(next),
                    "current step glyph is not bigger than upcoming steps");
                Assert.AreNotEqual(Color.grey, bar.GlyphColor(cur),
                    "current step glyph is the same grey as upcoming steps");
                Assert.AreEqual(Color.grey, bar.GlyphColor(next),
                    "upcoming step glyph is not the waiting grey");
            }
            finally { Time.timeScale = 1f; }
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
            Object.FindFirstObjectByType<FlightGaugeSystem>().enabled = false;
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
            Object.FindFirstObjectByType<FlightGaugeSystem>().enabled = false;
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
                spawner.Tick(target, 0f, 10f, FlightTier.Medium, false); // exact: no step overshoot
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
        public IEnumerator GaugeWiredInScene()
        {
            var gauge = Object.FindFirstObjectByType<FlightGaugeSystem>();
            Assert.IsNotNull(gauge, "gauge not wired in scene");
            Assert.AreEqual(0f, gauge.CurrentGauge, 0.001f);
            Assert.AreEqual(FlightTier.None, gauge.StateMachine.ActiveTier);
            yield break;
        }

        [UnityTest]
        public IEnumerator CleanRingReachesMediumGear()
        {
            // Fully manual: no live frames anywhere. Mover, spawner and
            // gauge all freeze upfront; rings trigger via direct
            // CheckRingTrigger on an explicit manual clock. One clean
            // sequence (4x+4 plus +8 jackpot = 24 gauge) clears the
            // Medium floor at 16 with a tier-up kick (testing tank).
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            var mover = sm.GetComponent<PlayerMovementController>();
            var spawner = Object.FindFirstObjectByType<TimingPromptSpawner>();
            var gauge = Object.FindFirstObjectByType<FlightGaugeSystem>();
            var cam = Object.FindFirstObjectByType<CameraSpeedReactor>();
            sm.SetTier(FlightTier.Medium);
            sm.Momentum.CurrentSpeed = 10f; sm.Momentum.TargetSpeed = 10f;
            mover.enabled = false; // single-step AFTER SetTier (see Setup note)
            spawner.enabled = false;
            gauge.enabled = false;
            // Manual triggers engage ring slow-mo; the spawner stays
            // disabled so UpdateSlowMo never restores it.
            try
            {
                Assert.AreEqual(0f, gauge.CurrentGauge, 0.001f);
                // Seed the manual clock at live Time.time (read once, no
                // live frames consumed): Tick drops inputs on rewound
                // clocks (pause guard) and Update already ticked it with
                // live time during setup frames, so a 0-start would eat
                // the first manual inputs and fail a Hold-first step 0.
                float now = Time.time;
                for (int ring = 0; ring < 1; ring++)
                {
                    mover.transform.position = spawner.Rings[ring].transform.position;
                    spawner.CheckRingTrigger(now, mover.transform.position);
                    Assert.IsTrue(spawner.ChargeActive, "ring did not trigger");
                    int guard = 0;
                    while (spawner.ChargeActive && guard++ < 100)
                    {
                        now += 0.5f;
                        spawner.Tick(now, 0.1f, 10f, FlightTier.Medium, false, false, spawner.Charge.CurrentArrow);
                    }
                    Assert.IsFalse(spawner.ChargeActive, "charge never finished");
                }
                Assert.GreaterOrEqual(gauge.CurrentGauge, 16f, "clean ring did not reach Medium");
                Assert.AreEqual(FlightTier.Medium, sm.ActiveTier);
                Assert.AreEqual(1f, cam.KickEnvelope, 0.001f);
            }
            finally { Time.timeScale = 1f; }
            yield break;
        }

        [UnityTest]
        public IEnumerator RunwayFilledWithRings()
        {
            // Testing layout: a ring every 150m down the 2x runway so every
            // stretch has charge chances (positions shuffle per run, but z
            // pacing and count are fixed).
            var spawner = Object.FindFirstObjectByType<TimingPromptSpawner>();
            Assert.AreEqual(20, spawner.Rings.Count);
            for (int i = 0; i < 20; i++)
                Assert.AreEqual(150f * (i + 1), spawner.Rings[i].transform.position.z, 0.5f);
            yield break;
        }

        [UnityTest]
        public IEnumerator SwimLocksDepthAndPitch()
        {
            // Ship rules: underwater, W/S does nothing — depth holds near the
            // surface, pitch stays level, but yaw still steers in the cone.
            var sm = Object.FindFirstObjectByType<FlightStateMachine>();
            var mover = sm.GetComponent<PlayerMovementController>();
            sm.SetTier(FlightTier.Medium);
            mover.enabled = false; // single-step AFTER SetTier (see Setup note)
            sm.Momentum.CurrentSpeed = 33f; sm.Momentum.TargetSpeed = 33f;
            const float step = 1f / 60f;
            for (int i = 0; i < 120; i++) mover.TickMove(new Vector2(1f, 1f), step);
            Assert.AreEqual(PlayerLocomotionState.Swimming, sm.Locomotion);
            Assert.AreEqual(0f, mover.Pitch, 0.5f);
            Assert.Less(Mathf.Abs(mover.transform.position.y - -1.5f), 0.5f);
            Assert.Greater(Mathf.Abs(mover.Yaw), 20f, "ship does not steer");
            yield break;
        }

        [UnityTest]
        public IEnumerator PauseFreezesSimulation()
        {
            var mover = Object.FindFirstObjectByType<PlayerMovementController>();
            mover.StateMachine.SetTier(FlightTier.Low);
            Object.FindFirstObjectByType<FlightGaugeSystem>().enabled = false;
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
