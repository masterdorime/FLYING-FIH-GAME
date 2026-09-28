# Flying Fish Momentum — Milestone 1 (Movement Proof) Design

**Date:** 2026-09-27
**Status:** Approved (§1–§3 reviewed section by section)
**PRD:** `FlyingFishMomentum_Master_PRD_v3.0.md` (single source of truth; this doc scopes M1 only)
**Scope:** PRD §29 Milestone 1 + movement-relevant §§4–6, 9–10, 16, 24, 34.

## Goal

A playable Unity scene where a primitive placeholder fish swims and flies with
momentum-based movement, steering, and a speed-reactive camera. No timing,
gauge, progression, menus, audio, or VFX. Success = "the fish already feels
good to control without any progression systems" (PRD §29).

## Locked decisions

| # | Question | Decision |
|---|----------|----------|
| 1 | Scope | Milestone 1: Movement Proof |
| 2 | Editor | Install latest LTS via `unity install lts` + `unity auth login` |
| 3 | Physics | CharacterController + custom kinematic momentum (PRD §23.2 option A) |
| 4 | Assets | Unity primitives, flat colors (fish: capsule + cone + box fins, orange) |
| 5 | Movement model | Auto-forward + steer (yaw/pitch), endless-runner style (PRD §1.1) |
| 6 | Swim model | Underwater 3D volume below y=0 plane; buoyancy branch, breach to fly |
| 7 | Swim↔Fly trigger | Automatic on surface breach + speed threshold (no toggle key) |
| 8 | M1 speed driver | Debug keys 1–5 snap TargetSpeed to tier maxes (`[M1-SCAFFOLD]`, removed in M2) |
| 9 | Construction | Single-scene slice (water + air, transitions, profiles, overlay in one scene) |

## §1 Architecture (approved)

**PlayerRoot** (prefab): `CharacterController` (skinWidth 0.08) +
`PlayerMomentumController` + `PlayerMovementController` + `FlightStateMachine`.

- `PlayerMomentumController`: `CurrentSpeed`, `TargetSpeed`, `SetLimits(max, accel)`,
  `AddSpeed(amount)` (clamps to active max), `Tick(dt)` — accel up at profile rate,
  drag down toward target, clamp `[MinSpeed 8, profileMax]`. Deterministic, dt-scaled,
  zero allocations. `ApplyTimingResult` is **M2 scope — does not exist in M1**.
- `PlayerMovementController`: heading via pure static `HeadingMath.Step(yaw, pitch,
  input, turnRate, dt)` (yaw += x·rate·dt; pitch clamped ±60°); forward = heading ×
  speed; swim branch = neutral buoyancy (no gravity accumulation; vertical velocity damped toward 0) + pitch authority at 0.5× turn rate + swim drag;
  fly branch = gravity accumulation × profile `GravityScale` + full authority (`AirControl` scales turn rate);
  single swept `CharacterController.Move` per frame (slide on contact, speed preserved).
- `FlightStateMachine`: `ActiveTier`, `Locomotion` (Swimming/Flying),
  `SetTier(t)` (no-op if unchanged; pushes profile limits; fires
  `OnTierChanged(old, new)`), `SetLocomotion(s)`, breach detector (owns §2 rules).
  M3 calls `SetTier` from gauge events — no gauge code in M1.
- Enums per PRD §5: `PlayerLocomotionState{Swimming, Flying}`,
  `FlightTier{None, Low, Medium, High, Max}`.

**Tuning (ScriptableObjects, no magic numbers):** `MomentumSettings`
(MinSpeed 8, DragSwimming 1.5, DragFlying 0.6), 5× `FlightTierProfile` with PRD §9.2
table values (speeds 25/35/50/70/110; FOV 60/65/72/80/95; accel 35/45/60/80/120;
turn 120/140/160/180/200; gravity 1.0/0.85/0.70/0.55/0.40) + new
`BreachSpeedThreshold` (default 30 — above swim max 25, below Low max 35),
`CameraSettings` (BaseFOV 60, PositionLag 0.12, TierUpCameraKick 0.35,
MissCameraShake 0.4). Profile art fields (`WingVisualScale`, `TrailColor`) exist
for M5 but are unused in M1.

**CameraRig** (prefab): `CameraSpeedReactor` — chase behind+above, exponential lag,
velocity look-ahead, FOV = lerp(60 → tier FOV) by clamped speed fraction (never
overshoots), bank roll target = clamp(−yawRate ÷ profile TurnRate × 8°, −8°, 8°)
smoothed, position smoothing factor `1 − exp(−dt ÷ PositionLag)`, `PlayTierUpKick()` / `PlayMissShake()`
fully implemented and tested, wired to debug keys T/G until M3 events exist.
Pause-safe via scaled dt.

