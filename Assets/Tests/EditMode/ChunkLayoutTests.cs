using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FlyingFishMomentum.Run;

namespace FlyingFishMomentum.Tests.EditMode
{
    // Phase (i): showcase chunks. A ChunkLayout hand-places one chunk's
    // obstacles; the spec carries it optionally (null = procedural).
    // Placement is relative to chunk origin (zStart added at build, y
    // absolute); shells resolve by prefab name against the skin sets.
    public class ChunkLayoutTests
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

        private GameObject BoxStub(string name)
        {
            var root = new GameObject(name);
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(root.transform, false);
            _spawned.Add(root);
            return root;
        }

        // One ring, one island, one spire, one arch; shells resolve by name.
        private ChunkSpec LayoutSpec()
        {
            var island = BoxStub("LayoutIso");
            var spire = BoxStub("LayoutSpire");
            var pillar = BoxStub("LayoutPillar");
            var lintel = BoxStub("LayoutLintel");
            var skin = ScriptableObject.CreateInstance<RealmSkin>();
            skin.IslandPrefabs = new GameObject[] { island };
            skin.SpirePrefabs = new GameObject[] { spire };
            skin.ArchPillarPrefabs = new GameObject[] { pillar };
            skin.ArchLintelPrefabs = new GameObject[] { lintel };
            var layout = ScriptableObject.CreateInstance<ChunkLayout>();
            layout.LayoutId = "Showcase1";
            layout.Seed = 4;
            layout.Rings = new ChunkLayout.RingEntry[]
            {
                new ChunkLayout.RingEntry { Id = "r1", Position = new Vector3(5f, -3f, 100f) },
            };
            layout.Islands = new ChunkLayout.IslandEntry[]
            {
                new ChunkLayout.IslandEntry { Id = "i1", Position = new Vector3(15f, 5f, 200f),
                    Scale = new Vector3(10f, 25f, 10f), ShellName = "LayoutIso" },
            };
            layout.Spires = new ChunkLayout.SpireEntry[]
            {
                new ChunkLayout.SpireEntry { Id = "s1", Position = new Vector3(-12f, 55f, 300f),
                    Height = 70f, ShellName = "LayoutSpire" },
            };
            layout.Arches = new ChunkLayout.ArchEntry[]
            {
                new ChunkLayout.ArchEntry { Id = "a1", Anchor = new Vector3(0f, -3f, 400f),
                    PillarShell = "LayoutPillar", LintelShell = "LayoutLintel" },
            };
            var spec = ScriptableObject.CreateInstance<ChunkSpec>();
            spec.ChunkId = "Showcase"; spec.Skin = skin; spec.Layout = layout;
            spec.Length = 500f; spec.CoinsPerTrail = 2;
            return spec;
        }

        [Test]
        public void LayoutBranch_PlacesAuthoredTransforms()
        {
            var builder = NewBuilder();
            builder.BuildChunk(LayoutSpec(), 1000f, 7);
            Assert.AreEqual(1, builder.Rings.Count);
            Assert.AreEqual(new Vector3(5f, -3f, 1100f), builder.Rings[0].transform.position);
            // Placement order mirrors procedural: 3 arch cubes, then islands.
            Assert.AreEqual(4, builder.Islands.Count);
            var island = builder.Islands[3];
            Assert.AreEqual(new Vector3(15f, 5f, 1200f), island.transform.position);
            Assert.AreEqual(new Vector3(10f, 25f, 10f), island.transform.localScale);
            Assert.AreEqual(1, builder.Spires.Count);
            Assert.AreEqual(new Vector3(-12f, 55f, 1300f), builder.Spires[0].transform.position);
            Assert.AreEqual(new Vector3(8f, 70f, 8f), builder.Spires[0].transform.localScale);
            // Arch: pillars flank the anchor, lintel clears above.
            Assert.AreEqual(new Vector3(-8f, -3f, 1400f), builder.Islands[0].transform.position);
            Assert.AreEqual(new Vector3(8f, -3f, 1400f), builder.Islands[1].transform.position);
            Assert.AreEqual(new Vector3(0f, 8.5f, 1400f), builder.Islands[2].transform.position);
            // Coins derive from the authored ring (trail of 2).
            Assert.AreEqual(2, builder.Coins.Count);
        }

        [Test]
        public void LayoutBranch_IgnoresWallSeed()
        {
            var a = NewBuilder(); var b = NewBuilder();
            a.BuildChunk(LayoutSpec(), 1000f, 7);
            b.BuildChunk(LayoutSpec(), 1000f, 77);
            Assert.AreEqual(a.Rings[0].transform.position, b.Rings[0].transform.position);
            Assert.AreEqual(a.Islands[0].transform.position, b.Islands[0].transform.position);
            Assert.AreEqual(a.Spires[0].transform.position, b.Spires[0].transform.position);
            Assert.AreEqual(a.Coins[0].transform.position, b.Coins[0].transform.position);
        }

        [Test]
        public void LayoutBranch_SkipsBadEntries()
        {
            var builder = NewBuilder();
            var spec = LayoutSpec();
            var bad = new List<ChunkLayout.IslandEntry>(spec.Layout.Islands);
            bad.Add(new ChunkLayout.IslandEntry { Id = "nan", Position = new Vector3(float.NaN, 0f, 0f),
                Scale = Vector3.one * 10f, ShellName = "LayoutIso" });
            bad.Add(new ChunkLayout.IslandEntry { Id = "ghost", Position = new Vector3(0f, 0f, 50f),
                Scale = Vector3.one * 10f, ShellName = "NoSuchPrefab" });
            spec.Layout.Islands = bad.ToArray();
            // Skips log errors by design: expect both before building.
            LogAssert.Expect(LogType.Error,
                "[ChunkBuilder] layout 'Showcase1' skips invalid island 'nan'.");
            LogAssert.Expect(LogType.Error,
                "[ChunkBuilder] layout 'Showcase1' island 'ghost' shell does not resolve.");
            builder.BuildChunk(spec, 1000f, 7);
            Assert.AreEqual(1 + 3, builder.Islands.Count); // valid island + arch cubes only
        }

        [Test]
        public void Validate_RejectsSealedRing()
        {
            var spec = LayoutSpec();
            // Authored 10³ test volume moved onto the ring swallows it
            // (explicit Scale, not the bible default — the seal math is
            // what matters, not the numbers).
            spec.Layout.Islands[0].Position = new Vector3(5f, -3f, 100f);
            var errors = spec.Layout.Validate(spec);
            Assert.Greater(errors.Count, 0);
            Assert.IsTrue(errors[0].Contains("r1"), "error should name the sealed ring");
        }

        [Test]
        public void Validate_AcceptsClearLayout()
        {
            var spec = LayoutSpec();
            Assert.AreEqual(0, spec.Layout.Validate(spec).Count);
        }

        [Test]
        public void Validate_RejectsDuplicatesOutOfBoundsAndSchema()
        {
            var spec = LayoutSpec();
            spec.Layout.Rings = new ChunkLayout.RingEntry[]
            {
                new ChunkLayout.RingEntry { Id = "dup", Position = new Vector3(0f, -3f, 10f) },
                new ChunkLayout.RingEntry { Id = "dup", Position = new Vector3(999f, -3f, 10f) },
            };
            var errors = spec.Layout.Validate(spec);
            Assert.GreaterOrEqual(errors.Count, 2); // duplicate id + out of lane
            spec.Layout.Version = 999;
            Assert.Greater(
                spec.Layout.Validate(spec).Count, errors.Count); // + schema error
        }
    }
}
