using UnityEngine;

namespace FlyingFishMomentum
{
    // Auto-forward + steer (approved design). Kinematic CharacterController drive:
    // heading from Move input, speed from momentum, gravity as accumulated
    // vertical drift in Fly. Swim is ship rules: locked surface depth and
    // level pitch, yaw steering only. Applies the active tier profile by
    // subscribing to OnTierChanged, and sets TargetSpeed to the tier max,
    // so SetTier alone drives everything (debug keys in M1, gauge events in M3).
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovementController : MonoBehaviour
    {
        const float StandardGravity = 9.81f;

        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public float Bank { get; private set; }
        public int LaunchCount { get; private set; }
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
        float _bank;

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

        // Gauge-full launch (spec: ship + launch). Caller guarantees the
        // rising-edge context; this just flips state and kicks upward.
        public void Launch()
        {
            LaunchCount++;
            StateMachine.SetLocomotion(PlayerLocomotionState.Flying);
            _vertVel = _settings != null ? _settings.LaunchVelocity : 40f;
        }

        // Firework climb pop (flight v2): spent rockets kick upward.
        public void RocketPop(float amount)
        {
            _vertVel += amount;
        }

        // Thin-air factor 1..0: full lift below ThinAirStartY, nothing at
        // ThinAirFullY and above — altitude falls back to the obstacle
        // band instead of climbing out of the world. Structural band
        // edges (like the pitch cone), not tuning: sky content tops ~100.
        public const float ThinAirStartY = 60f;
        public const float ThinAirFullY = 110f;
        public const float FlightCeilingY = 115f;

        public static float ThinAirFactor(float y)
        {
            return Mathf.Clamp01(1f - (y - ThinAirStartY) / (ThinAirFullY - ThinAirStartY));
        }

        public void TickMove(Vector2 input, float dt)
        {
            bool swimming = StateMachine.Locomotion == PlayerLocomotionState.Swimming;
            var profile = StateMachine.ActiveProfile;
            float rate = swimming ? _turnRate : _turnRate * _airControl;
            // Authority scales with speed in air (mushy when slow, full at
            // cruise — the gentle stall); swim stays crisp.
            if (!swimming && profile != null)
                rate *= 0.3f + 0.7f * Mathf.Clamp01(Momentum.CurrentSpeed / Mathf.Max(profile.MaxSpeed, 0.01f));
            // Ship rules: underwater the fish holds surface depth and level
            // pitch — W/S does nothing, only yaw steers. Air keeps full control.
            float pitchScale = swimming ? 0f : 1f;
            var h = HeadingMath.Step(Yaw, Pitch, input, rate, pitchScale, dt);
            Yaw = h.yaw;
            Pitch = swimming ? 0f : h.pitch;
            // Bank into steered turns (visual only — yaw physics and the
            // wide-turns rule untouched).
            float bankTarget = swimming ? 0f : -input.x * _settings.BankAngle;
            _bank = Mathf.Lerp(_bank, bankTarget, 1f - Mathf.Exp(-dt / 0.15f));
            Bank = _bank;
            transform.rotation = HeadingMath.Orientation(Yaw, Pitch, _bank);

            float drag = swimming ? _settings.DragSwimming : _settings.DragFlying;
            float bonus = swimming ? 1f : _settings.FlySpeedBonus;
            float slope = swimming ? 0f : Mathf.Sin(Pitch * Mathf.Deg2Rad);
            Momentum.Tick(dt, drag, Mathf.Abs(Yaw), bonus, slope);

            if (swimming) _vertVel = 0f;
            // v2 lift: scales with speed squared against the tier top (soar
            // speed). Cruise holds weight minus a base sink (flight always
            // ends); slow flight sinks hard. Sink-only damping caps the
            // terminal fall so cruise descends gently.
            else if (profile != null)
            {
                float soar = Mathf.Max(profile.MaxSpeed, 0.01f);
                float lift = Mathf.Clamp01(Mathf.Pow(Momentum.CurrentSpeed / soar, 2f));
                lift *= ThinAirFactor(transform.position.y);
                _vertVel += (StandardGravity * _gravityScale * (lift - 1f) - _settings.GlideBaseSink) * dt;
                if (_vertVel < 0f) _vertVel -= _vertVel * _settings.GlideSinkDamp * dt;
            }
            else _vertVel -= StandardGravity * _gravityScale * dt;

            // Forward speed is the authority for FOV/thresholds; vertical drift
            // is a separate channel (dive steepens descent, climb fights gravity).
            Vector3 vel = HeadingMath.Forward(Yaw, Pitch) * Momentum.CurrentSpeed;
            vel.y += _vertVel;
            float prevY = transform.position.y;
            _controller.Move(vel * dt);
            if (swimming)
            {
                var p = transform.position;
                p.y = _settings.SwimDepthY;
                transform.position = p;
            }
            else if (transform.position.y > FlightCeilingY)
            {
                // Hard lid over the sky band: pin altitude, kill the climb.
                // Thin air below already softens the approach, so this
                // rarely bites — it guarantees no escape, not the feel.
                var p = transform.position;
                p.y = FlightCeilingY;
                transform.position = p;
                _vertVel = Mathf.Min(_vertVel, 0f);
            }
            CurrentVelocity = vel;
            StateMachine.EvaluateSurface(prevY, transform.position.y);
        }
    }
}
