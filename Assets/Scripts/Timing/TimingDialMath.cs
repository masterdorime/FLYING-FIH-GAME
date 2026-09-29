using UnityEngine;

namespace FlyingFishMomentum
{
    // Pure dial geometry for the Outlast-style prompt meter (spec M2 change
    // record). Angles in degrees; needle rotation.z, 0 = straight up.
    public static class TimingDialMath
    {
        public const float SweepDegrees = 360f;

        // Full clockwise sweep ending exactly on the hit angle. Unclamped:
        // past the hit moment the needle keeps drifting (sweep then drift).
        public static float NeedleAngle(float progress01, float hitDeg = 0f) =>
            hitDeg - SweepDegrees + SweepDegrees * progress01;

        public static float HalfWidthDeg(float windowSeconds, float leadSeconds) =>
            SweepDegrees * windowSeconds / Mathf.Max(leadSeconds, 0.001f);

        public static float Progress01(float targetTime, float now, float lead) =>
            1f - (targetTime - now) / Mathf.Max(lead, 0.001f);
    }
}
