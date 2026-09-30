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
            spawner.Configure(null, momentum, null, timing, mom, null, null);
            spawner.SetSeed(42);
            return spawner;
        }

        // M3 gauge wiring: spawner with a REAL gauge + state machine
        // (Task 2 helper pattern) so charge fills and beat-Miss drains end
        // to end, including tier reconcile.
        TimingPromptSpawner NewSpawnerWithGauge(
            out PlayerMomentumController momentum,
            out FlightGaugeSystem gauge,
            out FlightStateMachine sm)
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
            var ggo = new GameObject("gauge");
            var gmom = ggo.AddComponent<PlayerMomentumController>();
            var gmomSettings = ScriptableObject.CreateInstance<MomentumSettings>();
            gmomSettings.MinSpeed = 5f;
            gmomSettings.DragSwimming = 1.5f;
            gmomSettings.DragFlying = 0.6f;
            gmomSettings.BreachSpeedThreshold = 20f;
            var gtiming = ScriptableObject.CreateInstance<TimingSettings>();
            gmom.Configure(gmomSettings, gtiming);
            sm = ggo.AddComponent<FlightStateMachine>();
            FlightTierProfile Profile(FlightTier tier)
            {
                var p = ScriptableObject.CreateInstance<FlightTierProfile>();
                p.Tier = tier;
                p.MaxSpeed = 10f + (float)tier * 10f;
                p.Acceleration = 45f;
                return p;
            }
            sm.Configure(
                new System.Collections.Generic.List<FlightTierProfile>
                {
                    Profile(FlightTier.None),
                    Profile(FlightTier.Low),
                    Profile(FlightTier.Medium),
                    Profile(FlightTier.High),
                    Profile(FlightTier.Max),
                },
                gmom, gmomSettings);
            var gsettings = ScriptableObject.CreateInstance<FlightGaugeSettings>();
            gsettings.MaxGauge = 120f;
            gsettings.StartGauge = 0f;
            gsettings.FlyDrainPerSecond = 3.5f;
            gsettings.MissDrain = 10f;
            gsettings.TierThresholds = new float[] { 20f, 40f, 70f, 100f };
            gauge = ggo.AddComponent<FlightGaugeSystem>();
            gauge.Configure(sm, gsettings, null);
            spawner.Configure(null, momentum, sm, timing, mom, null, gauge);
            spawner.SetSeed(42);
            return spawner;
        }

        TimingPromptSpawner NewSpawnerWithRingAndGauge(
            out PlayerMomentumController momentum,
            out FlightGaugeSystem gauge,
            out ChargeRing ring)
        {
            var spawner = NewSpawnerWithGauge(out momentum, out gauge, out _);
            var rgo = new GameObject("ring");
            rgo.transform.position = Vector3.zero;
            ring = rgo.AddComponent<ChargeRing>();
            spawner.SetRings(new System.Collections.Generic.List<ChargeRing> { ring });
            return spawner;
        }

        [TearDown]
        public void Cleanup()
        {
            Time.timeScale = 1f; // ring triggers touch global slow-mo
            foreach (var go in GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (go.name == "spawner" || go.name == "m" || go.name == "gauge" || go.name == "ring") Object.DestroyImmediate(go);
        }

        [Test]
        public void PromptBeatWithinRange()
        {
            // Beat is drawn 40-80m: nothing at 35m, guaranteed open by 80m.
            var spawner = NewSpawner(out _);
            float now = 0f;
            for (int i = 0; i < 7; i++) { now += 0.1f; spawner.Tick(now, 0.1f, 50f, FlightTier.Medium, false); }
            Assert.IsFalse(spawner.Active.Open, "opened before 40m");
            for (int i = 0; i < 9; i++) { now += 0.1f; spawner.Tick(now, 0.1f, 50f, FlightTier.Medium, false); }
            Assert.IsTrue(spawner.Active.Open, "no prompt by 80m");
            Assert.GreaterOrEqual(spawner.Active.TargetTime, now);
        }

        [Test]
        public void SecondPromptSuppressedWhileOpen()
        {
            // Frozen clock: +100m of travel with no time passing must not
            // open a second prompt (nor expire the first).
            var spawner = NewSpawner(out _);
            float now = 0f;
            for (int i = 0; i < 16; i++) { now += 0.1f; spawner.Tick(now, 0.1f, 50f, FlightTier.Medium, false); }
            Assert.IsTrue(spawner.Active.Open);
            float first = spawner.Active.TargetTime;
            for (int i = 0; i < 20; i++) spawner.Tick(now, 0.1f, 50f, FlightTier.Medium, false);
            Assert.IsTrue(spawner.Active.Open);
            Assert.AreEqual(first, spawner.Active.TargetTime, 0.001f);
        }

        [Test]
        public void SameScheduleIsDeterministic()
        {
            var a = NewSpawner(out _);
            float now = 0f;
            for (int i = 0; i < 16; i++) { now += 0.1f; a.Tick(now, 0.1f, 50f, FlightTier.Medium, false); }
            Cleanup();
            var b = NewSpawner(out _);
            now = 0f;
            for (int i = 0; i < 16; i++) { now += 0.1f; b.Tick(now, 0.1f, 50f, FlightTier.Medium, false); }
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
            for (int i = 0; i < 16; i++) { now += 0.1f; spawner.Tick(now, 0.1f, 50f, FlightTier.Medium, false); }
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
            for (int i = 0; i < 16; i++) { now += 0.1f; spawner.Tick(now, 0.1f, 50f, FlightTier.Medium, false); }
            spawner.Tick(spawner.Active.TargetTime, 0f, 50f, FlightTier.Medium, true);
            Assert.IsTrue(spawner.HasResolved);
            Assert.AreEqual(TimingResult.Perfect, spawner.LastResult);
            Assert.AreEqual(55f, momentum.CurrentSpeed, 0.5f);
        }

        [Test]
        public void ExpiryResolvesMiss()
        {
            var spawner = NewSpawner(out var momentum);
            momentum.CurrentSpeed = 50f; momentum.TargetSpeed = 50f;
            float now = 0f;
            for (int i = 0; i < 16; i++) { now += 0.1f; spawner.Tick(now, 0.1f, 50f, FlightTier.Medium, false); }
            float target = spawner.Active.TargetTime;
            spawner.Tick(target + 0.3f, 0f, 50f, FlightTier.Medium, false);
            Assert.IsTrue(spawner.HasResolved);
            Assert.AreEqual(TimingResult.Miss, spawner.LastResult);
            Assert.IsFalse(spawner.Active.Open);
            Assert.Less(momentum.CurrentSpeed, 50f);
        }

        [Test]
        public void StreakCounterResetsOnMiss()
        {
            var spawner = NewSpawner(out var momentum);
            momentum.CurrentSpeed = 50f; momentum.TargetSpeed = 50f;
            float now = 0f;
            for (int i = 0; i < 16; i++) { now += 0.1f; spawner.Tick(now, 0.1f, 50f, FlightTier.Medium, false); }
            spawner.Tick(spawner.Active.TargetTime, 0f, 50f, FlightTier.Medium, true);
            Assert.AreEqual(TimingResult.Perfect, spawner.LastResult);
            Assert.AreEqual(1, spawner.StreakCount);
            for (int i = 0; i < 16; i++) { now += 0.1f; spawner.Tick(now, 0.1f, 50f, FlightTier.Medium, false); }
            spawner.Tick(spawner.Active.TargetTime + 0.3f, 0f, 50f, FlightTier.Medium, false);
            Assert.AreEqual(TimingResult.Miss, spawner.LastResult);
            Assert.AreEqual(0, spawner.StreakCount);
        }

        [Test]
        public void FrozenClockIgnoresPress()
        {
            // Pause freezes Time.time: a press on a frozen clock must not
            // resolve anything (no risk-free banked Perfects, no unfair Miss).
            var spawner = NewSpawner(out _);
            float now = 0f;
            for (int i = 0; i < 16; i++) { now += 0.1f; spawner.Tick(now, 0.1f, 50f, FlightTier.Medium, false); }
            Assert.IsTrue(spawner.Active.Open);
            for (int i = 0; i < 10; i++) spawner.Tick(now, 0f, 50f, FlightTier.Medium, true);
            Assert.IsTrue(spawner.Active.Open, "frozen press resolved the prompt");
            Assert.IsFalse(spawner.HasResolved);
        }

        [Test]
        public void HitAngleInRangeAndSeeded()
        {
            var a = NewSpawner(out _);
            float now = 0f;
            for (int i = 0; i < 16; i++) { now += 0.1f; a.Tick(now, 0.1f, 50f, FlightTier.Medium, false); }
            Assert.IsTrue(a.Active.Open);
            Assert.GreaterOrEqual(a.HitAngleDeg, 0f);
            Assert.Less(a.HitAngleDeg, 360f);
            float firstHit = a.HitAngleDeg;
            Cleanup();
            var b = NewSpawner(out _);
            now = 0f;
            for (int i = 0; i < 16; i++) { now += 0.1f; b.Tick(now, 0.1f, 50f, FlightTier.Medium, false); }
            Assert.AreEqual(firstHit, b.HitAngleDeg, 0.001f);
        }

        TimingPromptSpawner NewSpawnerWithRing(out PlayerMomentumController momentum, out ChargeRing ring)
        {
            var spawner = NewSpawner(out momentum);
            var rgo = new GameObject("ring");
            rgo.transform.position = Vector3.zero;
            ring = rgo.AddComponent<ChargeRing>();
            spawner.SetRings(new System.Collections.Generic.List<ChargeRing> { ring });
            return spawner;
        }

        [Test]
        public void RingTriggerStartsCharge()
        {
            var spawner = NewSpawnerWithRing(out _, out var ring);
            Assert.IsFalse(spawner.ChargeActive);
            spawner.CheckRingTrigger(0f, new Vector3(0f, 0f, 1f));
            Assert.IsTrue(spawner.ChargeActive);
            Assert.IsTrue(ring.Consumed);
            spawner.CheckRingTrigger(1f, new Vector3(100f, 0f, 100f));
            Assert.IsTrue(spawner.ChargeActive); // far ring check changes nothing
        }

        [Test]
        public void ChargeOrderSeededReplay()
        {
            var a = NewSpawnerWithRing(out _, out _);
            a.CheckRingTrigger(0f, Vector3.zero);
            string orderA = string.Join(",", System.Array.ConvertAll(a.ChargeOrder, k => k.ToString()));
            Cleanup();
            var b = NewSpawnerWithRing(out _, out _);
            b.CheckRingTrigger(0f, Vector3.zero);
            string orderB = string.Join(",", System.Array.ConvertAll(b.ChargeOrder, k => k.ToString()));
            Assert.AreEqual(orderA, orderB);
            Assert.AreEqual(3, a.ChargeOrder.Length);
        }

        [Test]
        public void ChargeHoldBanksViaMomentum()
        {
            // Adaptive driver: hold each HOLD step to just past required,
            // tap each TAP step at its target. Every step succeeds:
            // 3x+2 plus +4 jackpot: 10 → 20.
            var spawner = NewSpawnerWithRing(out var momentum, out _);
            momentum.CurrentSpeed = 10f; momentum.TargetSpeed = 10f;
            spawner.CheckRingTrigger(0f, Vector3.zero);
            float now = 0f;
            int guard = 0;
            while (spawner.ChargeActive && guard++ < 300)
            {
                now += 0.1f;
                var ch = spawner.Charge;
                bool isHold = ch.CurrentKind == ChargeStepKind.Hold;
                bool held = isHold && now < ch.StepStartTime + spawner.Settings.HoldRequired + 0.05f;
                bool press = !isHold && now >= ch.StepStartTime + spawner.Settings.TapLead - 0.05f;
                spawner.Tick(now, 0.1f, 10f, FlightTier.Medium, press, held);
            }
            Assert.IsFalse(spawner.ChargeActive, "charge never finished");
            Assert.AreEqual(20f, momentum.CurrentSpeed, 0.5f);
        }

        [Test]
        public void BeatSuspendedDuringCharge()
        {
            var spawner = NewSpawnerWithRing(out _, out _);
            spawner.CheckRingTrigger(0f, Vector3.zero);
            float now = 0f;
            for (int i = 0; i < 20; i++) { now += 0.1f; spawner.Tick(now, 0.1f, 50f, FlightTier.Medium, false, true); }
            Assert.IsFalse(spawner.Active.Open, "beat prompt opened during charge");
        }

        [Test]
        public void EarlyPressWhileOpenIsMiss()
        {
            // Anti-mash rule at spawner level, not just the evaluator unit.
            var spawner = NewSpawner(out _);
            float now = 0f;
            for (int i = 0; i < 16; i++) { now += 0.1f; spawner.Tick(now, 0.1f, 50f, FlightTier.Medium, false); }
            Assert.IsTrue(spawner.Active.Open);
            spawner.Tick(now + 0.05f, 0.05f, 50f, FlightTier.Medium, true);
            Assert.IsTrue(spawner.HasResolved);
            Assert.AreEqual(TimingResult.Miss, spawner.LastResult);
            Assert.IsFalse(spawner.Active.Open);
        }

        [Test]
        public void HugeDtResolvesAtMostOnePrompt()
        {
            // A 10s hitch resolves the open prompt as Miss and banks meters
            // without opening (and instantly missing) a second prompt.
            var spawner = NewSpawner(out _);
            float now = 0f;
            for (int i = 0; i < 16; i++) { now += 0.1f; spawner.Tick(now, 0.1f, 50f, FlightTier.Medium, false); }
            Assert.IsTrue(spawner.Active.Open);
            spawner.Tick(now + 10f, 10f, 50f, FlightTier.Medium, false);
            Assert.IsTrue(spawner.HasResolved);
            Assert.AreEqual(TimingResult.Miss, spawner.LastResult);
            Assert.IsFalse(spawner.Active.Open, "huge dt opened a second prompt in the same tick");
        }

        [Test]
        public void RingTriggerCancelsOpenBeat()
        {
            // The two QTEs never overlap: touching a ring with a beat prompt
            // open closes the beat silently (no judgment, no penalty) and
            // starts charge. A fresh beat waits a full gap after charge ends.
            var spawner = NewSpawnerWithRing(out _, out _);
            float now = 0f;
            for (int i = 0; i < 16; i++) { now += 0.1f; spawner.Tick(now, 0.1f, 50f, FlightTier.Medium, false); }
            Assert.IsTrue(spawner.Active.Open, "beat never opened");
            spawner.CheckRingTrigger(now, Vector3.zero);
            Assert.IsTrue(spawner.ChargeActive, "charge did not start");
            Assert.IsFalse(spawner.Active.Open, "beat stayed open under charge");
            Assert.IsFalse(spawner.HasResolved, "cancelled beat judged anything");
        }

        [Test]
        public void RingTriggerStartsSlowMo()
        {
            var spawner = NewSpawnerWithRing(out _, out _);
            try
            {
                spawner.CheckRingTrigger(0f, Vector3.zero);
                Assert.AreEqual(0.4f, Time.timeScale, 0.001f);
            }
            finally { Time.timeScale = 1f; }
        }

        [Test]
        public void SlowMoExpiresBackToFullSpeed()
        {
            var spawner = NewSpawnerWithRing(out _, out _);
            try
            {
                spawner.CheckRingTrigger(0f, Vector3.zero);
                spawner.UpdateSlowMo(0.5f);
                Assert.AreEqual(0.4f, Time.timeScale, 0.001f);
                spawner.UpdateSlowMo(0.8f);
                Assert.AreEqual(1f, Time.timeScale, 0.001f);
            }
            finally { Time.timeScale = 1f; }
        }

        [Test]
        public void SlowMoYieldsToPause()
        {
            var spawner = NewSpawnerWithRing(out _, out _);
            try
            {
                spawner.CheckRingTrigger(0f, Vector3.zero);
                Time.timeScale = 0f; // player pauses mid-slow-mo
                spawner.UpdateSlowMo(5f);
                Assert.AreEqual(0f, Time.timeScale, 0.001f);
            }
            finally { Time.timeScale = 1f; }
        }

        [Test]
        public void FastJumpOverRingStillTriggers()
        {
            // A 0.1s hitch at 70u/s moves 7m through the 4m window: the
            // segment check must catch the crossing, not just endpoints.
            var spawner = NewSpawnerWithRing(out _, out _);
            spawner.CheckRingTrigger(0f, new Vector3(0f, 0f, -10f));
            Assert.IsFalse(spawner.ChargeActive);
            spawner.CheckRingTrigger(0.1f, new Vector3(0f, 0f, 10f));
            Assert.IsTrue(spawner.ChargeActive, "tunneled the ring");
        }

        [Test]
        public void ChargeGainFillsGauge()
        {
            // Charge gains fill the gauge by the banked amount: a full
            // hold run banks 10 speed, so the gauge rises by 10.
            var spawner = NewSpawnerWithRingAndGauge(out var momentum, out var gauge, out _);
            momentum.CurrentSpeed = 10f; momentum.TargetSpeed = 10f;
            Assert.AreEqual(0f, gauge.CurrentGauge, 0.001f);
            spawner.CheckRingTrigger(0f, Vector3.zero);
            float now = 0f;
            int guard = 0;
            while (spawner.ChargeActive && guard++ < 300)
            {
                now += 0.1f;
                var ch = spawner.Charge;
                bool isHold = ch.CurrentKind == ChargeStepKind.Hold;
                bool held = isHold && now < ch.StepStartTime + spawner.Settings.HoldRequired + 0.05f;
                bool press = !isHold && now >= ch.StepStartTime + spawner.Settings.TapLead - 0.05f;
                spawner.Tick(now, 0.1f, 10f, FlightTier.Medium, press, held);
            }
            Assert.IsFalse(spawner.ChargeActive, "charge never finished");
            float banked = momentum.CurrentSpeed - 10f;
            Assert.Greater(banked, 0f, "nothing banked");
            Assert.AreEqual(banked, gauge.CurrentGauge, 0.5f);
        }

        [Test]
        public void BeatMissDrainsGauge()
        {
            // Pre-filled gauge loses MissDrain on beat expiry; tier follows.
            var spawner = NewSpawnerWithGauge(out var momentum, out var gauge, out var sm);
            momentum.CurrentSpeed = 50f; momentum.TargetSpeed = 50f;
            gauge.AddFill(25f);
            Assert.AreEqual(FlightTier.Low, sm.ActiveTier);
            float now = 0f;
            for (int i = 0; i < 16; i++) { now += 0.1f; spawner.Tick(now, 0.1f, 50f, FlightTier.Medium, false); }
            Assert.IsTrue(spawner.Active.Open);
            spawner.Tick(spawner.Active.TargetTime + 0.3f, 0f, 50f, FlightTier.Medium, false);
            Assert.AreEqual(TimingResult.Miss, spawner.LastResult);
            Assert.AreEqual(15f, gauge.CurrentGauge, 0.001f);
            Assert.AreEqual(FlightTier.None, sm.ActiveTier);
        }

        [Test]
        public void TapsDoNotFillGauge()
        {
            // Perfect beat taps never touch the gauge.
            var spawner = NewSpawnerWithGauge(out var momentum, out var gauge, out _);
            momentum.CurrentSpeed = 50f; momentum.TargetSpeed = 50f;
            gauge.AddFill(25f);
            float now = 0f;
            for (int i = 0; i < 16; i++) { now += 0.1f; spawner.Tick(now, 0.1f, 50f, FlightTier.Medium, false); }
            spawner.Tick(spawner.Active.TargetTime, 0f, 50f, FlightTier.Medium, true);
            Assert.AreEqual(TimingResult.Perfect, spawner.LastResult);
            Assert.AreEqual(25f, gauge.CurrentGauge, 0.001f);
        }

        [Test]
        public void NullGaugeNeverThrows()
        {
            // Null gauge (tests/scenes without one) skips gauge calls:
            // a full hold still banks speed, no exception.
            var spawner = NewSpawnerWithRing(out var momentum, out _);
            momentum.CurrentSpeed = 10f; momentum.TargetSpeed = 10f;
            spawner.CheckRingTrigger(0f, Vector3.zero);
            float now = 0f;
            int guard = 0;
            Assert.DoesNotThrow(() =>
            {
                float t = now;
                int g = guard;
                while (spawner.ChargeActive && g++ < 300)
                {
                    t += 0.1f;
                    var ch = spawner.Charge;
                    bool isHold = ch.CurrentKind == ChargeStepKind.Hold;
                    bool held = isHold && t < ch.StepStartTime + spawner.Settings.HoldRequired + 0.05f;
                    bool press = !isHold && t >= ch.StepStartTime + spawner.Settings.TapLead - 0.05f;
                    spawner.Tick(t, 0.1f, 10f, FlightTier.Medium, press, held);
                }
                now = t; guard = g;
            });
            Assert.IsFalse(spawner.ChargeActive, "charge never finished");
            Assert.AreEqual(20f, momentum.CurrentSpeed, 0.5f);
        }
    }
}
