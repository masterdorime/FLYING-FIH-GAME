using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FlyingFishMomentum.Run;

// W0: headless visual verification. Opens the real M1 scene (light,
// sea, sky rig all live there), builds one chunk per bookmark spec at
// fixed seeds, frames each bookmark subject from the built content,
// and writes PNGs to Temp/ (never committed) for agent inspection:
//   unity run . -- -executeMethod VisualVerify.CaptureAll
// The scene is never saved. Rendering goes through an offscreen
// RenderTexture so no window or Play mode is needed.
public static class VisualVerify
{
    private const string M1Scene = "Assets/Scenes/M1_MovementProof.unity";
    private const int Width = 960;
    private const int Height = 540;

    [MenuItem("FlyingFish/Capture Visual Verify")]
    public static void CaptureAll()
    {
        string outDir = "Temp/VisualVerify";
        Directory.CreateDirectory(outDir);
        // Never trash the user's open work: opening the M1 scene drops
        // the current one. Abort instead and let them save first.
        if (EditorSceneManager.GetActiveScene().isDirty)
        {
            Debug.LogError("[VisualVerify] open scene has unsaved changes — save first, then re-run.");
            return;
        }
        EditorSceneManager.OpenScene(M1Scene);
        var builder = Object.FindFirstObjectByType<ChunkBuilder>();
        if (builder == null)
        {
            Debug.LogError("[VisualVerify] no ChunkBuilder in " + M1Scene);
            return;
        }
        // Mood is global RenderSettings: build each distinct (spec,
        // seed) chunk and shoot its frames immediately, so every frame
        // carries its own realm's sky/fog/ambient. Chunks stay
        // contiguous like production streaming.
        var camGo = new GameObject("VerifyCam");
        var cam = camGo.AddComponent<Camera>();
        cam.farClipPlane = 4000f;
        var rt = new RenderTexture(Width, Height, 24);
        try
        {
        var done = new HashSet<string>();
        // Verification chunks live at z>=2000: the M1 scene bakes legacy
        // slalom islands at z=30..90 (Island_West/East_*) that would
        // photobomb chunk framing. Water/seabed span z 0..4000 and the
        // sky shell is centered at z=2000, so 2000+ stays in-world.
        float z = 2000f;
        foreach (var b in VisualVerifyPlan.Bookmarks)
        {
            string key = b.Spec + ":" + b.Seed;
            if (done.Contains(key)) continue;
            var spec = AssetDatabase.LoadAssetAtPath<ChunkSpec>(
                "Assets/Configs/ChunkSpec_" + b.Spec + ".asset");
            if (spec == null)
            {
                Debug.LogError("[VisualVerify] missing spec for " + b.Spec);
                return;
            }
            int ringsBefore = builder.Rings.Count;
            int islandsBefore = builder.Islands.Count;
            int spiresBefore = builder.Spires.Count;
            Debug.Log("[VisualVerify] chunk " + key + " Length=" + spec.Length
                + " rings=" + spec.RingCount + "+" + spec.SkyRingCount
                + " arches=" + spec.ArchCount + " islands=" + spec.IslandPairs
                + " spires=" + spec.SkySpireCount + " zStart=" + z);
            builder.BuildChunk(spec, z, b.Seed);
            float zStart = z;
            z += spec.Length;
            AuditChunk(builder, key, islandsBefore, builder.Islands.Count,
                spiresBefore, builder.Spires.Count);
            done.Add(key);
            // Shoot every bookmark frame belonging to this chunk now,
            // while its mood is still applied.
            foreach (var f in VisualVerifyPlan.Bookmarks)
            {
                if (f.Spec + ":" + f.Seed != key) continue;
                Frame(cam, builder, f, zStart, ringsBefore, islandsBefore, spiresBefore);
                Shoot(cam, rt, Path.Combine(outDir, f.Name + ".png"));
            }
        }
        Debug.Log("[VisualVerify] done: " + VisualVerifyPlan.Bookmarks.Length + " frames in " + outDir);
        Debug.LogWarning("[VisualVerify] scene holds verification chunks + VerifyCam — do NOT save the scene.");
        }
        finally
        {
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(rt);
        }
    }

