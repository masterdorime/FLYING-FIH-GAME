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

## Change record — ring charge sequences (§34.1, 2026-09-29, approved)

- **Old:** only distance-beat tap prompts.
- **Why:** feel pass wants a second, different skill expression for gaining speed.
- **New:** 5 fixed rings in the world (alternating swim/air depth).
  REVISED twice by feel pass: (1) holds never landed in wall-clock
  perception, so charge steps are arrow taps — swim through, match 3
  seeded arrows (WASD/stick flicks) for +2 each, +4 all-clean jackpot,
  wrong key +0, 6s overall timeout, no penalties; (2) marker cubes
  (grey pending/green hit/red missed) + overlay arrow text. No streak
  coupling, no penalties in charge mode; gains ratchet cruise to the 70
  cap like tap hits. Beats suspend during charge. Ring entry dips the
  world to 0.4× for 1.2s wall clock (unpausable timer; pause wins ties)
  so hands can answer.

## Change record — gearless climb + drifting needle (§34.1, 2026-09-29, approved)

- **Old:** tier cruise speeds; needle parked on red at the hit moment.
- **Why:** feel pass wants a climb-from-slow progression and a live needle.
- **New:** runs spawn at `StartSpeed` 10 with target pinned 10 (debug
  scaffold); Perfect/Good stack through burst overflow, hard cap
  `SpeedCap` 70 enforced in `ApplyTimingResult` (tier max still rules
  upward, so M3 gauge tiers keep working); hits ratchet the cruise
  target itself so gains stick — drag sags toward earned speed, Miss
  drops target to the MinSpeed floor. Needle progress unclamped — it
  drifts past red until press or expiry. Note: the burst ceiling is
  `max(tier max + boost, SpeedCap)`, so a Max-tier Perfect can legally
  reach 83; unreachable in M2 (tier locked Medium) — M3 must pick one
  rule when tiers move again.
  Tier bands stay for turning/breach/windows but are dormant for speed
  until M3 replans the gauge around this world. All taps judge on the
  flat Medium row at any tier (`Spawner.BeatRowFor`; the None row's
  zeros froze speed at spawn — reported bug, fixed).

## Change record — red sliver, moving hit, slow world (§34.1, 2026-09-29, approved)

- **Old:** green perfect slice coplanar with yellow (invisible); hit always
  at top; Good floor 0.09s; boosts +12/+16/+22/+30 & +6/+8/+11/+15;
  tiers 25/35/50/70/110, Min 8, breach 30.
- **Why:** feel pass could not tell Perfect from Good; world too fast.
- **New:** perfect slice is red, depth-stacked above yellow (base 0,
  good 0.01, perfect 0.02 toward camera); hit angle drawn per prompt
  from the run seed (`Spawner.HitAngleDeg`, needle sweeps a full turn
  onto it); `MinGoodWindow` 0 with grace capped at the window (top
  speed is Perfect-or-Miss); boosts cut to +4/+5/+7/+10 and
  +2/+3/+4/+5 (Miss untouched); tiers 17/23/33/47/73, Min 5,
  breach 20, `MaxSpeedRef` 73. Accel/turn/gravity untouched.
- **Tests:** sliver at top, Good-gone, seeded hit range + replay,
  needle-on-red, rescaled tables/assets/pins.

## Change record — difficulty: seeded beats + curve + streak (§34.1, 2026-09-29, approved)

- **Old:** fixed 60m beat; linear window shrink (perfect floor 0.035s); no streak effect.
- **Why:** feel pass found timing too easy and fully predictable.
- **New:** beat drawn 40–80m from a `System.Random` seeded fresh every
  run (`SetSeed` locks it for tests — deterministic rule honored);
  squared speed curve (gentle cruise, brutal top); perfect floor 0.015s
  (sliver); every straight hit multiplies windows ×0.97 down to half,
  any Miss resets the streak (internal counter only, mirrored as `xN`
  in the debug overlay — no combo system).
- **Tests:** same-seed replay equality, beat bounds, curve values, streak
  narrow + floor + reset, sliver at top speed.

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

## Change record — ship swim + arrow glyphs (§34.1, 2026-09-30, approved)

- **Old:** swim pitched freely (W/S) and could breach upward; charge
  markers were plain cubes; sequences were 3 steps.
- **Why:** feel pass wants ship-like water (steer-only) and readable arrows.
- **New:** swim locks `SwimDepthY` (−1.5) and level pitch — W/S ignored
  underwater, yaw steers in the cone; air keeps full heading control.
  Swim-up breach retired (depth lock makes it unreachable). Charge steps
  are line-drawn arrow glyphs (grey/green/red) + overlay arrow text;
  sequences are 4 arrows (`+2` each, `+4` jackpot = 12 max per ring).
  Reading order is left-to-right from step 0; the live step pulses big
  and yellow so it never blends into the grey steps ahead.

## Handoff notes for M3

- M3 adds `FlightGaugeSystem`; gauge events call existing
  `FlightStateMachine.SetTier`; digits stay deleted.
- M3 may re-tune `PromptEveryMeters`/`LeadTime` per-tier; spawner reads
  settings live so no code change needed.
- M5 replaces the dial + overlay text with real UI/VFX and deletes
  `[M1-SCAFFOLD]` leftovers.
