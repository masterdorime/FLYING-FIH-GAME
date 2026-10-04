using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
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
    }
}
