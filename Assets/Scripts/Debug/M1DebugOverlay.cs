using UnityEngine;

namespace FlyingFishMomentum
{
    // [M1-SCAFFOLD] IMGUI debug overlay (PRD §24.1 subset). Debug builds and
    // the Editor only; shows live gameplay-critical state. Deleted in M5.
    public class M1DebugOverlay : MonoBehaviour
    {
        // Serialized so scene wiring (set via Configure at build time) persists.
        [SerializeField] PlayerMomentumController _momentum;
        [SerializeField] FlightStateMachine _sm;
        [SerializeField] MomentumSettings _settings;
        [SerializeField] PlayerMovementController _movement;
        [SerializeField] TimingPromptSpawner _spawner;
        float _fps;

        public void Configure(
            PlayerMomentumController momentum,
            FlightStateMachine sm,
            MomentumSettings settings,
            PlayerMovementController movement,
            TimingPromptSpawner spawner)
        {
            _momentum = momentum;
            _sm = sm;
            _settings = settings;
            _movement = movement;
            _spawner = spawner;
        }

        void Update()
        {
            _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.1f);
        }

        void OnGUI()
        {
            if (!Debug.isDebugBuild && !Application.isEditor) return;
            if (_momentum == null || _sm == null || _settings == null || _movement == null) return;
            string timing = "-";
            if (_spawner != null)
                timing = _spawner.HasResolved ? _spawner.LastResult.ToString().ToUpper()
                    : _spawner.Active.Open ? $"OPEN {Mathf.Max(0f, _spawner.Active.TargetTime - Time.time):F2}s"
                    : "-";
            timing += $" x{_spawner?.StreakCount}";
            string charge = _spawner != null ? _spawner.ChargeText : string.Empty;
            GUI.Label(new Rect(10, 10, 420, 170),
                $"Speed {_momentum.CurrentSpeed:F1} / Target {_momentum.TargetSpeed:F1}\n" +
                $"State {_sm.Locomotion} Tier {_sm.ActiveTier}\n" +
                $"Breach >= {_settings.BreachSpeedThreshold:F0} Deviate {Mathf.Abs(_movement.Yaw):F0}°\n" +
                $"Timing {timing}\n" +
                (string.IsNullOrEmpty(charge) ? string.Empty : charge + "\n") +
                $"FPS {_fps:F0}");
        }
    }
}
