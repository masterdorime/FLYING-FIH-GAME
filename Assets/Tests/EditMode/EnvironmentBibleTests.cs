using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using FlyingFishMomentum.Run;

namespace FlyingFishMomentum.Tests.EditMode
{
    public class EnvironmentBibleTests
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

        [Test]
        public void IslandCollider_MatchesBible()
        {
            var builder = NewBuilder();
            var spec = ScriptableObject.CreateInstance<ChunkSpec>();
            spec.ChunkId = "Bible"; spec.Skin = null;
            spec.Length = 500f; spec.IslandPairs = 1;
            builder.BuildChunk(spec, 0f, 9);
            Assert.AreEqual(2, builder.Islands.Count);
            Assert.AreEqual(new Vector3(13f, 20f, 13f), builder.Islands[0].transform.localScale);
            Assert.AreEqual(16.5f, Mathf.Abs(builder.Islands[0].transform.position.x), 0.01f);
        }

        [Test]
        public void SwimRings_RideTheSurfaceLane()
        {
            var builder = NewBuilder();
            var spec = ScriptableObject.CreateInstance<ChunkSpec>();
            spec.ChunkId = "Bible"; spec.Skin = null;
            spec.Length = 500f; spec.RingCount = 2; spec.RingSpacing = 150f;
            spec.CoinsPerTrail = 1;
            builder.BuildChunk(spec, 0f, 9);
            Assert.AreEqual(ChunkBuilder.SurfaceRingY, builder.Rings[0].transform.position.y, 0.01f);
            Assert.AreEqual(ChunkBuilder.SurfaceRingY, builder.Rings[1].transform.position.y, 0.01f);
        }
    }
}
