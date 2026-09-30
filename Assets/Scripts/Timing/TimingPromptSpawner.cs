using System.Collections.Generic;
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
        [SerializeField] List<ChargeRing> _rings = new List<ChargeRing>();
        Vector3 _lastPlayerPos;
        bool _hasLastPos;
        float _meters;
        float _nextBeat = -1f;
        float _lastTickNow = float.MinValue;
        System.Random _rngBeat;
        System.Random _rngCharge;
        bool _seeded;
        public int StreakCount { get; private set; }
        public float HitAngleDeg { get; private set; }
        public readonly ChargeSequencer Charge = new ChargeSequencer();
        public bool ChargeActive => Charge.IsActive;
        public ChargeStepKind[] ChargeOrder { get; private set; } = new ChargeStepKind[0];
        public string ChargeText => Charge.IsActive && ChargeOrder.Length > 0
            ? $"CHARGE {Charge.StepIndex + 1}/{Charge.StepCount} {Charge.CurrentKind}"
            : string.Empty;
        public IReadOnlyList<ChargeRing> Rings => _rings;

        public void SetSeed(int seed)
        {
            // Separate streams so charge draws never perturb the beat schedule.
            _rngBeat = new System.Random(seed);
            _rngCharge = new System.Random(seed);
            _seeded = true;
            DrawBeat();
        }

        public void SetRings(List<ChargeRing> rings)
        {
            _rings = rings ?? new List<ChargeRing>();
        }

        public void CheckRingTrigger(float now, Vector3 playerPos)
        {
            if (Charge.IsActive || _rings == null) return;
            if (_rngCharge == null) _rngCharge = new System.Random(0);
            // Swept check: a hitch can move the fish clean through the 4m
            // window between calls, so test the segment, not just endpoints.
            Vector3 prev = _hasLastPos ? _lastPlayerPos : playerPos;
            _lastPlayerPos = playerPos;
            _hasLastPos = true;
            foreach (var ring in _rings)
            {
                if (ring == null || ring.Consumed) continue;
                if (SegmentPassesDisc(prev, playerPos, ring.transform.position, 3f))
                {
                    ring.Consume();
                    // Never overlap: an open beat prompt closes silently
                    // (no judgment, no penalty) — the ring takes over.
                    Active = new ActivePrompt();
                    Progress01 = 0f;
                    _meters = 0f;
                    var order = new ChargeStepKind[3];
                    for (int i = 0; i < order.Length; i++)
                        order[i] = _rngCharge.Next(0, 2) == 0 ? ChargeStepKind.Hold : ChargeStepKind.Tap;
                    ChargeOrder = order;
                    Charge.Begin(order, now);
                    return;
                }
            }
        }

        static bool SegmentPassesDisc(Vector3 a, Vector3 b, Vector3 center, float radius)
        {
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(center - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
            if ((a + ab * t - center).magnitude > radius) return false;
            float za = a.z - center.z;
            float zb = b.z - center.z;
            if (Mathf.Abs(za) < 2f || Mathf.Abs(zb) < 2f) return true;
            return za * zb < 0f;
        }

        void Start()
        {
            // Fresh shuffle every run (unpredictable beats); tests lock a seed.
            if (!_seeded) SetSeed(System.Environment.TickCount);
        }

        void DrawBeat()
        {
            if (_rngBeat == null) _rngBeat = new System.Random(0);
            if (_timing == null) { _nextBeat = 60f; return; }
            _nextBeat = Mathf.Lerp(_timing.BeatMinMeters, _timing.BeatMaxMeters,
                (float)_rngBeat.NextDouble());
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
            bool pressed = false;
            bool held = false;
            if (_movement != null && _movement.Input != null)
            {
                pressed = _movement.Input.Gameplay.TimingAction.WasPressedThisFrame();
                held = _movement.Input.Gameplay.TimingAction.IsPressed();
                CheckRingTrigger(Time.time, _movement.transform.position);
            }
            Tick(Time.time, Time.deltaTime,
                _momentum.CurrentSpeed,
                _sm != null ? _sm.ActiveTier : FlightTier.None,
                pressed, held);
        }

        public void Tick(float now, float dt, float speed, FlightTier tier, bool pressed, bool held = false)
        {
            if (_momentum == null || _timing == null || _momSettings == null) return;
            // Frozen or rewound clock (pause) carries no input: a press must
            // advance time to count. Recorded before any early return below.
            if (now <= _lastTickNow) { pressed = false; held = false; }
            _lastTickNow = now;
            // Charge mode owns the button: beats suspend, presses route here.
            if (Charge.IsActive)
            {
                float gain = Charge.Tick(now, dt, held, pressed, speed,
                    _timing, _momSettings.MinSpeed, _timing.MaxSpeedRef, StreakCount);
                if (gain > 0f) _momentum.AddChargeGain(gain);
                if (!Charge.IsActive) _meters = 0f; // fresh beat gap after charge
                return;
            }
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
                if (_rngBeat == null) _rngBeat = new System.Random(0);
                HitAngleDeg = (float)(_rngBeat.NextDouble() * 360.0);
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
