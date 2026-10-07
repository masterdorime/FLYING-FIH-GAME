# Visual Reconstruction — Design Spec (2026-10-07)

## Context

M4 endless run renders programmer art: LineRenderer rings, gold-sphere
coins, scaled-cube islands/rocks/spires/silhouettes, RGB-tinted
water/sky/cloud-sea. `ChunkBuilder.ApplyMood` tints fog/ambient/sky/water
per `ChunkSpec` (Lagoon, Gauntlet, Storm, Sky). `Assets/ArtVendor/` now
holds approved CC0 art (committed `bfd8ae7`): 5 Kenney panoramas, Kenney
props (gates/buoys/arrows/ramps), 46 KenneyPirate FBX (palms, rocks/sand,
fortress, docks, flags, chest, shipwreck), 25 KenneyNature FBX (rocks
incl. tall spires, palms, pines, cliffs/waterfalls, platforms,
`statue_ring`). License-flagged `Rocks/` and `tsundereshark/` stay
UNUSED (no license file — verify before shipping, unchanged by this spec).

Goal: skybox integration → gameplay props → cohesion pass → verify.
Presentation-only: no judging, timing, gauge, scoring, or difficulty
changes (PRD §34 rules 3/4/10).

## Approaches considered

**A. Dressing-only overlay.** FBX prefabs into the decor pool + panorama
per realm; rings/coins/islands/spires stay primitives. Cheapest, but two
visual languages coexist permanently and islands/spires (the most-seen
volumes) stay cubes.

**B. Full prefab replacement.** Rings→torus mesh, coins→FBX gem,
islands→platform FBX with fitted colliders. Most cohesive, but: no torus
ring model in hand; ring readability is gameplay-critical (PRD rule 4 —
timing evaluation stays independent from presentation, and the ring IS
the timing target visually); placement/clearance math is coupled to
primitive sizes (`PlaceRock` scales, `ResolvePromptClearance`). Largest
blast radius.

**C. Realm-skin system (RECOMMENDED).** New `RealmSkin` ScriptableObject
per chunk type (panorama, tints, decor/island/spire prefab sets);
`ChunkBuilder` consumes it in `ApplyMood`/build. Gameplay-critical
shapes (rings, coins) keep geometry for readability but join the shared
palette; decor/islands/spires/arches render FBX visuals over unchanged
collider shells. SO-driven tuning (PRD rule 2), seeded prefab choice
(rule 3), UI untouched (rule 5).

## Design (approach C)

### 1. `RealmSkin` SO (new file `Assets/Scripts/Run/RealmSkin.cs`)

```csharp
namespace FlyingFishMomentum.Run {
[CreateAssetMenu(fileName = "RealmSkin", menuName = "FlyingFish/Realm Skin")]
public class RealmSkin : ScriptableObject {
    public Texture2D SkyPanorama;      // 2:1 equirect, Skybox/Panoramic
    public Color SkyTint = Color.white; // multiplied over panorama
    public Color FogColor; public float FogDensity;
    public Color WaterTint;             // RGB only (alpha stays 0.6)
    public Color CloudTint = new Color(0.95f, 0.97f, 1f);
    public Color SilhouetteColor = new Color(0.16f, 0.2f, 0.3f);
    public Color RingTint = new Color(1f, 0.85f, 0.2f); // prompt palette
    public Color CoinTint = new Color(1f, 0.75f, 0.15f);
    public GameObject[] DecorPrefabs;   // seeded pick, decor pool
    public GameObject[] IslandPrefabs;  // visual shells over island colliders
    public GameObject[] SpirePrefabs;   // visual shells over spire colliders
    public GameObject[] ArchPrefabs;    // pillar/lintel shells (Gauntlet)
}
}
```

`ChunkSpec` gains one field: `public RealmSkin Skin;` (null = current
primitive behavior, so old specs keep working and tests pin the fallback).

### 2. Skybox mapping (approved)

| Realm  | Panorama             |
|--------|----------------------|
| Lagoon | `skybox-day.png`     |
| Gauntlet | `skybox-morning.png` |
| Storm  | `skybox-night.png`   |
| Sky    | `skybox-alien.png`   |

