using UnityEngine;

namespace FlyingFishMomentum.Scoring
{
    // Score chase: owns Score/Coins, multiplier rides the live tier.
    // Spawner pushes events (beats resolve, distance traveled, coins);
    // no Update of its own.
    public class ScoreSystem : MonoBehaviour
    {
        public float Score { get; private set; }
        public int Coins { get; private set; }
        public int Multiplier => _sm != null ? (int)_sm.ActiveTier + 1 : 1;

        [SerializeField] FlightStateMachine _sm;
        [SerializeField] ScoringSettings _settings;

        public void Configure(FlightStateMachine sm, ScoringSettings settings)
        {
            _sm = sm;
            _settings = settings;
        }

        public void AddDistance(float meters)
        {
            if (_settings == null || meters <= 0f) return;
            Score += meters * _settings.DistancePerMeter;
        }

        public void AddCoins(int n)
        {
            if (_settings == null || n <= 0) return;
            Coins += n;
            Score += n * _settings.CoinValue * Multiplier;
        }

        public void OnBeat(TimingResult result)
        {
            if (_settings == null) return;
            if (result == TimingResult.Perfect) Score += _settings.PerfectBonus * Multiplier;
            else if (result == TimingResult.Good) Score += _settings.GoodBonus * Multiplier;
        }
    }
}
