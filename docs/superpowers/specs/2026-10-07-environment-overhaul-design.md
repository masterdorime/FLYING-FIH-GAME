# Environment Overhaul — scale bible, stacking quality, Gauntlet re-theme

Owner-approved 2026-10-07 (big-bang A). This supersedes visual-spec
options that produced the skewer-stacking and the medieval Gauntlet
(both driven by asset availability, never by game fiction). The PRD
mandates no realm fiction (biomes are PROPOSED) and requires §12.4
safety (no impossible collisions, no sealed prompts, paths within
control limits) through every change below.

Ruler for everything: the fish is ~3.5 long, ~1.5 tall
(`M1SceneBuilder` fish parts; controller 2 high, 0.5 radius).

## 1. Scale bible (colliders change — gameplay Rubicon, stated openly)

| Element | Before | After | Notes |
|---|---|---|---|
| Swim ring lane | `SwimY` −3 (fully sunk) | new `SurfaceRingY` ≈ +0.5 | arch breaks surface; fish swims at −3 and breaches through; judging follows (positional) |
| Spire collider | 4 × (50–90) | 8 × (30–45) | believable needle; `SpireCenterY` kept, top presence kept |
| Island collider | 10×25×10 @ y=5 | 13×20×13 @ y=5 | wider stance, sea-mound rise kept; `IslandHalfX` 5 → 6.5 updated everywhere consistently (lanes ±16.5) |
| Arch pillar | 4×20×4 | 6×18×6 | anchors ±8 unchanged (ring-relative) |
| Arch lintel | 20×4×4 | 20×5×5 | span unchanged |
| Fly/sky lanes | unchanged | unchanged | `FlyY`, `SkyBaseY`, spire centers untouched |

`SpireHalfDepth` (reclaim timing) updated with the new width. Every
widened volume re-passes clearance validation; breach feasibility
(−3 to +0.5 within control limits) gets a dedicated playtest — the
movement code already chases ring positions, but feel needs eyes.

## 2. Stacking quality (no more skewers)

`AttachStackedShell` grows up: per-segment seeded picks from the set
(spires/islands vary; arch pillars keep one shared pick so arches stay
matched), ~5% upward taper per level like real sea stacks, small seeded
rotation jitter, cap 8 segments (larger segments below it). Same
volumes, same inside-bounds guarantee, same determinism — no uniform
repeats. The old "one prefab per cube" test expectation is superseded
and rewritten (it pinned the skewer look).

## 3. Gauntlet re-theme (castles out)

Gauntlet becomes sea-canyon cliffs + nautical race course:

- pillars/lintels/islands/spires: cliff-rock sets only (`rock_tall*`,
  `cliff_rock`, `cliff_large_rock`, `rock_large*` as fitting).
- islands add **breached hulls** (`ship-small`, `ship-cargo-a` — a ~13m
  hull inside a 13m island volume, the fit the old boxes never had).
- decor adds **buoys** (new `buoy` kind parked at y≈0 like real race
  markers), buoy-flags, cargo containers/piles as seabed debris.
- purged from every Gauntlet set: castle/tower/wall/structure pieces.
  Chest + signal flags stay (treasure and seamanship, not masonry).

Pipeline imports the new watercraft models (`V1Watercraft` covers them;
same colormap pattern as pirate). Other realms keep their sets,
rescaled by the bible. Mood/fog left alone (not complained about).

## 4. Density

Decor counts up ~50% per spec (Lagoon 8→12, Gauntlet 6→10,
Storm 8→12, Sky 6→8; exact numbers playtest-settled), x-bands
unchanged (near-band decor deferred — small floating objects near
lanes risk reading as obstacles). Islands/spires/distant silhouettes
carry the midground.

## 5. Validation and tests (TDD, RED first)

- Unit: bible volumes (exact scales/positions), surface-ring lane,
  stacked variety (segments draw across the set, capped at 8 per
  cube), taper bounds,
  inside-collider invariant kept, decor kind placement (buoys float),
  clearance re-validation on widened volumes.
