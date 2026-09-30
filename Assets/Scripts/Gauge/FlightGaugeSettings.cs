using UnityEngine;

namespace FlyingFishMomentum
{
    // M3 gauge tuning (spec S1). Tier floors: below 20 is None.
    [CreateAssetMenu(fileName = "FlightGaugeSettings", menuName = "FlyingFish/Gauge Settings")]
    public class FlightGaugeSettings : ScriptableObject
    {
        public float MaxGauge = 120f;
        public float StartGauge = 0f;
        public float ChargeStepFill = 2f;
        public float ChargeJackpotFill = 4f;
        public float FlyDrainPerSecond = 3.5f;
        public float MissDrain = 10f;
        public float[] TierThresholds = { 20f, 40f, 70f, 100f };

        public FlightTier TierFor(float gauge)
        {
            float g = Mathf.Clamp(gauge, 0f, MaxGauge);
            FlightTier tier = FlightTier.None;
            for (int i = 0; i < TierThresholds.Length; i++)
            {
                if (g >= TierThresholds[i])
                {
                    tier = (FlightTier)(i + 1);
                }
            }
            return tier;
        }
    }
}
