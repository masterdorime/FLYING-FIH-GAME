# Flying Fish M2 Design — Timing Proof (approved 2026-09-29)

**Scope:** PRD Milestone 2 only (PRD §29): timing prompts, Perfect/Good/Miss,
speed changes, input buffering, basic feedback. Success: timing feels
understandable and responsive.
**Source of truth:** `FlyingFishMomentum_Master_PRD_v3.0.md` §§6–7, 15–18.
Prior milestone: `docs/superpowers/specs/2026-09-27-flying-fish-m1-design.md`.

## S1 — Components (all new unless noted)

1. **TimingPromptSpawner** (MonoBehaviour): accumulates distance traveled from
   the movement controller; opens a prompt every `PromptEveryMeters` (default
   60). Skips (no queue) while a prompt is already open. Deterministic:
   pure function of accumulated distance, no `Random`.
2. **TimingEvaluator** (pure static, like M1 `HeadingMath`/`SurfaceCrossing`):
   `(pressTime - targetMoment)` + current speed → `TimingResult`.
   Windows shrink with speed per PRD §7.2. Late grace `LateBuffer` (default
   0.06s) past the Good edge still counts as Good.
3. **TimingResult** (new enum): `Perfect, Good, Miss` (PRD §7.4).
4. **Applier**: `PlayerMomentumController.ApplyTimingResult(result, tier)`
   per PRD §6.3, boost/penalty table from a new `TimingSettings`
   ScriptableObject (PRD §6.4 values). Perfect/Good bursts may overflow
   the tier max up to `max + boost`, then decay back through normal drag
   (without this, a Perfect at cruise speed would do nothing). Miss
   applies via `AddSpeed(−penalty)`; the `MinSpeed` floor is enforced
   by `Tick`.
5. **TimingPromptRing** (world-space placeholder, M1 primitive style): a ring
   around the fish that shrinks and meets its target circle exactly at the
   hit moment (`LeadTime` default 1.0s). Deleted/recycled on resolve.
6. **Text mirror**: `M1DebugOverlay` shows the last result
   (PERFECT/GOOD/MISS, PRD §17.4 labels) plus prompt state.
7. **Input**: new `Gameplay/TimingAction` binding — `<Keyboard>/space` +
   `<Gamepad>/buttonSouth`. M1 actions untouched.
8. **M1DebugInput**: digit keys 1–5 DELETED (per its own M1 comment: timing
   drives speed now). Tier locked to Medium (breach-capable, mid speed)
   until M3. T/G camera demo keys and pause stay.

## S2 — Data flow

Swim/fly → meters accumulate → prompt opens, ring starts wide →
press (or window + grace expires) → evaluator returns result →
`ApplyTimingResult` changes speed → feedback: camera kick on Perfect
(`PlayTierUpKick`, exists), camera shake on Miss (`PlayMissShake`,
exists), overlay result word → accumulator resets → next chunk.
A press before the window opens is a Miss and consumes the prompt
(no button-mashing). Expiry past Good + grace is a Miss.

## S3 — Numbers (all `TimingSettings` SO, no hard code)

- `PromptEveryMeters`: 60. `LeadTime`: 1.0s.
- `PerfectWindow` 0.07s → `MinPerfectWindow` 0.035s;
  `GoodWindow` 0.18s → `MinGoodWindow` 0.09s, lerped by
  `InverseLerp(MinSpeed, 110, CurrentSpeed)` (PRD §7.2).
- `LateBuffer`: 0.06s (PRD 50–80ms recommendation).
- Boosts/penalties per tier (PRD §6.4): Perfect +12/+16/+22/+30,
  Good +6/+8/+11/+15, Miss −18/−28/−45/−70. Only Medium reachable in M2.

## S4 — Tests (TDD, red → green → commit)

EditMode: window edges (0.069s Perfect, 0.071s Good, 0.181s Miss,
grace-edge Good, past-grace Miss), early-press Miss, windows shrink at
speed, spawner skip-while-open, same-distance-same-schedule determinism,
full §6.4 table per tier, `TimingAction` binding exists.
PlayMode: real Perfect gains speed in game; real expiry loses speed.
Human (closes M2): 60–90s at locked Medium — hit 5 prompts on purpose,
Perfect boost felt, Miss drop felt, prompt always readable, camera never
lost, FPS noted.

## S5 — Out of scope (explicitly NOT M2)

Flight gauge (§8), tier changes (locked Medium), combo/streak (§15),
scoring, menus, tutorial, audio, VFX/juice (§18 stays camera + text
only), obstacles, RunManager, saves. PRD §34 agent rules still bind:
SO-only tuning, dt-scaled determinism, timing evaluation independent
from visual presentation (evaluator knows nothing of the dial),
UI independent from gameplay state, events over coupling.

## Change record — dial meter + longer runway (§34.1, 2026-09-29, approved)

- **Old:** shrinking world-space ring around the fish; water/seabed span
  z −100..700.
- **Why:** feel pass found the ring unreadable and the runway too short
  for timing play at speed.
- **New:** Outlast-style world-space dial above the fish (billboarded):
  green/yellow/red rim zones sized from live windows, needle lands on
  green at the hit moment (`TimingDialMath` pure static + `TimingPromptDial`;
  `TimingPromptSpawner.Progress01` exposes progress clock-independently).
  Camera tagged `MainCamera` (billboarding depends on it). Water/seabed
  span z −100..1500, camera far plane 2000. `TimingPromptRing` deleted.
- **M5 note updated:** M5 reskins the dial (not the ring) + overlay text.

## Handoff notes for M3

- M3 adds `FlightGaugeSystem`; gauge events call existing
  `FlightStateMachine.SetTier`; digits stay deleted.
- M3 may re-tune `PromptEveryMeters`/`LeadTime` per-tier; spawner reads
  settings live so no code change needed.
- M5 replaces the dial + overlay text with real UI/VFX and deletes
  `[M1-SCAFFOLD]` leftovers.
