using System.Collections.Generic;
using UnityEngine;

namespace FlyingFishMomentum.Run
{
    // M4 endless run: run-clock difficulty (elapsed / RampSeconds) plus the
    // seeded chunk-type deck. Pure-ish seam like spawner.Tick: no scene needed.
    public class RunManager : MonoBehaviour
    {
        // Serialized so scene wiring (set via Configure/WireScene at build
        // time) survives save/load — the PlayerMomentumController pattern.
        // Spawner.Configure is NEVER re-called here (Task 4 transient
        // clone leak); only SetRings/SetCoins wholesale per stream step.
        [SerializeField] private TimingPromptSpawner _spawner;
        [SerializeField] private Scoring.ScoreSystem _score;
        [SerializeField] private FlightStateMachine _sm;
        [SerializeField] private DifficultySettings _difficulty;
        [SerializeField] private List<ChunkSpec> _deck;
        [SerializeField] private ChunkBuilder _builder;
        [SerializeField] private PlayerMomentumController _momentum;
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

        // M4 Task 7 streaming state: chunk registry (spec + zStart per
        // built chunk) so missions track the fish's own chunk; _nextZ /
        // _chunkSeed continue the layout stream past the starter.
        private readonly List<ChunkSpec> _chunkSpecs = new List<ChunkSpec>();
        private readonly List<float> _chunkStarts = new List<float>();
        private int _enteredChunkIndex = -1;
        private float _nextZ;
        private int _chunkSeed;
        private bool _running;

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

        // Scene wiring, called once at build (persists via SerializeFields).
        public void WireScene(ChunkBuilder builder, PlayerMomentumController momentum)
        {
            _builder = builder;
            _momentum = momentum;
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
            _chunkSpecs.Clear();
            _chunkStarts.Clear();
            _enteredChunkIndex = -1;
            _nextZ = 0f;
            _chunkSeed = 0;
            _running = false;
        }

        void Start()
        {
            StartRun();
        }

        // Live entry: fresh deck stream, deterministic seed-0 starter,
        // then per-frame Tick + StreamAhead take over in Update.
        public void StartRun()
        {
            SetSeed(System.Environment.TickCount);
            BuildStarter(0);
            _running = true;
        }

        void Update()
        {
            if (!_running || _momentum == null) return;
            float fishZ = _momentum.transform.position.z;
            float dt = Time.deltaTime;
            Tick(fishZ, dt);
            StreamAhead(fishZ);
        }

        // Deterministic Lagoon x2 from z=0 (seed 0 live): the fish never
        // spawns over empty water. Feeds the spawner wholesale so it
        // judges builder content from the first frame.
        public void BuildStarter(int seed)
        {
            if (_builder == null || _deck == null) return;
            ChunkSpec lagoon = null;
            foreach (var s in _deck)
                if (s != null && s.ChunkId == "Lagoon") { lagoon = s; break; }
            if (lagoon == null) return;
            _builder.BuildChunk(lagoon, 0f, seed);
            _chunkSpecs.Add(lagoon);
            _chunkStarts.Add(0f);
            _builder.BuildChunk(lagoon, lagoon.Length, seed + 1);
            _chunkSpecs.Add(lagoon);
            _chunkStarts.Add(lagoon.Length);
            _nextZ = lagoon.Length * 2f;
            _chunkSeed = seed + 2;
            EnterChunk(lagoon);
            SetGates(new List<float>());
            Tick(0f, 0f); // observe the entry now (dt 0: clock untouched)
            FeedSpawner();
        }

        // Endless stream: extend the horizon, reclaim the wake, recycle
        // water, re-feed the spawner, track the fish's chunk for missions.
        // Idempotent per fishZ — safe under live + manual double-drive.
        public void StreamAhead(float fishZ)
        {
            if (_builder == null || _deck == null || _deck.Count == 0) return;
            float ahead = _difficulty != null && _difficulty.SpawnAheadMeters > 0f
                ? _difficulty.SpawnAheadMeters : 1500f;
            float behind = _difficulty != null && _difficulty.ReclaimBehindMeters > 0f
                ? _difficulty.ReclaimBehindMeters : 300f;
            int guard = 0;
            while (_builder.FurthestContentZ < fishZ + ahead && guard++ < 8)
            {
                ChunkSpec spec = NextType();
                if (spec == null) break;
                _builder.BuildChunk(spec, _nextZ, _chunkSeed++);
                _chunkSpecs.Add(spec);
                _chunkStarts.Add(_nextZ);
                _nextZ += Mathf.Max(spec.Length, 1f);
            }
            _builder.ReclaimBefore(fishZ - behind);
            _builder.RecycleSegments(fishZ);
            FeedSpawner();
            ObserveContainingChunk(fishZ);
            Transform sky = _builder.SkyAnchor;
            if (sky != null)
            {
                Vector3 p = sky.position;
                p.z = fishZ;
                sky.position = p;
            }
        }

        // Missions track the chunk the fish is actually in — not the last
        // one built (a horizon build must not steal the active card, and
        // Storm gates must come from the fish's own chunk to ever cross).
        private void ObserveContainingChunk(float fishZ)
        {
            int containing = -1;
            for (int i = 0; i < _chunkStarts.Count; i++)
                if (_chunkStarts[i] <= fishZ) containing = i;
            if (containing < 0 || containing == _enteredChunkIndex) return;
            _enteredChunkIndex = containing;
            ChunkSpec spec = _chunkSpecs[containing];
            EnterChunk(spec);
            float z0 = _chunkStarts[containing];
            float z1 = z0 + Mathf.Max(spec.Length, 1f);
            var gates = new List<float>();
            foreach (var island in _builder.Islands)
            {
                if (island == null) continue;
                float z = island.transform.position.z;
                if (z < z0 || z > z1) continue;
                bool dup = false;
                foreach (float g in gates)
                    if (Mathf.Abs(g - z) < 1f) { dup = true; break; }
                if (!dup) gates.Add(z);
            }
            SetGates(gates);
            Tick(fishZ, 0f); // observe the entry now (dt 0: clock untouched)
        }

        private void FeedSpawner()
        {
            if (_spawner == null || _builder == null) return;
            _spawner.SetRings(new List<ChargeRing>(_builder.Rings));
            _spawner.SetCoins(new List<Scoring.CoinPickup>(_builder.Coins));
        }

        public void Tick(float playerZ, float dt)
        {
            _elapsed += Mathf.Max(0f, dt);
            _playerZ = playerZ;
            // R1: difficulty rides every tick, missions or not.
            if (_spawner != null) _spawner.SetDifficulty(DifficultyT);
            Creep(dt);
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

        // Baseline target speed creeps up on the run clock, capped at
        // SpeedCap — only while a run is live (pause gives dt 0 anyway).
        // Cap reads the timing asset through the spawner view, the same
        // source momentum clamps to; without it creep stays off.
        private void Creep(float dt)
        {
            if (!_running || dt <= 0f || _momentum == null || _difficulty == null) return;
            if (_spawner == null || _spawner.EffectiveSettings == null) return;
            float cap = _spawner.EffectiveSettings.SpeedCap;
            if (_momentum.TargetSpeed >= cap) return;
            _momentum.TargetSpeed = Mathf.Min(
                _momentum.TargetSpeed + _difficulty.SpeedCreep * dt, cap);
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
