# Environment Overhaul Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the approved environment overhaul (scale bible, stacking quality, Gauntlet re-theme, surface rings, density) in 5 phased, independently verifiable tasks.

**Architecture:** Collider-first retune on existing place functions (same code paths, new volumes), then visual-quality upgrade of the shell composer, then pipeline data for the re-theme; W0 headless captures verify every phase visually; user playtests verify feel.

**Tech Stack:** Unity 6000.6.3f1, C# (Runtime + Editor assemblies), Unity CLI only (`unity test`, `unity run`), PowerShell 5.1 (`; if ($?)` chains, never `&&`).

**Spec:** `docs/superpowers/specs/2026-10-07-environment-overhaul-design.md` — the plan argues from the spec; executors read both.

## Global Constraints

- PowerShell ONLY for shell (`Select-String`, `$LASTEXITCODE`, `; if ($?)`); never `grep`/`&&`.
- Unity operations via the `unity` CLI only; user must close the Editor before every `unity test`/`unity run` (project lock) — ask them, do not proceed locked.
- TDD red-green-commit per behavior; full EditMode + PlayMode suites green before each commit.
- No `Library/`, `Temp/`, `Logs/`, `obj/`, `*.csproj`, `*.sln` commits; `.meta` files committed.
- No gameplay-rule changes beyond the spec bible (colliders/lanes/density as specified); judging stays positional; everything seeded deterministic; SO-only tuning (no new magic numbers — derived or const with comment).
- `requesting-code-review` after each task; `verification-before-completion` before any done claim (show real command output).

## Review Focus

1. Widened islands (13) vs lane math: if any placement/clearance code still assumes half-width 5, rocks will sit in prompt lanes — expect island centers at ±16.5 and clearance passing; Task 1 pins both.
2. Breach reachability: fish must climb −3 → +0.5 within movement limits — expect PlayMode green plus explicit user playtest; a test cannot settle feel.
3. Buoy decor at y≈0 near swim-ring depth: no collision by construction, but visual confusion with prompts is possible — expect buoys ≤3 units and off-lane (|x|≥25); Task 4 pins size/position.
4. Taper/jitter pushing a segment outside its collider on extreme aspects — expect the inside-bounds invariant test to hold for tower, rock, and flat visuals; Task 2 pins all three.
5. Arch draw-count change ripples: pillar+lintel draws already changed streams once (CR-3); any further draw change breaks exact-sequence tests — expect zero draw-count changes in Tasks 1–4 unless the task says so.

---

### Task 1: Scale bible colliders + surface ring lane

**Files:**
- Modify: `Assets/Scripts/Run/ChunkBuilder.cs` (consts, `PlaceIsland` scale, spire call width/height, arch offsets, swim-ring lane, island-lane `cx`)
- Test: `Assets/Tests/EditMode/EnvironmentBibleTests.cs` (new)

**Interfaces:**
- Consumes: existing `PlaceRock`/`PlaceSpire`/`PlaceIsland` signatures, `IslandHalfX`, `SpireHalfDepth`, `GateHalfWidth`, `SwimY`, `FlyY` consts.
- Produces: `SurfaceRingY` const; new collider volumes used by Tasks 2–5; `IslandHalfX = 6.5f` used by lane math.

- [ ] **Step 1: Search every use of the changing consts**

```powershell
Select-String -Path "Assets\Scripts\Run\ChunkBuilder.cs" -Pattern "IslandHalfX|SpireHalfDepth|GateHalfWidth|SwimY"
```

Expected: hits in island `cx` computation, `FurthestContentZ`, reclaim (`ReclaimBefore` island/spire depth checks), arch fallback anchor, ring placement ternary. Record every line number; all of them are in scope for consistent update.

- [ ] **Step 2: Write the failing tests** (`Assets/Tests/EditMode/EnvironmentBibleTests.cs`)

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using FlyingFishMomentum.Run;

