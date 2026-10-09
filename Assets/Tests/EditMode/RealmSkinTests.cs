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
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
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
                var water = builder.WaterMaterial.GetColor("_Color_Shallow");
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
                var sets = new System.Collections.Generic.Dictionary<string, GameObject[]>
                {
                    { "decor", skin.DecorPrefabs },
                    { "island", skin.IslandPrefabs },
                    { "spire", skin.SpirePrefabs },
                    { "arch", skin.ArchPrefabs },
                };
                foreach (var kv in sets)
                {
                    if (kv.Value == null) continue;
                    foreach (var p in kv.Value)
                    {
                        Assert.IsNotNull(p, r + " " + kv.Key + " null prefab ref");
                        Assert.IsNull(p.GetComponentInChildren<Collider>(true), p.name + " carries a collider");
                    }
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
                    // Stacked policy (visual W2): one or more
                    // aspect-preserved segments filling the collider,
                    // never poking out (single-cube stub has one renderer).
                    int shells = 0;
                    foreach (Transform c in skinned.Islands[i].transform)
                    {
                        if (!c.name.StartsWith("ChunkShell_")) continue;
                        shells++;
                        var rb = c.GetComponentInChildren<MeshRenderer>().bounds;
                        Assert.IsTrue(sb.Contains(rb.min + Vector3.one * 0.05f)
                            && sb.Contains(rb.max - Vector3.one * 0.05f), "segment pokes out");
                    }
                    Assert.Greater(shells, 0, "no shell attached");
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

        // House style is tolerance comparison (exact Color equality trips
        // on sub-1e3 material round-trips).
        private static void AssertColor(Color expected, Color actual)
        {
            Assert.AreEqual(expected.r, actual.r, 0.001f, "r");
            Assert.AreEqual(expected.g, actual.g, 0.001f, "g");
            Assert.AreEqual(expected.b, actual.b, 0.001f, "b");
            Assert.AreEqual(expected.a, actual.a, 0.001f, "a");
        }

        [Test]
        public void ApplyMood_Skin_TintsCoinsSilhouettesAndCloudSkipsRings()
        {
            var builder = NewBuilder();
            var skin = ScriptableObject.CreateInstance<RealmSkin>();
            skin.SkyPanorama = new Texture2D(4, 2);
            skin.RingTint = Color.magenta; skin.CoinTint = Color.cyan;
            skin.SilhouetteColor = Color.black; skin.CloudTint = Color.white;
            var spec = ScriptableObject.CreateInstance<ChunkSpec>();
            spec.ChunkId = "Storm"; spec.Skin = skin;
            spec.Length = 500f; spec.RingCount = 1; spec.RingSpacing = 150f;
            spec.CoinsPerTrail = 1;
            try
            {
                builder.BuildChunk(spec, 0f, 3);
                // Rings use game-asset colors (§34.1): the mood tint path
                // must not touch them — the shared pipeline material stays.
                var pipeMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/Materials/V1Watercraft.mat");
                var mesh = builder.Rings[0].GetComponentInChildren<MeshRenderer>();
                Assert.IsNotNull(mesh, "ring model missing");
                Assert.AreSame(pipeMat, mesh.sharedMaterial, "ring material swapped or copied by mood");
                var gem = builder.Coins[0].transform.Find("Gem").GetComponent<MeshRenderer>();
                Assert.AreEqual(Color.cyan, gem.sharedMaterial.GetColor("_BaseColor"));
            }
            finally { RenderSettings.skybox = null; Object.DestroyImmediate(skin.SkyPanorama); }
        }

        [Test]
        public void SkyMood_IsCoolNotPink()
        {
            // The Sky realm washed pink (rose tint x alien panorama x rose
            // fog): cool blue-dominant multipliers, panorama keeps identity.
            var skin = UnityEditor.AssetDatabase.LoadAssetAtPath<RealmSkin>(
                "Assets/Configs/RealmSkin_Sky.asset");
            Assert.IsNotNull(skin, "missing Sky skin");
            Assert.Greater(skin.SkyTint.b, skin.SkyTint.r, "SkyTint still pink");
            Assert.Greater(skin.FogColor.b, skin.FogColor.r, "FogColor still pink");
        }

        [Test]
        public void ChunkSpecAssets_HaveSkinsWired()
        {
            string[] realms = { "Lagoon", "Gauntlet", "Storm", "Sky" };
            foreach (var r in realms)
            {
                var spec = UnityEditor.AssetDatabase.LoadAssetAtPath<ChunkSpec>(
                    "Assets/Configs/ChunkSpec_" + r + ".asset");
                Assert.IsNotNull(spec, "missing ChunkSpec for " + r);
                Assert.IsNotNull(spec.Skin, r + " ChunkSpec has no RealmSkin wired");
                Assert.IsNotNull(spec.Skin.SkyPanorama, r + " skin panorama");
                Assert.Greater(spec.Skin.DecorPrefabs.Length, 0, r + " skin decor empty");
            }
        }

        [Test]
        public void BuildChunk_LegacyReuse_StripsStaleShells()
        {
            var builder = NewBuilder();
            var shellPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _spawned.Add(shellPrefab);
            Object.DestroyImmediate(shellPrefab.GetComponent<Collider>());
            var skin = ScriptableObject.CreateInstance<RealmSkin>();
            skin.IslandPrefabs = new GameObject[] { shellPrefab };
            var skinned = ScriptableObject.CreateInstance<ChunkSpec>();
            skinned.ChunkId = "Skinned"; skinned.Skin = skin;
            skinned.Length = 500f; skinned.IslandPairs = 1;
            var legacy = ScriptableObject.CreateInstance<ChunkSpec>();
            legacy.ChunkId = "Legacy"; legacy.Skin = null;
            legacy.Length = 500f; legacy.IslandPairs = 1;
            builder.BuildChunk(skinned, 0f, 9);
            Assert.Greater(CountShells(builder.Islands[0]), 0);
            builder.ReclaimBefore(100000f);
            builder.BuildChunk(legacy, 0f, 9);
            foreach (var island in builder.Islands)
            {
                Assert.AreEqual(0, CountShells(island));
                Assert.IsTrue(island.GetComponent<MeshRenderer>().enabled);
            }
        }

        [Test]
        public void BuildChunk_NullPrefabEntries_Skipped()
        {
            var builder = NewBuilder();
            var realPrefab = new GameObject("RealDecor");
            _spawned.Add(realPrefab);
            var skin = ScriptableObject.CreateInstance<RealmSkin>();
            skin.DecorPrefabs = new GameObject[] { null, realPrefab };
            var spec = ScriptableObject.CreateInstance<ChunkSpec>();
            spec.ChunkId = "Lagoon"; spec.Skin = skin;
            spec.Length = 500f; spec.DecorKind = "rubble"; spec.DecorCount = 2;
            builder.BuildChunk(spec, 0f, 5);
            Assert.AreEqual(2, builder.Decor.Count);
            foreach (var d in builder.Decor)
                Assert.AreEqual("ChunkDecor_RealDecor", d.name);
        }

        [Test]
        public void ApplyMood_LegacySpec_RestoresCoinSilhouetteCloudDefaults()
        {
            var builder = NewBuilder();
            var skin = ScriptableObject.CreateInstance<RealmSkin>();
            skin.SkyPanorama = new Texture2D(4, 2);
            skin.RingTint = Color.magenta; skin.CoinTint = Color.cyan;
            var skinned = ScriptableObject.CreateInstance<ChunkSpec>();
            skinned.ChunkId = "Storm"; skinned.Skin = skin;
            skinned.Length = 500f; skinned.RingCount = 1; skinned.RingSpacing = 150f;
            skinned.CoinsPerTrail = 1;
            var legacy = ScriptableObject.CreateInstance<ChunkSpec>();
            legacy.ChunkId = "Legacy"; legacy.Skin = null;
            legacy.FogColor = Color.red; legacy.FogDensity = 0.01f;
            legacy.SkyTint = Color.blue; legacy.WaterTint = Color.green;
            try
            {
                builder.BuildChunk(skinned, 0f, 3);
                builder.ApplyMood(legacy);
                // A null-skin mood restores coin/silhouette/cloud defaults
                // but never touches rings: asset colors, not defaults.
                var mesh = builder.Rings[0].GetComponentInChildren<MeshRenderer>();
                Assert.IsNotNull(mesh, "ring model missing");
                var pipeMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/Materials/V1Watercraft.mat");
                Assert.AreSame(pipeMat, mesh.sharedMaterial, "ring material touched by mood");
                var gem = builder.Coins[0].transform.Find("Gem").GetComponent<MeshRenderer>();
                AssertColor(new Color(1f, 0.75f, 0.15f), gem.sharedMaterial.GetColor("_BaseColor"));
                Assert.IsNull(RenderSettings.skybox);
            }
            finally { RenderSettings.skybox = null; Object.DestroyImmediate(skin.SkyPanorama); }
        }
    }
}
