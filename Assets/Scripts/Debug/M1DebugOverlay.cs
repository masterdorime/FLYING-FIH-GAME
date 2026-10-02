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
        [SerializeField] FlightGaugeSystem _gauge;
        [SerializeField] Scoring.ScoreSystem _score;
        float _fps;

        public void Configure(
            PlayerMomentumController momentum,
            FlightStateMachine sm,
            MomentumSettings settings,
            PlayerMovementController movement,
            TimingPromptSpawner spawner,
            FlightGaugeSystem gauge,
            Scoring.ScoreSystem score)
        {
            _momentum = momentum;
            _sm = sm;
            _settings = settings;
            _movement = movement;
            _spawner = spawner;
            _gauge = gauge;
            _score = score;
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
                timing = _spawner.Active.Open
                    ? $"OPEN {Mathf.Max(0f, _spawner.Active.TargetTime - Time.time):F2}s"
                    : _spawner.HasResolved ? _spawner.LastResult.ToString().ToUpper() : "-";
            timing += $" x{_spawner?.StreakCount}";
            string charge = _spawner != null ? _spawner.ChargeText : string.Empty;
            // Camera-bug telemetry (user report: dive tumble loses fish).
            // Roll = twist about view axis (the tumble); depr 90 = looking
            // straight down (LookAt singular zone); fish@ = viewport pos.
            string cam = "-";
            var mainCam = Camera.main;
            if (mainCam != null && _movement != null)
            {
                Vector3 fwd = mainCam.transform.forward;
                Vector3 levelUp = Vector3.up - fwd * Vector3.Dot(Vector3.up, fwd);
                float roll = levelUp.sqrMagnitude > 0.0001f
                    ? Vector3.Angle(mainCam.transform.up, levelUp) : 999f;
                float depr = 90f - Vector3.Angle(fwd, Vector3.down);
                float dist = (mainCam.transform.position - _movement.transform.position).magnitude;
                Vector3 sp = mainCam.WorldToScreenPoint(_movement.transform.position);
                string behind = sp.z < 0f ? " BEHIND" : string.Empty;
                cam = $"Cam roll {roll:F0} depr {depr:F0} dist {dist:F1} fish@({sp.x / Screen.width:F2},{sp.y / Screen.height:F2}){behind}";
            }
            GUI.Label(new Rect(10, 10, 460, 220),
                $"Speed {_momentum.CurrentSpeed:F1} / Target {_momentum.TargetSpeed:F1}\n" +
                $"State {_sm.Locomotion} Tier {_sm.ActiveTier}\n" +
                $"Breach >= {_settings.BreachSpeedThreshold:F0} Deviate {Mathf.Abs(_movement.Yaw):F0}°\n" +
                $"Timing {timing}\n" +
                (string.IsNullOrEmpty(charge) ? string.Empty : charge + "\n") +
                $"FPS {_fps:F0}\n" +
                $"Gauge {_gauge.CurrentGauge:F0}/{_gauge.MaxGauge:F0} Tier {_sm.ActiveTier}\n" +
                (_spawner != null ? $"Rockets {_spawner.RocketCount}/{_spawner.RocketMax}\n" : string.Empty) +
                (_score != null ? $"Score {_score.Score:F0} x{_score.Multiplier} • {_score.Coins}c\n" : string.Empty) +
                cam);
        }
    }
}
