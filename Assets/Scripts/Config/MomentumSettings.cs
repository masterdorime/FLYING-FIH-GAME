using UnityEngine;

namespace FlyingFishMomentum
{
    [CreateAssetMenu(fileName = "MomentumSettings", menuName = "FlyingFish/Momentum Settings")]
    public class MomentumSettings : ScriptableObject
    {
        public float MinSpeed = 5f;
        public float DragSwimming = 1.5f;
        public float DragFlying = 0.6f;
        public float BreachSpeedThreshold = 20f;
        // Ship rules: swimming locks this depth near the surface.
        public float SwimDepthY = -1.5f;
        // Gauge-full launch: upward kick in u/s (sky-high arc, then glide out).
        public float LaunchVelocity = 40f;
        // Haste-like forward pressure: drag scales with angle off forward
        // (0 = due +Z). Gain 3 means full-backward flight drags ~4x normal.
        public float DeviationDragGain = 3f;
        // Off-forward flight also caps the chased target: holding the cone
        // edge costs (penalty * |yaw|/180) of top speed. 0.5 = -17% at 60°.
        public float DeviationSpeedPenalty = 0.5f;
        // Flying sustains a higher top speed than swimming at the same tier
        // (fast-and-swoopy air, grippy water). 1.2 = +20% chased target in Fly.
        public float FlySpeedBonus = 1.2f;
        // v2 lift model: lift scales with speed squared against the tier top
        // (soar speed) instead of the old linear term it replaces.
        // Base sink keeps every flight ending; sink damping caps the
        // terminal fall so cruise descends gently instead of accelerating.
        public float GlideBaseSink = 2f;
        public float GlideSinkDamp = 0.5f;
        // Slope exchange: downhill (sin<0) raises the chased target toward
        // the cap, uphill lowers it — the existing chase does the work,
        // same pattern as the deviation penalty. 0.5 = ±35% at 45°.
        public float GlideSlopeTarget = 0.5f;
        // Uphill drag multiplier per sin so climbs observably bleed.
        public float GlideSlopeDrag = 3f;
        // Firework rockets (elytra engine): earned per completed ring,
        // spent by pressing in open air. Boost along the nose + climb pop.
        public int RocketMax = 3;
        public float RocketBoost = 15f;
        public float RocketPop = 8f;
        // Bank-to-turn (visual only): fish rolls into steered turns.
        public float BankAngle = 25f;
    }
}
