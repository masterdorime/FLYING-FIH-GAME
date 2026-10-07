using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using FlyingFishMomentum.Run;

// Visual reconstruction Phase 0: headless art pipeline. Builds shared
// materials, one prefab per staged ArtVendor FBX (MeshFilter +
// MeshRenderer, no colliders), and the four RealmSkin assets. Idempotent:
// overwrite always, so re-runs converge. Run headless with:
//   unity run . -- -executeMethod VisualImport.BuildAll -quit
public static class VisualImport
{
    private const string PirateDir = "Assets/ArtVendor/KenneyPirate/Models/FBX format";
    private const string PirateTex = PirateDir + "/Textures/colormap.png";
    private const string NatureDir = "Assets/ArtVendor/KenneyNature/Models/FBX format";
    private const string DecorDir = "Assets/Prefabs/Decor";
    private const string MatDir = "Assets/Materials";
    private const string WatercraftDir = "Assets/ArtVendor/Kenney/Models/FBX format";
    private const string WatercraftTex = WatercraftDir + "/Textures/colormap.png";

    private static readonly string[] PirateModels = new string[]
    {
        "palm-bend", "palm-detailed-bend", "palm-detailed-straight", "palm-straight",
        "rocks-a", "rocks-b", "rocks-c", "rocks-sand-a", "rocks-sand-b", "rocks-sand-c",
        "castle-door", "castle-gate", "castle-wall", "castle-window",
        "tower-base", "tower-base-door", "tower-complete-large", "tower-complete-small",
        "tower-middle", "tower-middle-windows", "tower-roof", "tower-top", "tower-watch",
        "platform", "platform-planks", "structure", "structure-fence", "structure-fence-sides",
        "structure-platform", "structure-platform-dock", "structure-platform-dock-small",
        "structure-platform-small", "structure-roof",
        "flag", "flag-high", "flag-pennant", "flag-high-pennant",
        "chest", "ship-wreck", "grass", "grass-patch", "grass-plant",
        "patch-grass", "patch-grass-foliage", "patch-sand", "patch-sand-foliage",
    };

    // Nature FBX base name -> shared material asset name.
    private static readonly Dictionary<string, string> NatureModels = new Dictionary<string, string>
    {
        { "rock_largeA", "V1NatureRock" }, { "rock_largeB", "V1NatureRock" },
        { "rock_smallA", "V1NatureRock" }, { "rock_smallB", "V1NatureRock" },
        { "rock_tallA", "V1NatureRock" }, { "rock_tallB", "V1NatureRock" }, { "rock_tallC", "V1NatureRock" },
        { "cliff_rock", "V1NatureRock" }, { "cliff_large_rock", "V1NatureRock" },
        { "tree_palm", "V1NatureFoliage" }, { "tree_palmTall", "V1NatureFoliage" },
        { "tree_palmShort", "V1NatureFoliage" }, { "tree_palmBend", "V1NatureFoliage" },
        { "tree_pineTallA", "V1NatureFoliage" }, { "tree_cone", "V1NatureFoliage" },
        { "tree_simple", "V1NatureFoliage" },
        { "plant_bush", "V1NatureGrass" }, { "plant_bushSmall", "V1NatureGrass" },
        { "grass", "V1NatureGrass" }, { "grass_large", "V1NatureGrass" },
        { "platform_beach", "V1NatureSand" }, { "platform_grass", "V1NatureGrass" },
        { "statue_ring", "V1NatureStatue" },
        { "cliff_waterfall_rock", "V1NatureWater" }, { "cliff_waterfallTop_rock", "V1NatureWater" },
    };

