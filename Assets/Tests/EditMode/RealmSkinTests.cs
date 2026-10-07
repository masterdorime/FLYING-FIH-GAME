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

        [Test]
        public void FitScale_FitsLargestAxisUniformly()
        {
            Assert.AreEqual(4f, ChunkBuilder.FitScale(new Vector3(4f, 20f, 4f), new Vector3(1f, 2f, 1f)), 0.001f);
            Assert.AreEqual(1f, ChunkBuilder.FitScale(Vector3.zero, Vector3.one), 0.001f);
        }

        [Test]
        public void BuildChunk_SkinnedDecor_UsesPrefabsDeterministically()
        {
            var a = NewBuilder(); var b = NewBuilder();
            var skin = ScriptableObject.CreateInstance<RealmSkin>();
            var prefab = new GameObject("PalmTest");
            _spawned.Add(prefab);
            skin.DecorPrefabs = new GameObject[] { prefab };
            var spec = ScriptableObject.CreateInstance<ChunkSpec>();
            spec.ChunkId = "Lagoon"; spec.Skin = skin;
            spec.Length = 500f; spec.DecorKind = "rubble"; spec.DecorCount = 3;
            try
            {
                a.BuildChunk(spec, 0f, 42); b.BuildChunk(spec, 0f, 42);
                Assert.AreEqual(a.Decor.Count, b.Decor.Count);
                for (int i = 0; i < a.Decor.Count; i++)
                    Assert.AreEqual(
                        a.Decor[i].transform.position, b.Decor[i].transform.position);
            }
            finally { RenderSettings.skybox = null; }
        }

        [Test]
        public void BuildChunk_SkinnedIsland_ColliderBoundsMatchLegacy()
        {
            var legacy = NewBuilder(); var skinned = NewBuilder();
            var shellPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _spawned.Add(shellPrefab);
            Object.DestroyImmediate(shellPrefab.GetComponent<Collider>());
            var skin = ScriptableObject.CreateInstance<RealmSkin>();
            skin.IslandPrefabs = new GameObject[] { shellPrefab };
            var mkSpec = new System.Func<RealmSkin, ChunkSpec>(s =>
            {
                var sp = ScriptableObject.CreateInstance<ChunkSpec>();
                sp.ChunkId = "X"; sp.Skin = s; sp.Length = 500f; sp.IslandPairs = 1;
                return sp;
            });
            try
            {
                legacy.BuildChunk(mkSpec(null), 0f, 9);
                skinned.BuildChunk(mkSpec(skin), 0f, 9);
                Assert.AreEqual(legacy.Islands.Count, skinned.Islands.Count);
                for (int i = 0; i < legacy.Islands.Count; i++)
                {
                    var lb = legacy.Islands[i].GetComponent<Collider>().bounds;
                    var sb = skinned.Islands[i].GetComponent<Collider>().bounds;
                    Assert.AreEqual(lb.extents, sb.extents);
                    Assert.IsFalse(skinned.Islands[i].GetComponent<MeshRenderer>().enabled);
                    Assert.AreEqual(1, CountShells(skinned.Islands[i]));
                }
            }
            finally { RenderSettings.skybox = null; }
        }

        private static int CountShells(GameObject go)
        {
            int n = 0;
            foreach (Transform c in go.transform)
                if (c.name.StartsWith("ChunkShell_")) n++;
            return n;
        }
    }
}
