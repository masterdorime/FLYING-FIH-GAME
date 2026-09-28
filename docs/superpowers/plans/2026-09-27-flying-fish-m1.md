# Flying Fish Momentum — Milestone 1 (Movement Proof) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A playable Unity scene where a primitive placeholder fish swims and flies with momentum-based movement, steering, and a speed-reactive camera — no timing, gauge, or progression systems.

**Architecture:** Custom kinematic momentum (`PlayerMomentumController`, driven by `PlayerMovementController` so there is exactly one update point) moves a `CharacterController`; `FlightStateMachine` applies per-tier `FlightTierProfile` ScriptableObjects and owns Swim/Fly transitions via surface-breach detection; `CameraSpeedReactor` follows in `LateUpdate` with lag + speed-driven FOV. Pure math (`HeadingMath`, `SurfaceCrossing`, `CameraMath`) is static and EditMode-tested; only M1 debug keys are scaffold.

**Tech Stack:** Unity 6 LTS (exact version recorded in AGENTS.md at Task 0), C#, Input System package (`com.unity.inputsystem`), Test Framework (`com.unity.test-framework`, default package — verify, add only if missing), git.

**Spec:** `C:\Users\TRISTAN\FLYING FIH\docs\superpowers\specs\2026-09-27-flying-fish-m1-design.md` — the plan argues from the spec; executors read both. PRD: `c:\Users\TRISTAN\Downloads\FlyingFishMomentum_Master_PRD_v3.0.md`.

## Global Constraints

- Repo root IS the Unity project root (`C:\Users\TRISTAN\FLYING FIH`). Do not switch render pipeline, API compatibility, or C# language version from template defaults.
- PC standalone; keyboard WASD + gamepad left stick = `Gameplay/Move`; Esc/Start = `Gameplay/Pause`. No `TimingAction` in M1 (YAGNI — assert its absence in tests).
- Held direction MUST NOT generate significant speed (PRD §6.2): speed changes only via `TargetSpeed` (debug keys, M1) / `AddSpeed` / drag.
- All tunables in ScriptableObjects. No magic numbers in MonoBehaviours.
- Deterministic gameplay: dt-scaled, no `Random` in gameplay code, zero per-frame allocations in Tick paths.
- Assemblies: `FlyingFishMomentum.Runtime`, `FlyingFishMomentum.Tests.EditMode`, `FlyingFishMomentum.Tests.PlayMode`. Namespace matches folder.
- Scaffold marked `[M1-SCAFFOLD]`; tracked for M5 removal. No other throwaway code.
- Commits: `feat|test|chore: <what> (M1 task N)`. Test-command: `unity test "C:\Users\TRISTAN\FLYING FIH" --mode EditMode|PlayMode [--filter <name>]`.
- Before writing MonoBehaviours / input / SOs, the executor invokes the matching required skill per AGENTS.md (`unity-csharp-scripting`, `unity-input-system`, `unity-scriptableobjects`, `unity-physics`, `unity-package-management`, `ponytail`, `test-driven-development`).

## Review Focus

- Fish at 110 u/s vs thin island colliders → slides/deflects, never tunnels (PlayMode slalom test, Task 7).
- Move input released mid-turn at high speed → heading holds steady, no NaN, no oscillation (`HeadingMath` zero-input test, Task 5).
- `Time.timeScale = 0` → zero displacement, timers frozen, timescale always restored (`PauseFreeze` PlayMode test, Task 7).
- Fly-state fish below y=0 (or Swim-state high in air) → legal in M1 (state is explicit); overlay always shows state so mismatch is visible, never silent (overlay fields, Task 7).
- FOV at/below MinSpeed is exactly BaseFOV; at/above profile max exactly tier FOV — never overshoots (`CameraMath` endpoint tests, Task 6).

---

## File Structure