`skybox-space.png` stays spare. Sky shell uses `Skybox/Panoramic` shader;
`SkyTint` multiplies (existing `SkyMaterial.color` assignment becomes
tint-over-panorama). `ApplyMood` sets `RenderSettings.skybox` from the
skin when non-null, else leaves current behavior.

### 3. Prefab sets (Phase 0 import — materials + prefabs, no code)

- Materials: pirate `colormap.png` → one shared Standard material;
  nature sets → prefix-tinted shared Standard materials (amended
  2026-10-07 per import probe: NO staged FBX carries vertex colors, so
  the vertex-color material is dropped; nature models use multi-slot
  FBX materials, all slots remapped to the set tint). Ring/coin
  materials re-tinted to shared palette (geometry unchanged).
- Prefabs under `Assets/Prefabs/Decor/` (vendor folder stays untouched):
  per-model prefabs cloning the FULL FBX hierarchy (multi-mesh models
  like ship-wreck keep all parts) with remapped shared materials,
  NO colliders on decor; island/spire/arch prefabs are VISUAL ONLY.
- Collider-shell pattern: `PlaceIsland`/`PlaceSpire`/arch code keeps
  exact positions/scales/colliders; the visible cube renderer is disabled
  and the seeded FBX visual is parented under it (unit-scaled to the
  shell bounds). Gameplay volumes provably unchanged → clearance tests
  keep passing unmodified.

Prefab → chunk-type mapping (initial, tunable in the skin assets):

| Set | Lagoon | Gauntlet | Storm | Sky |
|-----|--------|----------|-------|-----|
| Decor | palms, sand patches, shipwreck | fortress walls/towers, flags, chest | rocks, pines, crags | `statue_ring`, platforms |
| Island shell | `platform_beach`, rock_large | `platform_grass`, cliff_rock | rock_large, cliff_rock | `platform_grass`, `statue_ring` |
| Spire shell | rock_tallA/B | tower-complete-small, rock_tallC | rock_tallA/C | rock_tallB, `statue_ring` |
| Arch shell | rocks-sand pillars | castle-wall/gate pieces | rocks pillars | — (Sky has no arches) |

`DecorKind` strings (`coral, rubble, crag, cloud`) keep working as
fallback selectors when a skin's `DecorPrefabs` is empty.

### 4. Determinism

Prefab index comes from the existing per-`BuildChunk` `System.Random`
(`rng.Next(prefabs.Length)` at each placement site). Same seed → same
dressing. No new RNG streams, no `Random` in gameplay paths.

### 5. Cohesion pass

Silhouette color + cloud tint ride the skin (fields above); ring/coin
materials re-tinted per skin in `ApplyMood` (shared instances, same
pattern as water/sky). Water keeps RGB-only stamping (alpha 0.6) —
existing transparency fix stands.

### 6. Tests (TDD, red → green → commit per behavior)

EditMode (all drivable via `Tick`/`BuildChunk` directly, no synthetic input):
- Skin asset validity per realm: panorama non-null, prefab refs non-null,
  arrays non-empty (fails until Phase 0 import + skin assets exist).
- `ApplyMood` with skin sets `RenderSettings.skybox`, fog, ambient,
  water RGB; null skin keeps legacy tint path.
- Seeded determinism: same seed → same decor prefab instance names;
  different seed may differ.
- Fallback: empty `DecorPrefabs` → primitive decor (existing tests cover).
- Collider invariance: island/spire/arch collider bounds identical with
  and without skin (existing clearance tests keep passing unmodified).
PlayMode: full suite stays green; no new input/camera-transient tests
(batchmode can't settle visuals — ledger precedent; visual proof is a
live playtest screenshot, not an assert).

### 7. Out of scope (NOT this track)

Fish player model (stays procedural), shark boss (blocked on license),
coin/ring geometry changes, UI/HUD reskin (M5), audio, saves, tutorial.
Overlay/debug scaffold stands.

## §34.1 change record

