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

        MomentumSettings _settings;
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

        public void Tick(float dt, float dragRate)
        {
            if (CurrentSpeed < TargetSpeed)
                CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, TargetSpeed, _accelRate * dt);
            else
                CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, TargetSpeed, dragRate * dt);
            CurrentSpeed = Mathf.Clamp(CurrentSpeed, _settings.MinSpeed, _maxSpeed);
        }
    }
}
