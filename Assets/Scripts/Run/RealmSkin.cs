using UnityEngine;

namespace FlyingFishMomentum.Run
{
    // Visual reconstruction: one ScriptableObject per chunk type (Lagoon,
    // Gauntlet, Storm, Sky). Declares the realm presentation: panorama,
    // tints, and the prefab sets dressing consumes with seeded picks.
    [CreateAssetMenu(fileName = "RealmSkin", menuName = "FlyingFish/Realm Skin")]
    public class RealmSkin : ScriptableObject
    {
        public Texture2D SkyPanorama;      // 2:1 equirect, Skybox/Panoramic
        public Color SkyTint = Color.white; // multiplied over panorama
        public Color FogColor;
        public float FogDensity;
        public Color WaterTint;             // RGB only (alpha stays 0.6)
        public Color CloudTint = new Color(0.95f, 0.97f, 1f);
        public Color SilhouetteColor = new Color(0.16f, 0.2f, 0.3f);
        public Color RingTint = new Color(1f, 0.85f, 0.2f); // reserved: rings use asset colors (§34.1), kept so existing .asset files still load
        public Color CoinTint = new Color(1f, 0.75f, 0.15f);
        public GameObject[] DecorPrefabs;   // seeded pick, decor pool
        public GameObject[] IslandPrefabs;  // visual shells over island colliders
        public GameObject[] SpirePrefabs;   // visual shells over spire colliders
        public GameObject[] ArchPrefabs;    // pillar/lintel shells (Gauntlet)
        // Matched arch sets (optional): one seeded pick per arch, both
        // pillars share it. Empty/null falls back to ArchPrefabs.
        public GameObject[] ArchPillarPrefabs; // tall pieces
        public GameObject[] ArchLintelPrefabs; // wide pieces
    }
}