New `RealmSkin` SO type + one `ChunkSpec.Skin` field; presentation-only
subsystem. No gameplay rules, timings, or judging change; no PRD mechanic
added. Spec file is the §34.1 documentation for this divergence (visuals
previously undefined beyond mood tints).

### CR-2 (2026-10-07): rings are gate-arch models, never tinted

Current Requirement: option A — rings keep LineRenderer geometry,
re-tinted per realm (`RingTint`); option B (prefab replacement) rejected
for readability and no in-hand model.

Reason for Conflict: playtest showed re-tinting changed nothing — all
four skins were authored with the legacy gold, so rings looked identical
to programmer art. Owner directed rings to use game assets (watercraft
swim-ring, identified in-editor as the `gate` arch).

Proposed Change: `CreateRing` instantiates `Assets/Resources/Ring.prefab`
(watercraft `gate` + `V1Watercraft` colormap, built by
`VisualImport.BuildRing`), uniform-fits the outer height onto the
judging disc (`TimingPromptSpawner.RingPromptRadius`, now the single
source both sides reference), centers bounds on the ring position.
`ApplyMood` no longer touches rings; `RealmSkin.RingTint` kept declared
(asset compat) but unused. Legacy `LineRenderer` remains as last-resort
fallback only. Judging, pooling, `Consume`/`Reset` untouched.

Affected Systems: `ChunkBuilder.CreateRing`/`ApplyMood`/`Configure`
(new optional `ringPrefab`, `Resources` fallback — no scene rebuild),
`VisualImport` (watercraft support, `BuildRing`), `TimingPromptSpawner`
(`RingPromptRadius` const), `RingModelTests` + 2 rewritten tint tests.

New Behavior: every realm shows the orange/white gate arch facing
travel, legs vanishing into the sea; coins stay gold spheres.

Updated Acceptance Criteria: model bounds match judging disc (unit test
+ headless capture inspected); EditMode + PlayMode suites green; no
`LineRenderer` in captures while `Ring.prefab` exists.

### CR-3 (2026-10-07): stacked shells, matched arches, decor scale

Current Requirement: one min-axis-fitted shell per collider cube,
centered; random prefab per cube; skinned decor at natural FBX scale.

Reason for Conflict: playtest screenshot (mismatched + floating
pieces) plus headless captures: min-axis fit makes visuals far smaller
than tall colliders (8-unit blob centered in a 70-unit spire column),
per-cube random picks build incoherent arches, decor renders as ~2-unit
dots vs 8-unit legacy blobs.

Proposed Change: `AttachStackedShell` — one seeded pick per obstacle,
min-axis fit, floor-fill count of bottom-aligned segments (tall visuals
collapse to a single exact-fit segment, so lintels behave as before);
arch loop draws one pillar prefab (both pillars match) + one lintel
prefab from new optional `ArchPillarPrefabs`/`ArchLintelPrefabs` (empty
falls back to `ArchPrefabs`); skinned decor uniform-fits legacy kind
volumes (coral 2.5x3.5x2.5, cloud 8x4x6, crag 3x10x3, rubble 2.5 cubed)
about the prefab pivot. Gauntlet pillar set = tall towers, lintel set =
wide castle pieces; Sky spires drop `statue_ring` for three rock_talls.
Collider cubes, positions, judging, RNG shape (one draw per obstacle)
unchanged.

Affected Systems: `ChunkBuilder` (shell composer, arch/island/spire
call sites, `Draw` helper, `DecorFit` map), `RealmSkin` (2 optional
fields), `VisualImport.BuildSkin` (pillar/lintel params, Sky spires),
regenerated Decor prefabs + 4 skins, `ObstacleShellTests`, one
rewritten shell-count expectation (single yields stacked).

New Behavior: spires/islands read as stacked rock/tower columns rising
from sea/clouds; arches read as matched gate + towers; decor carries
legacy weight. Verified in headless captures per realm.

Updated Acceptance Criteria: 1+ segments per cube, all inside collider
bounds, matched names per arch, decor at kind size (unit tests); suites
green; captures inspected (no gray cubes, no floating blobs).
