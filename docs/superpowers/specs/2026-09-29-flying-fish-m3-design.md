# Flying Fish M3 Design — Gauge & Tier Proof (approved 2026-09-29)

**Scope:** PRD Milestone 3 only: gauge, thresholds, tier transitions,
tier-specific movement (already exists — wired here), tier feedback.
Success: the player clearly feels the difference between tiers.
**Source of truth:** `FlyingFishMomentum_Master_PRD_v3.0.md` §§8–9, 15–18.
Prior milestones: `docs/superpowers/specs/2026-09-27-flying-fish-m1-design.md`,
`docs/superpowers/specs/2026-09-29-flying-fish-m2-design.md` (incl. all
§34.1 records, especially gearless climb).

## S1 — Gauge core

New `FlightGaugeSystem` (MonoBehaviour) with an explicit-clock
`Tick(dt, flying)` seam like the spawner (unit-testable; `Update`
feeds live clocks). New `FlightGaugeSettings` ScriptableObject:

- `MaxGauge` 120 (overfill allowed as buffer), `StartGauge` 0.
- Fill (charge only — taps feed speed, never gauge): step +2, jackpot +4.
- `FlyDrainPerSecond` 3.5 while flying; `MissDrain` 10 on any Miss.
- Thresholds: None <20, Low 20–40, Medium 40–70, High 70–100, Max ≥100.
- Crossing a threshold calls existing `FlightStateMachine.SetTier`
  (the M1 comment "M3 calls SetTier from gauge events" lands here).
- No swimming drain. Gauge floor 0 (never negative).

## S2 — Tiers as speed caps

Tier bands become speed ceilings: None 17, Low 23, Medium 33, High 47,
Max 70 (Max profile says 73; the global 70 `SpeedCap` wins — one ruled
conflict, recorded here). `SetTier` gains down-clamp only: if
`TargetSpeed` exceeds the new tier max it drops to fit; tier-up never
gifts speed (climb stays earned). Turn rates, gravity, air control,
windows, and prompt tuning per tier already exist and are untouched.
Tier-up fires existing `CameraSpeedReactor.PlayTierUpKick`, tier-down
fires `PlayMissShake` (PRD §16 mapping). Debug overlay gains a gauge
line (amount + tier). M1DebugInput stops forcing Medium: spawn is gauge
0 / tier None (bootstrap default) / speed 10. Digits stay deleted.

## S3 — Tests (TDD, red → green → commit)

EditMode: fill per source (step/jackpot/tap-zero), flying drain rate,
Miss drain, up/down threshold crossings all five bands, cap holds at
70, down-clamp never up-gifts, spawn state 0/None/10.
PlayMode: two clean ring sequences reach Low gear in game; starving the
gauge (no input) drops tiers back; kick fires on tier-up.
Human (closes M3): 60–90s — feel every gear change, report readability
of the gauge line and fairness of the leak. Full suites green.

## S4 — Out of scope (explicitly NOT M3)

Combo/streak scoring (§15), RunManager/chunks/difficulty (M4), menus,
tutorial, audio, VFX/juice, obstacles, results, saves (M5). PRD §34
agent rules still bind: SO-only tuning, dt-scaled determinism (seeded
RNG only), gauge evaluation independent from presentation, UI
independent from gameplay state, events/direct-wiring per M1 precedent.

## Handoff notes for M4

- M4 adds `RunManager`/chunks; gauge thresholds may re-tune per
  difficulty — spawner and gauge read settings live.
- The `max(tier max + boost, SpeedCap)` burst-ceiling contradiction
  (M2 record) is still open: revisit when tiers move under load.
- M5 replaces dial/bar/overlay with real UI/VFX and deletes
  `[M1-SCAFFOLD]` leftovers (incl. the Medium-era debug input).
