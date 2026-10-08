using NUnit.Framework;
using UnityEngine;
using FlyingFishMomentum;

namespace FlyingFishMomentum.Tests.EditMode
{
    // Water package task 3: the fish drags a fading white wake ribbon
    // (bubble trail underwater, foam wake at the surface — same ribbon).
    public class FishWakeTests
    {
        private GameObject _fish;

        [TearDown]
        public void TearDown()
        {
            if (_fish != null) Object.DestroyImmediate(_fish);
        }

        [Test]
        public void Wake_BuildsFadingTrail()
        {
            _fish = new GameObject("Fish");
            var wake = _fish.AddComponent<FishWake>();
            Assert.IsNotNull(wake);
            // Explicit build: AddComponent does not synchronously fire
            // Awake in every context (verified headless); Awake calls
            // this same path in play.
            var trail = wake.EnsureTrail();
            Assert.IsNotNull(trail, "no wake ribbon built");
            Assert.AreSame(trail, _fish.GetComponent<TrailRenderer>());
            Assert.IsTrue(trail.emitting, "trail not emitting");
            Assert.AreEqual(1.2f, trail.time, 0.01f);
            Assert.Greater(trail.startWidth, trail.endWidth, "ribbon does not taper");
            Assert.IsNotNull(trail.sharedMaterial, "ribbon has no material");
        }

        [Test]
        public void Wake_ReusesExistingTrail()
        {
            _fish = new GameObject("Fish");
            _fish.AddComponent<TrailRenderer>();
            _fish.AddComponent<FishWake>();
            Assert.AreEqual(1, _fish.GetComponents<TrailRenderer>().Length, "doubled the trail");
        }
    }
}
