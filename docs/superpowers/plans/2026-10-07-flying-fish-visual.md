# Visual Reconstruction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Render realms with Kenney panoramas + FBX dressing via per-type `RealmSkin` SOs, gameplay volumes unchanged.

**Architecture:** New `RealmSkin` ScriptableObject consumed by `ChunkBuilder.ApplyMood`/`BuildChunk`; a headless editor script (`VisualImport`) authors materials+prefabs+skin assets from `ArtVendor/`; seeded prefab picks with collider-shell pattern keep judging/clearance identical.

**Tech Stack:** Unity 6000.6.3f1, C#, Unity CLI (`unity test`, `unity run`), NUnit EditMode/PlayMode.

**Spec:** `docs/superpowers/specs/2026-10-07-flying-fish-visual-design.md`

## Global Constraints

- PowerShell ONLY (`Select-String`, `; if ($?)` — no `grep`/`&&`).
- Unity operations via the `unity` CLI only; batchmode PlayMode drives `Tick`/`BuildChunk` directly, no synthetic input.
- TDD red → green → commit per behavior; parse `test-results.xml`; compiler errors via `Select-String "error CS"` on `Logs/Editor.log`.
- Exactly one `in_progress` todo; tests use `try/finally` to restore `RenderSettings` (global state).
- Never commit `Library/ Temp/ Logs/ obj/ *.csproj *.sln`; DO commit `.meta`/assets/prefabs.
- SO-only tuning, seeded `System.Random` only, UI untouched, no gameplay rule changes.

## Review Focus

- Panoramas must be 2:1 equirect or the sky stretches — Task 2 pins dimensions (width == 2 × height); default Texture2D import feeds `Skybox/Panoramic` correctly, no importer change needed.
- Kenney FBX unit scale vs shell bounds — shells fit by bounds math, so any unit works; Task 3 pins `FitScale`.
- Prefabs must carry zero colliders or decor starts blocking lanes — Task 3 asserts none.
- `RenderSettings` leaks between tests/scenes — every test restores in `finally`; null skin clears `skybox`.
- Skinned chunks intentionally consume RNG differently from legacy — legacy path (Skin null) is byte-identical; Task 3 pins legacy positions unchanged.

---

### Task 1: RealmSkin SO + skinned ApplyMood

**Files:**
- Create: `Assets/Scripts/Run/RealmSkin.cs`
- Modify: `Assets/Scripts/Run/ChunkSpec.cs` (add `public RealmSkin Skin;`)
- Modify: `Assets/Scripts/Run/ChunkBuilder.cs` (`ApplyMood` skin path)
- Test: `Assets/Tests/EditMode/RealmSkinTests.cs`

**Interfaces:**
- Consumes: `ChunkSpec` mood fields, `ChunkBuilder.ApplyMood(ChunkSpec)`, `SkyMaterial`/`WaterMaterial` lazy properties.
- Produces: `RealmSkin` type (fields per spec §1 + `RingTint`, `CoinTint`); `ApplyMood` sets `RenderSettings.skybox` (panoramic runtime material, cached per skin in `_skyboxMats`), fog, ambient, water RGB, `_cloudMat` color, silhouette colors; null skin → `RenderSettings.skybox = null` + legacy tints.

- [ ] **Step 1: Write the failing test** (`Assets/Tests/EditMode/RealmSkinTests.cs`, namespace `FlyingFishMomentum.Tests.EditMode`):

