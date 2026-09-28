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

        public PlayerMomentumController Momentum { get; private set; }

        public event Action<FlightTier, FlightTier> OnTierChanged;

        List<FlightTierProfile> _profiles = new List<FlightTierProfile>();
        MomentumSettings _settings;

        public void Configure(
            List<FlightTierProfile> profiles,
            PlayerMomentumController momentum,
            MomentumSettings settings)
        {
            _profiles = profiles ?? new List<FlightTierProfile>();
            Momentum = momentum;
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
            if (Momentum != null) Momentum.SetLimits(profile.MaxSpeed, profile.Acceleration);
            OnTierChanged?.Invoke(old, tier);
        }

        public void SetLocomotion(PlayerLocomotionState state)
        {
            Locomotion = state;
        }

        public void EvaluateSurface(float prevY, float newY)
        {
            if (_settings == null || Momentum == null) return;
            SetLocomotion(SurfaceCrossing.Evaluate(
                prevY, newY, Momentum.CurrentSpeed,
                _settings.BreachSpeedThreshold, Locomotion));
        }
    }
}
