using UnityEngine;

namespace FlyingFishMomentum
{
    // M2 prompt scheduler (spec S1-S2). Distance beat opens prompts, press or
    // expiry resolves them through the pure evaluator, consequences apply via
    // momentum. Tick is the test seam (explicit clock); Update feeds it live.
    public class TimingPromptSpawner : MonoBehaviour
    {
        public struct ActivePrompt
        {
            public bool Open;
            public float TargetTime;
        }

        public ActivePrompt Active { get; private set; }
        public TimingResult LastResult { get; private set; }
        public bool HasResolved { get; private set; }
        public TimingSettings Settings => _timing;
        public MomentumSettings MomSettings => _momSettings;
        public float Progress01 { get; private set; }

        [SerializeField] PlayerMovementController _movement;
        [SerializeField] PlayerMomentumController _momentum;
        [SerializeField] FlightStateMachine _sm;
        [SerializeField] TimingSettings _timing;
        [SerializeField] MomentumSettings _momSettings;
        [SerializeField] CameraSpeedReactor _camera;
        float _meters;

        public void Configure(
            PlayerMovementController movement,
            PlayerMomentumController momentum,
            FlightStateMachine sm,
            TimingSettings timing,
            MomentumSettings momSettings,
            CameraSpeedReactor camera)
        {
            _movement = movement;
            _momentum = momentum;
            _sm = sm;
            _timing = timing;
            _momSettings = momSettings;
            _camera = camera;
        }

        void Update()
        {
            if (_momentum == null || _timing == null || _momSettings == null) return;
            bool pressed = _movement != null && _movement.Input != null &&
                _movement.Input.Gameplay.TimingAction.WasPressedThisFrame();
            Tick(Time.time, Time.deltaTime,
                _momentum.CurrentSpeed,
                _sm != null ? _sm.ActiveTier : FlightTier.None,
                pressed);
        }

        public void Tick(float now, float dt, float speed, FlightTier tier, bool pressed)
        {
            if (_momentum == null || _timing == null || _momSettings == null) return;
            if (Active.Open)
            {
                Progress01 = TimingDialMath.Progress01(
                    Active.TargetTime, now, _timing.LeadTime);
                float good = TimingEvaluator.GoodWindowAt(
                    speed, _timing, _momSettings.MinSpeed, _timing.MaxSpeedRef);
                if (pressed)
                    Resolve(TimingEvaluator.Evaluate(now - Active.TargetTime,
                        speed, _timing, _momSettings.MinSpeed, _timing.MaxSpeedRef), tier);
                else if (now > Active.TargetTime + good + _timing.LateBuffer)
                    Resolve(TimingResult.Miss, tier);
                return;
            }
            _meters += speed * dt;
            if (_meters >= _timing.PromptEveryMeters)
            {
                _meters = 0f;
                Active = new ActivePrompt { Open = true, TargetTime = now + _timing.LeadTime };
            }
        }

        void Resolve(TimingResult result, FlightTier tier)
        {
            _momentum.ApplyTimingResult(result, tier);
            if (result == TimingResult.Perfect && _camera != null) _camera.PlayTierUpKick();
            if (result == TimingResult.Miss && _camera != null) _camera.PlayMissShake();
            LastResult = result;
            HasResolved = true;
            Active = new ActivePrompt();
            Progress01 = 0f;
            _meters = 0f;
        }
    }
}
