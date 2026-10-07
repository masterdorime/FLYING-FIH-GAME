# Showcase Chunks (B3) — hand-placed map editing, phase (i) built

Owner-approved 2026-10-07 (map-editor track, following the visual
reconstruction). Phase (i) — data model + builder + tests — is
implemented, reviewed, and green. Phases (ii) editor window and (iii)
validation/capture integration are planned below and start only on
separate approval.

## 1. Problem

Endless-run content is fully procedural (`ChunkSpec` counts + seed).
There is no way to author a specific arrangement ("I want THAT gate
sequence in my run"). ChunkSpec numbers tune density, never placement.

## 2. Decision: B3 showcase chunks

Three models were considered: B1 realm templates (kills variety without
layout sets), B2 procedural nudges (fiddly, edits offsets against dice
rolls), **B3 showcase chunks (chosen)** — hand-designed fixed chunks
that join the rotation while the procedural stream flows around them.
Smallest runtime change, fully deterministic, zero variety loss.

## 3. Data model — `ChunkLayout` SO

One asset per showcase chunk (`FlyingFishMomentum.Run`,
`CreateAssetMenu`). Schema `Version` (= 1), `LayoutId`, fixed `Seed`,
and entry arrays with stable string IDs:

- rings: `Position` (layout-relative; zStart added at build, y absolute
  — height is gameplay, so Y is deliberately NOT locked).
- islands: `Position`, collider `Scale`, `ShellName`.
- spires: `Position`, `Height`, `ShellName`.
- arches: `Anchor`, `PillarShell`, `LintelShell` (one entry per arch, so
  pillars match by construction).

Shells resolve **by prefab name** against the spec skin's per-kind sets
(names survive pipeline regenerations; fileIDs do not). Coins derive
from placed rings with the standard trail code on the layout-fixed
seed; decor stays procedural (kind/count from the spec). A layout chunk
streams identically every time by design.

`ChunkSpec` carries one optional `Layout` field; null = procedural
(byte-identical behavior, proven by the untouched suite).

## 4. Runtime — smallest touch

`BuildChunk` branches to `BuildLayoutContent` after skin caching,
reusing every place function with explicit params (same colliders,
shells, pooling, gate rings) and closing with the shared
`SyncTransforms` + `ResolvePromptClearance` tail. Procedural coin-trail
and decor loops were extracted verbatim into shared helpers (suite
proves no behavior change). Judging untouched (positional).
`RunManager` untouched — the deck is already `List<ChunkSpec>`.
Invalid entries are skipped with `Debug.LogError`, never throwing.

## 5. Validation — `ChunkLayout.Validate(spec)`

Pure ship-gate returning error strings: schema, LayoutId, count caps
(10/10/8/5, mirroring the largest procedural specs), duplicate/empty
IDs, finite values, lane/height/z bands (|x| ≤ 20, y in [-15,80],
z in [0,Length]), scale/height ranges, shell resolution, and ring
clearance — disc-vs-rock-AABB with the ±2m judging sweep window, edge
touch passes. Arch validation covers derived pillar (±8x) and lintel
(+12y) volumes, not just the anchor. Runtime guards are deliberately a
crash-guard subset; the editor tool (phase iii) runs Validate as the
hard gate before a layout can stream.

## 6. Tests (TDD, RED first)

`ChunkLayoutTests`: exact authored transforms (arch offsets included),
wall-seed independence, bad-entry skipping (`LogAssert.Expect`), and
validation accepts/rejects (sealed ring, clear layout, duplicates,
out-of-bounds, schema). Full EditMode + PlayMode suites stay green.

## 7. Rollout

- Phase (i) ✅ data model + builder + tests (place via Inspector
  numbers; preview via the headless capture loop).
- Phase (ii) (planned): `ShowcaseChunkEditor` window — build preview in
  the open scene, Scene-view drag handles + per-entry fields, save back
  to the SO, Undo support. Preview objects are draft-only (scene never
  saved — same discipline as visual-verify captures).
- Phase (iii) (planned): Validate wired into the window as ship-gate +
  capture integration (before/after frames per layout).
- Registration: layout specs join the deck list in `M1SceneBuilder`
  (scene rebuild required — fileID churn is known; scoped, verified by
  diff). Deck weights stay low until layouts prove themselves in
  playtests; fixed chunks repeat identically (seeded cosmetic variation
  explicitly deferred).

## 8. PRD compliance (§34)

No gameplay rules, timings, judging, or colliders change; no new
mechanic (same prompts, authored positions). Parameters live in SOs.
Deterministic (fixed seed). Visuals stay inside collider volumes. Debug
visibility: validation errors name the exact entry (`ring 'r1' sealed
by island 'i1'`). This file is the design record for the subsystem.
