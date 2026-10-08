using UnityEngine;

namespace FlyingFishMomentum.Run
{
    // Water package task 1: deterministic foam art. Pure functions (no
    // AssetDatabase, no file IO) so EditMode tests pin them: same seed
    // paints byte-identical pixels. The editor entry point that writes
    // the PNG assets lives in Assets/Editor/WaterArtImport.cs.
    public static class WaterArt
    {
        // Tiling white streaks + dots over transparency. Every plot wraps
        // modulo size, so shapes continue across tile edges (the tiling is
        // seamless by construction). Calibrated for ~10m tiles: fine 1-6m
        // lines and flecks at ~10-20% white coverage (pinned 5-30% by
        // test) so minification leaves texture instead of glare.
        public static Texture2D PaintFoam(int size, int seed)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            var rng = new System.Random(seed);
            int streaks = size / 2;
            for (int n = 0; n < streaks; n++)
            {
                int y = rng.Next(size);
                int x0 = rng.Next(size);
                int len = size / 16 + rng.Next(size / 8);
                int thick = 1 + rng.Next(2);
                byte alpha = (byte)(150 + rng.Next(106));
                for (int i = 0; i < len; i++)
                    for (int t = 0; t < thick; t++)
                    {
                        int x = (x0 + i) % size;
                        int yy = (y + t) % size;
                        px[yy * size + x] = new Color32(255, 255, 255, alpha);
                    }
            }
            int dots = size;
            for (int n = 0; n < dots; n++)
            {
                int x = rng.Next(size);
                int y = rng.Next(size);
                int r = 2 + rng.Next(2);
                byte alpha = (byte)(140 + rng.Next(116));
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (dx * dx + dy * dy > r * r) continue;
                        int xx = (x + dx + size) % size;
                        int yy = (y + dy + size) % size;
                        px[yy * size + xx] = new Color32(255, 255, 255, alpha);
                    }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        // Soft radial white puff (opaque center, transparent edge).
        public static Texture2D PaintPuff(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    byte alpha = (byte)(255f * Mathf.Pow(a, 1.5f));
                    px[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
