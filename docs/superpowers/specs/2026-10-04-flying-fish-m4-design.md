# M4 — Endless Run (spec, approved in brainstorming 2026-10-04)

Goal: the dead-end 3100m runway becomes an endless run. A RunManager
builds chunked world ahead of the fish and deletes it behind, forever.
Difficulty ramps on the run clock. Recovery is in-flow (lagoon
breathers); runs never end (no fail screens — M5 owns results/menus).
Success (PRD Milestone 4): a complete 60–90 second playable run exists.

Player goal (explicit): high score by distance traveled and coins
collected. Score = distance trickle + coin pickups + timing bonuses,
all scaled by the live tier multiplier (score chase v1, already live).
Every system serves the score: tiers multiply it, rings feed the gauge
that sustains the flight that reaches the coins, missions bonus it.
Session score shows in the overlay; saved high scores wait for M5.

Decisions already approved: endless + always recoverable; hybrid chunks
(all four types below); difficulty = run clock; testing tunes revert to
spec; spawn director (no prefabs); score-chase + coin lines feed chunks;
environment looks ride per chunk type.

## S1 — Architecture (all new unless noted)

- `RunManager` (new, on a GameManager object): owns the difficulty clock
  (elapsed run seconds → 0..1 ramp), the type deck (weighted shuffle of
  the four chunk types, seeded; Storm weight grows with difficulty), and
  spawn/recycle (spawn horizon 2 chunk lengths ahead of the fish,
  reclaim anything more than 1 chunk length behind).
- `ChunkSpec` (new ScriptableObjects, one per type): length, ring lines,
  coin trails, island set, mood params (sky color, fog density, water
  tint), difficulty bands it may appear in, mission card text, difficulty
  rating. Declares the PRD §12.5 set: entry/exit speed range, required
  movement space, obstacle set, prompt set, recovery availability.
- `ChunkBuilder` (new): turns a spec + seed into placed content using
  pooled primitives (rings, coins, islands — the existing entity types;
  water/seabed become long recycled segments, not one plane).
- `TimingPromptSpawner` (existing, untouched logic): keeps judging taps,
  charges, beats, rockets, coins; receives its rings/coins from the
  builder instead of the scene file. Behaviors (windows, gains, jackpots,
  slow-mo, shuffle) do not change in M4.
- `DifficultySettings` (new SO): ramp rate (seconds to full difficulty),
  band edges, speed-ramp cap, window-tighten factor, prompt-density
  factor, per-type weight curves.
- Boundaries (PRD rule 6): RunManager never touches timing evaluation,
  gauge math, or scoring math. All numbers in SOs (rule 2); seeded RNG
  only (rule 3); evaluation independent from presentation (rule 4).

## S2 — Chunk types (gameplay + look + mission)

- **Lagoon** (recovery breather): calm water, coin trails, easy beats,
  no islands. Bright day, clear water. Mission: "collect 30 coins".
- **Ring gauntlet** (gauge/rocket building): dense charge-ring lines
  with coin connectors. Golden-hour light. Mission: "2 clean rings".
