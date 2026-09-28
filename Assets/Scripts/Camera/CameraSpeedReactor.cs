using UnityEngine;

namespace FlyingFishMomentum
{
    // Chase rig: follows behind+above with exponential lag, speed-driven FOV,
    // turn banking, and tier/miss impulses. All refs are inspector-wired on the
    // CameraRig prefab (Task 7). Scaled dt throughout, so pause freezes the rig.
    // Kick/shake presentation mapping (×20 FOV, ×6 distance) is M1-chosen for
    // the PRD TierUpCameraKick tunable and documented here.
    [RequireComponent(typeof(Camera))]
    public class CameraSpeedReactor : MonoBehaviour
    {
        const float KickDecayRate = 2.5f;
        const float KickFovGain = 20f;
        const float KickDistanceGain = 6f;
        const float RollSmoothing = 0.1f;
        const float LookAhead = 0.3f;
        const float ShakeFrequency = 40f;

        public float KickEnvelope { get; private set; }
        public float ShakeEnvelope { get; private set; }

        [SerializeField] CameraSettings _camSettings;
        [SerializeField] MomentumSettings _momSettings;
        [SerializeField] PlayerMomentumController _momentum;
        [SerializeField] FlightStateMachine _stateMachine;
        [SerializeField] PlayerMovementController _movement;
        [SerializeField] Transform _target;

        Camera _cam;
        float _displayFov;
        float _roll;
        float _lastYaw;

        void Awake()
        {
            _cam = GetComponent<Camera>();
        }

        public void PlayTierUpKick() => KickEnvelope = 1f;
        public void PlayMissShake() => ShakeEnvelope = 1f;

        void LateUpdate()
        {
            if (_target == null || _momentum == null || _movement == null ||
                _stateMachine == null || _camSettings == null || _momSettings == null)
                return;
            var profile = _stateMachine.ActiveProfile;
            if (profile == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (_displayFov <= 0f) _displayFov = _camSettings.BaseFOV;

            float speed = _momentum.CurrentSpeed;
            float target = CameraMath.TargetFov(
                speed, _momSettings.MinSpeed, profile.MaxSpeed,
                _camSettings.BaseFOV, profile.CameraFOV);
            _displayFov = Mathf.Lerp(_displayFov, target,
                1f - Mathf.Exp(-dt / Mathf.Max(_camSettings.FOVSpeedResponse, 0.001f)));
            _displayFov = CameraMath.ClampFov(_displayFov, _camSettings.MaxFOV);

            KickEnvelope = CameraMath.Decay(KickEnvelope, KickDecayRate, dt);
            ShakeEnvelope = CameraMath.Decay(ShakeEnvelope, KickDecayRate, dt);

            float fov = _displayFov + KickEnvelope * _camSettings.TierUpCameraKick * KickFovGain;
            float dist = profile.CameraDistance + KickEnvelope * _camSettings.TierUpCameraKick * KickDistanceGain;

            float yawRate = Mathf.DeltaAngle(_lastYaw, _movement.Yaw) / dt;
            _lastYaw = _movement.Yaw;
            _roll = Mathf.Lerp(_roll, CameraMath.RollTarget(yawRate, profile.TurnRate),
                1f - Mathf.Exp(-dt / RollSmoothing));

            Vector3 forward = HeadingMath.Forward(_movement.Yaw, _movement.Pitch);
            Vector3 anchor = _target.position - forward * dist + Vector3.up * (dist * 0.4f);
            transform.position = Vector3.Lerp(transform.position, anchor,
                1f - Mathf.Exp(-dt / Mathf.Max(_camSettings.PositionLag, 0.001f)));

            if (ShakeEnvelope > 0f)
                transform.position += transform.right *
                    (Mathf.Sin(Time.time * ShakeFrequency) * ShakeEnvelope * _camSettings.MissCameraShake);

            transform.LookAt(_target.position + _movement.CurrentVelocity * LookAhead, Vector3.up);
            transform.Rotate(0f, 0f, _roll);
            _cam.fieldOfView = fov;
        }
    }
}