    [MenuItem("FlyingFish/Import Visual Art")]
    public static void BuildAll()
    {
        Directory.CreateDirectory(DecorDir);
        // All prefabs here are generated: wipe first so re-runs converge
        // (also resolves pirate/nature base-name collisions like grass).
        foreach (var stale in Directory.GetFiles(DecorDir, "*.prefab"))
            File.Delete(stale);
        AssetDatabase.Refresh();

        var pirateMat = GetOrCreateMaterial("V1Pirate",
            new Color(1f, 1f, 1f),
            AssetDatabase.LoadAssetAtPath<Texture2D>(PirateTex));
        var mats = new Dictionary<string, Material>
        {
            { "V1Pirate", pirateMat },
            { "V1NatureRock", GetOrCreateMaterial("V1NatureRock", new Color(0.45f, 0.46f, 0.48f), null) },
            { "V1NatureFoliage", GetOrCreateMaterial("V1NatureFoliage", new Color(0.25f, 0.55f, 0.3f), null) },
            { "V1NatureGrass", GetOrCreateMaterial("V1NatureGrass", new Color(0.3f, 0.6f, 0.32f), null) },
            { "V1NatureSand", GetOrCreateMaterial("V1NatureSand", new Color(0.8f, 0.7f, 0.5f), null) },
            { "V1NatureStatue", GetOrCreateMaterial("V1NatureStatue", new Color(0.75f, 0.76f, 0.8f), null) },
            { "V1NatureWater", GetOrCreateMaterial("V1NatureWater", new Color(0.3f, 0.65f, 0.85f), null) },
        };

        int prefabs = 0;
        var prefabByName = new Dictionary<string, GameObject>();
        foreach (var baseName in PirateModels)
            prefabByName[baseName] = BuildPrefab(PirateDir + "/" + baseName + ".fbx", baseName, mats["V1Pirate"], DecorDir, ref prefabs);
        foreach (var kv in NatureModels)
            prefabByName[kv.Key] = BuildPrefab(NatureDir + "/" + kv.Key + ".fbx", "nature-" + kv.Key, mats[kv.Value], DecorDir, ref prefabs);

        BuildSkin("Lagoon", "skybox-day.png",
            new string[] { "tree_palm", "tree_palmTall", "tree_palmShort", "tree_palmBend", "palm-bend", "palm-straight", "palm-detailed-bend", "palm-detailed-straight", "patch-sand", "patch-sand-foliage", "ship-wreck" },
            new string[] { "platform_beach", "rock_largeA" },
            new string[] { "rock_tallA", "rock_tallB" },
            new string[] { "rocks-sand-a", "rocks-sand-b" },
            prefabByName);
        BuildSkin("Gauntlet", "skybox-morning.png",
            new string[] { "flag", "flag-high", "flag-pennant", "chest", "castle-wall", "tower-complete-small", "structure-fence", "grass-patch" },
            new string[] { "platform_grass", "cliff_rock" },
            new string[] { "tower-complete-small", "rock_tallC" },
            new string[] { "castle-gate", "castle-wall", "tower-base" },
            prefabByName);
        BuildSkin("Storm", "skybox-night.png",
            new string[] { "rocks-a", "rocks-b", "rocks-c", "tree_pineTallA", "tree_cone", "rock_smallA", "rock_smallB" },
            new string[] { "rock_largeB", "cliff_rock" },
            new string[] { "rock_tallA", "rock_tallC" },
            new string[] { "rocks-a", "rocks-b" },
            prefabByName);
        BuildSkin("Sky", "skybox-alien.png",
            new string[] { "statue_ring", "platform_grass", "grass_large" },
            new string[] { "platform_grass", "statue_ring" },
            new string[] { "rock_tallB", "statue_ring" },
            new string[0],
            prefabByName);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[VisualImport] done: " + prefabs + " prefabs, 4 skins.");
        BuildRing();
    }