- **Storm slalom** (pressure): rock stacks to steer around (colliders
  block like today's islands, no damage), sparse rewards. Dark sky,
  choppy water tint, denser fog. Mission: "pass 4 rock gates".
- **Sky arcs** (flight legs): high air-coin arcs + dive gaps between
  swim stretches. Sunset, clouds. Mission: "stay airborne 20s".
- Mission cards show as one overlay line on chunk entry (scaffold text;
  M5 does real UI). Completing a mission pays `MissionBonus` (100) ×
  multiplier (new `ScoringSettings` field).

## S3 — Difficulty (run clock)

- `t = clamp01(elapsed / rampSeconds)`: baseline target speed creeps up
  (capped), timing windows tighten ×(1−t·factor), beat/ring density rises,
  Storm deck weight grows, Lagoon keeps a weight floor (a breather is
  always drawable — the recovery guarantee).
- Gauge thresholds may re-tune per difficulty band (M3 handoff); final
  numbers in the plan, bounded by the reverted spec table.

## S4 — Tune lock (testing tunes revert to spec in M4)

- AMENDED per controller ruling R3 (M4 Task 7, PRD §34.1 change
  protocol — rationale recorded, no silent rule change): tank STAYS
  48, bands STAY 8/16/28/40 (flat +6/ring gains × 120-tank ≈ 20
  rings to launch — unplayable, contradicts the standing
  2-ring-flight goal). ONLY slow-mo reverts to spec: 0.4×/1.2s
  (code defaults + asset). Original revert line it replaces: tank
  120, bands 20/40/70/100, slow-mo 0.4×/1.2s.
- Flattened 2026-10-04 per playtest (below spec, replaces the revert
  for these rows): charge +1/+2 (perfect ring 6), Perfect {0,1,1,2,2},
  Good {0,1,1,1,1}, Miss {0,4,6,10,16}, MissDrain 3 — tiny gains, tiny
  losses. Consequence accepted: ~8 perfect rings to fill the tank;
  retune launch pacing in the plan if testing drags.
- Keep (approved mechanics, not testing tunes): 8s timeout (pairs with
  4-arrow sequences), ship swim, launch-keeps-tank, arrow glyphs, glide
  v2, rockets, score chase, all-swim rings (rings dissolve into chunk
  spawning anyway; air rings return via Sky arcs).
- Open (deferred, revisit under load): the burst-ceiling contradiction
  (`max(tier max + boost, SpeedCap)`).

## S5 — Safety (PRD §12.4, must-prevents, pinned by tests)

- No impossible collision configurations (gates wider than turn radius
  at the band's top speed), no unavoidable timing failures (beat gaps
  fit inside chunk lengths at band speed), no prompts hidden behind
  geometry (spawns keep line-of-sight from entry), no paths beyond
  control limits (pitch/yaw cone respected by arc placement), no
  impossible recovery states (Lagoon floor + swim always rebuilds).

## S6 — Tests (TDD, red → green → commit)

- Streaming: chunks spawn ahead / reclaim behind over long runs; fish
  never outruns content; no duplicate spawns.
- Determinism: same seed replays the same chunk order + ring/coin layout.
- Difficulty: clock advances bands; windows tighten; Storm weight grows;
  Lagoon floor holds.
- Tune lock: asset values back at spec table (revert pins).
- Missions: entering a chunk shows its card; completing pays the bonus.
- Full EditMode + PlayMode suites green (batchmode; live-only camera
  transients stay telemetry-covered per ledger).

## S7 — Out of scope (explicitly NOT M4)

Menus, results screens, audio, real UI/VFX (overlay text only),
tutorial, saves, damage/obstacle penalties, chaser (M5 obstacles),
high-score persistence (M5 saves), prefabs for chunks, multiplayer.

## Change record — map density: decor + gates (§34.1, approved)

- **Old:** 3 of 4 chunk types held nothing but rings and coins; spires
  uniform; horizon void.
- **Why:** playtest: the map feels empty; flight especially has no
  obstacles.
- **New:** per-chunk decor (`DecorKind`/`DecorCount`: coral/rubble/crag/
  cloud) — pooled visuals, never colliding, parked off-lane, so decor
  carries no safety burden. Gauntlet rock gates (`ArchCount`): blocking
  pillar pairs + lintel anchored over swim ring lines, steer through the
  middle (no damage — M5 owns teeth). Spire heights vary 50–90 per seed.
  Horizon mesas follow the fish. Assets: Lagoon coral 8,   Gauntlet rubble
  6 + 3 gates, Storm crag 8, Sky cloud 6.

## Change record — sky layer per chunk + realm blend (§34.1, approved)

- **Old:** chunks were water-only; flight had nowhere to go and the sky
  was empty mood.
- **Why:** flight needs a destination: a cloud realm parallel to the
  whole run (launch can happen at any z, so sky content can't live in
  zones).
- **New:** each chunk builds a sky half from new spec fields
  (`SkyRingCount/SkyCoinTrails/SkySpireCount`; Lagoon 0/1/0, Gauntlet
  2/1/2, Storm 0/1/3, Sky 4/2/4): rings/coins above 55m through the
  existing pools and spawner feed, spires as tall colliders under the
  same clearance rule (3D bounds). A cloud-sea deck fades in 40→60m
  (`RealmBlend`) and follows the fish; ocean below, clouds above, no
  teleport, descent always lands back in the swim flow. Spawner, gauge,
  missions, and difficulty untouched (all altitude-blind already).

## Change record — water stays transparent (§34.1, approved)

- **Old:** mood tints stamped full water color incl. alpha 1 onto an
  opaque runtime material — the sea hid the fish and the swim rings.
- **Why:** gameplay visibility beats mood: underwater play must read.
- **New:** sea material keeps the M1 transparent blend (alpha 0.6);
  moods tint RGB only. Storm murk stays a color shift, never opacity.

## Handoff notes for M5

- M5 reskins dial/bar/overlay/coins/gems with real UI/VFX and deletes
  `[M1-SCAFFOLD]` leftovers (incl. debug input + overlay).
- M5 adds menus, tutorial, audio, obstacles-with-penalties (chaser),
  results, saves, settings; chunk mission cards graduate to real UI.
- Final tuning (M6 territory): windows, gains, drains, profiles, camera,
  VFX, obstacle density, difficulty curve.