```csharp
using NUnit.Framework;
using UnityEngine;
using FlyingFishMomentum.Run;

namespace FlyingFishMomentum.Tests.EditMode
{
    public class RealmSkinTests
    {
        private readonly System.Collections.Generic.List<GameObject> _spawned =
            new System.Collections.Generic.List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned) Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        private ChunkBuilder NewBuilder()
        {
            var go = new GameObject("chunkBuilder");
            _spawned.Add(go);
            return go.AddComponent<ChunkBuilder>();
        }

        [Test]
        public void ApplyMood_NullSkin_KeepsLegacyTintPath()
        {
            var builder = NewBuilder();
            var spec = ScriptableObject.CreateInstance<ChunkSpec>();
            spec.ChunkId = "Legacy"; spec.Skin = null;
            spec.FogColor = Color.red; spec.FogDensity = 0.01f;
            spec.SkyTint = Color.blue; spec.WaterTint = Color.green;
            try
            {
                builder.ApplyMood(spec);
                Assert.IsNull(RenderSettings.skybox);
                Assert.AreEqual(Color.red, RenderSettings.fogColor);
            }
            finally { RenderSettings.skybox = null; }
        }

        [Test]
        public void ApplyMood_Skin_SetsSkyboxFogAndWater()
        {
            var builder = NewBuilder();
            var skin = ScriptableObject.CreateInstance<RealmSkin>();
            skin.SkyPanorama = new Texture2D(4, 2);
            skin.SkyTint = Color.white; skin.FogColor = Color.gray;
            skin.FogDensity = 0.02f; skin.WaterTint = new Color(0.1f, 0.2f, 0.3f);
            var spec = ScriptableObject.CreateInstance<ChunkSpec>();
            spec.ChunkId = "Lagoon"; spec.Skin = skin;
            try
            {
                builder.ApplyMood(spec);
                Assert.IsNotNull(RenderSettings.skybox);
                Assert.AreEqual(Color.gray, RenderSettings.fogColor);
                Assert.AreEqual(0.02f, RenderSettings.fogDensity, 0.0001f);
                var water = builder.WaterMaterial.color;
                Assert.AreEqual(0.1f, water.r, 0.001f);
                Assert.AreEqual(0.6f, water.a, 0.001f); // alpha never stamped
            }
            finally { RenderSettings.skybox = null; Object.DestroyImmediate(skin.SkyPanorama); }
        }
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `unity test --mode EditMode`
Expected: FAIL — compile errors (`RealmSkin`/`Skin` undefined). Confirm via `Select-String "error CS" Logs/Editor.log` or the test run failing.

- [ ] **Step 3: Write minimal implementation**

`RealmSkin.cs` (fields per spec §1 plus `public Color RingTint = new Color(1f, 0.85f, 0.2f);` and `public Color CoinTint = new Color(1f, 0.75f, 0.15f);`). `ChunkSpec`: add `public RealmSkin Skin;`. `ChunkBuilder`: add `private readonly Dictionary<RealmSkin, Material> _skyboxMats = ...`; in `ApplyMood`, after the null-guard: if `spec.Skin != null` → build/cache `new Material(Shader.Find("Skybox/Panoramic"))` with `_MainTex = skin.SkyPanorama`, `_Tint`-ish color = `skin.SkyTint`, assign `RenderSettings.skybox`, then fog/ambient/water RGB/cloud/silhouette from skin; else → `RenderSettings.skybox = null` + existing legacy lines verbatim.

- [ ] **Step 4: Run to verify it passes**

Run: `unity test --mode EditMode`, parse `test-results.xml` (`testcasecount`/`passed`/`failed`).
Expected: all PASS (prior 162 + 2 new).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Run/RealmSkin.cs Assets/Scripts/Run/ChunkSpec.cs Assets/Scripts/Run/ChunkBuilder.cs Assets/Tests/EditMode/RealmSkinTests.cs
git commit -m "feat: RealmSkin SO with skinned mood path (visual task 1)"
```

### Task 2: Headless art import (materials + prefabs + skin assets)

**Files:**
- Create: `Assets/Editor/VisualImport.cs` (static `BuildAll`, menu item `FlyingFish/Import Visual Art`)
- Create (generated, committed): `Assets/Materials/V1Pirate.mat`, `Assets/Materials/V1Nature_*.mat` (5–6 prefix-tinted), `Assets/Prefabs/Decor/*.prefab`, `Assets/Configs/RealmSkin_{Lagoon,Gauntlet,Storm,Sky}.asset`
- Test: append to `Assets/Tests/EditMode/RealmSkinTests.cs` (asset-validity tests via `UnityEditor.AssetDatabase`)