    private static void Frame(Camera cam, ChunkBuilder builder,
        VisualVerifyPlan.Bookmark b, float zStart,
        int ringsBefore, int islandsBefore, int spiresBefore)
    {
        Vector3 look = new Vector3(0f, 2f, zStart + 90f);
        Vector3 pos = new Vector3(0f, 10f, zStart - 30f);
        switch (b.Subject)
        {
            case VisualVerifyPlan.Subject.FirstRing:
                if (builder.Rings.Count > ringsBefore)
                    Aim(builder.Rings[ringsBefore].transform.position,
                        new Vector3(0f, 2.5f, -13f), out pos, out look);
                break;
            case VisualVerifyPlan.Subject.FirstArch:
                // Arch pillars land in Islands; the gate center sits
                // one pillar offset (+x) from the first pillar.
                if (builder.Islands.Count > islandsBefore)
                    Aim(builder.Islands[islandsBefore].transform.position + new Vector3(8f, 2f, 0f),
                        new Vector3(0f, 4f, -18f), out pos, out look);
                break;
            case VisualVerifyPlan.Subject.FirstIsland:
                if (builder.Islands.Count > islandsBefore)
                    Aim(builder.Islands[islandsBefore].transform.position,
                        new Vector3(0f, 5f, -22f), out pos, out look);
                break;
            case VisualVerifyPlan.Subject.FirstSpire:
                if (builder.Spires.Count > spiresBefore)
                    Aim(builder.Spires[spiresBefore].transform.position,
                        new Vector3(0f, 6f, -26f), out pos, out look);
                break;
        }
        cam.transform.position = pos;
        cam.transform.LookAt(look);
    }

    // Shell audit: every rock cube this chunk placed should carry a
    // ChunkShell_ visual with its own renderer off. Bare cubes render
    // as flat gray RockMaterial boxes (the in-game "mismatched" look),
    // so log the split per chunk instead of guessing from pixels.
    private static void AuditChunk(ChunkBuilder builder, string key,
        int islandsBefore, int islandsAfter, int spiresBefore, int spiresAfter)
    {
        int bare = 0, shelled = 0;
        for (int i = islandsBefore; i < islandsAfter; i++)
            Tally(builder.Islands[i], ref bare, ref shelled);
        for (int i = spiresBefore; i < spiresAfter; i++)
            Tally(builder.Spires[i], ref bare, ref shelled);
        Debug.Log("[VisualVerify] " + key + " cubes shelled=" + shelled + " bare=" + bare);
    }

    private static void Tally(GameObject go, ref int bare, ref int shelled)
    {
        bool hasShell = false;
        string shellName = "-";
        if (go != null)
            foreach (Transform c in go.transform)
                if (c.name.StartsWith("ChunkShell_")) { hasShell = true; shellName = c.name; break; }
        var rend = go != null ? go.GetComponent<MeshRenderer>() : null;
        if (hasShell && (rend == null || !rend.enabled)) shelled++;
        else bare++;
        Debug.Log("[VisualVerify] cube name=" + (go != null ? go.name : "null")
            + " pos=" + (go != null ? go.transform.position.ToString() : "-")
            + " scale=" + (go != null ? go.transform.localScale.ToString() : "-")
            + " shell=" + shellName
            + " cubeRenderer=" + (rend != null ? rend.enabled.ToString() : "none"));
    }

    private static void Aim(Vector3 target, Vector3 offset, out Vector3 pos, out Vector3 look)
    {
        look = target;
        pos = target + offset;
    }

    private static void Shoot(Camera cam, RenderTexture rt, string path)
    {
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Debug.Log("[VisualVerify] " + path);
    }
}
