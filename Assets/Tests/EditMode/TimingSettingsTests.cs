using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FlyingFishMomentum.Tests.EditMode
{
    public class TimingSettingsTests
    {
        [Test]
        public void TableMatchesPRD()
        {
            var s = ScriptableObject.CreateInstance<TimingSettings>();
            Assert.AreEqual(60f, s.PromptEveryMeters, 0.001f);
            Assert.AreEqual(1f, s.LeadTime, 0.001f);
            Assert.AreEqual(0.07f, s.PerfectWindow, 0.0001f);
            Assert.AreEqual(16f, s.DeltaFor(TimingResult.Perfect, FlightTier.Medium), 0.001f);
            Assert.AreEqual(8f, s.DeltaFor(TimingResult.Good, FlightTier.Medium), 0.001f);
            Assert.AreEqual(-28f, s.DeltaFor(TimingResult.Miss, FlightTier.Medium), 0.001f);
            Assert.AreEqual(0f, s.DeltaFor(TimingResult.Perfect, FlightTier.None), 0.001f);
        }

        [Test]
        public void AssetFileLoadsWithPRDValues()
        {
            // Pin test for the hand-written YAML (never watched fail).
            var asset = AssetDatabase.LoadAssetAtPath<TimingSettings>(
                "Assets/Configs/TimingSettings.asset");
            Assert.IsNotNull(asset, "TimingSettings.asset failed to import");
            Assert.AreEqual(60f, asset.PromptEveryMeters, 0.001f);
            Assert.AreEqual(0.06f, asset.LateBuffer, 0.0001f);
            Assert.AreEqual(30f, asset.DeltaFor(TimingResult.Perfect, FlightTier.Max), 0.001f);
            Assert.AreEqual(-70f, asset.DeltaFor(TimingResult.Miss, FlightTier.Max), 0.001f);
        }
    }
}
