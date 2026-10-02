using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FlyingFishMomentum.Tests.EditMode
{
    public class CameraPresentationTests
    {
        static FlightTierProfile Tier(string name) =>
            AssetDatabase.LoadAssetAtPath<FlightTierProfile>(
                "Assets/Configs/TierProfile_" + name + ".asset");

        [Test]
        public void TierCameraDistanceConstant()
        {
            // Constant-size fish: every tier holds the same chase distance.
            foreach (var t in new[] { "None", "Low", "Medium", "High", "Max" })
            {
                var p = Tier(t);
                Assert.IsNotNull(p, t);
                Assert.AreEqual(11f, p.CameraDistance, 0.001f, t);
            }
        }

        [Test]
        public void TierFovLadderIsMild()
        {
            // Speed zoom noticeable but small: 60 → 70 across tiers.
            var want = new System.Collections.Generic.Dictionary<string, float>
            {
                { "None", 60f }, { "Low", 62f }, { "Medium", 64f },
                { "High", 67f }, { "Max", 70f },
            };
            foreach (var kv in want)
                Assert.AreEqual(kv.Value, Tier(kv.Key).CameraFOV, 0.001f, kv.Key);
        }

        [Test]
        public void CameraSettingsMildMax()
        {
            var s = AssetDatabase.LoadAssetAtPath<CameraSettings>(
                "Assets/Configs/CameraSettings.asset");
            Assert.IsNotNull(s);
            Assert.AreEqual(72f, s.MaxFOV, 0.001f);
        }
    }
}
