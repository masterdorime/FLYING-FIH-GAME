using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using FlyingFishMomentum.Run;

namespace FlyingFishMomentum.Tests.EditMode
{
    // W1: rings are gate models from game assets (asset colors, never
    // tinted). The builder fits the model opening to the judging disc
    // (radius 3, same disc CheckRingTrigger sweeps) and centers it on
    // the ring position. Null prefab keeps the legacy LineRenderer.
    public class RingModelTests
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

        // Stub with gate-like aspect (2 wide, 4 tall, 1 deep).
        private GameObject GateStub()
        {
            var root = new GameObject("GateStub");
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(root.transform, false);
            part.transform.localScale = new Vector3(2f, 4f, 1f);
            _spawned.Add(root);
            return root;
        }

        private static ChunkSpec RingSpec()
        {
            var spec = ScriptableObject.CreateInstance<ChunkSpec>();
            spec.ChunkId = "RingModel"; spec.Skin = null;
            spec.Length = 500f; spec.RingCount = 1; spec.RingSpacing = 150f;
            spec.CoinsPerTrail = 1;
            return spec;
        }

        [Test]
        public void CreateRing_WithPrefab_SpawnsMeshFittedToJudgingDisc()
        {
            var builder = NewBuilder();
            builder.Configure(null, null, null, GateStub());
            builder.BuildChunk(RingSpec(), 0f, 3);
            var ring = builder.Rings[0];
            Assert.IsNull(ring.GetComponent<LineRenderer>(), "legacy line still built");
            var mesh = ring.GetComponentInChildren<MeshRenderer>();
            Assert.IsNotNull(mesh, "no model child");
            var bounds = mesh.bounds;
            float disc = FlyingFishMomentum.TimingPromptSpawner.RingPromptRadius * 2f;
            Assert.AreEqual(disc, bounds.size.y, 0.01f, "opening height != judging disc diameter");
            Assert.AreEqual(0f, Vector3.Distance(bounds.center, ring.transform.position), 0.01f,
                "model not centered on ring position");
            Assert.Less(bounds.size.z, bounds.size.y, "model not thin along travel (z)");
        }

        [Test]
        public void CreateRing_WithoutExplicitPrefab_LoadsPipelineRing()
        {
            // No Configure injection: the builder falls back to the
            // committed pipeline prefab. If Ring.prefab goes missing this
            // fails (legacy line built instead) — regenerate it with
            // VisualImport.BuildRing.
            var builder = NewBuilder();
            builder.BuildChunk(RingSpec(), 0f, 3);
            var ring = builder.Rings[0];
            Assert.IsNull(ring.GetComponent<LineRenderer>(), "legacy line built despite Ring.prefab");
            var mesh = ring.GetComponentInChildren<MeshRenderer>();
            Assert.IsNotNull(mesh, "pipeline ring model not loaded");
            Assert.AreEqual("RingVisual_Ring", ring.transform.GetChild(0).name);
        }

        [Test]
        public void CreateRing_RendererlessPrefab_FallsBackToLine()
        {
            // Last-resort branch: configured prefab with no renderers
            // destroys the empty visual and builds the legacy line.
            var builder = NewBuilder();
            var empty = new GameObject("NoMesh");
            _spawned.Add(empty);
            builder.Configure(null, null, null, empty);
            builder.BuildChunk(RingSpec(), 0f, 3);
            Assert.IsNotNull(builder.Rings[0].GetComponent<LineRenderer>(), "no fallback line");
        }

        [Test]
        public void PipelineRing_ResolvesAtRuntimePath()
        {
            // Production (and bare test builders) load Ring.prefab from
            // Resources: fail fast here if the artifact goes missing.
            var prefab = Resources.Load<GameObject>("Ring");
            Assert.IsNotNull(prefab, "Assets/Resources/Ring.prefab missing — re-run VisualImport.BuildRing");
            Assert.IsNotNull(prefab.GetComponentInChildren<MeshRenderer>(), "Ring.prefab has no mesh");
        }

        [Test]
        public void PooledRing_KeepsModelAcrossReuse()
        {
            var builder = NewBuilder();
            builder.Configure(null, null, null, GateStub());
            builder.BuildChunk(RingSpec(), 0f, 3);
            builder.ReclaimBefore(100000f);
            builder.BuildChunk(RingSpec(), 0f, 3);
            var mesh = builder.Rings[0].GetComponentInChildren<MeshRenderer>();
            Assert.IsNotNull(mesh, "model lost on pool reuse");
            Assert.IsTrue(mesh.enabled, "model left disabled by Reset");
        }

        [Test]
        public void ApplyMood_Skin_LeavesRingAssetColors()
        {
            var builder = NewBuilder();
            var stub = GateStub();
            builder.Configure(null, null, null, stub);
            var skin = ScriptableObject.CreateInstance<RealmSkin>();
            skin.SkyPanorama = new Texture2D(4, 2);
            skin.RingTint = Color.magenta;
            var spec = RingSpec();
            spec.Skin = skin;
            var before = stub.GetComponentInChildren<MeshRenderer>().sharedMaterial.color;
            try
            {
                builder.BuildChunk(spec, 0f, 3);
                var mesh = builder.Rings[0].GetComponentInChildren<MeshRenderer>();
                Assert.IsNotNull(mesh, "no model child");
                Assert.AreEqual(before, mesh.sharedMaterial.color, "ring re-tinted by mood");
            }
            finally { RenderSettings.skybox = null; Object.DestroyImmediate(skin.SkyPanorama); }
        }
    }
}
