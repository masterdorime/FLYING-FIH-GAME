using UnityEngine;

namespace FlyingFishMomentum
{
    // M3 flight gauge (spec S1-S2). Owns CurrentGauge 0..MaxGauge and
    // reconciles ActiveTier via settings.TierFor after every change.
    // AddFill/DrainMiss reconcile immediately (controller ruling) so
    // manually-driven tests observe tier changes without gauge ticks.
    public class FlightGaugeSystem : MonoBehaviour
    {
        public float CurrentGauge { get; private set; }
        public float MaxGauge => _settings != null ? _settings.MaxGauge : 0f;
        public FlightStateMachine StateMachine => _sm;

        [SerializeField] FlightStateMachine _sm;
        [SerializeField] FlightGaugeSettings _settings;
        [SerializeField] CameraSpeedReactor _camera;
        [SerializeField] PlayerMovementController _movement;

        public void Configure(FlightStateMachine sm, FlightGaugeSettings settings, CameraSpeedReactor camera)
        {
            _sm = sm;
            _settings = settings;
            _camera = camera;
        }

        public void Configure(
            FlightStateMachine sm,
            FlightGaugeSettings settings,
            CameraSpeedReactor camera,
            PlayerMovementController movement)
        {
            Configure(sm, settings, camera);
            _movement = movement;
        }

        void Start()
        {
            if (_settings != null) CurrentGauge = _settings.StartGauge;
        }

        public void AddFill(float amount)
        {
            if (_settings == null) return;
            bool wasMax = CurrentGauge >= _settings.MaxGauge;
            CurrentGauge = Mathf.Clamp(CurrentGauge + amount, 0f, _settings.MaxGauge);
            // Rising edge to full while swimming: launch sky-high, empty the
            // tank. Sitting full (or flying) fires nothing — dip and refill.
            if (!wasMax && CurrentGauge >= _settings.MaxGauge
                && _movement != null && _sm != null
                && _sm.Locomotion == PlayerLocomotionState.Swimming)
            {
                _movement.Launch();
                CurrentGauge = 0f;
            }
            Reconcile();
        }

        public void DrainMiss()
        {
            if (_settings == null) return;
            CurrentGauge = Mathf.Max(CurrentGauge - _settings.MissDrain, 0f);
            Reconcile();
        }

        public void Tick(float dt, bool flying)
        {
            if (_settings == null) return;
            if (flying)
                CurrentGauge = Mathf.Max(CurrentGauge - _settings.FlyDrainPerSecond * dt, 0f);
            Reconcile();
        }

        void Reconcile()
        {
            if (_sm == null || _settings == null) return;
            var next = _settings.TierFor(CurrentGauge);
            var current = _sm.ActiveTier;
            if (next == current) return;
            _sm.SetTier(next);
            if (next > current) _camera?.PlayTierUpKick();
            else _camera?.PlayMissShake();
        }

        void Update()
        {
            if (_sm == null || _settings == null) return;
            Tick(Time.deltaTime, _sm.Locomotion == PlayerLocomotionState.Flying);
        }
    }
}