```text
C:\Users\TRISTAN\FLYING FIH\            <- git repo root + Unity project root
├── AGENTS.md                            (exists; Task 0 records LTS version in it)
├── .gitignore                           (Task 0, content below)
├── docs/superpowers/{specs,plans}/      (exist)
├── Assets/
│   ├── Scenes/M1_MovementProof.unity    (Task 7)
│   ├── Scripts/
│   │   ├── Config/MomentumSettings.cs, FlightTierProfile.cs, CameraSettings.cs (Task 2)
│   │   ├── Player/PlayerLocomotionState.cs, FlightTier.cs (Task 4)
│   │   │        PlayerMomentumController.cs (Task 3)
│   │   │        FlightStateMachine.cs, SurfaceCrossing.cs (Task 4)
│   │   │        PlayerMovementController.cs, HeadingMath.cs (Task 5)
│   │   ├── Camera/CameraSpeedReactor.cs, CameraMath.cs (Task 6)
│   │   └── Debug/M1DebugInput.cs, M1DebugOverlay.cs   [M1-SCAFFOLD] (Task 7)
│   ├── Configs/*.asset                  (Task 2: Momentum, Camera, 5 tier profiles)
│   ├── Prefabs/PlayerRoot.prefab, CameraRig.prefab (Task 7)
│   ├── Materials/M1Fish.mat, M1Water.mat (Task 7)
│   ├── Input/PlayerInputActions.inputactions + generated .cs (Task 1)
│   └── Tests/EditMode/*.cs + PlayMode/*.cs (+ 2 test asmdefs)
└── Packages/manifest.json               (Task 0: com.unity.inputsystem)
```

---

### Task 0: Environment, project, repo

**Files:** Unity project at working-dir root; `.gitignore` (create, content below); `AGENTS.md` (modify: record LTS version); `Packages/manifest.json` (modify: add inputsystem).

- [ ] **Step 1: Install Editor + auth.** Run `unity install lts`, then `unity auth login` (human signs in via browser). Verify: `unity editors -i` lists the LTS; `unity auth status` shows logged in.
- [ ] **Step 2: Create project.** Run `unity projects create --help`; create a local-only project (`--no-cloud`) with its root at `C:\Users\TRISTAN\FLYING FIH` (directory is empty — valid target). Keep the template's default render pipeline; change nothing about pipeline/API level/language version. Verify: `Assets/`, `Packages/`, `ProjectSettings/` exist at root and `unity open "C:\Users\TRISTAN\FLYING FIH"` opens it.
- [ ] **Step 3: Add Input System.** Per the `unity-package-management` skill, add `com.unity.inputsystem` headless; set Active Input Handler to Both (Player Settings) so existing UI packages keep working. Verify: package appears in `Packages/manifest.json`.
- [ ] **Step 4: Git + AGENTS.md.** `git init`; write `.gitignore` with exactly:

```gitignore
[Ll]ibrary/
[Tt]emp/
[Oo]bj/
[Bb]uild/
[Bb]uilds/
[Ll]ogs/
[Uu]ser[Ss]ettings/
*.csproj
*.sln
*.user
*.userprefs
.vs/
```

Record the exact LTS version (from Step 1) in `AGENTS.md` line 3. Commit: `git add -A && git commit -m "chore: M1 bootstrap — Unity 6 LTS project + git (M1 task 0)"`. Verify: `git status` clean, `git log --oneline` shows 1 commit.

### Task 1: Input actions (Move + Pause only)

