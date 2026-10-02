using UnityEngine;

namespace FlyingFishMomentum
{
    // Pure camera math (spec §1). Fully unit-testable, no Unity state.
    public static class CameraMath
    {
        const float MaxRoll = 8f;

        public static float TargetFov(float speed, float minSpeed, float maxSpeed, float baseFov, float tierFov)
        {
            float t = Mathf.Clamp01(Mathf.InverseLerp(minSpeed, maxSpeed, speed));
            return Mathf.Lerp(baseFov, tierFov, t);
        }

        public static float RollTarget(float yawRateDegSec, float turnRate)
        {
            if (turnRate <= 0f) return 0f;
            return Mathf.Clamp(-yawRateDegSec / turnRate * MaxRoll, -MaxRoll, MaxRoll);
        }

        public static float ClampFov(float fov, float maxFov)
        {
            return Mathf.Min(fov, maxFov);
        }

        // Final frame FOV: display FOV plus the tier-up kick addend, capped
        // at the max — the kick must never stack past it.
        public static float FinalFov(float displayFov, float kickAdd, float maxFov)
        {
            return Mathf.Min(displayFov + kickAdd, maxFov);
        }

        public static float Decay(float envelope, float rate, float dt)
        {
            return Mathf.Max(0f, envelope - rate * dt);
        }
    }
}
