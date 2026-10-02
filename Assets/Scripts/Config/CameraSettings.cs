using UnityEngine;

namespace FlyingFishMomentum
{
    [CreateAssetMenu(fileName = "CameraSettings", menuName = "FlyingFish/Camera Settings")]
    public class CameraSettings : ScriptableObject
    {
        public float BaseFOV = 60f;
        public float MaxFOV = 85f;
        public float FOVSpeedResponse = 0.15f;
        public float PositionLag = 0.12f;
        public float TierUpCameraKick = 0.35f;
        public float MissCameraShake = 0.4f;
    }
}
