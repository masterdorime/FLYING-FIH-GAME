using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FlyingFishMomentum.Tests.EditMode
{
    public class FlightGaugeSettingsTests
    {
        [Test]
        public void TableMatchesSpec()
        {
            var s = ScriptableObject.CreateInstance<FlightGaugeSettings>();
            Assert.AreEqual(120f, s.MaxGauge, 0.001f);
            Assert.AreEqual(0f, s.StartGauge, 0.001f);
            Assert.AreEqual(FlightTier.None, s.TierFor(0f));
            Assert.AreEqual(FlightTier.Low, s.TierFor(20f));
            Assert.AreEqual(FlightTier.High, s.TierFor(70f));
            Assert.AreEqual(FlightTier.Max, s.TierFor(120f));
        }

        [Test]
        public void AssetFileLoadsWithSpecValues()
        {
            var asset = AssetDatabase.LoadAssetAtPath<FlightGaugeSettings>(
                "Assets/Configs/FlightGaugeSettings.asset");
            Assert.IsNotNull(asset, "FlightGaugeSettings.asset failed to import");
            Assert.AreEqual(3.5f, asset.FlyDrainPerSecond, 0.001f);
            Assert.AreEqual(10f, asset.MissDrain, 0.001f);
        }
    }
}