- Rewritten: one-prefab-per-cube expectation; any exact-sequence pins
  the arch-draw change breaks (documented, deterministic still).
- Suites: full EditMode + PlayMode green. PlayMode cannot settle feel —
  breach + dodge playtests are explicit acceptance items for the user.
- Captures: headless before/after per realm, inspected by the agent;
  user playtest for feel.

## 6. Rollout (one track, phased commits)

1. Bible colliders + lanes + dependent consts (gameplay core).
2. Stacking quality upgrade.
3. Pipeline + Gauntlet re-theme sets (+ regen, ref-stability check).
4. Density + buoy kind.
5. Suites + captures + review + §34.1 records + push.

Each phase independently verifiable; thrash contained by phase.

## 7. Risks, honestly

- Widened colliders change dodge space: gaps get tighter. Clearance
  tests + §12.4 re-validation + user playtest are the mitigation, in
  that order. If breach or dodge feels wrong, lanes/widths retune as
  data (consts), not redesign.
- Fixed-count decor rise costs draw calls: trivial meshes, accepted.
- Big-bang mixes feel + look: phase commits + per-realm captures keep
  attribution possible; any phase can be reverted independently.
- KNOWN GAP (accepted): the 0.6 coverage rule still hides up to 40% air
  on the binding axis. Timing windows should assume ~1–2m of forgiveness
  around rock visuals (M2). The pink source was panorama hue × rose
  multipliers (fixed by cooling the multipliers; panorama kept).

## 8. §34.1 change records (implementation divergences)

### CR-O1: no per-segment draws, no jitter

Current Requirement: §2 specified per-segment seeded prefab picks and
±15° yaw jitter for stacked variety.

Reason for Conflict: per-segment draws change the rng stream shape
(exact-sequence breakage); any yaw on exact-width fits pokes out of
the collider (verified against the inside-bounds invariant).

Proposed Change: one seeded pick per obstacle (W2 stream shape
preserved, zero draw-count change); taper + cap-8 alone fix the
skewer silhouette. Obstacles still vary across each other by pick.

Affected Systems: `AttachStackedShell`, `ObstacleShellTests` (taper +
cap pins, no variety assertion).

New Behavior: stacked columns share one prefab per cube, narrowing
upward, max 8 levels.

Updated Acceptance Criteria: taper scales strictly decrease, count
caps at 8, all levels inside bounds (unit tests); captures inspected.

### CR-O2: Gauntlet decor kind + composition

Current Requirement: §3 re-theme sets; §4 density counts.

Reason for Conflict: buoys placed under kind `rubble` sink to −10
(the buoy test proved it); pole flags and grass do not float.

Proposed Change: Gauntlet `DecorKind` rubble→buoy (y≈0 surface
flotsam field); decor set trimmed to buoy, buoy-flag, cargo ×2,
chest; pipeline source updated identically so regens converge.
`IslandHalfDepth` 5→6.5 added alongside `IslandHalfX` (z-half of the
13-wide island, same reclaim-margin reasoning).

Affected Systems: `PlaceDecor`, `DecorFit`, `ChunkSpec_Gauntlet`
(kind, count), `RealmSkin_Gauntlet` (5-entry decor set),
`VisualImport` Gauntlet decor list.

New Behavior: buoys/markers/cargo bob on the surface off-lane;
Gauntlet decor count 10.

Updated Acceptance Criteria: buoy float + scale + lanes test;
Gauntlet no-castles test; suites green.

### CR-O3: lintel meets pillar tops

Current Requirement: bible lintel offset +12 (spec §1 carried the old
value against shorter 18-tall pillars).

Reason for Conflict: headless capture showed a 0.5m sky slit between
pillar tops (9.0) and lintel bottom (10.0).

Proposed Change: lintel offset +12→+11.5 in both build paths and the
layout validator (exact touch at 9.0).

Affected Systems: `ChunkBuilder` arch sites, `ChunkLayout`
validation volumes, layout arch test.

New Behavior: lintel bottom touches pillar tops; gate reads continuous.

Updated Acceptance Criteria: arch position unit tests; capture
inspected.