**Interfaces:**
- Consumes: `RealmSkin` (Task 1); ArtVendor FBX paths; panorama PNG paths.
- Produces: materials (pirate = Standard + `colormap.png`; nature = Standard tinted by name-prefix table below); prefabs (MeshFilter+MeshRenderer, material assigned, NO colliders); skin assets (panorama + tints + prefab refs per spec §2–§3 mapping; tints: reuse existing `ChunkSpec_*` mood colors verbatim); panorama `TextureImporter.textureShape = Equirect`.

Name-prefix → nature material table (shared, SO-free): `tree_palm*`/`tree_*` foliage green `(0.25,0.55,0.3)`, `rock_*`/`cliff_*`/`stone` gray `(0.45,0.46,0.48)`, `platform_beach`/`patch-sand` sand `(0.8,0.7,0.5)`, `platform_grass`/`plant_*`/`grass*` green `(0.3,0.6,0.32)`, `statue_ring` pale stone `(0.75,0.76,0.8)`, `flag*`/`chest`/`castle*`/`tower*`/`structure*`/`ship-wreck` → pirate colormap material, `cliff_waterfall*` water blue `(0.3,0.65,0.85)`.

- [ ] **Step 1: Write the failing test** (append):

```csharp
        [Test]
        public void SkinAssets_ExistWithPanoramaAndPrefabs()
        {
            string[] realms = { "Lagoon", "Gauntlet", "Storm", "Sky" };
            foreach (var r in realms)
            {
                var skin = UnityEditor.AssetDatabase.LoadAssetAtPath<RealmSkin>(
                    "Assets/Configs/RealmSkin_" + r + ".asset");
                Assert.IsNotNull(skin, "missing skin asset for " + r);
                Assert.IsNotNull(skin.SkyPanorama, r + " panorama");
                Assert.AreEqual(skin.SkyPanorama.width, skin.SkyPanorama.height * 2,
                    r + " panorama is not 2:1 equirect");
                Assert.IsNotNull(skin.DecorPrefabs, r + " decor set");
                Assert.Greater(skin.DecorPrefabs.Length, 0, r + " decor empty");
                foreach (var p in skin.DecorPrefabs)
                {
                    Assert.IsNotNull(p, r + " null prefab ref");
                    Assert.IsNull(p.GetComponentInChildren<Collider>(true), p.name + " carries a collider");
                }
            }
        }
```

(EditMode tests compile for the Editor platform, so `UnityEditor.AssetDatabase` needs no extra asmdef reference.)

- [ ] **Step 2: Run to verify it fails**

