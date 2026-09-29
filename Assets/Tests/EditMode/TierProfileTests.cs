using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FlyingFishMomentum.Tests.EditMode
{
    public class TierProfileTests
    {
        [Test]
        public void ProfileAssetsMatchPrdTables()
        {
            var guids = AssetDatabase.FindAssets("t:FlightTierProfile");
            Assert.AreEqual(5, guids.Length, "expected 5 tier profiles");
            Assert.AreEqual(17f, SpeedOf(guids, FlightTier.None));
            Assert.AreEqual(23f, SpeedOf(guids, FlightTier.Low));
            Assert.AreEqual(33f, SpeedOf(guids, FlightTier.Medium));
            Assert.AreEqual(47f, SpeedOf(guids, FlightTier.High));
            Assert.AreEqual(73f, SpeedOf(guids, FlightTier.Max));
        }

        [Test]
        public void MomentumSettingsMatchPrd()
        {
            var guids = AssetDatabase.FindAssets("t:MomentumSettings");
            Assert.AreEqual(1, guids.Length, "expected 1 momentum settings asset");
            var s = AssetDatabase.LoadAssetAtPath<MomentumSettings>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
            Assert.AreEqual(5f, s.MinSpeed);
            Assert.AreEqual(1.5f, s.DragSwimming);
            Assert.AreEqual(0.6f, s.DragFlying);
            Assert.AreEqual(20f, s.BreachSpeedThreshold);
        }

        static float SpeedOf(string[] guids, FlightTier tier)
        {
            foreach (var g in guids)
            {
                var p = AssetDatabase.LoadAssetAtPath<FlightTierProfile>(
                    AssetDatabase.GUIDToAssetPath(g));
                if (p != null && p.Tier == tier) return p.MaxSpeed;
            }
            Assert.Fail($"no profile asset for tier {tier}");
            return -1f;
        }
    }
}
