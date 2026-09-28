using UnityEngine;

namespace FlyingFishMomentum
{
    [CreateAssetMenu(fileName = "MomentumSettings", menuName = "FlyingFish/Momentum Settings")]
    public class MomentumSettings : ScriptableObject
    {
        public float MinSpeed = 8f;
        public float DragSwimming = 1.5f;
        public float DragFlying = 0.6f;
        public float BreachSpeedThreshold = 30f;
    }
}
