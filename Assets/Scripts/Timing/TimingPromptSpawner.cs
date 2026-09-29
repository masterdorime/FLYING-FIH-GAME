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
        float _nextBeat = -1f;
        System.Random _rng;
        bool _seeded;
        public int StreakCount { get; private set; }
        public float HitAngleDeg { get; private set; }

        public void SetSeed(int seed)
        {
            _rng = new System.Random(seed);
            _seeded = true;
            DrawBeat();
        }

        void Start()
        {
            // Fresh shuffle every run (unpredictable beats); tests lock a seed.
            if (!_seeded) SetSeed(System.Environment.TickCount);
        }

        void DrawBeat()
        {
            if (_rng == null) _rng = new System.Random(0);
            if (_timing == null) { _nextBeat = 60f; return; }
            _nextBeat = Mathf.Lerp(_timing.BeatMinMeters, _timing.BeatMaxMeters,
                (float)_rng.NextDouble());
        }

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
            if (_nextBeat <= 0f) DrawBeat();
            if (Active.Open)
            {
                Progress01 = TimingDialMath.Progress01(
                    Active.TargetTime, now, _timing.LeadTime);
                float good = TimingEvaluator.GoodWindowAt(
                    speed, _timing, _momSettings.MinSpeed, _timing.MaxSpeedRef, StreakCount);
                if (pressed)
                    Resolve(TimingEvaluator.Evaluate(now - Active.TargetTime,
                        speed, _timing, _momSettings.MinSpeed, _timing.MaxSpeedRef, StreakCount), tier);
                else if (now > Active.TargetTime + good + Mathf.Min(_timing.LateBuffer, good))
                    Resolve(TimingResult.Miss, tier);
                return;
            }
            _meters += speed * dt;
            if (_meters >= _nextBeat)
            {
                _meters = 0f;
                if (_rng == null) _rng = new System.Random(0);
                HitAngleDeg = (float)(_rng.NextDouble() * 360.0);
                Active = new ActivePrompt { Open = true, TargetTime = now + _timing.LeadTime };
            }
        }

        void Resolve(TimingResult result, FlightTier tier)
        {
            _momentum.ApplyTimingResult(result, tier);
            if (result == TimingResult.Miss) StreakCount = 0;
            else StreakCount++;
            if (result == TimingResult.Perfect && _camera != null) _camera.PlayTierUpKick();
            if (result == TimingResult.Miss && _camera != null) _camera.PlayMissShake();
            LastResult = result;
            HasResolved = true;
            Active = new ActivePrompt();
            Progress01 = 0f;
            _meters = 0f;
            DrawBeat();
        }
    }
}
