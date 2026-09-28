using UnityEngine;

namespace FlyingFishMomentum
{
    // Pure heading math (spec §1). Yaw 0 faces +Z; +yaw turns right (+X).
    // +pitch climbs. Fully unit-testable, no Unity state.
    public static class HeadingMath
    {
        const float MaxPitch = 60f;

        public static (float yaw, float pitch) Step(
            float yaw, float pitch, Vector2 input,
            float turnRate, float pitchScale, float dt)
        {
            yaw += input.x * turnRate * dt;
            pitch += input.y * pitchScale * turnRate * dt;
            pitch = Mathf.Clamp(pitch, -MaxPitch, MaxPitch);
            yaw = Mathf.Repeat(yaw + 180f, 360f) - 180f;
            return (yaw, pitch);
        }

        // Visual facing for a yaw/pitch heading. By construction,
        // Orientation(yaw, pitch) * Vector3.forward == Forward(yaw, pitch),
        // so the model always faces its direction of motion.
        public static Quaternion Orientation(float yawDeg, float pitchDeg)
        {
            return Quaternion.Euler(-pitchDeg, yawDeg, 0f);
        }

        public static Vector3 Forward(float yawDeg, float pitchDeg)
        {
            float yaw = yawDeg * Mathf.Deg2Rad;
            float pitch = pitchDeg * Mathf.Deg2Rad;
            float cp = Mathf.Cos(pitch);
            return new Vector3(Mathf.Sin(yaw) * cp, Mathf.Sin(pitch), Mathf.Cos(yaw) * cp);
        }
    }
}
