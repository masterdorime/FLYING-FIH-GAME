using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using FlyingFishMomentum.Run;

namespace FlyingFishMomentum.Tests.EditMode
{
    // W2: obstacles read as coherent structures. Vertical colliders get
    // stacked shell segments (one seeded pick per obstacle,
    // bottom-aligned, never poking out); arches draw matched pillar and
    // lintel sets; skinned decor scales to legacy kind sizes.
    public class ObstacleShellTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

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

        // Unit-cube stub with a collider-free box child of the given size.
        private GameObject BoxStub(string name, Vector3 size)
        {
            var root = new GameObject(name);
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(root.transform, false);
            part.transform.localScale = size;
            _spawned.Add(root);
            return root;
        }

        private static ChunkSpec StormLike(GameObject island, GameObject spire)
        {
            var skin = ScriptableObject.CreateInstance<RealmSkin>();
            skin.IslandPrefabs = new GameObject[] { island };
            skin.SpirePrefabs = new GameObject[] { spire };
            var spec = ScriptableObject.CreateInstance<ChunkSpec>();
            spec.ChunkId = "StormLike"; spec.Skin = skin;
            spec.Length = 500f; spec.IslandPairs = 2; spec.SkySpireCount = 2;
            return spec;
        }

        private static List<GameObject> Shells(GameObject cube)
        {
            var shells = new List<GameObject>();
            foreach (Transform c in cube.transform)
                if (c.name.StartsWith("ChunkShell_")) shells.Add(c.gameObject);
            return shells;
        }

        private static Bounds Combined(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<MeshRenderer>();
            var bounds = new Bounds(go.transform.position, Vector3.zero);
            bool any = false;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return bounds;
        }

        private static void AssertStackedFromBaseInside(GameObject cube)
        {
            var shells = Shells(cube);
            Assert.Greater(shells.Count, 1, cube.name + " has no stacked segments");
            var names = new HashSet<string>();
            var cb = cube.GetComponent<Collider>().bounds;
            float top = float.NegativeInfinity, bottom = float.PositiveInfinity;
            foreach (var s in shells)
            {
                names.Add(s.name);
                var sb = Combined(s);
                Assert.IsTrue(cb.Contains(sb.min + Vector3.one * 0.05f)
                    && cb.Contains(sb.max - Vector3.one * 0.05f),
                    s.name + " pokes out of its collider");
                top = Mathf.Max(top, sb.max.y);
                bottom = Mathf.Min(bottom, sb.min.y);
            }
            Assert.AreEqual(1, names.Count, "segments of one obstacle use different prefabs");
            Assert.Less(bottom - cb.min.y, 1f, "stack does not start at the collider base");
            // Tapered stacks leave a top gap by construction: bound it to
            // under 1.5 average levels (the 8-vs-70 class sins leave 30+).
            float filled = top - bottom;
            Assert.Less(cb.max.y - top, 1.5f * filled / shells.Count + 0.5f, "stack does not fill the collider height");
        }

        [Test]
        public void Spires_StackSegmentsFromBaseInsideCollider()
        {
            var builder = NewBuilder();
            builder.BuildChunk(StormLike(BoxStub("Iso", new Vector3(4f, 3f, 4f)),
                BoxStub("Spire", new Vector3(3f, 4f, 3f))), 0f, 11);
            Assert.AreEqual(2, builder.Spires.Count);
            foreach (var spire in builder.Spires)
                AssertStackedFromBaseInside(spire);
        }

        [Test]
        public void Islands_StackSegmentsFromBaseInsideCollider()
        {
            var builder = NewBuilder();
            builder.BuildChunk(StormLike(BoxStub("Iso", new Vector3(4f, 3f, 4f)),
                BoxStub("Spire", new Vector3(3f, 4f, 3f))), 0f, 11);
            Assert.AreEqual(4, builder.Islands.Count);
            foreach (var island in builder.Islands)
                AssertStackedFromBaseInside(island);
        }

