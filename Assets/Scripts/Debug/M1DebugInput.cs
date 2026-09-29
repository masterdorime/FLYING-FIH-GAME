using UnityEngine;
using UnityEngine.InputSystem;

namespace FlyingFishMomentum
{
    // [M1-SCAFFOLD] Debug-only driver. Owns all M1 stand-ins: initial tier,
    // speed keys (deleted in M2 when timing drives speed), camera demo keys,
    // minimal timeScale pause (full menu is M5). Deleted in M5.
    public class M1DebugInput : MonoBehaviour
    {
        // Serialized so scene wiring (set via Configure at build time) persists.
        [SerializeField] FlightStateMachine _sm;
        [SerializeField] CameraSpeedReactor _camera;
        [SerializeField] PlayerMovementController _movement;

        public void Configure(
            FlightStateMachine sm,
            CameraSpeedReactor camera,
            PlayerMovementController movement)
        {
            _sm = sm;
            _camera = camera;
            _movement = movement;
        }

        void Start()
        {
            // Initial tier bootstraps itself in FlightStateMachine.Awake —
            // nothing here may depend on Start() ordering (see ledger).
            // M2: timing drives speed now (digits deleted); tier locked to
            // Medium until the M3 gauge moves it. Subscribers attach in
            // OnEnable, so this SetTier reaches ApplyProfile.
            if (_sm != null) _sm.SetTier(FlightTier.Medium);
            if (_movement != null)
                _movement.Input.Gameplay.Pause.performed += OnPause;
        }

        void OnDisable()
        {
            if (_movement != null)
                _movement.Input.Gameplay.Pause.performed -= OnPause;
        }

        void OnPause(InputAction.CallbackContext _)
        {
            Time.timeScale = Time.timeScale > 0f ? 0f : 1f;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || _sm == null) return;
            if (kb.tKey.wasPressedThisFrame && _camera != null) _camera.PlayTierUpKick();
            if (kb.gKey.wasPressedThisFrame && _camera != null) _camera.PlayMissShake();
        }
    }
}
