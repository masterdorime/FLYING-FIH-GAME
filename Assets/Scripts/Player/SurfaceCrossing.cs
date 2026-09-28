namespace FlyingFishMomentum
{
    // Pure surface-breach rule (spec §2). No Unity dependencies: fully unit-testable.
    // Surface is the y=0 logical threshold. Upward crossings need speed >= threshold;
    // any downward crossing returns to water (it always catches you, PRD §10.2).
    public static class SurfaceCrossing
    {
        public static PlayerLocomotionState Evaluate(
            float prevY, float newY, float speed, float threshold,
            PlayerLocomotionState current)
        {
            bool crossedUp = prevY < 0f && newY >= 0f;
            bool crossedDown = prevY >= 0f && newY < 0f;
            if (crossedUp && speed >= threshold) return PlayerLocomotionState.Flying;
            if (crossedDown) return PlayerLocomotionState.Swimming;
            return current;
        }
    }
}