        [Test]
        public void Arches_DrawMatchedPillarAndLintelSets()
        {
            var builder = NewBuilder();
            var skin = ScriptableObject.CreateInstance<RealmSkin>();
            skin.ArchPillarPrefabs = new GameObject[]
                { BoxStub("PillarA", Vector3.one), BoxStub("PillarB", Vector3.one) };
            skin.ArchLintelPrefabs = new GameObject[] { BoxStub("Lintel", Vector3.one * 2f) };
            var spec = ScriptableObject.CreateInstance<ChunkSpec>();
            spec.ChunkId = "GauntletLike"; spec.Skin = skin;
            spec.Length = 500f; spec.ArchCount = 2;
            builder.BuildChunk(spec, 0f, 7);
            // Two pillars + one lintel per arch, in placement order.
            Assert.AreEqual(6, builder.Islands.Count);
            for (int a = 0; a < 2; a++)
            {
                var left = Shells(builder.Islands[a * 3])[0].name;
                var right = Shells(builder.Islands[a * 3 + 1])[0].name;
                Assert.AreEqual(left, right, "arch pillars mismatched");
                Assert.IsTrue(left.Contains("Pillar"), "pillar not from pillar set: " + left);
                var lintel = Shells(builder.Islands[a * 3 + 2])[0].name;
                Assert.IsTrue(lintel.Contains("Lintel"), "lintel not from lintel set: " + lintel);
            }
        }

        [Test]
        public void Decor_ScalesToKindSize()
        {
            var builder = NewBuilder();
            var skin = ScriptableObject.CreateInstance<RealmSkin>();
            skin.DecorPrefabs = new GameObject[] { BoxStub("Kelp", Vector3.one) };
            var spec = ScriptableObject.CreateInstance<ChunkSpec>();
            spec.ChunkId = "LagoonLike"; spec.Skin = skin;
            spec.Length = 500f; spec.DecorKind = "coral"; spec.DecorCount = 2;
            builder.BuildChunk(spec, 0f, 5);
            Assert.AreEqual(2, builder.Decor.Count);
            foreach (var d in builder.Decor)
            {
                var size = Combined(d).size;
                Assert.AreEqual(2.5f, size.x, 0.1f, "coral decor not scaled to legacy size");
            }
        }

        [Test]
        public void Segments_TaperAndCapInsideCollider()
        {
            // Micro-slab stub (4,1,4): uncapped this wants 15+ repeats;
            // the cap binds at MaxShellSegments for every legal height,
            // taper shrinks each level, all stay inside the collider.
            var builder = NewBuilder();
            var skin = ScriptableObject.CreateInstance<RealmSkin>();
            skin.SpirePrefabs = new GameObject[] { BoxStub("Slab", new Vector3(4f, 1f, 4f)) };
            var spec = ScriptableObject.CreateInstance<ChunkSpec>();
            spec.ChunkId = "Slabs"; spec.Skin = skin;
            spec.Length = 500f; spec.SkySpireCount = 1;
            builder.BuildChunk(spec, 0f, 11);
            Assert.AreEqual(1, builder.Spires.Count);
            var shells = Shells(builder.Spires[0]);
            Assert.AreEqual(ChunkBuilder.MaxShellSegments, shells.Count);
            var cb = builder.Spires[0].GetComponent<Collider>().bounds;
            float prev = float.PositiveInfinity;
            var names = new HashSet<string>();
            foreach (var s in shells)
            {
                names.Add(s.name);
                var b = Combined(s);
                Assert.IsTrue(cb.Contains(b.min + Vector3.one * 0.05f) && cb.Contains(b.max - Vector3.one * 0.05f), "segment pokes out");
                float w = s.transform.localScale.x;
                Assert.Less(w, prev, "taper does not narrow upward");
                prev = w;
            }
            Assert.AreEqual(1, names.Count, "one pick per obstacle");
        }

