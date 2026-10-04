using UnityEngine;

namespace FlyingFishMomentum.Run
{
    // M4 endless run: one ScriptableObject per chunk type (Lagoon,
    // Gauntlet, Storm, Sky). Declares content counts, the difficulty
    // band it may appear in, deck weight, mission card, and mood.
    [CreateAssetMenu(fileName = "ChunkSpec", menuName = "FlyingFish/Chunk Spec")]
    public class ChunkSpec : ScriptableObject
    {
        public string ChunkId;
        public float Length = 300f;
        public int RingCount;
        public float RingSpacing = 150f;
        public float RingSwimFraction = 1f;
        public int CoinTrails;
        public int CoinsPerTrail = 4;
        public int IslandPairs;
        // Cloud-realm layer (above 55m): per-chunk sky content so flight
        // has somewhere to go. Zero keeps a type water-only.
        public int SkyRingCount;
        public int SkyCoinTrails;
        public int SkySpireCount;
        // Map dressing: one decor kind per chunk + count budget (tunable,
        // not hardcoded). Kinds: coral, rubble, crag, cloud. Decor never
        // collides and parks off the prompt lanes (see builder).
        public string DecorKind;
        public int DecorCount;
        // Gauntlet gates: rock arches (pillars + lintel, all blocking)
        // anchored over swim ring lines — steer through the middle.
        public int ArchCount;
        public float MinDifficulty;
        public float MaxDifficulty = 1f;
        public float Weight = 1f;
        public string MissionText;
        public int MissionTarget = 1;
        public Color SkyTint;
        public Color FogColor;
        public float FogDensity;
        public Color WaterTint;
    }
}