    private static Material GetOrCreateMaterial(string name, Color color, Texture2D tex)
    {
        string path = MatDir + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = color;
        mat.SetTexture("_MainTex", tex);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static GameObject BuildPrefab(string fbxPath, string prefabName, Material mat, string outDir, ref int count)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (model == null)
        {
            Debug.LogError("[VisualImport] missing FBX: " + fbxPath);
            return null;
        }
        var srcFilter = model.GetComponentInChildren<MeshFilter>();
        if (srcFilter == null || srcFilter.sharedMesh == null)
        {
            Debug.LogError("[VisualImport] no mesh in FBX: " + fbxPath);
            return null;
        }
        // Pipeline telemetry: mesh fidelity evidence (multi-mesh truncation
        // and vertex-color questions are review findings until this proves
        // otherwise). Loud on anything but a single mesh.
        var allFilters = model.GetComponentsInChildren<MeshFilter>();
        var mesh = srcFilter.sharedMesh;
        bool hasColors = mesh.colors != null && mesh.colors.Length > 0;
        var srcRenderer = srcFilter.GetComponent<MeshRenderer>();
        int slotCount = srcRenderer != null && srcRenderer.sharedMaterials != null
            ? srcRenderer.sharedMaterials.Length : 0;
        if (allFilters.Length > 1 || hasColors || slotCount > 1)
            Debug.LogWarning("[VisualImport] rich FBX " + fbxPath + ": meshes=" + allFilters.Length
                + " vertexColors=" + hasColors + " materialSlots=" + slotCount);
        // Full-hierarchy clone: multi-mesh models (ship-wreck, chest)
        // and multi-slot meshes (nature rocks/trees) keep ALL geometry.
        // Every renderer slot gets the set's single shared material —
        // probe evidence: no staged FBX carries vertex colors, so flat
        // set tints are the cohesive palette (spec §3 amendment).
        var go = Object.Instantiate(model);
        go.name = prefabName;
        foreach (var col in go.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(col);
        foreach (var renderer in go.GetComponentsInChildren<MeshRenderer>(true))
        {
            var slots = renderer.sharedMaterials;
            for (int i = 0; i < slots.Length; i++) slots[i] = mat;
            renderer.sharedMaterials = slots;
        }
        string prefabPath = outDir + "/" + prefabName + ".prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        count++;
        return prefab;
    }

    // W1: the ring prompt is the watercraft gate arch (user-approved
    // model, verified opening faces ±z). Builds V1Watercraft (white +
    // colormap, same pattern as pirate) and Assets/Resources/Ring.prefab.
    // Resources (not Prefabs) so ChunkBuilder loads it at runtime with
    // no scene rebuild; explicit Configure injection still wins for tests.
    // Non-destructive: never touches DecorDir, safe to re-run alone.
    [MenuItem("FlyingFish/Import Ring Prefab")]
    public static void BuildRing()
    {
        var mat = GetOrCreateMaterial("V1Watercraft", new Color(1f, 1f, 1f),
            AssetDatabase.LoadAssetAtPath<Texture2D>(WatercraftTex));
        int count = 0;
        Directory.CreateDirectory("Assets/Resources");
        var ring = BuildPrefab(WatercraftDir + "/gate.fbx", "Ring", mat, "Assets/Resources", ref count);
        // CreateRing fits/counters by uniform scale on an identity root:
        // fail the import (not the game) if the model ever violates it.
        if (ring != null && (ring.transform.localScale != Vector3.one
            || ring.transform.localRotation != Quaternion.identity))
            Debug.LogError("[VisualImport] Ring prefab root must be identity scale/rotation.");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[VisualImport] ring done.");
    }

    private static void BuildSkin(string realm, string panoramaFile,
        string[] decor, string[] islands, string[] spires, string[] arches,
        Dictionary<string, GameObject> prefabByName)
    {
        var mood = AssetDatabase.LoadAssetAtPath<ChunkSpec>("Assets/Configs/ChunkSpec_" + realm + ".asset");
        if (mood == null)
        {
            Debug.LogError("[VisualImport] missing ChunkSpec for " + realm);
            return;
        }
        var skin = ScriptableObject.CreateInstance<RealmSkin>();
        skin.SkyPanorama = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/ArtVendor/Kenney/Skyboxes/" + panoramaFile);
        skin.SkyTint = mood.SkyTint;
        skin.FogColor = mood.FogColor;
        skin.FogDensity = mood.FogDensity;
        skin.WaterTint = mood.WaterTint;
        skin.DecorPrefabs = Resolve(decor, prefabByName);
        skin.IslandPrefabs = Resolve(islands, prefabByName);
        skin.SpirePrefabs = Resolve(spires, prefabByName);
        skin.ArchPrefabs = Resolve(arches, prefabByName);
        string path = "Assets/Configs/RealmSkin_" + realm + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<RealmSkin>(path);
        RealmSkin skinRef;
        if (existing != null)
        {
            EditorUtility.CopySerialized(skin, existing);
            Object.DestroyImmediate(skin);
            skinRef = existing;
        }
        else
        {
            AssetDatabase.CreateAsset(skin, path);
            skinRef = skin;
        }
        // Wire the skin into the chunk spec: without this the production
        // BuildChunk path stays on legacy primitives (review finding).
        if (mood.Skin != skinRef)
        {
            mood.Skin = skinRef;
            EditorUtility.SetDirty(mood);
        }
    }

    private static GameObject[] Resolve(string[] names, Dictionary<string, GameObject> prefabByName)
    {
        var list = new List<GameObject>(names.Length);
        foreach (var n in names)
        {
            GameObject p;
            if (!prefabByName.TryGetValue(n, out p) || p == null)
                Debug.LogError("[VisualImport] unresolved prefab: " + n);
            else
                list.Add(p);
        }
        return list.ToArray();
    }
}
