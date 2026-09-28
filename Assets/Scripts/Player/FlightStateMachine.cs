using System;
using System.Collections.Generic;
using UnityEngine;

namespace FlyingFishMomentum
{
    // Owns ActiveTier + Locomotion. Pushes tier limits into momentum directly;
    // movement and camera observe via OnTierChanged / ActiveProfile polling
    // (events over tight coupling, PRD rule 6) — the state machine never
    // references the movement class, so each stays independently testable.
    // M3 calls SetTier from gauge events; no gauge code lives here.
    public class FlightStateMachine : MonoBehaviour
    {
        public FlightTier ActiveTier { get; private set; } = FlightTier.None;
        public FlightTierProfile ActiveProfile { get; private set; }
        public PlayerLocomotionState Locomotion { get; private set; } = PlayerLocomotionState.Swimming;

        public PlayerMomentumController Momentum => _momentum;

        public event Action<FlightTier, FlightTier> OnTierChanged;

        // Serialized so scene/prefab wiring (set via Configure at build time)
        // survives save/load. State (ActiveTier/Locomotion) stays unserialized
        // and always starts at None/Swimming.
        [SerializeField] List<FlightTierProfile> _profiles = new List<FlightTierProfile>();
        [SerializeField] PlayerMomentumController _momentum;
        [SerializeField] MomentumSettings _settings;

        // Self-bootstrap: the initial None tier is APPLIED here (not via an
        // event) because Awake runs synchronously at load while subscribers
        // only attach in OnEnable — an event fired here would reach nobody.
        // Consumers also apply the current profile when they subscribe, so
        // tier state never depends on Start() ordering.
        void Awake()
        {
            var profile = _profiles.Find(p => p != null && p.Tier == FlightTier.None);
            if (profile == null) return;
            ActiveTier = FlightTier.None;
            ActiveProfile = profile;
            if (Momentum != null)
            {
                Momentum.SetLimits(profile.MaxSpeed, profile.Acceleration);
                Momentum.TargetSpeed = profile.MaxSpeed;
            }
        }

        public void Configure(
            List<FlightTierProfile> profiles,
            PlayerMomentumController momentum,
            MomentumSettings settings)
        {
            _profiles = profiles ?? new List<FlightTierProfile>();
            _momentum = momentum;
            _settings = settings;
        }

        public void SetTier(FlightTier tier)
        {
            if (tier == ActiveTier) return;
            var profile = _profiles.Find(p => p != null && p.Tier == tier);
            if (profile == null) return;
            var old = ActiveTier;
            ActiveTier = tier;
            ActiveProfile = profile;
            if (_momentum != null) _momentum.SetLimits(profile.MaxSpeed, profile.Acceleration);
            OnTierChanged?.Invoke(old, tier);
        }

        public void SetLocomotion(PlayerLocomotionState state)
        {
            Locomotion = state;
        }

        public void EvaluateSurface(float prevY, float newY)
        {
            if (_settings == null || _momentum == null) return;
            SetLocomotion(SurfaceCrossing.Evaluate(
                prevY, newY, _momentum.CurrentSpeed,
                _settings.BreachSpeedThreshold, Locomotion));
        }
    }
}
