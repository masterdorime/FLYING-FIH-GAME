using UnityEngine;

namespace FlyingFishMomentum.Run
{
    // M4 endless run: run-clock difficulty tuning (all SO, no magic numbers).
    [CreateAssetMenu(fileName = "DifficultySettings", menuName = "FlyingFish/Difficulty Settings")]
    public class DifficultySettings : ScriptableObject
    {
        public float RampSeconds = 240f;
        public float SpeedCreep = 0.5f;
        public float WindowTighten = 0.3f;
        public float GapShrink = 0.5f;
        public float StormWeightEnd = 3f;
        public float LagoonFloor = 0.5f;
        // M4 Task 7 streaming: RunManager keeps this much content ahead
        // of the fish and reclaims past this much behind (SO, not magic).
        public float SpawnAheadMeters = 1500f;
        public float ReclaimBehindMeters = 300f;
    }
}
