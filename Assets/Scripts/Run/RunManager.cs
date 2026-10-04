using System.Collections.Generic;
using UnityEngine;

namespace FlyingFishMomentum.Run
{
    // M4 endless run: run-clock difficulty (elapsed / RampSeconds) plus the
    // seeded chunk-type deck. Pure-ish seam like spawner.Tick: no scene needed.
    public class RunManager : MonoBehaviour
    {
        // Stored spawner ref is intentional forward wiring for Tasks 5/7;
        // SetDifficulty lands in Task 4, its per-frame feed in Task 7.
        private TimingPromptSpawner _spawner;
        private Scoring.ScoreSystem _score;
        private FlightStateMachine _sm;
        private DifficultySettings _difficulty;
        private List<ChunkSpec> _deck;
        private float _elapsed;
        private System.Random _rng = new System.Random(0);

        // M4 missions (Task 5): one card per chunk, tracked by polling
        // existing public state only — score Coins, spawner charge, sm
        // locomotion, player z. Kind is keyed by ChunkSpec.ChunkId
        // (Lagoon collect, Gauntlet rings, Storm gates, Sky airtime).
        // EnterChunk stages the spec; the next Tick observes the change,
        // snapshots baselines, and resets progress (same-spec re-entry
        // resets too, via the generation counter — the starter runs
        // Lagoon twice in a row).
        private ChunkSpec _activeSpec;
        private ChunkSpec _pendingSpec;
        private long _entryId;
        private long _enteredId;
        private int _coinsAtEntry;
        private float _entryZ;
        private float _playerZ;
        private int _ringsDone;
        private float _airTime;
        private bool _bonusPaid;
        private bool _wasChargeActive;
        private List<float> _gates = new List<float>();

        public string MissionText => (_pendingSpec ?? _activeSpec)?.MissionText ?? string.Empty;

        public float MissionProgress01
        {
            get
            {
                if (_activeSpec == null || _pendingSpec != null) return 0f;
                return Mathf.Clamp01(MissionRaw() / Mathf.Max(1, _activeSpec.MissionTarget));
            }
        }

        public void EnterChunk(ChunkSpec spec)
        {
            _pendingSpec = spec;
            _entryId++;
        }

        public void SetGates(List<float> gateZs)
        {
            _gates = gateZs ?? new List<float>();
        }

        public void Configure(TimingPromptSpawner spawner, Scoring.ScoreSystem score, DifficultySettings diff, List<ChunkSpec> deck)
        {
            Configure(spawner, score, null, diff, deck);
        }

        public void Configure(TimingPromptSpawner spawner, Scoring.ScoreSystem score, FlightStateMachine sm, DifficultySettings diff, List<ChunkSpec> deck)
        {
            _spawner = spawner;
            _score = score;
            _sm = sm;
            _difficulty = diff;
            _deck = deck;
        }

        public void SetSeed(int seed)
        {
            _elapsed = 0f;
            _rng = new System.Random(seed);
            _activeSpec = null;
            _pendingSpec = null;
            _enteredId = _entryId;
            _ringsDone = 0;
            _airTime = 0f;
            _bonusPaid = false;
        }

        public void Tick(float playerZ, float dt)
        {
            _elapsed += Mathf.Max(0f, dt);
            _playerZ = playerZ;
            if (_enteredId != _entryId)
            {
                _enteredId = _entryId;
                _activeSpec = _pendingSpec;
                _pendingSpec = null;
                _ringsDone = 0;
                _airTime = 0f;
                _bonusPaid = false;
                _coinsAtEntry = _score != null ? _score.Coins : 0;
                _entryZ = playerZ;
                _wasChargeActive = _spawner != null && _spawner.ChargeActive;
            }
            if (_activeSpec == null) return;
            PollMission(Mathf.Max(0f, dt));
            if (!_bonusPaid && MissionRaw() >= Mathf.Max(1, _activeSpec.MissionTarget))
            {
                _bonusPaid = true;
                if (_score != null) _score.AddBonus(_score.MissionBonus);
            }
            _wasChargeActive = _spawner != null && _spawner.ChargeActive;
        }

        public float DifficultyT
        {
            get
            {
                if (_difficulty == null) return 0f;
                if (_difficulty.RampSeconds <= 0f) return 1f;
                return Mathf.Clamp01(_elapsed / _difficulty.RampSeconds);
            }
        }

        public ChunkSpec NextType()
        {
            if (_deck == null || _deck.Count == 0) return null;
            float t = DifficultyT;
            ChunkSpec lagoon = null;
            float total = 0f;
            foreach (var spec in _deck)
            {
                if (spec == null) continue;
                if (spec.ChunkId == "Lagoon") lagoon = spec;
                if (t >= spec.MinDifficulty && t <= spec.MaxDifficulty)
                    total += EffectiveWeight(spec, t);
            }
            if (total <= 0f) return lagoon ?? _deck[0];
            float roll = (float)_rng.NextDouble() * total;
            foreach (var spec in _deck)
            {
                if (spec == null) continue;
                if (t < spec.MinDifficulty || t > spec.MaxDifficulty) continue;
                roll -= EffectiveWeight(spec, t);
                if (roll <= 0f) return spec;
            }
            return lagoon ?? _deck[0];
        }

        private float EffectiveWeight(ChunkSpec spec, float t)
        {
            if (_difficulty == null) return spec.Weight;
            if (spec.ChunkId == "Storm") return Mathf.Lerp(spec.Weight, _difficulty.StormWeightEnd, t);
            if (spec.ChunkId == "Lagoon") return _difficulty.LagoonFloor;
            return spec.Weight;
        }

        private void PollMission(float dt)
        {
            switch (_activeSpec.ChunkId)
            {
                case "Gauntlet":
                    if (_spawner != null
                        && _wasChargeActive
                        && !_spawner.ChargeActive
                        && _spawner.Charge.StepIndex >= _spawner.Charge.StepCount)
                        _ringsDone++;
                    break;
                case "Sky":
                    if (_sm != null && _sm.Locomotion == PlayerLocomotionState.Flying)
                        _airTime += dt;
                    break;
            }
        }

        private float MissionRaw()
        {
            switch (_activeSpec.ChunkId)
            {
                case "Lagoon":
                    return (_score != null ? _score.Coins : 0) - _coinsAtEntry;
                case "Gauntlet":
                    return _ringsDone;
                case "Sky":
                    return _airTime;
                case "Storm":
                    int n = 0;
                    foreach (float g in _gates)
                        if (g > _entryZ && g <= _playerZ) n++;
                    return n;
                default:
                    return 0f;
            }
        }
    }
}
