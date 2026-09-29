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
        float _maxSpeed;
        float _accelRate;

        public void Configure(MomentumSettings settings)
        {
            _settings = settings;
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
            if (CurrentSpeed < effectiveTarget)
                CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, effectiveTarget, _accelRate * dt);
            else
                CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, effectiveTarget, drag * dt);
            CurrentSpeed = Mathf.Clamp(CurrentSpeed, _settings.MinSpeed, _maxSpeed * speedBonus);
        }
    }
}