        [Test]
        public void RendererlessPrefab_LeavesVisibleCube()
        {
            // Degenerate shell must not leave an invisible collidable box.
            var builder = NewBuilder();
            var empty = new GameObject("NoMesh");
            _spawned.Add(empty);
            var skin = ScriptableObject.CreateInstance<RealmSkin>();
            skin.IslandPrefabs = new GameObject[] { empty };
            var spec = ScriptableObject.CreateInstance<ChunkSpec>();
            spec.ChunkId = "Degenerate"; spec.Skin = skin;
            spec.Length = 500f; spec.IslandPairs = 1;
            builder.BuildChunk(spec, 0f, 9);
            Assert.AreEqual(2, builder.Islands.Count);
            foreach (var island in builder.Islands)
            {
                Assert.AreEqual(0, Shells(island).Count);
                Assert.IsTrue(island.GetComponent<MeshRenderer>().enabled);
            }
        }

        [Test]
        public void BuoyDecor_FloatsAtSurfaceScaled()
        {
            var builder = NewBuilder();
            var skin = ScriptableObject.CreateInstance<RealmSkin>();
            var buoy = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Decor/buoy.prefab");
            Assert.IsNotNull(buoy, "buoy prefab missing — Task 3 must land first");
            skin.DecorPrefabs = new GameObject[] { buoy };
            var spec = ScriptableObject.CreateInstance<ChunkSpec>();
            spec.ChunkId = "Buoy"; spec.Skin = skin;
            spec.Length = 500f; spec.DecorKind = "buoy"; spec.DecorCount = 2;
            builder.BuildChunk(spec, 0f, 5);
            Assert.AreEqual(2, builder.Decor.Count);
            foreach (var d in builder.Decor)
            {
                Assert.AreEqual(0f, d.transform.position.y, 0.01f, "buoy not at the surface");
                Assert.GreaterOrEqual(Mathf.Abs(d.transform.position.x), 25f, "buoy inside lanes");
            }
        }

        [Test]
        public void GauntletSets_HaveNoCastles()
        {
            var skin = UnityEditor.AssetDatabase.LoadAssetAtPath<RealmSkin>("Assets/Configs/RealmSkin_Gauntlet.asset");
            Assert.IsNotNull(skin, "missing Gauntlet skin");
            var all = new List<GameObject>();
            foreach (var set in new GameObject[][] { skin.DecorPrefabs, skin.IslandPrefabs, skin.SpirePrefabs, skin.ArchPrefabs, skin.ArchPillarPrefabs, skin.ArchLintelPrefabs })
                if (set != null) all.AddRange(set);
            Assert.Greater(all.Count, 0, "Gauntlet sets empty");
            string[] banned = { "castle", "tower", "structure", "platform" };
            foreach (var p in all)
                foreach (var b in banned)
                    Assert.IsFalse(p.name.Contains(b), "medieval remnant in Gauntlet: " + p.name);
            bool hull = false, buoy = false;
            foreach (var p in all) { if (p.name.Contains("ship")) hull = true; if (p.name.Contains("buoy")) buoy = true; }
            Assert.IsTrue(hull, "no hulls in Gauntlet");
            Assert.IsTrue(buoy, "no buoys in Gauntlet");
        }

        [Test]
        public void SkySpireSet_HasNoStatueRing()
        {
            var skin = UnityEditor.AssetDatabase.LoadAssetAtPath<RealmSkin>(
                "Assets/Configs/RealmSkin_Sky.asset");
            Assert.IsNotNull(skin, "missing Sky skin");
            Assert.IsNotNull(skin.SpirePrefabs, "Sky spire set missing");
            Assert.Greater(skin.SpirePrefabs.Length, 1, "Sky spire set needs variety");
            foreach (var p in skin.SpirePrefabs)
                Assert.IsFalse(p.name.Contains("statue_ring"), "statue used as spire shell");
        }
    }
}
