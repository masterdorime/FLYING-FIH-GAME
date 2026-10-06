using System.Collections.Generic;
using UnityEngine;
using FlyingFishMomentum.Scoring;

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

        // M4 difficulty view: runtime clone of the timing asset. Judging
        // reads _view; Settings keeps returning the pristine original.
        // t=0 reproduces legacy values bit-identically (scale factor 1).
        TimingSettings _view;
        public TimingSettings EffectiveSettings => _view;

        public void SetDifficultyFactors(float gapShrink, float windowTighten)
        {
            _gapShrink = gapShrink;
            _windowTighten = windowTighten;
        }

        float _gapShrink = 0.5f;
        float _windowTighten = 0.3f;

        public void SetDifficulty(float t)
        {
            t = Mathf.Clamp01(t);
            if (_timing == null || _view == null) return;
            _view.BeatMinMeters = Mathf.Max(0.01f, _timing.BeatMinMeters * (1f - _gapShrink * t));
            _view.BeatMaxMeters = Mathf.Max(0.01f, _timing.BeatMaxMeters * (1f - _gapShrink * t));
            _view.PerfectWindow = Mathf.Max(0.01f, _timing.PerfectWindow * (1f - _windowTighten * t));
            _view.GoodWindow = Mathf.Max(0.01f, _timing.GoodWindow * (1f - _windowTighten * t));
        }

        [SerializeField] PlayerMovementController _movement;
        [SerializeField] PlayerMomentumController _momentum;
        [SerializeField] FlightStateMachine _sm;
        [SerializeField] TimingSettings _timing;
        [SerializeField] MomentumSettings _momSettings;
        [SerializeField] CameraSpeedReactor _camera;
        [SerializeField] FlightGaugeSystem _gauge;
        [SerializeField] List<ChargeRing> _rings = new List<ChargeRing>();
        [SerializeField] List<CoinPickup> _coins = new List<CoinPickup>();
        [SerializeField] ScoreSystem _score;
        Vector3 _lastCoinPos;
        bool _hasCoinPos;
        Vector2 _lastMoveVec;
        Vector3 _lastPlayerPos;
        bool _hasLastPos;
        float _meters;
        float _nextBeat = -1f;
        float _lastTickNow = float.MinValue;
        System.Random _rngBeat;
        System.Random _rngCharge;
        System.Random _rngLayout;
        bool _seeded;
        public int StreakCount { get; private set; }
        public float HitAngleDeg { get; private set; }
        public readonly ChargeSequencer Charge = new ChargeSequencer();
        public bool ChargeActive => Charge.IsActive;
        // Firework rockets (flight v2 engine): +1 per completed ring
        // (capped), spent by pressing in open air for a burst + climb pop.
        public int RocketCount { get; private set; }
        public int RocketMax => _momSettings != null ? _momSettings.RocketMax : 3;
        public ChargeArrow[] ChargeOrder { get; private set; } = new ChargeArrow[0];
        public string ChargeText
        {
            get
            {
                if (!Charge.IsActive || ChargeOrder.Length == 0) return string.Empty;
                var parts = new System.Text.StringBuilder("CHARGE ");
                parts.Append(Charge.StepIndex + 1).Append('/').Append(Charge.StepCount).Append(' ');
                for (int i = 0; i < ChargeOrder.Length; i++)
                {
                    string g = ChargeSequencer.Glyph(ChargeOrder[i]);
                    if (i == Charge.StepIndex) parts.Append('[').Append(g).Append(']');
                    else parts.Append(g);
                }
                return parts.ToString();
            }
        }
        public IReadOnlyList<ChargeRing> Rings => _rings;
        public float SlowTimer { get; private set; }

        // Gearless world: taps always judge on the Medium row, no matter the
        // active tier. (The table's None row is all zeros, which froze speed
        // at spawn — reported bug.) M3 gauge tiers may re-enable per-tier
        // rows; changing this back must update BeatRowIsFlatMediumAtAnyTier.
        public static FlightTier BeatRowFor(FlightTier activeTier) => FlightTier.Medium;

        public void SetSeed(int seed)
        {
            // Separate streams so charge draws never perturb the beat schedule.
            _rngBeat = new System.Random(seed);
            _rngCharge = new System.Random(seed);
            _rngLayout = new System.Random(seed);
            _seeded = true;
            DrawBeat();
            ShuffleRingLayout();
        }

        // Testing layout: every run re-rolls each ring's x lane from the
        // seed (live runs seed by wall clock, so every playthrough
        // differs). All rings sit on the swim lane — fly-depth rings need
        // a full gauge to reach, which made the first launch uncatchable.
        // Z pacing is fixed so timing rhythm stays stable. Bounds are
        // test-scaffold consts until M4 owns chunk layout (see M2 spec).
        const float LayoutLaneX = 20f;
        const float LayoutSwimY = -3f;

        void ShuffleRingLayout()
        {
            if (_rings == null || _rngLayout == null) return;
            foreach (var ring in _rings)
            {
                if (ring == null) continue;
                var p = ring.transform.position;
                p.x = Mathf.Lerp(-LayoutLaneX, LayoutLaneX, (float)_rngLayout.NextDouble());
                p.y = LayoutSwimY;
                ring.transform.position = p;
            }
        }

        public void SetRings(List<ChargeRing> rings)
        {
            _rings = rings ?? new List<ChargeRing>();
        }

        public void SetScoreSystem(ScoreSystem score)
        {
            _score = score;
        }

        public IReadOnlyList<CoinPickup> Coins => _coins;

        public void SetCoins(List<CoinPickup> coins)
        {
            _coins = coins ?? new List<CoinPickup>();
        }

        // Coin trails: same swept-segment pattern as rings (generous 2m
        // window for feel), own position history so ring/coin cadences
        // never share a stale segment.
        public void CheckCoinPickup(float now, Vector3 playerPos)
        {
            if (_coins == null) return;
            Vector3 prev = _hasCoinPos ? _lastCoinPos : playerPos;
            _lastCoinPos = playerPos;
            _hasCoinPos = true;
            foreach (var coin in _coins)
            {
                if (coin == null || coin.Collected) continue;
                if (SegmentPassesDisc(prev, playerPos, coin.transform.position, 2f))
                {
                    coin.Collect();
                    if (_score != null) _score.AddCoins(1);
                }
            }
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
                    // Slow-mo beat to answer the charge: pause always wins.
                    if (_view != null && Time.timeScale != 0f)
                    {
                        Time.timeScale = _view.ChargeSlowScale;
                        SlowTimer = _view.ChargeSlowDuration;
                    }
                    var order = new ChargeArrow[4];
                    for (int i = 0; i < order.Length; i++)
                        order[i] = (ChargeArrow)_rngCharge.Next(0, 4);
                    ChargeOrder = order;
                    Charge.Begin(order, now);
                    if (_movement != null) _movement.SetChargeHold(true);
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

        void Awake()
        {
            // Build-time Configure fills serialized fields, but _view is
            // not serialized: clone at load so live judging never sees
            // null. (Configure re-clones when given a new asset.)
            if (_view == null && _timing != null)
                _view = Object.Instantiate(_timing);
        }

        void Start()
        {
            // Fresh shuffle every run (unpredictable beats); tests lock a seed.
            if (!_seeded) SetSeed(System.Environment.TickCount);
        }

        void DrawBeat()
        {
            if (_rngBeat == null) _rngBeat = new System.Random(0);
            if (_view == null) { _nextBeat = 60f; return; }
            _nextBeat = Mathf.Lerp(_view.BeatMinMeters, _view.BeatMaxMeters,
                (float)_rngBeat.NextDouble());
        }

        public void Configure(
            PlayerMovementController movement,
            PlayerMomentumController momentum,
            FlightStateMachine sm,
            TimingSettings timing,
            MomentumSettings momSettings,
            CameraSpeedReactor camera,
            FlightGaugeSystem gauge)
        {
            _movement = movement;
            _momentum = momentum;
            _sm = sm;
            _timing = timing;
            // Clone the asset, never mutate it: judging reads _view.
            _view = timing != null ? Object.Instantiate(timing) : null;
            _momSettings = momSettings;
            _camera = camera;
            _gauge = gauge;
        }

        void Update()
        {
            if (_momentum == null || _view == null || _momSettings == null) return;
            UpdateSlowMo(Time.unscaledDeltaTime);
            bool pressed = false;
            bool held = false;
            ChargeArrow? arrow = null;
            if (_movement != null && _movement.Input != null)
            {
                pressed = _movement.Input.Gameplay.TimingAction.WasPressedThisFrame();
                held = _movement.Input.Gameplay.TimingAction.IsPressed();
                Vector2 moveVec = _movement.Input.Gameplay.Move.ReadValue<Vector2>();
                arrow = ChargeSequencer.ArrowFromStick(_lastMoveVec, moveVec);
                _lastMoveVec = moveVec;
                CheckRingTrigger(Time.time, _movement.transform.position);
                CheckCoinPickup(Time.time, _movement.transform.position);
            }
            Tick(Time.time, Time.deltaTime,
                _momentum.CurrentSpeed,
                _sm != null ? _sm.ActiveTier : FlightTier.None,
                pressed, held, arrow);
        }

        // Slow-mo runs on the unpausable clock; an expired timer restores
        // full speed, but never overrides an active pause (scale 0).
        public void UpdateSlowMo(float unscaledDt)
        {
            if (SlowTimer <= 0f || _view == null) return;
            SlowTimer -= unscaledDt;
            if (Time.timeScale != 0f) Time.timeScale = _view.ChargeSlowScale;
            if (SlowTimer <= 0f && Time.timeScale == _view.ChargeSlowScale)
                Time.timeScale = 1f;
        }

        public void Tick(float now, float dt, float speed, FlightTier tier, bool pressed, bool held = false, ChargeArrow? arrow = null)
        {
            if (_momentum == null || _view == null || _momSettings == null) return;
            // Frozen or rewound clock (pause) carries no input: a press must
            // advance time to count. Recorded before any early return below.
            if (now <= _lastTickNow) { pressed = false; held = false; arrow = null; }
            _lastTickNow = now;
            // Charge mode owns the button: beats suspend, arrows route here.
            if (Charge.IsActive)
            {
                float gain = Charge.Tick(now, arrow, _view);
                if (gain > 0f) _momentum.AddChargeGain(gain);
                if (gain > 0f && _gauge != null) _gauge.AddFill(gain);
                if (!Charge.IsActive)
                {
                    _meters = 0f; // fresh beat gap after charge
                    // Surviving all steps (clean or not) earns a firework.
                    // Timeouts abort early (StepIndex short) and earn nothing.
                    if (Charge.StepIndex >= Charge.StepCount) EarnRocket();
                    if (_movement != null) _movement.SetChargeHold(false);
                }
                return;
            }
            if (_nextBeat <= 0f) DrawBeat();
            if (Active.Open)
            {
                Progress01 = TimingDialMath.Progress01(
                    Active.TargetTime, now, _view.LeadTime);
                float good = TimingEvaluator.GoodWindowAt(
                    speed, _view, _momSettings.MinSpeed, _view.MaxSpeedRef, StreakCount);
                if (pressed)
                    Resolve(TimingEvaluator.Evaluate(now - Active.TargetTime,
                        speed, _view, _momSettings.MinSpeed, _view.MaxSpeedRef, StreakCount), BeatRowFor(tier));
                else if (now > Active.TargetTime + good + Mathf.Min(_view.LateBuffer, good))
                    Resolve(TimingResult.Miss, BeatRowFor(tier));
                return;
            }
            _meters += speed * dt;
            if (_score != null) _score.AddDistance(speed * dt);
            if (_meters >= _nextBeat)
            {
                _meters = 0f;
                if (_rngBeat == null) _rngBeat = new System.Random(0);
                HitAngleDeg = (float)(_rngBeat.NextDouble() * 360.0);
                Active = new ActivePrompt { Open = true, TargetTime = now + _view.LeadTime };
            }
            // Firework rocket: open air + fresh press spends one for a burst
            // along the nose plus a climb pop. Beats and charge own the
            // button first (above); swimming ignores presses as before.
            if (pressed && RocketCount > 0 && _sm != null
                && _sm.Locomotion == PlayerLocomotionState.Flying)
                FireRocket();
        }

        void EarnRocket()
        {
            if (_momSettings == null) return;
            RocketCount = Mathf.Min(RocketCount + 1, _momSettings.RocketMax);
        }

        void FireRocket()
        {
            if (_momSettings == null || _momentum == null || RocketCount <= 0) return;
            RocketCount--;
            _momentum.AddSpeed(_momSettings.RocketBoost);
            if (_movement != null) _movement.RocketPop(_momSettings.RocketPop);
        }

        void Resolve(TimingResult result, FlightTier tier)
        {
            _momentum.ApplyTimingResult(result, tier);
            if (_score != null) _score.OnBeat(result);
            // Beat-Miss drains the gauge; charge-internal misses never reach
            // Resolve, and taps never touch the gauge.
            if (result == TimingResult.Miss && _gauge != null) _gauge.DrainMiss();
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
