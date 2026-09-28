using UnityEngine;

namespace FlyingFishMomentum
{
    [CreateAssetMenu(fileName = "FlightTierProfile", menuName = "FlyingFish/Flight Tier Profile")]
    public class FlightTierProfile : ScriptableObject
    {
        public FlightTier Tier;
        public float MaxSpeed;
        public float Acceleration;
        public float TurnRate;
        public float GravityScale;
        public float AirControl;
        public float CameraFOV;
        public float CameraDistance;
        // M5 art fields — data only, unused in M1.
        public float WingVisualScale = 1f;
        public Color TrailColor = Color.white;
    }
}
