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
        private DifficultySettings _difficulty;
        private List<ChunkSpec> _deck;
        private float _elapsed;
        private System.Random _rng = new System.Random();

        public void Configure(TimingPromptSpawner spawner, Scoring.ScoreSystem score, DifficultySettings diff, List<ChunkSpec> deck)
        {
            _spawner = spawner;
            _score = score;
            _difficulty = diff;
            _deck = deck;
        }

        public void SetSeed(int seed)
        {
            _elapsed = 0f;
            _rng = new System.Random(seed);
        }

        public void Tick(float playerZ, float dt)
        {
            _elapsed += Mathf.Max(0f, dt);
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
    }
}
