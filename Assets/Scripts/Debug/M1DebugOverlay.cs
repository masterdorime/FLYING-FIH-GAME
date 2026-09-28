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
        float _fps;

        public void Configure(
            PlayerMomentumController momentum,
            FlightStateMachine sm,
            MomentumSettings settings)
        {
            _momentum = momentum;
            _sm = sm;
            _settings = settings;
        }

        void Update()
        {
            _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.1f);
        }

        void OnGUI()
        {
            if (!Debug.isDebugBuild && !Application.isEditor) return;
            if (_momentum == null || _sm == null || _settings == null) return;
            GUI.Label(new Rect(10, 10, 420, 110),
                $"Speed {_momentum.CurrentSpeed:F1} / Target {_momentum.TargetSpeed:F1}\n" +
                $"State {_sm.Locomotion} Tier {_sm.ActiveTier}\n" +
                $"Breach >= {_settings.BreachSpeedThreshold:F0}\n" +
                $"FPS {_fps:F0}");
        }
    }
}
