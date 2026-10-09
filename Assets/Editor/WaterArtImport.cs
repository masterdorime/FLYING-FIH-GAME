using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FlyingFishMomentum;
using FlyingFishMomentum.Run;

// Water package task 1: bakes WaterArt painters to committed PNG
// assets with mirrored wrap (provably seamless tiling for any
// content) and refreshes the database. Invoke:
//   unity run . -- -executeMethod WaterArtImport.WriteWaterArt
public static class WaterArtImport
{
    // Runtime-loaded (Resources, like the ring prefab): the builder
    // and the fish wake load these without scene wiring.
    public const string FoamPath = "Assets/Resources/WaterFoam.png";
    public const string PuffPath = "Assets/Resources/WaterPuff.png";

    [MenuItem("FlyingFish/Bake Water Art")]
    public static void WriteWaterArt()
    {
        Write(WaterArt.PaintFoam(256, 7), FoamPath);
        Write(WaterArt.PaintPuff(64), PuffPath);
        WireWaterMaterial();
        AssetDatabase.Refresh();
        Debug.Log("[WaterArtImport] baked foam + puff.");
    }

    // Keywords are Unity-owned (hand-edited m_ValidKeywords gets pruned
    // on import): wire emission through the API so it sticks.
    // Probe: unity run . -- -executeMethod WaterArtImport.ProbeWater
    public static void ProbeWater()
    {
        var water = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M1Water.mat");
        if (water == null) { Debug.LogError("[ProbeWater] missing M1Water.mat"); return; }
        Debug.Log("[ProbeWater] emissionTex=" + (water.GetTexture("_EmissionMap") != null)
            + " keyword=" + water.IsKeywordEnabled("_EMISSION")
            + " emissionColor=" + water.GetColor("_EmissionColor")
            + " keywords=" + string.Join(",", water.shaderKeywords));
        water.EnableKeyword("_EMISSION");
        Debug.Log("[ProbeWater] after EnableKeyword: " + water.IsKeywordEnabled("_EMISSION"));
        EditorUtility.SetDirty(water);
        AssetDatabase.SaveAssets();
    }

    // Wake probe: does AddComponent<FishWake> fire Awake in this context?
    // Run: unity run . -- -executeMethod WaterArtImport.ProbeWake
    public static void ProbeWake()
    {
        var go = new GameObject("ProbeFish");
        go.AddComponent<FishWake>();
        var names = new System.Collections.Generic.List<string>();
        foreach (var c in go.GetComponents<Component>())
            names.Add(c == null ? "null" : c.GetType().Name);
        Debug.Log("[ProbeWake] components: " + string.Join(",", names.ToArray()));
        Object.DestroyImmediate(go);
    }

    // Uber Stylized Water trial (MIT): template material on a quad next
    // to current M1Water, same light/camera. Template kept at defaults
    // (honest out-of-box look); staged asset untouched, nothing in the
    // game scene is rewired. Run:
    // unity run . -- -executeMethod WaterArtImport.ProbeUberWater
    public static void ProbeUberWater()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/M1_MovementProof.unity");
        var current = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M1Water.mat");
        var uber = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/ArtVendor/UberStylizedWater/UWa-Template-Tropical.mat");
        if (current == null || uber == null)
        {
            Debug.LogError("[ProbeUber] missing water materials (current=" + (current != null)
                + " uber=" + (uber != null) + ")");
            return;
        }
        Debug.Log("[ProbeUber] uber shader=" + (uber.shader != null ? uber.shader.name : "null"));
        // Template ships with surface foam off (its look rides
        // intersection foam, which needs a depth texture we don't
        // render): evaluate with surface foam on, everything else at
        // template defaults. In-memory clone — staged asset untouched.
        var uberFoam = new Material(uber);
        uberFoam.SetFloat("_Enable_SurfaceFoam", 1f);
        var q1 = MakeQuad("ProbeCurrent", new Vector3(-3f, 2f, 2052f), current);
        var q2 = MakeQuad("ProbeUber", new Vector3(3f, 2f, 2052f), uberFoam);
        // [M1-SCAFFOLD] diagnostic staging: destroyed below, never saved.
        var camGo = new GameObject("ProbeCam");
        var cam = camGo.AddComponent<Camera>();
        cam.farClipPlane = 4000f;
        cam.transform.position = new Vector3(0f, 2.5f, 2042f);
        cam.transform.LookAt(new Vector3(0f, 2f, 2052f));
        var rt = new RenderTexture(960, 540, 24);
        System.IO.Directory.CreateDirectory("Logs/VisualVerify");
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;
        System.IO.File.WriteAllBytes("Logs/VisualVerify/uber-vs-current.png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Debug.Log("[ProbeUber] wrote uber-vs-current.png");
        CleanupProbes(q1, q2, camGo);
        Object.DestroyImmediate(rt);
    }

