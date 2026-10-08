using NUnit.Framework;
using UnityEngine;
using FlyingFishMomentum.Run;

namespace FlyingFishMomentum.Tests.EditMode
{
    // Water package task 1: generated foam art must be deterministic
    // (same seed, byte-identical pixels), tile seamlessly, and carry
    // actual white foam over transparency.
    public class WaterArtTests
    {
        [Test]
        public void FoamTexture_IsDeterministicSeeded()
        {
            var a = WaterArt.PaintFoam(64, 7);
            var b = WaterArt.PaintFoam(64, 7);
            Assert.AreEqual(a.width, b.width);
            var pa = a.GetPixels32();
            var pb = b.GetPixels32();
            Assert.AreEqual(pa.Length, pb.Length);
            for (int i = 0; i < pa.Length; i++)
                Assert.AreEqual(pa[i], pb[i], "pixel " + i + " differs across runs");
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
        }

        [Test]
        public void FoamTexture_HasFoamOverTransparency()
        {
            var tex = WaterArt.PaintFoam(128, 7);
            try
            {
                var px = tex.GetPixels32();
                int white = 0, transparent = 0;
                foreach (var c in px)
                {
                    if (c.a > 128 && c.r > 200 && c.g > 200 && c.b > 200) white++;
                    if (c.a == 0) transparent++;
                }
                Assert.Greater(white, 100, "no foam painted");
                Assert.Greater(transparent, px.Length / 2, "background not mostly transparent");
            }
            finally { Object.DestroyImmediate(tex); }
        }

        [Test]
        public void BakedAssets_ExistMirrored()
        {
            // Paths mirror WaterArtImport.FoamPath/PuffPath (Editor class
            // invisible to this assembly): keep in sync by hand.
            foreach (var path in new string[] { "Assets/Resources/WaterFoam.png", "Assets/Resources/WaterPuff.png" })
            {
                var tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.IsNotNull(tex, "missing baked art (run WaterArtImport.WriteWaterArt): " + path);
                var importer = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
                Assert.IsNotNull(importer, "no importer for " + path);
                Assert.AreEqual(UnityEngine.TextureWrapMode.Mirror, importer.wrapMode,
                    "seamless tiling not set on " + path);
            }
        }

        [Test]
        public void WaterMaterial_HasEmissionFoam()
        {
            // Foam rides the emission map so it stays white under every
            // realm RGB tint (emission ignores the stamped base color).
            var builder = NewBuilder();
            var mat = builder.WaterMaterial;
            Assert.IsNotNull(mat.GetTexture("_EmissionMap"), "no foam emission on water");
        }

        [Test]
        public void SceneWaterMaterial_HasEmissionFoam()
        {
            var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M1Water.mat");
            Assert.IsNotNull(mat, "missing M1Water.mat");
            Assert.IsNotNull(mat.GetTexture("_EmissionMap"), "no foam emission on M1Water.mat");
            Assert.IsTrue(mat.IsKeywordEnabled("_EMISSION"), "emission keyword off on M1Water.mat");
            // Stale EmissiveIsBlack suppresses emission even with map +
            // keyword set (bisected): pin the realtime flag.
            Assert.AreEqual(UnityEngine.MaterialGlobalIlluminationFlags.RealtimeEmissive,
                mat.globalIlluminationFlags, "GI flag suppresses emission");
        }

        private GameObject _builderGo;

        private ChunkBuilder NewBuilder()
        {
            _builderGo = new GameObject("chunkBuilder");
            return _builderGo.AddComponent<ChunkBuilder>();
        }

        [TearDown]
        public void TearDownBuilders()
        {
            if (_builderGo != null) Object.DestroyImmediate(_builderGo);
        }

        [Test]
        public void PuffSprite_IsRadialWhite()
        {
            var tex = WaterArt.PaintPuff(64);
            try
            {
                // GetPixel returns normalized floats: center ≈0.96, corner 0.
                Assert.Greater(tex.GetPixel(32, 32).a, 0.8f, "center not opaque white");
                Assert.AreEqual(0f, tex.GetPixel(0, 0).a, "corner not transparent");
            }
            finally { Object.DestroyImmediate(tex); }
        }
    }
}
