using NUnit.Framework;
using UnityEngine;
using FlyingFishMomentum.Run;

namespace FlyingFishMomentum.Tests.EditMode
{
    public class RealmSkinTests
    {
        private readonly System.Collections.Generic.List<GameObject> _spawned =
            new System.Collections.Generic.List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned) Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        private ChunkBuilder NewBuilder()
        {
            var go = new GameObject("chunkBuilder");
            _spawned.Add(go);
            return go.AddComponent<ChunkBuilder>();
        }

        [Test]
        public void ApplyMood_NullSkin_KeepsLegacyTintPath()
        {
            var builder = NewBuilder();
            var spec = ScriptableObject.CreateInstance<ChunkSpec>();
            spec.ChunkId = "Legacy"; spec.Skin = null;
            spec.FogColor = Color.red; spec.FogDensity = 0.01f;
            spec.SkyTint = Color.blue; spec.WaterTint = Color.green;
            try
            {
                builder.ApplyMood(spec);
                Assert.IsNull(RenderSettings.skybox);
                Assert.AreEqual(Color.red, RenderSettings.fogColor);
            }
            finally { RenderSettings.skybox = null; }
        }

        [Test]
        public void ApplyMood_Skin_SetsSkyboxFogAndWater()
        {
            var builder = NewBuilder();
            var skin = ScriptableObject.CreateInstance<RealmSkin>();
            skin.SkyPanorama = new Texture2D(4, 2);
            skin.SkyTint = Color.white; skin.FogColor = Color.gray;
            skin.FogDensity = 0.02f; skin.WaterTint = new Color(0.1f, 0.2f, 0.3f);
            var spec = ScriptableObject.CreateInstance<ChunkSpec>();
            spec.ChunkId = "Lagoon"; spec.Skin = skin;
            try
            {
                builder.ApplyMood(spec);
                Assert.IsNotNull(RenderSettings.skybox);
                Assert.AreEqual(Color.gray, RenderSettings.fogColor);
                Assert.AreEqual(0.02f, RenderSettings.fogDensity, 0.0001f);
                var water = builder.WaterMaterial.color;
                Assert.AreEqual(0.1f, water.r, 0.001f);
                Assert.AreEqual(0.6f, water.a, 0.001f); // alpha never stamped
            }
            finally { RenderSettings.skybox = null; Object.DestroyImmediate(skin.SkyPanorama); }
        }

        [Test]
        public void SkinAssets_ExistWithPanoramaAndPrefabs()
        {
            string[] realms = { "Lagoon", "Gauntlet", "Storm", "Sky" };
            foreach (var r in realms)
            {
                var skin = UnityEditor.AssetDatabase.LoadAssetAtPath<RealmSkin>(
                    "Assets/Configs/RealmSkin_" + r + ".asset");
                Assert.IsNotNull(skin, "missing skin asset for " + r);
                Assert.IsNotNull(skin.SkyPanorama, r + " panorama");
                Assert.AreEqual(skin.SkyPanorama.width, skin.SkyPanorama.height * 2,
                    r + " panorama is not 2:1 equirect");
                Assert.IsNotNull(skin.DecorPrefabs, r + " decor set");
                Assert.Greater(skin.DecorPrefabs.Length, 0, r + " decor empty");
                foreach (var p in skin.DecorPrefabs)
                {
                    Assert.IsNotNull(p, r + " null prefab ref");
                    Assert.IsNull(p.GetComponentInChildren<Collider>(true), p.name + " carries a collider");
                }
            }
        }
    }
}
