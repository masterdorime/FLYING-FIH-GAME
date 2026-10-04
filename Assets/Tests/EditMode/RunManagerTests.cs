using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using FlyingFishMomentum;
using FlyingFishMomentum.Run;

namespace FlyingFishMomentum.Tests.EditMode
{
    public class RunManagerTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void DestroySpawnedManagers()
        {
            foreach (var go in _spawned)
                Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        private RunManager NewManager(int seed)
        {
            var go = new GameObject("runManager");
            _spawned.Add(go);
            var m = go.AddComponent<RunManager>();
            var diff = ScriptableObject.CreateInstance<DifficultySettings>();
            diff.RampSeconds = 240f;
            diff.StormWeightEnd = 3f;
            diff.LagoonFloor = 0.5f;
            var deck = new List<ChunkSpec>
            {
                NewSpec("Lagoon", 0f, 0.6f, 1f),
                NewSpec("Gauntlet", 0.1f, 1f, 1f),
                NewSpec("Storm", 0.35f, 1f, 0.3f),
                NewSpec("Sky", 0.2f, 1f, 0.7f),
            };
            m.Configure(null, null, diff, deck);
            m.SetSeed(seed);
            return m;
        }

        private static ChunkSpec NewSpec(string id, float min, float max, float weight)
        {
            var s = ScriptableObject.CreateInstance<ChunkSpec>();
            s.ChunkId = id;
            s.MinDifficulty = min;
            s.MaxDifficulty = max;
            s.Weight = weight;
            return s;
        }        [Test]
        public void ChunkSpecDefaultsAreSane()
        {
            var s = ScriptableObject.CreateInstance<ChunkSpec>();
            Assert.Greater(s.Length, 0f);
            Assert.GreaterOrEqual(s.Weight, 0f);
            Assert.LessOrEqual(s.MinDifficulty, s.MaxDifficulty);
            Assert.Greater(s.MissionTarget, 0);
        }

        [Test]
        public void MissionBonusPaysTimesMultiplier()
        {
            var go = new GameObject("score");
            var score = go.AddComponent<Scoring.ScoreSystem>();
            score.Configure(null, ScriptableObject.CreateInstance<Scoring.ScoringSettings>());
            score.AddBonus(100f);
            Assert.AreEqual(100f, score.Score, 0.001f);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void DeckReplaysPerSeed()
        {
            var a = NewManager(7); var b = NewManager(7);
            for (int i = 0; i < 20; i++)
                Assert.AreEqual(a.NextType().ChunkId, b.NextType().ChunkId);
        }

        [Test]
        public void DifficultyCapsAtOne()
        {
            var m = NewManager(1);
            m.Tick(0f, 9999f);
            Assert.AreEqual(1f, m.DifficultyT, 0.001f);
        }

        [Test]
        public void StormGrowsLagoonFloorHolds()
        {
            var m = NewManager(11);
            for (int i = 0; i < 20; i++)
                Assert.AreEqual("Lagoon", m.NextType().ChunkId);
            m.Tick(0f, 240f);
            Assert.AreEqual(1f, m.DifficultyT, 0.001f);
            var counts = new Dictionary<string, int>();
            for (int i = 0; i < 200; i++)
            {
                string id = m.NextType().ChunkId;
                counts[id] = counts.ContainsKey(id) ? counts[id] + 1 : 1;
            }
            Assert.Greater(counts["Storm"], counts["Gauntlet"]);
            Assert.Greater(counts["Storm"], counts["Sky"]);
            Assert.IsFalse(counts.ContainsKey("Lagoon"));
        }

        private ChunkBuilder NewBuilder()
        {
            var go = new GameObject("chunkBuilder");
            _spawned.Add(go);
            return go.AddComponent<ChunkBuilder>();
        }

        private static ChunkSpec NewBuildSpec(string id, float length, int rings, float spacing,
            float swimFrac, int trails, int perTrail, int pairs)
        {
            var s = ScriptableObject.CreateInstance<ChunkSpec>();
            s.ChunkId = id;
            s.Length = length;
            s.RingCount = rings;
            s.RingSpacing = spacing;
            s.RingSwimFraction = swimFrac;
            s.CoinTrails = trails;
            s.CoinsPerTrail = perTrail;
            s.IslandPairs = pairs;
            return s;
        }

        private GameObject NewSegment(float centerZ)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.position = new Vector3(0f, 0f, centerZ);
            go.transform.localScale = new Vector3(400f, 1f, 1000f);
            _spawned.Add(go);
            return go;
        }