**Files:** create `Assets/Input/PlayerInputActions.inputactions` (+ generated C#); test `Assets/Tests/EditMode/InputAssetTests.cs` (+ `FlyingFishMomentum.Tests.EditMode` asmdef referencing the Runtime asmdef; test-framework assembly setup per the `unity-csharp-scripting` skill).
**Interfaces:** consumes none. Produces generated `PlayerInputActions` class with `Gameplay/Move` (Vector2) and `Gameplay/Pause` (Button).

- [ ] **Step 1: Write the failing test.**

```csharp
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.InputSystem;

public class InputAssetTests
{
    const string Path = "Assets/Input/PlayerInputActions.inputactions";

    [Test]
    public void M1ActionsExistWithCorrectBindings()
    {
        var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(Path);
        Assert.IsNotNull(asset, "inputactions asset missing");
        var move = asset.FindAction("Gameplay/Move");
        Assert.IsNotNull(move);
        Assert.AreEqual("Vector2", move.expectedControlType);
        Assert.IsTrue(move.bindings.Any(b => b.path.Contains("/w")), "WASD binding missing");
        Assert.IsTrue(move.bindings.Any(b => b.path.Contains("leftStick")), "gamepad stick missing");
        Assert.IsNotNull(asset.FindAction("Gameplay/Pause"));
    }

    [Test]
    public void NoTimingActionInM1()
    {
        var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(Path);
        Assert.IsNull(asset.FindAction("Gameplay/TimingAction"), "TimingAction is M2 scope");
    }
}
```

- [ ] **Step 2: Run to verify it fails.** Run: `unity test "C:\Users\TRISTAN\FLYING FIH" --mode EditMode --filter InputAssetTests`. Expected: FAIL (asset missing).
- [ ] **Step 3: Create the asset** (per `unity-input-system` skill). Action Map `Gameplay`: `Move` = Vector2 composite WASD (`<Keyboard>/w,a,s,d`) + `<Gamepad>/leftStick`; `Pause` = Button `<Keyboard>/escape` + `<Gamepad>/start`. Enable C# code generation for the asset.
- [ ] **Step 4: Re-run.** Expected: PASS.
- [ ] **Step 5: Commit** `test,feat: M1 input actions Move+Pause (M1 task 1)`.

### Task 2: Config ScriptableObjects (PRD §§6.1, 9.1–9.2, 16.2 + spec §1)

**Files:** create `Assets/Scripts/Config/MomentumSettings.cs`, `FlightTierProfile.cs`, `CameraSettings.cs`, `FlyingFishMomentum.Runtime` asmdef; assets in `Assets/Configs/`; test `Assets/Tests/EditMode/TierProfileTests.cs`.
**Interfaces:** produces `MomentumSettings{MinSpeed=8, DragSwimming=1.5, DragFlying=0.6, BreachSpeedThreshold=30}` (test files in Tasks 1–2 require `using UnityEditor; using System.Linq;`); `FlightTierProfile{Tier, MaxSpeed, Acceleration, TurnRate, GravityScale, AirControl, CameraFOV, CameraDistance, WingVisualScale, TrailColor}`; `CameraSettings{BaseFOV=60, MaxFOV=100, FOVSpeedResponse=0.15, PositionLag=0.12, TierUpCameraKick=0.35, MissCameraShake=0.4}` (all six per PRD §16.2). Profile values: speeds 25/35/50/70/110; accel 35/45/60/80/120; turn 120/140/160/180/200; gravity 1.0/0.85/0.70/0.55/0.40; FOV 60/65/72/80/95 (PRD tables). M1-chosen, documented on the asset: `AirControl` (fly turn-rate multiplier) None 1.0 / Low 0.85 / Med 0.95 / High 1.0 / Max 1.05; `CameraDistance` None 8 / Low 9 / Med 11 / High 13 / Max 16 (camera height = distance × 0.4, fixed in code). `WingVisualScale`/`TrailColor` exist for M5, unused in M1.

- [ ] **Step 1: Write the failing test.**

```csharp
[Test]
public void ProfileAssetsMatchPrdTables()
{
    var guids = AssetDatabase.FindAssets("t:FlightTierProfile");
    Assert.AreEqual(5, guids.Length, "expected 5 tier profiles");
    float SpeedOf(FlightTier t) => AssetDatabase.LoadAssetAtPath<FlightTierProfile>(
        AssetDatabase.GUIDToAssetPath(guids.First(g =>
            AssetDatabase.LoadAssetAtPath<FlightTierProfile>(AssetDatabase.GUIDToAssetPath(g)).Tier == t))).MaxSpeed;
    Assert.AreEqual(25f, SpeedOf(FlightTier.None));
    Assert.AreEqual(110f, SpeedOf(FlightTier.Max));
}
```

- [ ] **Step 2: Run.** Expected: FAIL (no assets).
- [ ] **Step 3: Implement** the three SO classes (per `unity-scriptableobjects` skill, `[CreateAssetMenu]`, public fields, no logic) + Runtime asmdef; create the 7 `.asset` files with the values above.
- [ ] **Step 4: Re-run.** Expected: PASS.
- [ ] **Step 5: Commit** `feat: M1 tuning ScriptableObjects (M1 task 2)`.

### Task 3: PlayerMomentumController (PRD §6)

**Files:** create `Assets/Scripts/Player/PlayerMomentumController.cs`; test `Assets/Tests/EditMode/MomentumTests.cs`.
**Interfaces:** consumes `MomentumSettings` via `Configure()`, limits via `SetLimits(maxSpeed, accelRate)`. Produces `CurrentSpeed`, `TargetSpeed`, `CurrentMaxSpeed`, `AddSpeed(amount)` (clamps to max), `Tick(dt, dragRate)` (accel up at stored rate, drag down toward target, clamp `[MinSpeed, max]`). No `Update()` — driven by movement (Task 5), so there is exactly one update point. `ApplyTimingResult` does NOT exist (M2).

- [ ] **Step 1: Write the failing tests.**

```csharp
PlayerMomentumController NewMomentum(float max = 35f, float accel = 45f)
{
    var go = new GameObject("m");
    var m = go.AddComponent<PlayerMomentumController>();
    var s = ScriptableObject.CreateInstance<MomentumSettings>();
    s.MinSpeed = 8f; s.DragSwimming = 1.5f; s.DragFlying = 0.6f; s.BreachSpeedThreshold = 30f;
    m.Configure(s);
    m.SetLimits(max, accel);
    return m;
}

[TearDown]
public void Cleanup()
{
    foreach (var go in GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        if (go.name == "m") Object.DestroyImmediate(go);
}

[Test] public void AddSpeedClampsToActiveMax()
{
    var m = NewMomentum(); m.CurrentSpeed = 30f; m.AddSpeed(12f);
    Assert.AreEqual(35f, m.CurrentSpeed, 0.001f);
}
[Test] public void DragFallsTowardTargetButNeverBelowMin()
{
    var m = NewMomentum(); m.CurrentSpeed = 60f; m.TargetSpeed = 8f;
    m.Tick(1f, 1.5f);
    Assert.Less(m.CurrentSpeed, 60f);
    for (int i = 0; i < 120; i++) m.Tick(1f, 1.5f);
    Assert.AreEqual(8f, m.CurrentSpeed, 0.01f);
}
[Test] public void AccelRisesTowardTargetAtProfileRate()
{
    var m = NewMomentum(); m.CurrentSpeed = 8f; m.TargetSpeed = 35f;
    m.Tick(0.5f, 1.5f); // 45 * 0.5 = 22.5 -> 30.5
    Assert.AreEqual(30.5f, m.CurrentSpeed, 0.01f);
}
```

- [ ] **Step 2: Run.** Expected: FAIL (class missing).
- [ ] **Step 3: Implement** (per `unity-csharp-scripting` skill lifecycle rules; no `Update`, no allocations):

```csharp
public void Tick(float dt, float dragRate)
{
    if (CurrentSpeed < TargetSpeed)
        CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, TargetSpeed, _accelRate * dt);
    else
        CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, TargetSpeed, dragRate * dt);
    CurrentSpeed = Mathf.Clamp(CurrentSpeed, _settings.MinSpeed, _maxSpeed);
}
public void AddSpeed(float amount) { CurrentSpeed = Mathf.Min(CurrentSpeed + amount, _maxSpeed); }
```

- [ ] **Step 4: Re-run.** Expected: PASS.
- [ ] **Step 5: Commit** `feat: momentum controller (M1 task 3)`.

### Task 4: Locomotion enums + FlightStateMachine + breach detector

**Files:** create `PlayerLocomotionState.cs`, `FlightTier.cs`, `FlightStateMachine.cs`, `SurfaceCrossing.cs`; test `StateMachineTests.cs`.
**Interfaces:** consumes profile list + `PlayerMomentumController` + `PlayerMovementController` via `Configure()`; `MomentumSettings` for the breach threshold. Produces `ActiveTier`, `Locomotion`, exposed `Momentum`/`Movement` references, `OnTierChanged(old, new)`, `SetTier(t)` (no-op + no event when unchanged; pushes `SetLimits` + movement profile), `SetLocomotion(s)`, `EvaluateSurface(prevY, newY)` (pure rule in `SurfaceCrossing.Evaluate(prevY, newY, speed, threshold, current)` → new locomotion, same value when no crossing).

- [ ] **Step 1: Write the failing tests.**

```csharp
[Test] public void FastUpwardBreachEntersFly()
{
    var next = SurfaceCrossing.Evaluate(-1f, 1f, 32f, 30f, PlayerLocomotionState.Swimming);
    Assert.AreEqual(PlayerLocomotionState.Flying, next);
}
[Test] public void SlowUpwardBreachStaysSwimming()
{
    var next = SurfaceCrossing.Evaluate(-1f, 1f, 20f, 30f, PlayerLocomotionState.Swimming);
    Assert.AreEqual(PlayerLocomotionState.Swimming, next);
}
[Test] public void AnyDownwardCrossingSwims()
{
    var next = SurfaceCrossing.Evaluate(1f, -1f, 110f, 30f, PlayerLocomotionState.Flying);
    Assert.AreEqual(PlayerLocomotionState.Swimming, next);
}
[Test] public void SetTierPushesLimitsAndFiresEventOnce()
{
    var sm = NewStateMachineWithLowAndHigh(); int fires = 0;
    sm.OnTierChanged += (_, _) => fires++;
    sm.SetTier(FlightTier.High);
    Assert.AreEqual(1, fires);
    Assert.AreEqual(70f, sm.Momentum.CurrentMaxSpeed);
    sm.SetTier(FlightTier.High);
    Assert.AreEqual(1, fires, "same-tier set must not re-fire");
}
```

- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Implement.** `SurfaceCrossing.Evaluate`: crossed up = prevY < 0 && newY >= 0; crossed down = prevY >= 0 && newY < 0; up + speed >= threshold → Flying; down → Swimming; else current. `FlightStateMachine` holds `List<FlightTierProfile>` (inspector-injected), looks up by tier, calls `momentum.SetLimits(p.MaxSpeed, p.Acceleration)` + `movement.SetProfile(p.TurnRate, p.GravityScale, p.AirControl)`.
- [ ] **Step 4: Re-run.** Expected: PASS.
- [ ] **Step 5: Commit** `feat: flight state machine + breach detector (M1 task 4)`.

### Task 5: PlayerMovementController + HeadingMath

**Files:** create `HeadingMath.cs`, `PlayerMovementController.cs`; tests `HeadingTests.cs` (EditMode) + `MovementSceneTests.cs` (PlayMode — written here, runs green in Task 7's scene).
**Interfaces:** consumes `Move` Vector2 (polled via generated `PlayerInputActions`), `stateMachine` (Locomotion, ActiveProfile), `momentum` (CurrentSpeed, Tick). Produces heading (`Yaw`, `Pitch` fields), `CurrentVelocity`, one swept `CharacterController.Move` per `Update`; calls `stateMachine.EvaluateSurface(prevY, newY)` after each move. Exposes `StateMachine` and `Momentum` references (used by Task 7 tests). PlayMode test files require `using System.Collections; using NUnit.Framework; using UnityEngine; using UnityEngine.InputSystem; using UnityEngine.TestTools;`. Conventions: yaw 0 = +Z, yaw += x·rate·dt (D turns right toward +X), pitch += y·pitchScale·rate·dt clamped ±60° (W climbs), forward = (sin yaw·cos pitch, sin pitch, cos yaw·cos pitch). Yaw wraps to [-180, 180]. Swim: no gravity, vertical velocity damped to 0, pitch scale 0.5, drag = DragSwimming. Fly: `vertVel -= 9.81·GravityScale·dt`, turn rate × AirControl, drag = DragFlying. `SetProfile(turn, gravity, air)` writes plain fields (no physics in the call — keeps Task 4 tests physics-free).

- [ ] **Step 1: Write the failing EditMode tests.**

```csharp
[Test] public void ZeroInputHoldsHeadingWithoutNaN()
{
    var h = HeadingMath.Step(45f, 10f, Vector2.zero, 200f, 1f, 1f);
    Assert.IsFalse(float.IsNaN(h.yaw) || float.IsNaN(h.pitch));
    Assert.AreEqual(45f, h.yaw, 0.001f);
    Assert.AreEqual(10f, h.pitch, 0.001f);
}
[Test] public void RightInputTurnsRight_UpInputClimbs()
{
    var h = HeadingMath.Step(0f, 0f, new Vector2(1f, 1f), 200f, 1f, 0.25f);
    Assert.AreEqual(50f, h.yaw, 0.001f);
    Assert.AreEqual(50f, h.pitch, 0.001f);
}
[Test] public void PitchClampsAndYawWraps()
{
    var h = HeadingMath.Step(170f, 0f, new Vector2(1f, 1f), 200f, 1f, 1f);
    Assert.AreEqual(60f, h.pitch, 0.001f);
    Assert.AreEqual(10f, h.yaw, 0.001f); // 370 wraps to 10
}
```

- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Implement** `HeadingMath` (pure static, returns `(float yaw, float pitch)`) + controller per spec §1/§2 (per `unity-csharp-scripting` + `unity-physics` skills; CharacterController skinWidth 0.08 set on the prefab in Task 7).
- [ ] **Step 4: Re-run EditMode.** Expected: PASS.
- [ ] **Step 5: Write the PlayMode tests** (stay red until Task 7 builds the scene; use `InputTestFixture` so the real input path is exercised, not a test hook):

```csharp
public class MovementSceneTests : InputTestFixture
{
    [SetUp] public override void Setup()
    {
        base.Setup();
        InputSystem.AddDevice<Keyboard>();
        InputSystem.AddDevice<Gamepad>();
    }

    static IEnumerator WaitForState(FlightStateMachine sm, PlayerLocomotionState want, float timeout = 5f)
    {
        float t = 0f;
        while (sm.Locomotion != want && t < timeout) { t += Time.deltaTime; yield return null; }
    }

    [UnityTest] public IEnumerator MaxSpeedRunTravelsForwardInBounds()
    {
        // No steering input: heading holds at spawn yaw/pitch (0, 0), fish porpoises level toward +Z.
        var sm = Object.FindFirstObjectByType<FlightStateMachine>();
        sm.SetTier(FlightTier.Max);
        sm.Momentum.TargetSpeed = 110f;
        yield return new WaitForSeconds(2f);
        var p = sm.transform.position;
        Assert.IsFalse(float.IsNaN(p.x + p.y + p.z), "position went NaN at max speed");
        Assert.Greater(p.z, 100f, "fish did not travel forward at speed");
        Assert.Less(Mathf.Abs(p.x), 150f);
        Assert.GreaterOrEqual(p.y, -12f); Assert.Less(p.y, 120f);
    }

    [UnityTest] public IEnumerator SwimFlySwimRoundTripViaBreach()
    {
        var sm = Object.FindFirstObjectByType<FlightStateMachine>();
        Assert.AreEqual(PlayerLocomotionState.Swimming, sm.Locomotion, "spawn must be underwater Swimming");
        sm.SetTier(FlightTier.Low);
        sm.Momentum.CurrentSpeed = 35f; sm.Momentum.TargetSpeed = 35f;
        Press(Keyboard.current.wKey); // climb at 35 >= threshold 30
        yield return WaitForState(sm, PlayerLocomotionState.Flying);
        Release(Keyboard.current.wKey);
        Assert.AreEqual(PlayerLocomotionState.Flying, sm.Locomotion, "fast breach did not enter Fly");
        Press(Keyboard.current.sKey); // dive back down
        yield return WaitForState(sm, PlayerLocomotionState.Swimming);
        Release(Keyboard.current.sKey);
        Assert.AreEqual(PlayerLocomotionState.Swimming, sm.Locomotion, "downward crossing did not re-enter Swim");
    }
}
```

- [ ] **Step 6: Commit** `feat: movement controller + heading math (M1 task 5)` (PlayMode tests red — scene lands in Task 7).

### Task 6: CameraSpeedReactor + CameraMath (PRD §16)

**Files:** create `CameraMath.cs`, `CameraSpeedReactor.cs`; test `CameraMathTests.cs`.
**Interfaces:** consumes `momentum.CurrentSpeed`, `MinSpeed`, profile `CameraFOV/CameraDistance`, `CameraSettings`. Produces follow in `LateUpdate`: position smoothing factor `1 − exp(−dt ÷ PositionLag)` toward `anchor = fish − forward·distance + up·(distance·0.4)`; velocity look-ahead on the look target; displayed FOV chases `CameraMath.TargetFov(...)` at `FOVSpeedResponse`, clamped to `MaxFOV`; roll target = `clamp(−yawRate ÷ TurnRate × 8°, −8°, 8°)` smoothed; `PlayTierUpKick()` / `PlayMissShake()` set `KickEnvelope` / `ShakeEnvelope` (public read-only) to 1, decayed per-frame by `CameraMath.Decay(e, 2.5, dt)`; kick offsets = envelope × TierUpCameraKick × 20 (FOV deg) and × 6 (distance units) — M1-chosen presentation mapping of the PRD tunable, documented in code; shake = deterministic `sin(Time.time·40)` positional noise × envelope × `MissCameraShake` units. Scaled dt throughout so pause freezes the rig.

- [ ] **Step 1: Write the failing tests.**

```csharp
[Test] public void FovIsBaseAtMin_TierAtMax_NeverOvershoots()
{
    Assert.AreEqual(60f, CameraMath.TargetFov(8f, 8f, 110f, 60f, 95f), 0.01f);
    Assert.AreEqual(95f, CameraMath.TargetFov(110f, 8f, 110f, 60f, 95f), 0.01f);
    Assert.AreEqual(77.5f, CameraMath.TargetFov(59f, 8f, 110f, 60f, 95f), 0.01f);
    Assert.AreEqual(60f, CameraMath.TargetFov(-50f, 8f, 110f, 60f, 95f), 0.01f, "below min clamps");
}
[Test] public void RollProportionalAndClamped()
{
    Assert.AreEqual(-4f, CameraMath.RollTarget(100f, 200f), 0.01f);
    Assert.AreEqual(-8f, CameraMath.RollTarget(400f, 200f), 0.01f, "clamps at -8");
    Assert.AreEqual(0f, CameraMath.RollTarget(0f, 200f), 0.01f);
}
[Test] public void FovNeverExceedsMaxFov()
{
    Assert.LessOrEqual(CameraMath.ClampFov(150f, 100f), 100f);
    Assert.AreEqual(95f, CameraMath.ClampFov(95f, 100f), 0.01f);
}
[Test] public void EnvelopesDecayToZero()
{
    Assert.AreEqual(0f, CameraMath.Decay(1f, 2.5f, 1f), 0.01f);
    Assert.AreEqual(0.5f, CameraMath.Decay(1f, 2.5f, 0.2f), 0.01f);
}
```

- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Implement** (`TargetFov` lerps by `clamp01(InverseLerp(min, max, speed))`; `RollTarget` as spec'd; `ClampFov(fov, max) = Min(fov, max)`; `Decay(e, rate, dt) = Max(0, e − rate·dt)`).
- [ ] **Step 4: Re-run.** Expected: PASS.
- [ ] **Step 5: Commit** `feat: speed-reactive camera (M1 task 6)`.

### Task 7: Scene, prefabs, debug scaffold, PlayMode green, feel pass

**Files:** create `Assets/Scenes/M1_MovementProof.unity`, `Assets/Prefabs/PlayerRoot.prefab`, `CameraRig.prefab`, `Assets/Materials/M1Fish.mat` (flat orange), `M1Water.mat` (transparent blue, alpha 0.6), `Assets/Scripts/Debug/M1DebugInput.cs` + `M1DebugOverlay.cs` (`[M1-SCAFFOLD]`, overlay IMGUI `OnGUI`, debug builds only).
**Scene contents (exact):** water plane 400×800 centered (0,0,300) at y=0 with its auto-added MeshCollider **removed** (breach must pass through — verify in the Inspector); seabed plane same footprint at y=-12 **with** MeshCollider; 4 box islands (colliders kept) forming a slalom corridor z=30–90 with gaps ≥ 20 (turn radius at Low ≈ 14.3, so 20+ is steerable); open water z>120 for high-speed straight runs (turn radius at Max ≈ 31.5 — no gates there); template directional light + skybox untouched; PlayerRoot spawn (0,-3,0) facing +Z; CameraRig behind at (0,2,-12) looking at fish. Fish visual: capsule body (r 0.5, h 2, axis along Z) + cone nose + box tail fin; CharacterController height 2, radius 0.5, skinWidth 0.08. Wire inspector refs: profiles list, settings, input asset. `M1DebugInput`: keys 1–5 → `SetTier` + `TargetSpeed` = tier max; T/G → camera kick/shake; Pause action → `Time.timeScale` toggle 0/1. Overlay: CurrentSpeed, TargetSpeed, Locomotion, ActiveTier, BreachSpeedThreshold, FPS.

- [ ] **Step 1: Build scene + prefabs + debug scripts** per above.
- [ ] **Step 2: Run PlayMode suite.** Run: `unity test "C:\Users\TRISTAN\FLYING FIH" --mode PlayMode`. Expected: PASS (Task 5 tests + pause-freeze below). Fix scene/wiring until green — failures here are scene bugs; use `systematic-debugging`, never loosen asserts.

```csharp
[UnityTest] public IEnumerator PauseFreezesSimulation()
{
    var mover = Object.FindFirstObjectByType<PlayerMovementController>();
    mover.StateMachine.SetTier(FlightTier.Low);
    mover.StateMachine.Momentum.TargetSpeed = 35f;
    yield return new WaitForSeconds(0.5f);
    var before = mover.transform.position;
    Time.timeScale = 0f;
    try
    {
        yield return new WaitForSecondsRealtime(0.5f);
        Assert.AreEqual(before, mover.transform.position, "moved while paused");
    }
    finally { Time.timeScale = 1f; }
}
```

- [ ] **Step 3: Human feel pass (required, not automatable).** Play 60–90 s: steerable at Low, controllable at Max, breach/re-entry reliable both directions, camera never loses fish, overlay FPS stable (record the number). Human signs off or files specific feel defects → fix → re-test.
- [ ] **Step 4: Commit** `feat: M1 movement-proof scene (M1 task 7)`.

### Task 8: M1 acceptance + review

- [ ] **Step 1: Verify** PRD §28.1 M1-applicable items: #1 fish moves forward with visible momentum ✓ (Task 7), #8 camera reacts to speed ✓ (Task 6/7); M1 success "feels good with zero progression systems" = Task 7 Step 3 sign-off.
- [ ] **Step 2: Request code review** (`requesting-code-review`); address findings as new red→green→commit cycles.
- [ ] **Step 3: Verify full suite green.** Run: `unity test "C:\Users\TRISTAN\FLYING FIH" --mode EditMode` and `--mode PlayMode`. Expected: all PASS. Commit any fixes; final `git log --oneline` shows the M1 task chain.
