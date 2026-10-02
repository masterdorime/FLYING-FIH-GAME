using UnityEngine;

namespace FlyingFishMomentum
{
    // M2 timing tuning (PRD §§6.4, 7.2-7.3). Boost/penalty arrays indexed by
    // FlightTier int (None=0..Max=4); the None row is 0.
    [CreateAssetMenu(fileName = "TimingSettings", menuName = "FlyingFish/Timing Settings")]
    public class TimingSettings : ScriptableObject
    {
        public float BeatMinMeters = 40f;
        public float BeatMaxMeters = 80f;
        public float LeadTime = 1f;
        public float PerfectWindow = 0.07f;
        public float MinPerfectWindow = 0.015f;
        public float GoodWindow = 0.18f;
        // Zero at top: near max speed only Perfect-or-Miss exists (approved feel).
        public float MinGoodWindow = 0f;
        public float LateBuffer = 0.06f;
        // Streak ramp: every straight hit multiplies windows by StreakShrink
        // (floor StreakFloor of base); any Miss resets the streak.
        public float StreakShrink = 0.97f;
        public float StreakFloor = 0.5f;
        // Speed reference for window shrink (PRD §7.2: InverseLerp up to Max tier max).
        public float MaxSpeedRef = 73f;
        public float[] PerfectBoost = { 0f, 4f, 5f, 7f, 10f };
        public float[] GoodBoost = { 0f, 2f, 3f, 4f, 5f };
        public float[] MissPenalty = { 0f, 18f, 28f, 45f, 70f }; // positive magnitudes
        // Gearless climb: runs start slow, hits stack toward this cap.
        public float StartSpeed = 10f;
        public float SpeedCap = 70f;
        // Charge sequences (rings): tap the shown arrows in order.
        public float ChargeTimeout = 8f;
        public float ChargeStepGain = 4f;
        public float ChargeJackpot = 8f;
        // Ring entry slow-motion: world dips to this scale for this long
        // (wall clock), giving hands time to answer the charge steps.
        public float ChargeSlowScale = 0.25f;
        public float ChargeSlowDuration = 2f;

        public float DeltaFor(TimingResult r, FlightTier t)
        {
            int i = Mathf.Clamp((int)t, 0, 4);
            return r == TimingResult.Perfect ? PerfectBoost[i]
                : r == TimingResult.Good ? GoodBoost[i] : -MissPenalty[i];
        }
    }
}