        [Test]
        public void BuildChunkSpawnsSpecCountsInBounds()
        {
            var builder = NewBuilder();
            var spec = NewBuildSpec("Lagoon", 500f, 3, 150f, 1f, 2, 4, 0);
            builder.BuildChunk(spec, 0f, 42);
            Assert.AreEqual(3, builder.Rings.Count);
            Assert.AreEqual(8, builder.Coins.Count);
            Assert.AreEqual(150f, builder.Rings[0].transform.position.z, 0.01f);
            Assert.AreEqual(300f, builder.Rings[1].transform.position.z, 0.01f);
            Assert.AreEqual(450f, builder.Rings[2].transform.position.z, 0.01f);
            foreach (var ring in builder.Rings)
            {
                var p = ring.transform.position;
                Assert.GreaterOrEqual(p.z, 0f);
                Assert.LessOrEqual(p.z, 500f);
                Assert.AreEqual(-3f, p.y, 0.001f);
                Assert.GreaterOrEqual(p.x, -15f);
                Assert.LessOrEqual(p.x, 15f);
            }
            foreach (var coin in builder.Coins)
            {
                var p = coin.transform.position;
                Assert.GreaterOrEqual(p.z, 0f);
                Assert.LessOrEqual(p.z, 500f);
                Assert.AreEqual(-3f, p.y, 0.001f);
            }
        }

        [Test]
        public void SwimFractionSplitsSwimAndFlyLanes()
        {
            var builder = NewBuilder();
            var spec = NewBuildSpec("Sky", 500f, 4, 120f, 0.5f, 2, 3, 0);
            builder.BuildChunk(spec, 0f, 7);
            int swim = 0, fly = 0;
            foreach (var ring in builder.Rings)
            {
                float y = ring.transform.position.y;
                if (Mathf.Abs(y + 3f) < 0.001f) swim++;
                else if (Mathf.Abs(y - 10f) < 0.001f) fly++;
                else Assert.Fail("ring off both lanes: " + ring.transform.position);
            }
            Assert.AreEqual(2, swim);
            Assert.AreEqual(2, fly);
            int swimCoins = 0, flyCoins = 0;
            foreach (var coin in builder.Coins)
            {
                float y = coin.transform.position.y;
                if (Mathf.Abs(y + 3f) < 0.5f) swimCoins++;
                else if (Mathf.Abs(y - 10f) < 0.5f) flyCoins++;
            }
            Assert.AreEqual(3, swimCoins);
            Assert.AreEqual(3, flyCoins);
        }

        [Test]
        public void SameSeedReplaysIdenticalLayout()
        {
            var spec = NewBuildSpec("Storm", 300f, 2, 150f, 1f, 1, 4, 3);
            var a = NewBuilder();
            var b = NewBuilder();
            a.BuildChunk(spec, 0f, 99);
            b.BuildChunk(spec, 0f, 99);
            Assert.AreEqual(a.Rings.Count, b.Rings.Count);
            for (int i = 0; i < a.Rings.Count; i++)
                Assert.AreEqual(a.Rings[i].transform.position.ToString(), b.Rings[i].transform.position.ToString());
            for (int i = 0; i < a.Coins.Count; i++)
                Assert.AreEqual(a.Coins[i].transform.position.ToString(), b.Coins[i].transform.position.ToString());
            for (int i = 0; i < a.Islands.Count; i++)
                Assert.AreEqual(a.Islands[i].transform.position.ToString(), b.Islands[i].transform.position.ToString());
        }

        [Test]
        public void RingSpacingNeverBelowBeatGapFloor()
        {
            var builder = NewBuilder();
            var spec = NewBuildSpec("Gauntlet", 600f, 4, 40f, 1f, 0, 0, 0);
            builder.BuildChunk(spec, 0f, 5);
            Assert.AreEqual(4, builder.Rings.Count);
            for (int i = 1; i < builder.Rings.Count; i++)
                Assert.GreaterOrEqual(
                    builder.Rings[i].transform.position.z - builder.Rings[i - 1].transform.position.z,
                    100f, "beat gap must fit between rings at band speed (PRD 12.4)");
        }

