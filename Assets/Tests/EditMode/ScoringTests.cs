using NUnit.Framework;
using UnityEngine;

namespace FlyingFishMomentum.Tests.EditMode
{
    public class ScoringTests
    {
        Scoring.ScoreSystem NewScore(out FlightStateMachine sm)
        {
            var go = new GameObject("score");
            var score = go.AddComponent<Scoring.ScoreSystem>();
            var mgo = new GameObject("m");
            var momentum = mgo.AddComponent<PlayerMomentumController>();
            var mom = ScriptableObject.CreateInstance<MomentumSettings>();
            momentum.Configure(mom);
            var sgo = new GameObject("sm");
            sm = sgo.AddComponent<FlightStateMachine>();
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
                    Profile(FlightTier.Medium),
                    Profile(FlightTier.Max),
                },
                momentum, mom);
            var settings = ScriptableObject.CreateInstance<Scoring.ScoringSettings>();
            score.Configure(sm, settings);
            return score;
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (go.name == "score" || go.name == "m" || go.name == "sm") Object.DestroyImmediate(go);
        }

        [Test]
        public void ScoreStartsAtZero()
        {
            var score = NewScore(out _);
            Assert.AreEqual(0f, score.Score, 0.001f);
            Assert.AreEqual(0, score.Coins);
            Assert.AreEqual(1, score.Multiplier);
        }

        [Test]
        public void CoinPaysValueTimesTierMultiplier()
        {
            var score = NewScore(out var sm);
            sm.SetTier(FlightTier.Medium);
            Assert.AreEqual(3, score.Multiplier);
            score.AddCoins(1);
            Assert.AreEqual(1, score.Coins);
            Assert.AreEqual(30f, score.Score, 0.001f);
            score.AddCoins(1);
            Assert.AreEqual(60f, score.Score, 0.001f);
        }

        [Test]
        public void PerfectPaysBonusTimesMultiplier()
        {
            var score = NewScore(out var sm);
            sm.SetTier(FlightTier.Max);
            score.OnBeat(TimingResult.Perfect);
            Assert.AreEqual(125f, score.Score, 0.001f);
            score.OnBeat(TimingResult.Good);
            Assert.AreEqual(175f, score.Score, 0.001f);
        }

        [Test]
        public void MissPaysNothingAndKeepsScore()
        {
            // PRD §14: misses cut the multiplier, never the score. The cut
            // arrives via gauge drain lowering the tier; scoring itself only
            // withholds the bonus.
            var score = NewScore(out var sm);
            sm.SetTier(FlightTier.Medium);
            score.AddCoins(1);
            score.OnBeat(TimingResult.Miss);
            Assert.AreEqual(30f, score.Score, 0.001f);
        }

        [Test]
        public void DistanceTrickles()
        {
            var score = NewScore(out _);
            score.AddDistance(100f);
            Assert.AreEqual(100f, score.Score, 0.001f);
        }
    }
}