    // Prebuilt trial: WaterProDaytime on a quad next to a foam-emission
    // quad, same light/camera. Run: unity run . --
    // -executeMethod WaterArtImport.ProbePrebuiltWater
    public static void ProbePrebuiltWater()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/M1_MovementProof.unity");
        var prebuilt = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/ArtVendor/NaughtyWater/Materials/WaterProDaytime.mat");
        if (prebuilt == null) { Debug.LogError("[ProbePrebuilt] missing WaterProDaytime.mat"); return; }
        Debug.Log("[ProbePrebuilt] shader=" + (prebuilt.shader != null ? prebuilt.shader.name : "null"));
        var fresh = new Material(Shader.Find("Standard"));
        fresh.color = new Color(0.08f, 0.32f, 0.85f, 1f);
        var foam = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/WaterFoam.png");
        fresh.SetTexture("_EmissionMap", foam);
        fresh.SetColor("_EmissionColor", Color.white);
        fresh.EnableKeyword("_EMISSION");
        var q1 = MakeQuad("ProbePrebuilt", new Vector3(-3f, 2f, 2052f), prebuilt);
        var q2 = MakeQuad("ProbeCustom", new Vector3(3f, 2f, 2052f), fresh);
        // [M1-SCAFFOLD] diagnostic staging: destroyed below, never saved.
        var camGo = new GameObject("ProbeCam");
        var cam = camGo.AddComponent<Camera>();
        cam.farClipPlane = 4000f;
        cam.transform.position = new Vector3(0f, 2.5f, 2042f);
        cam.transform.LookAt(new Vector3(0f, 2f, 2052f));
        var rt = new RenderTexture(960, 540, 24);
        System.IO.Directory.CreateDirectory("Logs/VisualVerify");
        ShootPrebuilt(cam, rt);
        CleanupProbes(q1, q2, camGo);
        Object.DestroyImmediate(rt);
    }

    private static void ShootPrebuilt(Camera cam, RenderTexture rt)
    {
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;
        System.IO.File.WriteAllBytes("Logs/VisualVerify/prebuilt-vs-custom.png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Debug.Log("[ProbePrebuilt] wrote prebuilt-vs-custom.png");
    }

    // Foam bisect 2: isolate which M1Water.mat setting kills emission.
    // Four quads: fresh opaque+foam, fresh+transparent block, fresh+
    // transparent+(40,100) scale, M1Water.mat itself. Run: unity run . --
    // -executeMethod WaterArtImport.ProbeFoamRender
    public static void ProbeFoamRender()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/M1_MovementProof.unity");
        var foam = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/WaterFoam.png");
        var fresh = new Material(Shader.Find("Standard"));
        fresh.color = new Color(0.08f, 0.32f, 0.85f, 1f);
        fresh.SetTexture("_EmissionMap", foam);
        fresh.SetColor("_EmissionColor", Color.white);
        fresh.EnableKeyword("_EMISSION");
        Debug.Log("[ProbeFoam] fresh keyword=" + fresh.IsKeywordEnabled("_EMISSION"));
        var m1 = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M1Water.mat");
        var trans = TransBlock(new Material(Shader.Find("Standard")));
        trans.color = new Color(0.08f, 0.32f, 0.85f, 0.6f);
        trans.SetTexture("_EmissionMap", foam);
        trans.SetColor("_EmissionColor", Color.white);
        trans.EnableKeyword("_EMISSION");
        var transScaled = new Material(trans);
        transScaled.SetTextureScale("_EmissionMap", new Vector2(40f, 100f));
        // [M1-SCAFFOLD] diagnostic staging: destroyed below, never saved.
        var f1 = MakeQuad("ProbeFresh", new Vector3(-9f, 2f, 2052f), fresh);
        var f2 = MakeQuad("ProbeTrans", new Vector3(-3f, 2f, 2052f), trans);
        var f3 = MakeQuad("ProbeScaled", new Vector3(3f, 2f, 2052f), transScaled);
        var f4 = MakeQuad("ProbeM1", new Vector3(9f, 2f, 2052f), m1);
        var camGo = new GameObject("ProbeCam");
        var cam = camGo.AddComponent<Camera>();
        cam.farClipPlane = 4000f;
        cam.transform.position = new Vector3(0f, 2.5f, 2038f);
        cam.transform.LookAt(new Vector3(0f, 2f, 2052f));
        var rt = new RenderTexture(960, 540, 24);
        System.IO.Directory.CreateDirectory("Logs/VisualVerify");
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        System.IO.File.WriteAllBytes("Logs/VisualVerify/foam-bisect.png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        CleanupProbes(f1, f2, f3, f4, camGo);
        Object.DestroyImmediate(rt);
    }

    // M1Water.mat's exact transparent block, built fresh (isolates
    // asset corruption from blend-mode behavior).
    private static Material TransBlock(Material mat)
    {
        mat.SetFloat("_Mode", 3f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = 3000;
        return mat;
    }

    private static GameObject MakeQuad(string name, Vector3 pos, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = new Vector3(4f, 4f, 1f);
        Object.DestroyImmediate(go.GetComponent<MeshCollider>());
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    // Probes dirty the open scene: destroy everything staged so an
    // accidental save can never bake diagnostics into the scene file.
    private static void CleanupProbes(params GameObject[] staged)
    {
        foreach (var go in staged)
            if (go != null) Object.DestroyImmediate(go);
    }

    private static void WireWaterMaterial()
    {
        var water = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M1Water.mat");
        var foam = AssetDatabase.LoadAssetAtPath<Texture2D>(FoamPath);
        if (water == null || foam == null)
        {
            Debug.LogError("[WaterArtImport] missing M1Water.mat or foam.");
            return;
        }
        water.SetTexture("_EmissionMap", foam);
        water.SetColor("_EmissionColor", Color.white);
        water.EnableKeyword("_EMISSION");
        water.SetTextureScale("_EmissionMap", new Vector2(40f, 100f));
        water.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(water);
        AssetDatabase.SaveAssets();
    }

    private static void Write(Texture2D tex, string path)
    {
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.wrapMode = TextureWrapMode.Mirror;
            importer.SaveAndReimport();
        }
    }
}
