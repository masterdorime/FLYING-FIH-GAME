using UnityEngine;

namespace FlyingFishMomentum
{
    // Kinematic speed state. No Update of its own: PlayerMovementController
    // drives Tick once per frame, so there is exactly one update point.
    // ApplyTimingResult is M2 scope and does not exist here.
    public class PlayerMomentumController : MonoBehaviour
    {
        public float CurrentSpeed { get; set; }
        public float TargetSpeed { get; set; }
        public float CurrentMaxSpeed => _maxSpeed;

        // Serialized so scene/prefab wiring (set via Configure at build time) survives save/load.
        [SerializeField] MomentumSettings _settings;
        [SerializeField] TimingSettings _timing;
        float _maxSpeed;
        float _accelRate;

        public void Configure(MomentumSettings settings)
        {
            _settings = settings;
        }

        public void Configure(MomentumSettings settings, TimingSettings timing)
        {
            _settings = settings;
            _timing = timing;
        }

        public void SetLimits(float maxSpeed, float accelRate)
        {
            _maxSpeed = maxSpeed;
            _accelRate = accelRate;
            CurrentSpeed = Mathf.Min(CurrentSpeed, _maxSpeed);
        }

        public void AddSpeed(float amount)
        {
            CurrentSpeed = Mathf.Min(CurrentSpeed + amount, _maxSpeed);
        }

        // M2 timing consequences (PRD §6.3). Hits ratchet the cruise target
        // itself so gains stick (drag sags toward earned speed, never below
        // it); Miss drops target to the MinSpeed floor. Perfect/Good bursts
        // may overflow the tier max; the ceiling is whichever is higher:
        // tier max + boost or the gearless-climb SpeedCap (tiers stay
        // authoritative upward, so M3 gauge tiers keep working above the cap).
        public void ApplyTimingResult(TimingResult result, FlightTier tier)
        {
            if (_timing == null) return;
            float delta = _timing.DeltaFor(result, tier);
            TargetSpeed = Mathf.Clamp(TargetSpeed + delta, _settings.MinSpeed, _timing.SpeedCap);
            float ceiling = _maxSpeed + Mathf.Max(0f, delta);
            ceiling = Mathf.Max(ceiling, _timing.SpeedCap);
            CurrentSpeed = Mathf.Min(CurrentSpeed + delta, ceiling);
        }

        public void Tick(float dt, float dragRate, float deviationDeg = 0f, float speedBonus = 1f)
        {
            // Forward pressure (§34.1 change): off-forward headings bleed
            // speed. Deviation 0 behaves exactly as before. The drag
            // multiplier alone never bites at steady state (Current == Target
            // == max moves zero units), so deviation also caps the chased
            // target — holding a turn costs top speed, re-aiming recovers it.
            float devFrac = Mathf.Clamp01(Mathf.Abs(deviationDeg) / 180f);
            float drag = dragRate * (1f + _settings.DeviationDragGain * devFrac);
            float effectiveTarget = TargetSpeed * speedBonus * (1f - _settings.DeviationSpeedPenalty * devFrac);
            // The hard cap holds even with the fly bonus: air is faster
            // through lower drag, not by breaking the cap.
            if (_timing != null) effectiveTarget = Mathf.Min(effectiveTarget, _timing.SpeedCap);
            if (CurrentSpeed < effectiveTarget)
                CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, effectiveTarget, _accelRate * dt);
            else
                CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, effectiveTarget, drag * dt);
            // Upper clamp keeps applied bursts (timing overflow above tier max);
            // the chase above always moves down toward the target, so nothing
            // can run away upward through Tick. SetLimits still caps on tier change.
            CurrentSpeed = Mathf.Clamp(CurrentSpeed, _settings.MinSpeed,
                Mathf.Max(_maxSpeed * speedBonus, CurrentSpeed));
        }
    }
}