        [Test]
        public void ChargeRingResetClearsConsumed()
        {
            var builder = NewBuilder();
            builder.BuildChunk(NewBuildSpec("Lagoon", 500f, 1, 150f, 1f, 0, 0, 0), 0f, 3);
            var ring = builder.Rings[0];
            ring.Consume();
            Assert.IsTrue(ring.Consumed);
            ring.Reset();
            Assert.IsFalse(ring.Consumed);
            foreach (var r in ring.GetComponentsInChildren<Renderer>(true))
                Assert.IsTrue(r.enabled);
        }

        [Test]
        public void ReclaimBeforeRemovesPastLineContent()
        {
            var builder = NewBuilder();
            var spec = NewBuildSpec("Lagoon", 500f, 3, 150f, 1f, 2, 4, 0);
            builder.BuildChunk(spec, 0f, 11);
            builder.BuildChunk(spec, 500f, 12);
            Assert.AreEqual(6, builder.Rings.Count);
            builder.ReclaimBefore(600f);
            Assert.AreEqual(3, builder.Rings.Count);
            foreach (var ring in builder.Rings)
                Assert.GreaterOrEqual(ring.transform.position.z, 600f);
            foreach (var coin in builder.Coins)
                Assert.GreaterOrEqual(coin.transform.position.z, 600f);
        }

        [Test]
        public void ReclaimedContentResetsOnReuse()
        {
            var builder = NewBuilder();
            var spec = NewBuildSpec("Lagoon", 500f, 2, 150f, 1f, 1, 2, 0);
            builder.BuildChunk(spec, 0f, 21);
            builder.Rings[0].Consume();
            builder.Coins[0].Collect();
            builder.ReclaimBefore(10000f);
            Assert.AreEqual(0, builder.Rings.Count);
            Assert.AreEqual(0, builder.Coins.Count);
            builder.BuildChunk(spec, 0f, 22);
            Assert.AreEqual(2, builder.Rings.Count);
            Assert.AreEqual(2, builder.Coins.Count);
            foreach (var ring in builder.Rings)
                Assert.IsFalse(ring.Consumed);
            foreach (var coin in builder.Coins)
            {
                Assert.IsFalse(coin.Collected);
                foreach (var r in coin.GetComponentsInChildren<Renderer>(true))
                    Assert.IsTrue(r.enabled);
            }
        }

        [Test]
        public void StormGatesStayWideAndPromptsStayClear()
        {
            var spec = NewBuildSpec("Storm", 300f, 2, 150f, 1f, 1, 4, 3);
            for (int seed = 1; seed <= 5; seed++)
            {
                var builder = NewBuilder();
                builder.BuildChunk(spec, 0f, seed);
                Assert.AreEqual(6, builder.Islands.Count);
                Physics.SyncTransforms();
                var bounds = new List<Bounds>();
                foreach (var island in builder.Islands)
                    bounds.Add(island.GetComponent<Collider>().bounds);
                var used = new bool[bounds.Count];
                for (int i = 0; i < bounds.Count; i++)
                {
                    if (used[i]) continue;
                    int mate = -1;
                    for (int j = i + 1; j < bounds.Count; j++)
                        if (!used[j] && Mathf.Abs(bounds[j].center.z - bounds[i].center.z) < 0.01f) { mate = j; break; }
                    Assert.GreaterOrEqual(mate, 0, "island pair mate missing at seed " + seed);
                    used[i] = used[mate] = true;
                    var left = bounds[i].center.x < bounds[mate].center.x ? bounds[i] : bounds[mate];
                    var right = left == bounds[i] ? bounds[mate] : bounds[i];
                    Assert.GreaterOrEqual(right.min.x - left.max.x, 20f, "gate gap under 20m at seed " + seed);
                }
                foreach (var ring in builder.Rings)
                    foreach (var b in bounds)
                        Assert.IsFalse(b.Contains(ring.transform.position), "ring inside island at seed " + seed);
                foreach (var coin in builder.Coins)
                    foreach (var b in bounds)
                        Assert.IsFalse(b.Contains(coin.transform.position), "coin inside island at seed " + seed);
            }
        }

