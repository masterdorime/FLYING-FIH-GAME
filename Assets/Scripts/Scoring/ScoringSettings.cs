using UnityEngine;

namespace FlyingFishMomentum.Scoring
{
    [CreateAssetMenu(fileName = "ScoringSettings", menuName = "FlyingFish/Scoring Settings")]
    public class ScoringSettings : ScriptableObject
    {
        // Score chase v1: coins + timing bonuses scaled by the live tier
        // multiplier (None x1 .. Max x5). Misses withhold bonuses; the
        // multiplier cut arrives via gauge drain lowering the tier.
        public float CoinValue = 10f;
        public float PerfectBonus = 25f;
        public float GoodBonus = 10f;
        public float DistancePerMeter = 1f;
        public float MissionBonus = 100f;
    }
}
