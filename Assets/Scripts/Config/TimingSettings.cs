using UnityEngine;

namespace FlyingFishMomentum
{
    // M2 timing tuning (PRD §§6.4, 7.2-7.3). Boost/penalty arrays indexed by
    // FlightTier int (None=0..Max=4); the None row is 0.
    [CreateAssetMenu(fileName = "TimingSettings", menuName = "FlyingFish/Timing Settings")]
    public class TimingSettings : ScriptableObject
    {
        public float PromptEveryMeters = 60f;
        public float LeadTime = 1f;
        public float PerfectWindow = 0.07f;
        public float MinPerfectWindow = 0.035f;
        public float GoodWindow = 0.18f;
        public float MinGoodWindow = 0.09f;
        public float LateBuffer = 0.06f;
        // Speed reference for window shrink (PRD §7.2: InverseLerp up to Max tier max).
        public float MaxSpeedRef = 110f;
        public float[] PerfectBoost = { 0f, 12f, 16f, 22f, 30f };
        public float[] GoodBoost = { 0f, 6f, 8f, 11f, 15f };
        public float[] MissPenalty = { 0f, 18f, 28f, 45f, 70f }; // positive magnitudes

        public float DeltaFor(TimingResult r, FlightTier t)
        {
            int i = Mathf.Clamp((int)t, 0, 4);
            return r == TimingResult.Perfect ? PerfectBoost[i]
                : r == TimingResult.Good ? GoodBoost[i] : -MissPenalty[i];
        }
    }
}