        [Test]
        public void RecycleSegmentsKeepsWaterContiguous()
        {
            var builder = NewBuilder();
            var water = new List<GameObject>();
            var seabed = new List<GameObject>();
            for (int i = 0; i < 4; i++)
            {
                water.Add(NewSegment(500f + i * 1000f));
                seabed.Add(NewSegment(500f + i * 1000f));
            }
            builder.Configure(water, seabed);
            builder.RecycleSegments(1200f);
            Assert.AreEqual(4500f, water[0].transform.position.z, 0.01f);
            Assert.AreEqual(4500f, seabed[0].transform.position.z, 0.01f);
            var zs = new List<float>();
            foreach (var s in water) zs.Add(s.transform.position.z);
            zs.Sort();
            for (int i = 1; i < zs.Count; i++)
                Assert.AreEqual(1000f, zs[i] - zs[i - 1], 0.01f);
            foreach (var s in water)
                Assert.GreaterOrEqual(s.transform.position.z + 500f, 1200f);
        }

        // M4 Task 5: missions poll existing public state only (score Coins,
        // spawner charge, sm locomotion, player z). Real components, no mocks.
        private static ChunkSpec NewMissionSpec(string id, string text, int target)
        {
            var s = ScriptableObject.CreateInstance<ChunkSpec>();
            s.ChunkId = id;
            s.MissionText = text;
            s.MissionTarget = target;
            return s;
        }

        private RunManager NewMissionManager(
            out TimingPromptSpawner spawner,
            out Scoring.ScoreSystem score,
            out FlightStateMachine sm,
            out Scoring.ScoringSettings settings)
        {
            var smGo = new GameObject("missionSm");
            _spawned.Add(smGo);
            sm = smGo.AddComponent<FlightStateMachine>();
            var spawnerGo = new GameObject("missionSpawner");
            _spawned.Add(spawnerGo);
            spawner = spawnerGo.AddComponent<TimingPromptSpawner>();
            settings = ScriptableObject.CreateInstance<Scoring.ScoringSettings>();
            var scoreGo = new GameObject("missionScore");
            _spawned.Add(scoreGo);
            score = scoreGo.AddComponent<Scoring.ScoreSystem>();
            score.Configure(sm, settings);
            var go = new GameObject("missionRunManager");
            _spawned.Add(go);
            var m = go.AddComponent<RunManager>();
            m.Configure(spawner, score, sm, ScriptableObject.CreateInstance<DifficultySettings>(), null);
            m.SetSeed(5);
            return m;
        }

        private static void CompleteRingCharge(
            RunManager m, TimingPromptSpawner spawner, TimingSettings timing, float t0)
        {
            spawner.Charge.Begin(
                new[] { ChargeArrow.Up, ChargeArrow.Down, ChargeArrow.Left, ChargeArrow.Right }, t0);
            m.Tick(0f, 0.1f); // observe the active charge (arms the edge)
            spawner.Charge.Tick(t0 + 1f, ChargeArrow.Up, timing);
            spawner.Charge.Tick(t0 + 2f, ChargeArrow.Down, timing);
            spawner.Charge.Tick(t0 + 3f, ChargeArrow.Left, timing);
            spawner.Charge.Tick(t0 + 4f, ChargeArrow.Right, timing);
            Assert.IsFalse(spawner.Charge.IsActive);
            Assert.AreEqual(4, spawner.Charge.StepIndex);
            m.Tick(0f, 0.1f); // observe active->inactive (counts one ring)
        }

        [Test]
        public void CollectMissionCompletesOnCoinsDelta()
        {
            var spec = NewMissionSpec("Lagoon", "collect 30 coins", 30);
            var m = NewMissionManager(out _, out var score, out _, out _);
            score.AddCoins(5); // pre-entry coins must not count
            m.EnterChunk(spec);
            m.Tick(0f, 0.1f); // entry observed: baseline snapped, card shows
            Assert.AreEqual("collect 30 coins", m.MissionText);
            Assert.AreEqual(0f, m.MissionProgress01, 0.001f);
            score.AddCoins(15);
            m.Tick(10f, 0.1f);
            Assert.AreEqual(0.5f, m.MissionProgress01, 0.001f);
            score.AddCoins(15);
            m.Tick(20f, 0.1f);
            Assert.AreEqual(1f, m.MissionProgress01, 0.001f);
        }