Run: `unity test --mode EditMode`
Expected: FAIL — `missing skin asset for Lagoon` (assets don't exist yet).

- [ ] **Step 3: Write `VisualImport.BuildAll`** — idempotent: creates materials/prefabs/skins if missing (overwrite always, so re-runs converge); strips nothing (prefabs built fresh from FBX via `PrefabUtility.SaveAsPrefabAsset` on a temp GameObject with MeshFilter(mesh from FBX) + MeshRenderer(material)); panoramas need no importer change (default Texture2D import feeds `Skybox/Panoramic`); logs counts. Run headless: `unity run . -- -executeMethod VisualImport.BuildAll -quit` (forwarding syntax confirmed in `unity run --help` examples).

- [ ] **Step 4: Run to verify it passes**

Run: `unity run . -- -executeMethod VisualImport.BuildAll -quit`; then `unity test --mode EditMode`, parse `test-results.xml`.
Expected: all PASS.

- [ ] **Step 5: Commit** (code + generated assets + `.meta`):

```bash
git add Assets/Editor/VisualImport.cs Assets/Tests/EditMode/RealmSkinTests.cs Assets/Materials/V1* Assets/Prefabs/Decor Assets/Configs/RealmSkin_*
git commit -m "feat: headless visual import with skin assets (visual task 2)"
```

### Task 3: Seeded prefab dressing with collider shells

**Files:**
- Modify: `Assets/Scripts/Run/ChunkBuilder.cs` (`PlaceDecor`, `PlaceRock`, `PlaceSpire`, new `FitScale` + `AttachShell`)
- Test: append `Assets/Tests/EditMode/RealmSkinTests.cs`

**Interfaces:**
- Consumes: `spec.Skin` prefab arrays; existing `System.Random rng` in `BuildChunk`/`PlaceDecor`.
- Produces: `public static float FitScale(Vector3 shell, Vector3 visual)` (uniform, min-axis, guards zero extents → 1f); shells named `"ChunkShell_" + prefab.name`, parented to the collider cube, cube renderer disabled only when a shell attaches; pool reuse destroys prior `ChunkShell_*` children; empty array → legacy primitive path byte-identical.

- [ ] **Step 1: Write the failing tests** (append):

```csharp
        [Test]
        public void FitScale_FitsLargestAxisUniformly()
        {
            Assert.AreEqual(4f, ChunkBuilder.FitScale(new Vector3(4f, 20f, 4f), new Vector3(1f, 2f, 1f)), 0.001f);
            Assert.AreEqual(1f, ChunkBuilder.FitScale(Vector3.zero, Vector3.one), 0.001f);
        }

        [Test]
        public void BuildChunk_SkinnedDecor_UsesPrefabsDeterministically()
        {
            var a = NewBuilder(); var b = NewBuilder();
            var skin = ScriptableObject.CreateInstance<RealmSkin>();
            var prefab = new GameObject("PalmTest");
            _spawned.Add(prefab);
            skin.DecorPrefabs = new GameObject[] { prefab };
            var spec = ScriptableObject.CreateInstance<ChunkSpec>();
            spec.ChunkId = "Lagoon"; spec.Skin = skin;
            spec.Length = 500f; spec.DecorKind = "rubble"; spec.DecorCount = 3;
            try
            {
                a.BuildChunk(spec, 0f, 42); b.BuildChunk(spec, 0f, 42);
                Assert.AreEqual(a.Decor.Count, b.Decor.Count);
                for (int i = 0; i < a.Decor.Count; i++)
                    Assert.AreEqual(
                        a.Decor[i].transform.position, b.Decor[i].transform.position);
            }
            finally { RenderSettings.skybox = null; }
        }

        [Test]
        public void BuildChunk_SkinnedIsland_ColliderBoundsMatchLegacy()
        {
            var legacy = NewBuilder(); var skinned = NewBuilder();
            var shellPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _spawned.Add(shellPrefab);
            Object.DestroyImmediate(shellPrefab.GetComponent<Collider>());
            var skin = ScriptableObject.CreateInstance<RealmSkin>();
            skin.IslandPrefabs = new GameObject[] { shellPrefab };
            var mkSpec = new System.Func<RealmSkin, ChunkSpec>(s =>
            {
                var sp = ScriptableObject.CreateInstance<ChunkSpec>();
                sp.ChunkId = "X"; sp.Skin = s; sp.Length = 500f; sp.IslandPairs = 1;
                return sp;
            });
            try
            {
                legacy.BuildChunk(mkSpec(null), 0f, 9);
                skinned.BuildChunk(mkSpec(skin), 0f, 9);
                Assert.AreEqual(legacy.Islands.Count, skinned.Islands.Count);
                for (int i = 0; i < legacy.Islands.Count; i++)
                {
                    var lb = legacy.Islands[i].GetComponent<Collider>().bounds;
                    var sb = skinned.Islands[i].GetComponent<Collider>().bounds;
                    Assert.AreEqual(lb.extents, sb.extents);
                    var rend = legacy.Islands[i].GetComponent<MeshRenderer>();
                    Assert.IsFalse(skinned.Islands[i].GetComponent<MeshRenderer>().enabled);
                    Assert.AreEqual(1, CountShells(skinned.Islands[i]));
                }
            }
            finally { RenderSettings.skybox = null; }
        }

        private static int CountShells(GameObject go)
        {
            int n = 0;
            foreach (Transform c in go.transform)
                if (c.name.StartsWith("ChunkShell_")) n++;
            return n;
        }
```

(`spec.Skin` is null for legacy — C# allows passing null for the RealmSkin parameter.)

- [ ] **Step 2: Run to verify it fails**

Run: `unity test --mode EditMode`
Expected: FAIL — `FitScale` undefined (compile error surfaces in results).

- [ ] **Step 3: Write minimal implementation** — `FitScale` + `AttachShell(GameObject cube, GameObject prefab)`: disable cube renderer, instantiate, parent, uniform-fit via renderer bounds (fallback scale 1 when bounds empty), name `ChunkShell_*`. `PlaceDecor`: prefab branch when `skin?.DecorPrefabs` non-empty (position math unchanged, kind only sets y). `PlaceRock`/`PlaceSpire`: after cube setup, shell branch when arrays non-empty; pool-take path destroys existing `ChunkShell_*` children first. Cache `spec.Skin` arrays in private fields at `BuildChunk` start (null-safe).

- [ ] **Step 4: Run to verify it passes**

Run: `unity test --mode EditMode`, parse `test-results.xml`.
Expected: all PASS, including all pre-existing clearance/position tests unmodified.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Run/ChunkBuilder.cs Assets/Tests/EditMode/RealmSkinTests.cs
git commit -m "feat: seeded prefab dressing with collider shells (visual task 3)"
```

### Task 4: Prompt/silhouette cohesion + full verification

**Files:**
- Modify: `Assets/Scripts/Run/ChunkBuilder.cs` (ring/coin/silhouette/cloud tint from skin in `ApplyMood`)
- Test: append `Assets/Tests/EditMode/RealmSkinTests.cs`

**Interfaces:**
- Consumes: `RealmSkin.RingTint/CoinTint/SilhouetteColor/CloudTint`.
- Produces: skinned mood re-tints shared ring/coin/cloud/silhouette materials; legacy path untouched.

- [ ] **Step 1: Write the failing test** (append):

```csharp
        [Test]
        public void ApplyMood_Skin_TintsPromptsSilhouettesAndCloud()
        {
            var builder = NewBuilder();
            var skin = ScriptableObject.CreateInstance<RealmSkin>();
            skin.SkyPanorama = new Texture2D(4, 2);
            skin.RingTint = Color.magenta; skin.CoinTint = Color.cyan;
            skin.SilhouetteColor = Color.black; skin.CloudTint = Color.white;
            var spec = ScriptableObject.CreateInstance<ChunkSpec>();
            spec.ChunkId = "Storm"; spec.Skin = skin;
            spec.Length = 500f; spec.RingCount = 1; spec.RingSpacing = 150f;
            spec.CoinsPerTrail = 1;
            try
            {
                builder.BuildChunk(spec, 0f, 3);
                var ringMat = builder.Rings[0].GetComponent<LineRenderer>().sharedMaterial;
                Assert.AreEqual(Color.magenta, ringMat.color);
                var gem = builder.Coins[0].transform.Find("Gem").GetComponent<MeshRenderer>();
                Assert.AreEqual(Color.cyan, gem.sharedMaterial.color);
            }
            finally { RenderSettings.skybox = null; Object.DestroyImmediate(skin.SkyPanorama); }
        }
```

(No production seam needed: ring/coin colors read back through the spawned renderers, which share the builder's lazy materials.)

- [ ] **Step 2: Run to verify it fails**

Run: `unity test --mode EditMode`
Expected: FAIL — tint path missing (ring/coin stay default gold).

- [ ] **Step 3: Write minimal implementation** — tint in `ApplyMood` skin branch; silhouette material: `EnsureSilhouettes` currently news a material per slab — change to one shared `_silhouetteMat` created lazily, tinted from skin (legacy default `(0.16,0.2,0.3)` preserved when skin null); cloud `_cloudMat.color` RGB from `skin.CloudTint` keeping blend alpha logic in `Update`.

- [ ] **Step 4: Run BOTH suites to verify**

Run: `unity test --mode EditMode` and `unity test --mode PlayMode`, parse both `test-results.xml`.
Expected: EditMode all PASS, PlayMode 42/42 (or grown count) all PASS.

- [ ] **Step 5: Commit + review**

```bash
git add Assets/Scripts/Run/ChunkBuilder.cs Assets/Tests/EditMode/RealmSkinTests.cs
git commit -m "feat: prompt and silhouette cohesion tints (visual task 4)"
git push origin main
```

Then invoke the `requesting-code-review` skill for the task group before any further work. Live visual proof (panorama/dressing on screen) is a user playtest screenshot — batchmode cannot settle visuals (ledger precedent).
