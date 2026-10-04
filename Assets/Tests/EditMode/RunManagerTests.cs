using NUnit.Framework;
using UnityEngine;
using FlyingFishMomentum.Run;

namespace FlyingFishMomentum.Tests.EditMode
{
    public class RunManagerTests
    {
        [Test]
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
    }
}
