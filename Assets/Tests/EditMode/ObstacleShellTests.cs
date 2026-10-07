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
            Assert.Less(cb.max.y - top, 9f, "stack does not fill the collider height");
        }

        [Test]
        public void Spires_StackSegmentsFromBaseInsideCollider()
        {
            var builder = NewBuilder();
            builder.BuildChunk(StormLike(BoxStub("Iso", new Vector3(4f, 3f, 4f)),
                BoxStub("Spire", new Vector3(2f, 4f, 1f))), 0f, 11);
            Assert.AreEqual(2, builder.Spires.Count);
            foreach (var spire in builder.Spires)
                AssertStackedFromBaseInside(spire);
        }

        [Test]
        public void Islands_StackSegmentsFromBaseInsideCollider()
        {
            var builder = NewBuilder();
            builder.BuildChunk(StormLike(BoxStub("Iso", new Vector3(4f, 3f, 4f)),
                BoxStub("Spire", new Vector3(2f, 4f, 1f))), 0f, 11);
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
