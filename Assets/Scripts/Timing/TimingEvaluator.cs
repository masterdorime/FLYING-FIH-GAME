using UnityEngine;

namespace FlyingFishMomentum
{
    // Pure timing judgment (spec S2). Offset 0 = exact hit moment,
    // negative = early. Speed factor is squared (gentle cruise, brutal top);
    // streakCount multiplies windows down to the streak floor. No Unity
    // state: fully unit-testable.
    public static class TimingEvaluator
    {
        public static float PerfectWindowAt(
            float speed, TimingSettings s, float minSpeed, float maxSpeed, int streakCount = 0) =>
            Mathf.Lerp(s.PerfectWindow, s.MinPerfectWindow, SpeedFactor(speed, minSpeed, maxSpeed))
            * StreakMultiplier(streakCount, s);

        public static float GoodWindowAt(
            float speed, TimingSettings s, float minSpeed, float maxSpeed, int streakCount = 0) =>
            Mathf.Lerp(s.GoodWindow, s.MinGoodWindow, SpeedFactor(speed, minSpeed, maxSpeed))
            * StreakMultiplier(streakCount, s);

        static float SpeedFactor(float speed, float minSpeed, float maxSpeed)
        {
            float t = Mathf.InverseLerp(minSpeed, maxSpeed, speed);
            return t * t;
        }

        static float StreakMultiplier(int streakCount, TimingSettings s) =>
            Mathf.Max(s.StreakFloor, Mathf.Pow(s.StreakShrink, Mathf.Max(0, streakCount)));

        public static TimingResult Evaluate(
            float offsetSeconds, float speed, TimingSettings s,
            float minSpeed, float maxSpeed, int streakCount = 0)
        {
            float perfect = PerfectWindowAt(speed, s, minSpeed, maxSpeed, streakCount);
            float good = GoodWindowAt(speed, s, minSpeed, maxSpeed, streakCount);
            float abs = Mathf.Abs(offsetSeconds);
            if (abs <= perfect) return TimingResult.Perfect;
            if (abs <= good) return TimingResult.Good;
            // Grace never exceeds the window it extends: at top speed (good 0)
            // only Perfect-or-Miss exists.
            float grace = Mathf.Min(s.LateBuffer, good);
            if (offsetSeconds > 0f && offsetSeconds <= good + grace) return TimingResult.Good;
            return TimingResult.Miss;
        }
    }
}