namespace FlyingFishMomentum.Tests.EditMode
{
    public class EnvironmentBibleTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned) Object.DestroyImmediate(go);
            _spawned.Clear();
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
        }

        private ChunkBuilder NewBuilder()
        {
            var go = new GameObject("chunkBuilder");
            _spawned.Add(go);
            return go.AddComponent<ChunkBuilder>();
        }

        [Test]
        public void IslandCollider_MatchesBible()
        {
            var builder = NewBuilder();
            var spec = ScriptableObject.CreateInstance<ChunkSpec>();
            spec.ChunkId = "Bible"; spec.Skin = null;
            spec.Length = 500f; spec.IslandPairs = 1;
            builder.BuildChunk(spec, 0f, 9);
            Assert.AreEqual(2, builder.Islands.Count);
            Assert.AreEqual(new Vector3(13f, 20f, 13f), builder.Islands[0].transform.localScale);
            Assert.AreEqual(16.5f, Mathf.Abs(builder.Islands[0].transform.position.x), 0.01f);
        }

        [Test]
        public void SwimRings_RideTheSurfaceLane()
        {
            var builder = NewBuilder();
            var spec = ScriptableObject.CreateInstance<ChunkSpec>();
            spec.ChunkId = "Bible"; spec.Skin = null;
            spec.Length = 500f; spec.RingCount = 2; spec.RingSpacing = 150f;
            spec.CoinsPerTrail = 1;
            builder.BuildChunk(spec, 0f, 9);
            Assert.AreEqual(ChunkBuilder.SurfaceRingY, builder.Rings[0].transform.position.y, 0.01f);
            Assert.AreEqual(ChunkBuilder.SurfaceRingY, builder.Rings[1].transform.position.y, 0.01f);
        }
    }
}
```

- [ ] **Step 3: Run to verify RED**

Run: `unity test --mode EditMode` (user closes Editor first)
Expected: compile FAIL `CS0246 SurfaceRingY` + `CS1061` style errors; suite exit 6. Fails because the bible does not exist, not because of typos.

- [ ] **Step 4: Minimal implementation** (`Assets/Scripts/Run/ChunkBuilder.cs`)

```csharp
public const float SurfaceRingY = 0.5f;   // swim rings break the surface; fish swims at SwimY (-3) and breaches through
public const float IslandHalfX = 6.5f;    // matches the 13-wide island collider below
```

Change `PlaceIsland` scale to `new Vector3(13f, 20f, 13f)` (keep center `IslandCenterY`, span now −5..15). Change spire placement width 4→8 and height draw `50f + rng*40f` → `30f + rng*15f` at the `PlaceSpire` call site (keep `SpireCenterY`). Change `SpireHalfDepth` 2→4. Change arch pillar scale `(4f, 20f, 4f)` → `(6f, 18f, 6f)` and lintel `(20f, 4f, 4f)` → `(20f, 5f, 5f)` at the three arch call sites. Change swim-ring placement ternary `k < swimRings ? SwimY : FlyY` → `k < swimRings ? SurfaceRingY : FlyY`. Keep the ringless-arch fallback anchor at `SwimY` (underwater gates when no rings exist; no production spec does this). Update every `IslandHalfX`/`SpireHalfDepth` consumer found in Step 1 the same way. No other consts move.

- [ ] **Step 5: Run suites to verify GREEN**

Run: `unity test --mode EditMode`, then `unity test --mode PlayMode`
Expected: exit 0 both; EditMode total = prior + 2, failed = 0. If an exact-sequence or clearance test fails, STOP: read the failure (likely arch-draw or lane assumption), fix the test only if the new bible intentionally changed the expectation, with a comment citing the bible row.

- [ ] **Step 6: Commit**

```powershell
git add Assets/Scripts/Run/ChunkBuilder.cs Assets/Tests/EditMode/EnvironmentBibleTests.cs Assets/Tests/EditMode/EnvironmentBibleTests.cs.meta; if ($?) { git commit -m "feat: scale bible colliders plus surface ring lane (overhaul 1)" }
```

### Task 2: Stacking quality (variety, taper, jitter, cap)

**Files:**
- Modify: `Assets/Scripts/Run/ChunkBuilder.cs` (`AttachStackedShell` only)
- Test: `Assets/Tests/EditMode/ObstacleShellTests.cs` (rewrite one-prefab expectation)

**Interfaces:**
- Consumes: `FitScale`, `CounterScale`, `PlaceSegment`, `Draw` (unchanged signatures).
- Produces: varied tapered capped segments consumed visually by Tasks 3–5; no interface change outward.

- [ ] **Step 1: Write the failing tests** (append to `Assets/Tests/EditMode/ObstacleShellTests.cs`)

```csharp
[Test]
public void Segments_VaryAcrossSetTaperAndCap()
{
    var builder = NewBuilder();
    var skin = ScriptableObject.CreateInstance<RealmSkin>();
    skin.SpirePrefabs = new GameObject[]
        { BoxStub("TallA", new Vector3(2f, 6f, 2f)), BoxStub("TallB", new Vector3(3f, 5f, 3f)) };
    var spec = ScriptableObject.CreateInstance<ChunkSpec>();
    spec.ChunkId = "Variety"; spec.Skin = skin;
    spec.Length = 500f; spec.SkySpireCount = 1;
    builder.BuildChunk(spec, 0f, 11);
    Assert.AreEqual(1, builder.Spires.Count);
    var shells = Shells(builder.Spires[0]);
    Assert.LessOrEqual(shells.Count, 8, "segment cap exceeded");
    Assert.Greater(shells.Count, 1, "tall spire collapsed unexpectedly");
    var names = new HashSet<string>();
    float topWidth = float.MaxValue;
    foreach (var s in shells)
    {
        names.Add(s.name);
        var b = Combined(s);
        topWidth = Mathf.Min(topWidth, b.size.x);
        var cb = builder.Spires[0].GetComponent<Collider>().bounds;
        Assert.IsTrue(cb.Contains(b.min + Vector3.one * 0.05f) && cb.Contains(b.max - Vector3.one * 0.05f), "segment pokes out");
    }
    Assert.Greater(names.Count, 1, "segments never vary");
}
```

Also rewrite the existing `Assert.AreEqual(1, names.Count, ...)` inside `AssertStackedFromBaseInside` to `Assert.LessOrEqual(names.Count, 2, ...)` only if the shared stub sets have ≤2 entries — actually the helper builds from single-entry sets in spire/island tests, so names.Count stays 1 there and the helper is unchanged; the variety test above uses its own 2-entry set. No helper rewrite needed.

- [ ] **Step 2: Run to verify RED**

Run: `unity test --mode EditMode`
Expected: `Segments_VaryAcrossSetTaperAndCap` FAILS with "segments never vary" (today: one pick per cube). Fails on behavior, not compile.

- [ ] **Step 3: Minimal implementation** (`AttachStackedShell` in `Assets/Scripts/Run/ChunkBuilder.cs`)

Change the segment loop so each segment draws its own prefab from a per-cube set: thread the already-cached set through — `AttachStackedShell` currently takes `(cube, prefab)`. Change signature to `(cube, GameObject[] set, System.Random rng)` is wrong (rng draws inside composer break the one-draw-per-obstacle stream). Instead: caller draws a per-cube short list? Simplest stream-stable approach: keep one draw per obstacle for segment 0, then draw (count−1) more picks from the same set inside `AttachStackedShell` — but the composer no longer owns rng there. Look at the actual call sites: `PlaceRock`/`PlaceSpire` do NOT receive rng today (draw happens at call sites via `Draw(set, rng)`). So change `PlaceRock`/`PlaceSpire`/`PlaceIsland` to take `(GameObject[] set, System.Random rng)` again? That reverts the W2 draw-first refactor and changes arch matched semantics (arch loop draws pillar once deliberately).

Correct minimal approach honoring both: `AttachStackedShell(cube, firstPrefab, GameObject[] siblings, System.Random rng)` where the caller passes the first pick plus the set+stream for the remaining segments: islands/spires call sites do `Draw` once for segment 0 then pass `(set, rng)` for the rest; arch loop passes the matched pillarPrefab as segment 0 AND `(pillarSet, rng)` constrained to return the same prefab? No — matched pillars must share ONE prefab across both pillars AND their segments.

Cleanest: segment variety draws happen inside `AttachStackedShell(cube, GameObject[] set, System.Random rng, GameObject forcedFirst)` — null forcedFirst means draw all segments. Call sites: islands/spires pass `(set, rng, null)`; arch pillars pass `(pillarSet, rng, pillarPrefab)`; lintel passes `(lintelSet, rng, lintelPrefab)`. Arch rng draws per arch become: 1 (forced pillar) + (pillarSegments−1) + 1 (forced lintel) + (lintelSegments−1) — stream changes vs today, still seeded deterministic; suite proves nothing else breaks. Per-segment shaping inside the loop:

```csharp
float taper = 1f - 0.05f * i;                    // sea-stack narrowing, deterministic
seg.transform.localScale = fitted * taper;
seg.transform.localRotation = Quaternion.Euler(0f, (float)(rng.NextDouble() - 0.5) * 30f, 0f); // yaw only
```

(If the inside-bounds test fails on jitter, drop the rotation line and keep taper+variety, then re-run.) Cap with aspect preserved:

```csharp
float s = FitScale(parentScale, vis.size);                          // min-axis fit (existing)
float segH = vis.size.y * s;
int count = segH > 0f ? Mathf.Max(1, Mathf.FloorToInt(parentScale.y / segH)) : 1;
if (count > 8 && vis.size.y > 0f)
{
    count = 8;
    s = parentScale.y / (count * vis.size.y);                     // segments exactly fill height...
    if (vis.size.x > 0f) s = Mathf.Min(s, parentScale.x / vis.size.x);
    if (vis.size.z > 0f) s = Mathf.Min(s, parentScale.z / vis.size.z); // ...unless width binds: accept top gap (same accepted-gap rule as today)
    segH = vis.size.y * s;
}
```

Bottom-aligned base unchanged.

- [ ] **Step 4: Run suites to verify GREEN**

Run: `unity test --mode EditMode`, then `unity test --mode PlayMode`
Expected: exit 0 both; variety test passes; inside-bounds holds for tower/rock/flat visuals (if jitter breaks flat-platform bounds, drop jitter per the step above and re-run).

- [ ] **Step 5: Commit**

```powershell
git add Assets/Scripts/Run/ChunkBuilder.cs Assets/Tests/EditMode/ObstacleShellTests.cs; if ($?) { git commit -m "feat: varied tapered capped shell segments (overhaul 2)" }
```

### Task 3: Pipeline watercraft models + Gauntlet re-theme sets

**Files:**
- Modify: `Assets/Editor/VisualImport.cs` (`WatercraftModels` list, `BuildSkin` Gauntlet sets)
- Test: `Assets/Tests/EditMode/ObstacleShellTests.cs` (append set-content test)
- Generated: `Assets/Prefabs/Decor/<new watercraft prefabs>` + regen all Decor + 4 skins (via `BuildAll`)

**Interfaces:**
- Consumes: existing `BuildPrefab`/`GetOrCreateMaterial`/`V1Watercraft`, `Resolve`.
- Produces: `ship-small`, `ship-cargo-a`, `buoy`, `buoy-flag`, `cargo-container-a`, `cargo-pile-a` prefabs; Gauntlet sets (islands: `ship-small`, `ship-cargo-a`, `cliff_rock`; spires: `rock_tallA`, `rock_tallC`, `cliff_large_rock`; pillars: `rock_tallA`, `rock_tallB`, `rock_tallC`; lintels: `cliff_rock`, `cliff_large_rock`; decor: `buoy`, `buoy-flag`, `cargo-container-a`, `cargo-pile-a`, `chest`, `flag`, `flag-pennant`, `grass-patch`).

- [ ] **Step 1: Write the failing test** (append to `ObstacleShellTests.cs`)

```csharp
[Test]
public void GauntletSets_HaveNoCastles()
{
    var skin = UnityEditor.AssetDatabase.LoadAssetAtPath<RealmSkin>("Assets/Configs/RealmSkin_Gauntlet.asset");
    Assert.IsNotNull(skin, "missing Gauntlet skin");
    var all = new List<GameObject>();
    foreach (var set in new GameObject[][] { skin.DecorPrefabs, skin.IslandPrefabs, skin.SpirePrefabs, skin.ArchPrefabs, skin.ArchPillarPrefabs, skin.ArchLintelPrefabs })
        if (set != null) all.AddRange(set);
    Assert.Greater(all.Count, 0, "Gauntlet sets empty");
    string[] banned = { "castle", "tower", "structure", "platform" };
    foreach (var p in all)
        foreach (var b in banned)
            Assert.IsFalse(p.name.Contains(b), "medieval remnant in Gauntlet: " + p.name);
    bool hull = false, buoy = false;
    foreach (var p in all) { if (p.name.Contains("ship")) hull = true; if (p.name.Contains("buoy")) buoy = true; }
    Assert.IsTrue(hull, "no hulls in Gauntlet");
    Assert.IsTrue(buoy, "no buoys in Gauntlet");
}
```

- [ ] **Step 2: Run to verify RED**

Run: `unity test --mode EditMode`
Expected: FAIL "medieval remnant in Gauntlet" (castles still wired). Fails on behavior.

- [ ] **Step 3: Minimal implementation** (`Assets/Editor/VisualImport.cs`)

Add watercraft model entries built with `V1Watercraft` into `DecorDir` under plain names (`ship-small`, `ship-cargo-a`, `buoy`, `buoy-flag`, `cargo-container-a`, `cargo-pile-a`): extend the existing per-model loop pattern (mirror the `PirateModels`/`NatureModels` loops — add a `WatercraftModels` string array, no new machinery). Update the Gauntlet `BuildSkin` call with the sets from this task's Interfaces. Purge list is exactly: any name containing castle/tower/structure/platform leaves every Gauntlet array; chest/flag/flag-pennant/grass-patch stay in decor.

- [ ] **Step 4: Regenerate + verify ref stability**

Run: `unity run . -- -executeMethod VisualImport.BuildAll -logFile Temp\import.log`
Expected: exit 0, log shows `71+ prefabs, 4 skins` with the 6 new names and no `missing FBX`/`unresolved prefab` errors. Then `git status --short`: expect the 6 new prefabs + 4 skins + ~77 rewritten Decor prefabs (fileID churn, same GUIDs — known accepted noise). Then run `unity test --mode EditMode`: the `SkinAssets_ExistWithPanoramaAndPrefabs` test must pass (proves refs survived the regen); fix by re-running only if it does not.

- [ ] **Step 5: Run suites to verify GREEN**

Run: `unity test --mode EditMode`, then `unity test --mode PlayMode`
Expected: exit 0 both.

- [ ] **Step 6: Commit** (code + tests + all regenerated assets together — they are one atomic change)

```powershell
git add -A; if ($?) { git commit -m "feat: Gauntlet nautical re-theme sets plus watercraft pipeline (overhaul 3)" }
```

### Task 4: Density + buoy decor kind

**Files:**
- Modify: `Assets/Scripts/Run/ChunkBuilder.cs` (`DecorFit` buoy entry, `PlaceDecor` surface y for buoy kind), `Assets/Configs/ChunkSpec_*.asset` via spec edits? No — ChunkSpec assets are binary YAML; DecorCount changes go through a tiny editor snippet OR hand-edit YAML `DecorCount:` lines (integers, safe, verifiable by reload + tests). Prefer: hand-edit the four `DecorCount:` lines, then headless reload proves them.
- Test: `Assets/Tests/EditMode/ObstacleShellTests.cs` (buoy float + scale test)

**Interfaces:**
- Consumes: `DecorFit`, `PlaceDecor` kind switch.
- Produces: buoys floating at y≈0 off-lane; counts Lagoon 12, Gauntlet 10, Storm 12, Sky 8.

- [ ] **Step 1: Write the failing test** (append to `ObstacleShellTests.cs`)

```csharp
[Test]
public void BuoyDecor_FloatsAtSurfaceScaled()
{
    var builder = NewBuilder();
    var skin = ScriptableObject.CreateInstance<RealmSkin>();
    var buoy = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Decor/buoy.prefab");
    Assert.IsNotNull(buoy, "buoy prefab missing — Task 3 must land first");
    skin.DecorPrefabs = new GameObject[] { buoy };
    var spec = ScriptableObject.CreateInstance<ChunkSpec>();
    spec.ChunkId = "Buoy"; spec.Skin = skin;
    spec.Length = 500f; spec.DecorKind = "buoy"; spec.DecorCount = 2;
    builder.BuildChunk(spec, 0f, 5);
    Assert.AreEqual(2, builder.Decor.Count);
    foreach (var d in builder.Decor)
    {
        Assert.AreEqual(0f, d.transform.position.y, 0.01f, "buoy not at the surface");
        Assert.GreaterOrEqual(Mathf.Abs(d.transform.position.x), 25f, "buoy inside lanes");
    }
}
```

- [ ] **Step 2: Run to verify RED**

Run: `unity test --mode EditMode`
Expected: FAIL "buoy not at the surface" (buoys sink to −10 today). Fails on behavior. (If Task 3 has not landed, fails earlier on missing prefab — that is also a correct RED ordering signal; land Task 3 first.)

- [ ] **Step 3: Minimal implementation**

In `PlaceDecor`: `Vector3 pos = new Vector3(x, kind == "cloud" ? 95f : kind == "buoy" ? 0f : -10f, z);`. Add `DecorFit` entry `{ "buoy", new Vector3(2f, 3f, 2f) }`. Bump the four `DecorCount:` YAML lines (Lagoon 8→12, Gauntlet 6→10, Storm 8→12, Sky 6→8). No other changes.

- [ ] **Step 4: Run suites to verify GREEN**

Run: `unity test --mode EditMode`, then `unity test --mode PlayMode`
Expected: exit 0 both. If a count-pinning test fails on the new DecorCounts, update it only if the failure is the intended density change, citing this task.

- [ ] **Step 5: Commit**

```powershell
git add Assets/Scripts/Run/ChunkBuilder.cs Assets/Tests/EditMode/ObstacleShellTests.cs Assets/Configs/ChunkSpec_Lagoon.asset Assets/Configs/ChunkSpec_Gauntlet.asset Assets/Configs/ChunkSpec_Storm.asset Assets/Configs/ChunkSpec_Sky.asset; if ($?) { git commit -m "feat: decor density plus floating buoy kind (overhaul 4)" }
```

### Task 5: Proof — captures, review, records, push

**Files:**
- Modify: `docs/superpowers/specs/2026-10-07-environment-overhaul-design.md` (§34.1 CR entries if implementation diverged)
- Verify: `Temp/VisualVerify/*.png` (gitignored working files, never committed)

**Interfaces:**
- Consumes: all Tasks 1–4 green on `main`.
- Produces: pushed branch + explicit user-playtest acceptance items.

- [ ] **Step 1: Capture all realms headless**

Run: `unity run . -- -executeMethod VisualVerify.CaptureAll -logFile Temp\verify.log`
Expected: exit 0, 6 frames in `Temp/VisualVerify/`, audit lines show `bare=0` per chunk. Read every frame: gate arches breaking the surface, 8-wide spires reading as needles (not skewers), 13-wide islands as mounds, matched rock arches, buoys floating, no gray cubes, moods intact. If any frame is wrong, STOP: file a fix task, do not proceed.

- [ ] **Step 2: Request code review**

Dispatch `task` subagent `general` with the W-plan review template (BASE = first overhaul commit, HEAD = branch tip, DESCRIPTION = environment overhaul Tasks 1–4, REQUIREMENTS = this plan + spec). Fix Critical immediately, Important before push, note Minor.

- [ ] **Step 3: Append §34.1 records**

In the spec doc, under a `## A§34.1 change records` section, add one entry per divergence using the protocol format (Current Requirement / Reason for Conflict / Proposed Change / Affected Systems / New Behavior / Updated Acceptance Criteria). Commit: `docs: overhaul §34.1 records`.

- [ ] **Step 4: Push and hand to playtest**

Run: `git push origin main`
Expected: `main -> main`. Then report to the user with the two explicit acceptance items ONLY they can settle: (1) breach feel — swim −3 to surface ring +0.5 within control limits across tiers; (2) dodge feel — 8-wide spires / 13-wide islands at stream speed without unfair seals. No success claim until they playtest.
