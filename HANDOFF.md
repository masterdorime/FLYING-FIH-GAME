# HANDOFF — Flying Fish Momentum (written 2026-10-07, context near full)

Read this first in a fresh session, then re-read `AGENTS.md` (note: it is
STALE — still claims M1-only scope; the project has shipped M1→M4 plus
score chase, flight v2, sky realm, endless director; treat the specs in
`docs/superpowers/specs/` as current, not AGENTS.md's scope line).

## 1. Repo state right now

- Branch `main`, HEAD `7dfa637 feat: fish-anchored guide arrow points at objective`.
- Tree has ONE uncommitted item: **`Assets/ArtVendor/` (untracked, 44 files)** —
  staged vendor art awaiting user approval. Do NOT commit until approved.
- Everything else clean and pushed (`git status --short` shows only the above).

## 2. What this session did (latest-first)

- `7dfa637` target pointer rewrite: single MMO-style guide arrow above the
  fish, `GuideBearingDeg`, PlayMode bearing asserts.
- `c6cc55e` target pointer v1 (spawner `NextObjective`, chevron+edge — SUPERSEDED).
- `246582b` coin vacuum + magnet at speed (disc `2+0.05×speed`, `AttractCoins`).
- `9895317` coin trails lead into rings (one trail per ring; `CoinTrails`/`SkyCoinTrails` fields REMOVED from code+assets+tests).
- `8f711fa` QTE taps no longer steer (`SetChargeHold` zeroes input during charge).
- `7637a93` glyph L/R mirror fix (`DrawDirection` compensates billboard flip).
- `8a8baed` bigger weaving coins (2× gems, sine weave), deleted dead builder helpers.
- `a905f99` flight ceiling (thin-air fade 60→110m, lid 115m, base sink 0.5).
- `d7d411f` map density (decor kinds, Gauntlet gates, spire variety, silhouettes).
- `6d642ed` sky realm per chunk + cloud-sea `RealmBlend`.
- `fd271da` water transparency fix (mood RGB-only, alpha 0.6).
- `e1ab78a` M4 final review fixes; full M4 (Tasks 1–8) executed subagent-driven.
- Suites at last check: **EditMode 149/149, PlayMode 40/41+** (PlayMode count
  grew; re-run both suites before trusting any number here).

## 3. Game state (tuning that matters — all in SO assets + code defaults)

- Charge QTE: 4 seeded arrows, +1/step +2 jackpot (perfect ring = 6),
  8s timeout, slow-mo 0.4×/1.2s, strict cardinal taps (diagonals ignored),
  steering held during charge. Beats: Perfect {0,1,1,2,2},
  Good {0,1,1,1,1}, Miss {0,4,6,10,16}, MissDrain 3.
- Gauge tank 48, bands 8/16/28/40, FlyDrain 1.5; launch preserves tank+tier.
- Flight v2: lift ∝ speed² vs tier top, dive/climb drive chase target,
  rockets (max 3, +1/ring, Space in open air), bank visuals, speed-scaled
  authority, FOV ladder 60–70 cap 72, camera dist flat 11.
- Score: distance +1/m, coin 10×tier-mult, Perfect 25×, Good 10×,
  MissionBonus 100; multiplier = tier+1.
- M4 endless: RunManager (clock/deck/missions/creep) + ChunkBuilder
  (pools, recycle, moods) + 4 chunk types; difficulty = run clock.
- Missions on chunk cards; overlay is debug text (M5 reskins all UI).

## 4. Staged vendor art (UNCOMMITTED — needs user approval)

Under `Assets/ArtVendor/` (originals untouched, FBX-only, GLB/OBJ dups skipped):
- `Kenney/Skyboxes/` — 5 panoramas (day/morning/night/alien/space, 4K 2:1).
  Approved mapping: day→Lagoon, morning→Gauntlet, night→Storm, alien→Sky.
- `Kenney/Models/FBX format/` — 8 ship hulls, 5 cargo, 8 gameplay props
  (gates, buoys, ramps, arrows) + colormap. Boats/houses/liner skipped.
- `tsundereshark/` — shark OBJ+texture+blend+preview. **QUARANTINED:
  no license file in zip — verify source/license before shipping.**
- `Rocks/` — 9 rock sets + palette + guide. **Same license flag: no
  license file — verify before shipping.**

## 5. Pending decisions / open threads (oldest context first)

1. **Approve + commit ArtVendor?** User said "gather and I will approve".
   After approval: commit (incl. generated `.meta`), then Phase 0 import.
2. **Shark license** (blocking boss track): user must supply source/URL.
3. **Shark boss track** (planned, not started): arena duel, run-ender,
   ~3000m milestone. Full plan in chat history 2026-10-07.
4. **Visual reconstruction track** (planned, not started): skybox
   integration → gameplay props → cohesion pass → verify. Spec: M5 seed.
5. **Map-density follow-ups**: user asked for richer shapes (done for
   spires/gates/decor); watch playtest feedback.
6. **M5 slice** (menus, real UI/HUD, audio, tutorial, saves, results) —
   untouched. Overlay/debug scaffold still stands.
7. Known parked items: burst-ceiling contradiction; batchmode-untestable
   camera transients (telemetry-covered); per-frame list allocs;
   unbounded chunk registry (all fine at current scale).

## 6. Non-negotiable working rules (from AGENTS.md + ledger)

- PowerShell ONLY (`Select-String`, `; if ($?)` — no `grep`/`&&`).
- Skills: `brainstorming` before any feature/behavior change (classify,
  get approval); `test-driven-development` RED→GREEN→commit, no exceptions;
  `systematic-debugging` on any bug/test failure; `verification-before-
  completion` (run checks, show output); `requesting-code-review` per group.
- `ponytail`: YAGNI ladder, never overrides TDD/PRD rules.
- PRD rules §34 always bind (SO-only tuning, determinism, UI/gameplay
  separation, §34.1 spec change records — specs live in
  `docs/superpowers/specs/`).
- Tests: `unity test --mode EditMode` / `--mode PlayMode`; parse
  `test-results.xml`; compiler errors via `Select-String "error CS"`
  on `Logs/Editor.log`. Unity CLI only (`unity` exe); batchmode PlayMode:
  no synthetic input — drive `TickMove`/`Tick` directly; live frames
  settle nothing (use fixed steps or `WaitForSecondsRealtime`).
- Commits `feat|test|chore: <what>`; push `origin main`; never commit
  `Library/ Temp/ Logs/ obj/ *.csproj *.sln`; DO commit `.meta`/assets.
- Exactly one `in_progress` todo; plan mode = read-only (no edits,
  no shell writes, no commits); build mode = full action.

## 7. Hard-won ledger (do not relearn)

- Teleport + manual CharacterController stepping needs
  `Physics.SyncTransforms()` first (phantom-overlap fling otherwise).
- `SetTier` BEFORE `mover.enabled = false`; freeze autonomous systems in
  test pins; `Time.timeScale` hygiene via try/finally.
- Spawner `Configure` is build-time only — runtime needs `Awake` guards
  (null `_view` deadlocks judging).
- Never re-`Configure` spawner per chunk (transient SO-clone leak).
- Batchmode PlayMode can't do synthetic input or settle camera lag;
  use live telemetry (overlay Cam line) for visual/behavioral evidence.
- Billboarded rows mirror local +X to screen-left (positions AND arrow
  shapes must both compensate).
- `edit` tool: derive oldString from current file content; after any
  header-consuming edit, re-check section structure (it has bitten
  repeatedly — verify with grep after every structural edit).
- M4 was executed subagent-driven (Tasks 1–8 + reviews in
  `.superpowers/sdd/2026-10-04-flying-fish-m4/` — briefs, reports,
  review packages live there; consult before re-touching Run code).
- PRD source of truth:
  `c:\Users\TRISTAN\Downloads\FlyingFishMomentum_Master_PRD_v3.0.md`.
