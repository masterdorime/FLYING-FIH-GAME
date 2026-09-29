using UnityEngine;

namespace FlyingFishMomentum
{
    // Auto-forward + steer (approved design). Kinematic CharacterController drive:
    // heading from Move input, speed from momentum, gravity as accumulated
    // vertical drift in Fly (neutral buoyancy zeroes it in Swim — water catches
    // you). Applies the active tier profile by subscribing to OnTierChanged, and
    // sets TargetSpeed to the tier max, so SetTier alone drives everything
    // (debug keys in M1, gauge events in M3).
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovementController : MonoBehaviour
    {
        const float SwimPitchScale = 0.5f;
        const float StandardGravity = 9.81f;

        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public Vector3 CurrentVelocity { get; private set; }
        public FlightStateMachine StateMachine => _sm;
        public PlayerMomentumController Momentum => _momentum;
        public PlayerInputActions Input => _input;

        // Serialized so scene/prefab wiring (set via Configure at build time)
        // survives save/load.
        [SerializeField] FlightStateMachine _sm;
        [SerializeField] PlayerMomentumController _momentum;
        [SerializeField] MomentumSettings _settings;
        CharacterController _controller;
        PlayerInputActions _input;

        float _turnRate;
        float _gravityScale;
        float _airControl = 1f;
        float _vertVel;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _input = new PlayerInputActions();
        }

        public void Configure(
            FlightStateMachine stateMachine,
            PlayerMomentumController momentum,
            MomentumSettings settings)
        {
            _sm = stateMachine;
            _momentum = momentum;
            _settings = settings;
        }

        void OnEnable()
        {
            _input.Gameplay.Enable();
            if (_sm != null)
            {
                _sm.OnTierChanged += ApplyProfile;
                // Apply the current profile: tier state is authoritative from
                // Awake-time bootstrap, never from Start() ordering.
                if (_sm.ActiveProfile != null) ApplyProfile(_sm.ActiveTier, _sm.ActiveTier);
            }
        }

        void OnDisable()
        {
            _input.Gameplay.Disable();
            if (_sm != null) _sm.OnTierChanged -= ApplyProfile;
        }

        void Update()
        {
            if (_sm == null || _momentum == null || _settings == null) return;
            TickMove(_input.Gameplay.Move.ReadValue<Vector2>(), Time.deltaTime);
        }

        void ApplyProfile(FlightTier _, FlightTier __)
        {
            var p = _sm.ActiveProfile;
            if (p == null || _momentum == null) return;
            _turnRate = p.TurnRate;
            _gravityScale = p.GravityScale;
            _airControl = p.AirControl;
            _momentum.TargetSpeed = p.MaxSpeed;
        }

        public void TickMove(Vector2 input, float dt)
        {
            bool swimming = StateMachine.Locomotion == PlayerLocomotionState.Swimming;
            float rate = swimming ? _turnRate : _turnRate * _airControl;
            float pitchScale = swimming ? SwimPitchScale : 1f;
            var h = HeadingMath.Step(Yaw, Pitch, input, rate, pitchScale, dt);
            Yaw = h.yaw;
            Pitch = h.pitch;
            transform.rotation = HeadingMath.Orientation(Yaw, Pitch);

            float drag = swimming ? _settings.DragSwimming : _settings.DragFlying;
            float bonus = swimming ? 1f : _settings.FlySpeedBonus;
            Momentum.Tick(dt, drag, Mathf.Abs(Yaw), bonus);

            if (swimming) _vertVel = 0f;
            else _vertVel -= StandardGravity * _gravityScale * dt;

            // Forward speed is the authority for FOV/thresholds; vertical drift
            // is a separate channel (dive steepens descent, climb fights gravity).
            Vector3 vel = HeadingMath.Forward(Yaw, Pitch) * Momentum.CurrentSpeed;
            vel.y += _vertVel;
            float prevY = transform.position.y;
            _controller.Move(vel * dt);
            CurrentVelocity = vel;
            StateMachine.EvaluateSurface(prevY, transform.position.y);
        }
    }
}