        [Test]
        public void RingMissionCompletesOnFullCharge()
        {
            var spec = NewMissionSpec("Gauntlet", "2 clean rings", 2);
            var m = NewMissionManager(out var spawner, out _, out _, out _);
            var timing = ScriptableObject.CreateInstance<TimingSettings>();
            m.EnterChunk(spec);
            m.Tick(0f, 0.1f);
            // Aborted charge (timeout, steps short) counts nothing.
            spawner.Charge.Begin(
                new[] { ChargeArrow.Up, ChargeArrow.Down, ChargeArrow.Left, ChargeArrow.Right }, 0f);
            m.Tick(0f, 0.1f);
            spawner.Charge.Tick(100f, null, timing);
            Assert.IsFalse(spawner.Charge.IsActive);
            m.Tick(0f, 0.1f);
            Assert.AreEqual(0f, m.MissionProgress01, 0.001f);
            CompleteRingCharge(m, spawner, timing, 200f);
            Assert.AreEqual(0.5f, m.MissionProgress01, 0.001f);
            CompleteRingCharge(m, spawner, timing, 300f);
            Assert.AreEqual(1f, m.MissionProgress01, 0.001f);
        }

        [Test]
        public void AirtimeMissionAccumulatesFlyingSeconds()
        {
            var spec = NewMissionSpec("Sky", "stay airborne 20s", 20);
            var m = NewMissionManager(out _, out _, out var sm, out _);
            m.EnterChunk(spec);
            m.Tick(0f, 0.1f);
            sm.SetLocomotion(PlayerLocomotionState.Swimming);
            m.Tick(0f, 5f);
            Assert.AreEqual(0f, m.MissionProgress01, 0.001f);
            sm.SetLocomotion(PlayerLocomotionState.Flying);
            m.Tick(0f, 10f);
            Assert.AreEqual(0.5f, m.MissionProgress01, 0.001f);
            m.Tick(0f, 10f);
            Assert.AreEqual(1f, m.MissionProgress01, 0.001f);
        }

        [Test]
        public void GateMissionCompletesOnZCrossings()
        {
            var spec = NewMissionSpec("Storm", "pass 4 rock gates", 4);
            var m = NewMissionManager(out _, out _, out _, out _);
            m.EnterChunk(spec);
            m.SetGates(new List<float> { 10f, 100f, 200f, 300f, 400f });
            m.Tick(50f, 0.1f); // entry z = 50: gate at 10 stays behind
            Assert.AreEqual(0f, m.MissionProgress01, 0.001f);
            m.Tick(150f, 0.1f);
            Assert.AreEqual(0.25f, m.MissionProgress01, 0.001f);
            m.Tick(450f, 0.1f);
            Assert.AreEqual(1f, m.MissionProgress01, 0.001f);
        }

        [Test]
        public void CompletionPaysMissionBonusTimesMultiplierOnce()
        {
            FlightTierProfile Profile(FlightTier tier)
            {
                var p = ScriptableObject.CreateInstance<FlightTierProfile>();
                p.Tier = tier;
                p.MaxSpeed = 10f + (float)tier * 10f;
                p.Acceleration = 45f;
                return p;
            }
            var spec = NewMissionSpec("Lagoon", "collect 2 coins", 2);
            var m = NewMissionManager(out _, out var score, out var sm, out var settings);
            sm.Configure(
                new List<FlightTierProfile> { Profile(FlightTier.None), Profile(FlightTier.Medium) },
                null, null);
            sm.SetTier(FlightTier.Medium);
            Assert.AreEqual(3, score.Multiplier);
            m.EnterChunk(spec);
            m.Tick(0f, 0.1f);
            score.AddCoins(2);
            float before = score.Score;
            m.Tick(10f, 0.1f);
            Assert.AreEqual(before + settings.MissionBonus * score.Multiplier, score.Score, 0.001f);
            m.Tick(20f, 0.1f); // no double pay
            Assert.AreEqual(before + settings.MissionBonus * score.Multiplier, score.Score, 0.001f);
        }

        [Test]
        public void NewChunkEntryResetsProgress()
        {
            var m = NewMissionManager(out _, out var score, out _, out _);
            m.EnterChunk(NewMissionSpec("Lagoon", "collect 30 coins", 30));
            m.Tick(0f, 0.1f);
            score.AddCoins(15);
            m.Tick(10f, 0.1f);
            Assert.AreEqual(0.5f, m.MissionProgress01, 0.001f);
            m.EnterChunk(NewMissionSpec("Sky", "stay airborne 20s", 20));
            m.Tick(10f, 0.1f);
            Assert.AreEqual("stay airborne 20s", m.MissionText);
            Assert.AreEqual(0f, m.MissionProgress01, 0.001f);
        }
    }
}
