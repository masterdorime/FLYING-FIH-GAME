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

    // W1 verify: identify the circled swim-ring model among the
    // watercraft set without opening the Editor. Logs local-space
    // mesh bounds per FBX: a torus ring reads as roughly equal bounds
    // on two axes with a thin third (its plane), e.g. gate frames read
    // wide/flat. Run: unity run . -- -executeMethod VisualVerify.ProbeWatercraft
    public static void ProbeWatercraft()
    {
        string dir = "Assets/ArtVendor/Kenney/Models/FBX format";
        string[] models = new string[]
        {
            "arrow-standing", "arrow", "buoy-flag", "buoy",
            "cargo-container-a", "cargo-container-b", "cargo-container-c",
            "cargo-pile-a", "cargo-pile-b", "gate-finish", "gate",
            "ramp-wide", "ramp", "ship-cargo-a", "ship-cargo-b",
            "ship-cargo-c", "ship-large", "ship-ocean-liner-small",
            "ship-small-ghost", "ship-small",
        };
        foreach (var baseName in models)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(dir + "/" + baseName + ".fbx");
            if (model == null) { Debug.LogWarning("[Probe] missing FBX: " + baseName); continue; }
            var filters = model.GetComponentsInChildren<MeshFilter>();
            foreach (var f in filters)
            {
                if (f == null || f.sharedMesh == null) continue;
                var b = f.sharedMesh.bounds;
                var rend = f.GetComponent<MeshRenderer>();
                int slots = rend != null && rend.sharedMaterials != null ? rend.sharedMaterials.Length : 0;
                Debug.Log("[Probe] " + baseName + " mesh=" + f.sharedMesh.name
                    + " size=" + b.size.ToString("F2") + " center=" + b.center.ToString("F2")
                    + " tris=" + (f.sharedMesh.triangles.Length / 3) + " slots=" + slots);
            }
        }
    }

    // Phase (i) proof: builds one showcase chunk from an in-memory
    // layout (real Gauntlet skin + prefab names, no committed content)
    // and shoots ring + arch frames. Run: unity run . --
    // -executeMethod VisualVerify.CaptureSampleLayout
    public static void CaptureSampleLayout()
    {
        EditorSceneManager.OpenScene(M1Scene);
        var builder = Object.FindFirstObjectByType<ChunkBuilder>();
        if (builder == null)
        {
            Debug.LogError("[VisualVerify] no ChunkBuilder in " + M1Scene);
            return;
        }
        var spec = AssetDatabase.LoadAssetAtPath<ChunkSpec>("Assets/Configs/ChunkSpec_Gauntlet.asset");
        if (spec == null) { Debug.LogError("[VisualVerify] missing Gauntlet spec"); return; }
        var layout = ScriptableObject.CreateInstance<ChunkLayout>();
        layout.LayoutId = "ShowcaseSample";
        layout.Seed = 4;
        layout.Rings = new ChunkLayout.RingEntry[]
        {
            new ChunkLayout.RingEntry { Id = "r1", Position = new Vector3(0f, -3f, 150f) },
            new ChunkLayout.RingEntry { Id = "r2", Position = new Vector3(4f, -3f, 300f) },
        };
        layout.Arches = new ChunkLayout.ArchEntry[]
        {
            new ChunkLayout.ArchEntry { Id = "a1", Anchor = new Vector3(0f, -3f, 150f),
                PillarShell = "tower-base", LintelShell = "castle-gate" },
        };
        layout.Islands = new ChunkLayout.IslandEntry[]
        {
            new ChunkLayout.IslandEntry { Id = "i1", Position = new Vector3(-15f, 5f, 450f),
                Scale = new Vector3(10f, 25f, 10f), ShellName = "nature-cliff_rock" },
            new ChunkLayout.IslandEntry { Id = "i2", Position = new Vector3(15f, 5f, 450f),
                Scale = new Vector3(10f, 25f, 10f), ShellName = "nature-platform_grass" },
        };
        layout.Spires = new ChunkLayout.SpireEntry[]
        {
            new ChunkLayout.SpireEntry { Id = "s1", Position = new Vector3(13f, 55f, 600f),
                Height = 70f, ShellName = "nature-rock_tallC" },
        };
        var errors = layout.Validate(spec);
        if (errors.Count > 0)
        {
            foreach (var e in errors) Debug.LogError("[SampleLayout] " + e);
            Object.DestroyImmediate(layout);
            return;
        }
        spec.Layout = layout; // in-memory only: the scene is never saved
        var camGo = new GameObject("VerifyCam");
        var cam = camGo.AddComponent<Camera>();
        cam.farClipPlane = 4000f;
        var rt = new RenderTexture(Width, Height, 24);
        try
        {
        builder.BuildChunk(spec, 2000f, 7);
        System.IO.Directory.CreateDirectory("Temp/VisualVerify");
        var ringMark = new VisualVerifyPlan.Bookmark
            { Name = "showcase-ring", Spec = "Gauntlet", Seed = 7, Subject = VisualVerifyPlan.Subject.FirstRing };
        Frame(cam, builder, ringMark, 2000f, 0, 0, 0);
        Shoot(cam, rt, "Temp/VisualVerify/showcase-ring.png");
        var archMark = new VisualVerifyPlan.Bookmark
            { Name = "showcase-arch", Spec = "Gauntlet", Seed = 7, Subject = VisualVerifyPlan.Subject.FirstArch };
        Frame(cam, builder, archMark, 2000f, 0, 0, 0);
        Shoot(cam, rt, "Temp/VisualVerify/showcase-arch.png");
        }
        finally
        {
            spec.Layout = null;
            Object.DestroyImmediate(layout);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(rt);
        }
    }

    // W1 verify, part 2: the circled swim ring must be one of the
    // watercraft FBX but bounds alone can't tell which (buoy reads
    // tall, buoy-flag has a pole). Stage buoy / buoy-flag / gate side
    // by side on the water and shoot one frame for visual ID:
    // unity run . -- -executeMethod VisualVerify.ProbeCandidates
    public static void ProbeCandidates()
    {
        string dir = "Assets/ArtVendor/Kenney/Models/FBX format";
        EditorSceneManager.OpenScene(M1Scene);
        StageCandidate(dir, "buoy", new Vector3(-8f, 0f, 2052f));
        StageCandidate(dir, "buoy-flag", new Vector3(0f, 0f, 2052f));
        StageCandidate(dir, "gate", new Vector3(9f, 0f, 2052f));
        var camGo = new GameObject("ProbeCam");
        var cam = camGo.AddComponent<Camera>();
        cam.farClipPlane = 4000f;
        cam.transform.position = new Vector3(0f, 4f, 2034f);
        cam.transform.LookAt(new Vector3(1f, 1f, 2052f));
        var rt = new RenderTexture(Width, Height, 24);
        Directory.CreateDirectory("Temp/VisualVerify");
        Shoot(cam, rt, "Temp/VisualVerify/candidates.png");
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(rt);
    }

    private static void StageCandidate(string dir, string baseName, Vector3 pos)
    {
        StageCandidate(dir, baseName, pos, null);
    }

    // Colored variant: paint every renderer slot with the set's colormap
    // (white base, like the V1Pirate pipeline material) so ID frames show
    // true Kenney colors instead of flat FBX diffuse.
    private static void StageCandidate(string dir, string baseName, Vector3 pos, Texture2D colormap)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(dir + "/" + baseName + ".fbx");
        if (model == null) { Debug.LogWarning("[Probe] missing FBX: " + baseName); return; }
        var go = Object.Instantiate(model);
        go.name = "Probe_" + baseName;
        go.transform.position = pos;
        if (colormap != null)
        {
            var mat = new Material(Shader.Find("Standard"));
            mat.color = Color.white;
            mat.SetTexture("_MainTex", colormap);
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = mat;
                r.sharedMaterials = slots;
            }
        }
        Debug.Log("[Probe] staged " + baseName + " at " + pos.ToString());
    }

    // W1 verify, part 3: gate vs gate-finish in TRUE colors (colormap
    // on) to match the user's circled orange/white ring:
    // unity run . -- -executeMethod VisualVerify.ProbeGates
    public static void ProbeGates()
    {
        string dir = "Assets/ArtVendor/Kenney/Models/FBX format";
        var colormap = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/Textures/colormap.png");
        if (colormap == null) { Debug.LogError("[Probe] missing watercraft colormap"); return; }
        EditorSceneManager.OpenScene(M1Scene);
        StageCandidate(dir, "gate", new Vector3(-5f, 0f, 2052f), colormap);
        StageCandidate(dir, "gate-finish", new Vector3(5f, 0f, 2052f), colormap);
        var camGo = new GameObject("ProbeCam");
        var cam = camGo.AddComponent<Camera>();
        cam.farClipPlane = 4000f;
        cam.transform.position = new Vector3(0f, 3.5f, 2038f);
        cam.transform.LookAt(new Vector3(0f, 2f, 2052f));
        var rt = new RenderTexture(Width, Height, 24);
        Directory.CreateDirectory("Temp/VisualVerify");
        Shoot(cam, rt, "Temp/VisualVerify/gates.png");
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(rt);
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