**Debug (marked `[M1-SCAFFOLD]`, IMGUI overlay, debug builds only):**
`M1DebugInput` (keys 1–5 tier speed snap; T/G camera demo), `M1DebugOverlay`
(CurrentSpeed, TargetSpeed, Locomotion, ActiveTier, threshold, FPS).

**Data flow:** Input → heading → speed → displacement; StateMachine observes
position + speed → pushes limits; Camera observes speed + profile. No GameManager /
RunManager (M4), no timing/gauge (M2/M3), no menus/HUD (M5).

## §2 Transitions & speed (approved)

- Surface = y=0 logical threshold (translucent visual plane, **no collider**).
- Swim→Fly: upward crossing with `CurrentSpeed ≥ BreachSpeedThreshold` (30).
  M3 keeps this crossing logic, swapping the threshold source to gauge tier.
- Fly→Swim: any downward crossing (water always catches you, PRD §10.2).
- Pitch never changes speed in M1 (no energy exchange — deliberate, documented).
- Collisions slide, speed preserved (penalties are M5 obstacle work).
- Seabed collider at y=-12 keeps the fish in the volume; no ceiling
  (gravity + pitch clamp handle it).

## §3 Scene, verification, files (approved)

- Scene `Assets/Scenes/M1_MovementProof.unity`: water plane, seabed, 3–5 primitive
  islands as slalom corridor (colliders), directional light, default skybox,
  spawn (0,-3,0) facing +Z. Pipeline: template default, **no switch in M1**
  (pipeline choice locked in M5 art pass).
- Input asset `Assets/Input/PlayerInputActions.inputactions` (+ generated C#):
  `Gameplay/Move` (WASD composite + left stick), `Gameplay/Pause` (Esc + Start;
  minimal timeScale pause in M1, full menu in M5). `TimingAction` is M2 (YAGNI).
- Assemblies: `FlyingFishMomentum.Runtime`, `...Tests.EditMode`, `...Tests.PlayMode`.
- Tests: EditMode — momentum clamp/drag floor, heading no-drift + no-NaN, pitch
  clamp, FOV endpoints (60 at min, tier FOV at max), input bindings, profile values
  vs PRD tables. PlayMode — max-speed (110) slalom without tunneling + inside
  bounds; pause-freeze (zero displacement at timeScale 0).
- Human sign-off: 60–90s continuous control; steerable at Low, controllable at Max;
  breach/re-entry reliable; camera never loses fish; stable frame pacing (FPS noted).
- M1-applicable PRD §28.1 items: #1 (momentum movement), #8 (camera reacts).
- Non-goals restated: timing, gauge/tiers-as-progression, scoring, combo, menus,
  tutorial, audio, VFX/juice, obstacle penalties, RunManager/difficulty, saves.

## Handoff notes for M2/M3

- M2 adds `PrecisionTimingSystem` + `ApplyTimingResult(TimingResult, tier)` on the
  momentum controller; deletes debug speed keys (replaced by Perfect/Good boosts,
  Miss penalties per §6.4 table).
- M3 adds `FlightGaugeSystem`; gauge events call existing `FlightStateMachine.SetTier`;
  breach rule keeps its shape, threshold source becomes tier.
- M5 removes all `[M1-SCAFFOLD]` code and implements penalties, juice, menus, audio.

## Change record — yaw deviation drag (§34.1, 2026-09-28, approved + implemented fa74e2c)

- **Current requirement:** free yaw wrapping [-180, 180]; drag depends only on swim/fly branch (§2).
- **Reason for conflict:** free 180° turns let the player fly backward indefinitely, breaking the endless-forward-runner fantasy (PRD §§1.1, 1.5); Haste sustains forward pressure via consequences, not locks.
- **Proposed change:** drag scales with angle off forward: effective drag = branch drag × (1 + `DeviationDragGain` × |yaw|/180°), new tunable default 3 (≈4× drag flying fully backward). Forward flight is numerically identical to before.
- **Affected systems:** `PlayerMomentumController.Tick` (new optional `deviationDeg` param), `PlayerMovementController.TickMove` (passes `|Yaw|`), `MomentumSettings` (new field + asset value), `M1DebugOverlay` (shows deviation).
- **New behavior:** turns/dodges work at any angle, but sustained backward headings bleed to `MinSpeed` in ~1–2s; recovery = re-aim forward.
- **Updated acceptance:** `ZeroDeviationBehavesExactlyAsBefore`, `FullBackwardDeviationMultipliesDrag`, `DeviationDragNeverBreachesMinSpeed` (all green); M1 sign-off gains "flying backward is self-defeating."
