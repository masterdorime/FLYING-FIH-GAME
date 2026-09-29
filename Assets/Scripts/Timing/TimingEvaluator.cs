using UnityEngine;

namespace FlyingFishMomentum
{
    // Pure timing judgment (spec S2). Offset 0 = exact hit moment,
    // negative = early. No Unity state: fully unit-testable.
    public static class TimingEvaluator
    {
        public static float PerfectWindowAt(float speed, TimingSettings s, float minSpeed, float maxSpeed) =>
            Mathf.Lerp(s.PerfectWindow, s.MinPerfectWindow,
                Mathf.InverseLerp(minSpeed, maxSpeed, speed));

        public static float GoodWindowAt(float speed, TimingSettings s, float minSpeed, float maxSpeed) =>
            Mathf.Lerp(s.GoodWindow, s.MinGoodWindow,
                Mathf.InverseLerp(minSpeed, maxSpeed, speed));

        public static TimingResult Evaluate(
            float offsetSeconds, float speed, TimingSettings s, float minSpeed, float maxSpeed)
        {
            float perfect = PerfectWindowAt(speed, s, minSpeed, maxSpeed);
            float good = GoodWindowAt(speed, s, minSpeed, maxSpeed);
            float abs = Mathf.Abs(offsetSeconds);
            if (abs <= perfect) return TimingResult.Perfect;
            if (abs <= good) return TimingResult.Good;
            if (offsetSeconds > 0f && offsetSeconds <= good + s.LateBuffer) return TimingResult.Good;
            return TimingResult.Miss;
        }
    }
}
