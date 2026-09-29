# Flying Fish M2 (Timing Proof) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add distance-beat timing prompts with Perfect/Good/Miss judgment, speed consequences, and placeholder ring + text feedback, replacing debug speed keys.

**Architecture:** Pure-static evaluator (EditMode-tested, like M1 `HeadingMath`) plus a thin spawner MonoBehaviour with an injectable clock (`Tick(now, speed, pressed)`) so all timing logic is unit-testable; direct `Configure` wiring following the M1 pattern (no new event channels).

**Tech Stack:** Unity 6000.6.3f1, C#, Unity Input System, NUnit (EditMode + PlayMode batchmode via `unity` CLI).

**Spec:** `docs/superpowers/specs/2026-09-29-flying-fish-m2-design.md`

## Global Constraints

- PowerShell only: chain with `cmd1; if ($?) { cmd2 }`. No `grep` (use `Select-String`), no `&&`.
- Tests: `unity test "C:\Users\TRISTAN\FLYING FIH" --mode EditMode|PlayMode`; parse `test-results.xml` with the established python one-liner; results files are gitignored.
- Compiler errors: exact `file(line,col): error CSxxxx` lines live in `Logs/Editor.log`.
- Never commit `Library/`, `Temp/`, `Logs/`, `obj/`, `*.csproj`, `*.sln`.
- Commits: `feat|test|chore: <what> (M2 task N)`. Push `origin main` with feature commits.
- Gameplay: SO-only tuning, dt-scaled determinism, no `Random`. Red → green → commit, no exceptions.
- PlayMode batchmode: no synthetic input (`Press()` stalls the player loop); drive via direct method calls; disable auto-`Update` (`mover.enabled = false`) when pumping manually, and call `SetTier` BEFORE disabling (disabling unsubscribes `ApplyProfile`).

## Review Focus

- Press with no prompt open is ignored, never a Miss — pinned in Task 5.
- Prompt open across pause (`timeScale = 0` freezes `Time.time`) resumes unjudged — pinned in Task 5.
- Prompt open across swim/fly transition keeps working (spawner ignores locomotion) — pinned in Task 8.
- `InverseLerp` inputs clamp: speed below Min or above Max still yields sane windows — pinned in Task 2.
- Over-length frame (`dt` spike) cannot skip a whole prompt lifecycle — accumulator is dt-scaled; skip-while-open pinned in Task 5.

---

### Task 1: TimingResult enum + TimingSettings SO + asset

**Files:**
- Create: `Assets/Scripts/Timing/TimingResult.cs`
- Create: `Assets/Scripts/Config/TimingSettings.cs`
- Create: `Assets/Configs/TimingSettings.asset` (hand-written YAML, see steps)
- Create: `Assets/Tests/EditMode/TimingSettingsTests.cs`

**Interfaces:**
- Consumes: `FlightTier` (`Assets/Scripts/Player/FlightTier.cs`: None=0, Low=1, Medium=2, High=3, Max=4).
- Produces: `TimingResult` enum (`Perfect, Good, Miss`); `TimingSettings` with fields below + `public float DeltaFor(TimingResult r, FlightTier t)` (Miss returns negative).

```csharp
public enum TimingResult { Perfect, Good, Miss }

[CreateAssetMenu(fileName = "TimingSettings", menuName = "FlyingFish/Timing Settings")]
public class TimingSettings : ScriptableObject
{
    public float PromptEveryMeters = 60f;
    public float LeadTime = 1f;
    public float PerfectWindow = 0.07f;
    public float MinPerfectWindow = 0.035f;
    public float GoodWindow = 0.18f;
    public float MinGoodWindow = 0.09f;
    public float LateBuffer = 0.06f;
    public float[] PerfectBoost = { 0f, 12f, 16f, 22f, 30f };
    public float[] GoodBoost = { 0f, 6f, 8f, 11f, 15f };
    public float[] MissPenalty = { 0f, 18f, 28f, 45f, 70f }; // positive magnitudes
    public float DeltaFor(TimingResult r, FlightTier t)
    {
        int i = Mathf.Clamp((int)t, 0, 4);
        return r == TimingResult.Perfect ? PerfectBoost[i]
            : r == TimingResult.Good ? GoodBoost[i] : -MissPenalty[i];
    }
}
```
(`using UnityEngine;` for `Mathf`. Arrays indexed by tier int; None row is 0.)

- [ ] **Step 1: Write the failing test** (`Assets/Tests/EditMode/TimingSettingsTests.cs`):
```csharp
[Test]
public void TableMatchesPRD()
{
    var s = ScriptableObject.CreateInstance<TimingSettings>();
    Assert.AreEqual(60f, s.PromptEveryMeters, 0.001f);
    Assert.AreEqual(1f, s.LeadTime, 0.001f);
    Assert.AreEqual(0.07f, s.PerfectWindow, 0.0001f);
    Assert.AreEqual(16f, s.DeltaFor(TimingResult.Perfect, FlightTier.Medium), 0.001f);
    Assert.AreEqual(8f, s.DeltaFor(TimingResult.Good, FlightTier.Medium), 0.001f);
    Assert.AreEqual(-28f, s.DeltaFor(TimingResult.Miss, FlightTier.Medium), 0.001f);
    Assert.AreEqual(0f, s.DeltaFor(TimingResult.Perfect, FlightTier.None), 0.001f);
}
```
- [ ] **Step 2: Run test to verify it fails**

Run: `unity test "C:\Users\TRISTAN\FLYING FIH" --mode EditMode --filter TimingSettingsTests`
Expected: FAIL with exit code 2 — types do not exist.

- [ ] **Step 3: Write minimal implementation** — create the two files above exactly as specified (namespace `FlyingFishMomentum`).
- [ ] **Step 4: Create the .asset** — first run any `unity` command once so the `.cs` import generates `Assets/Scripts/Config/TimingSettings.cs.meta`; read the `guid:` from that `.meta`; then write `Assets/Configs/TimingSettings.asset` (exact shape copied from `Assets/Configs/MomentumSettings.asset`: `%YAML 1.1` header, `--- !u!114`, `MonoBehaviour:` with 2-space keys, arrays as 2-space dash items):
  - scalar fields: `PromptEveryMeters: 60`, `LeadTime: 1`, `PerfectWindow: 0.07`, `MinPerfectWindow: 0.035`, `GoodWindow: 0.18`, `MinGoodWindow: 0.09`, `LateBuffer: 0.06`
  - `PerfectBoost:` items `0, 12, 16, 22, 30`; `GoodBoost:` items `0, 6, 8, 11, 15`; `MissPenalty:` items `0, 18, 28, 45, 70`
- [ ] **Step 5: Run test to verify it passes**

Run: `unity test "C:\Users\TRISTAN\FLYING FIH" --mode EditMode --filter TimingSettingsTests`, parse `test-results.xml`.
Expected: PASS. If the asset fails to import, read `Logs/Editor.log` for the YAML error, fix indentation, rerun.

- [ ] **Step 6: Commit**

Run: `git add Assets/Scripts/Timing/TimingResult.cs Assets/Scripts/Config/TimingSettings.cs Assets/Configs/TimingSettings.asset Assets/Tests/EditMode/TimingSettingsTests.cs; if ($?) { git commit -m "feat: TimingResult + TimingSettings SO (M2 task 1)" }`

### Task 2: TimingEvaluator (pure static) + boundary tests

**Files:**
- Create: `Assets/Scripts/Timing/TimingEvaluator.cs`
- Create: `Assets/Tests/EditMode/TimingEvaluatorTests.cs`

**Interfaces:**
- Consumes: `TimingResult`, `TimingSettings` (Task 1).
- Produces: `public static TimingResult Evaluate(float offsetSeconds, float speed, TimingSettings s, float minSpeed, float maxSpeed)` plus window helpers `public static float PerfectWindowAt(...)` / `GoodWindowAt(...)` (same args minus offset; spawner uses `GoodWindowAt` for the expiry edge). `offsetSeconds = pressTime - targetMoment` (negative = early). `perfect = Lerp(PerfectWindow, MinPerfectWindow, InverseLerp(minSpeed, maxSpeed, speed))`, same shape for good. Rules: `|offset| <= perfect` → Perfect; `|offset| <= good` → Good; `good < offset <= good + LateBuffer` → Good (late grace only, early has no grace); else Miss (covers early press and expiry).

- [ ] **Step 1: Write the failing tests**
```csharp
[Test]
public void WindowEdges()
{
    var s = ScriptableObject.CreateInstance<TimingSettings>();
    // at speed 50: perfect ≈ 0.0556, good ≈ 0.143 (margins are float-safe)
    Assert.AreEqual(TimingResult.Perfect, TimingEvaluator.Evaluate(0.05f, 50f, s, 8f, 110f));
    Assert.AreEqual(TimingResult.Perfect, TimingEvaluator.Evaluate(-0.05f, 50f, s, 8f, 110f));
    Assert.AreEqual(TimingResult.Good, TimingEvaluator.Evaluate(0.06f, 50f, s, 8f, 110f));
    Assert.AreEqual(TimingResult.Miss, TimingEvaluator.Evaluate(-0.5f, 50f, s, 8f, 110f));
    Assert.AreEqual(0.07f, TimingEvaluator.PerfectWindowAt(8f, s, 8f, 110f), 0.0001f);
    Assert.AreEqual(0.035f, TimingEvaluator.PerfectWindowAt(110f, s, 8f, 110f), 0.0001f);
}
[Test]
public void LateGraceAndPastIt()
{
    var s = ScriptableObject.CreateInstance<TimingSettings>();
    // good at 50u/s ≈ 0.143; grace +0.06 → 0.19 Good, 0.21 Miss
    Assert.AreEqual(TimingResult.Good, TimingEvaluator.Evaluate(0.19f, 50f, s, 8f, 110f));
    Assert.AreEqual(TimingResult.Miss, TimingEvaluator.Evaluate(0.21f, 50f, s, 8f, 110f));
}
[Test]
public void WindowsShrinkAtSpeedAndClampOutOfRange()
{
    var s = ScriptableObject.CreateInstance<TimingSettings>();
    Assert.AreEqual(TimingResult.Perfect, TimingEvaluator.Evaluate(0.05f, 8f, s, 8f, 110f));
    Assert.AreEqual(TimingResult.Good, TimingEvaluator.Evaluate(0.05f, 110f, s, 8f, 110f));
    Assert.AreEqual(TimingResult.Good, TimingEvaluator.Evaluate(0.05f, 500f, s, 8f, 110f));
    Assert.AreEqual(TimingResult.Perfect, TimingEvaluator.Evaluate(0.05f, 0f, s, 8f, 110f));
}
```
- [ ] **Step 2: Run test to verify it fails**

Run: `unity test "C:\Users\TRISTAN\FLYING FIH" --mode EditMode --filter TimingEvaluatorTests`
Expected: FAIL with exit code 2 — type does not exist.

- [ ] **Step 3: Write minimal implementation** — the `Evaluate` method exactly per the rules above (namespace `FlyingFishMomentum`, `using UnityEngine;` for `Mathf`).
- [ ] **Step 4: Run test to verify it passes**

Run: same filter, parse `test-results.xml`. Expected: PASS.
- [ ] **Step 5: Commit**

Run: `git add Assets/Scripts/Timing/TimingEvaluator.cs Assets/Tests/EditMode/TimingEvaluatorTests.cs; if ($?) { git commit -m "test,feat: timing window evaluator (M2 task 2)" }`

### Task 3: ApplyTimingResult on momentum + table tests

**Files:**
- Modify: `Assets/Scripts/Player/PlayerMomentumController.cs` (add `_timing` field, extend `Configure`, add method)
- Modify: `Assets/Tests/EditMode/MomentumTests.cs` (helper creates both settings)
- Modify: `Assets/Editor/ProjectBootstrap/M1SceneBuilder.cs:25,92` (load asset, pass to `Configure`)

**Interfaces:**
- Consumes: `TimingResult`, `TimingSettings.DeltaFor`, `FlightTier`.
- Produces: `public void ApplyTimingResult(TimingResult result, FlightTier tier)` → burst with overflow: `CurrentSpeed = Mathf.Min(CurrentSpeed + _timing.DeltaFor(result, tier), _maxSpeed + Mathf.Max(0f, _timing.DeltaFor(result, tier)))` (Perfect/Good may exceed tier max up to max+boost and decay back via drag; Miss uses plain `AddSpeed`, floor held by `Tick`). `Configure(MomentumSettings settings, TimingSettings timing)`. Null-guard: if `_timing` is null the method adds 0 (defensive; all wired callers pass it).

- [ ] **Step 1: Write the failing tests** (append to `MomentumTests.cs`; helper gains `var t = ScriptableObject.CreateInstance<TimingSettings>();` with explicit field values mirroring the asset, passed as second `Configure` arg):
```csharp
[Test]
public void ApplyTimingResultFollowsTable()
{
    var m = NewMomentum(max: 100f, accel: 120f);
    m.CurrentSpeed = 50f; m.TargetSpeed = 50f;
    m.ApplyTimingResult(TimingResult.Good, FlightTier.Medium);
    Assert.AreEqual(58f, m.CurrentSpeed, 0.001f);
    m.ApplyTimingResult(TimingResult.Perfect, FlightTier.Max);
    Assert.AreEqual(88f, m.CurrentSpeed, 0.001f);
    m.ApplyTimingResult(TimingResult.Miss, FlightTier.Medium);
    Assert.AreEqual(60f, m.CurrentSpeed, 0.001f);
}
[Test]
public void MissNeverBreaksMinSpeed()
{
    var m = NewMomentum();
    m.CurrentSpeed = 10f; m.TargetSpeed = 8f;
    m.ApplyTimingResult(TimingResult.Miss, FlightTier.Max); // -70
    m.Tick(1f, 1.5f);
    Assert.AreEqual(8f, m.CurrentSpeed, 0.01f);
}
[Test]
public void PerfectOverflowsTierMaxThenDecays()
{
    var m = NewMomentum(max: 50f, accel: 120f);
    m.CurrentSpeed = 50f; m.TargetSpeed = 50f;
    m.ApplyTimingResult(TimingResult.Perfect, FlightTier.Medium); // +16 → 66, above max
    Assert.AreEqual(66f, m.CurrentSpeed, 0.001f);
    m.Tick(1f, 0.6f); // drag chase pulls back toward 50
    Assert.Less(m.CurrentSpeed, 66f);
    Assert.Greater(m.CurrentSpeed, 50f);
}
```
- [ ] **Step 2: Run test to verify it fails**

Run: `unity test "C:\Users\TRISTAN\FLYING FIH" --mode EditMode --filter MomentumTests`
Expected: FAIL — `ApplyTimingResult` does not exist.

- [ ] **Step 3: Write minimal implementation** — add `_timing` field, extend `Configure`, add the burst-with-overflow method + null-guard. Update `M1SceneBuilder.cs` load + `Configure` call.
- [ ] **Step 4: Run FULL EditMode suite** (helper change touches all momentum tests).

Run: `unity test "C:\Users\TRISTAN\FLYING FIH" --mode EditMode`, parse `test-results.xml`. Expected: all PASS.
- [ ] **Step 5: Commit**

Run: `git add Assets/Scripts/Player/PlayerMomentumController.cs Assets/Tests/EditMode/MomentumTests.cs Assets/Editor/ProjectBootstrap/M1SceneBuilder.cs; if ($?) { git commit -m "feat: ApplyTimingResult per PRD table (M2 task 3)" }`

### Task 4: TimingAction binding + input test rewrite

**Files:**
- Modify: `Assets/Input/PlayerInputActions.inputactions` (add action + 2 bindings)
- Modify (regenerated): `Assets/Scripts/Input/PlayerInputActions.cs` (do NOT hand-edit; verify regen)
- Modify: `Assets/Tests/EditMode/InputAssetTests.cs` (rewrite `NoTimingActionInM1`)
- Modify: `AGENTS.md` (input section: TimingAction is now live, Space + gamepad south)

**Interfaces:**
- Consumes: existing `.inputactions` JSON shape (copy the `Pause` action block verbatim, new `id` guid).
- Produces: `Gameplay/TimingAction` (type Button), bindings `<Keyboard>/space` + `<Gamepad>/buttonSouth`. Generated class gains `TimingAction` members by regeneration.

- [ ] **Step 1: Rewrite the test first** — replace `NoTimingActionInM1` with:
```csharp
[Test]
public void TimingActionExistsWithBindings()
{
    var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(Path);
    var timing = asset.FindAction("Gameplay/TimingAction");
    Assert.IsNotNull(timing, "TimingAction missing");
    Assert.AreEqual("Button", timing.expectedControlType);
    Assert.IsTrue(timing.bindings.Any(b => b.path.Contains("/space")), "space binding missing");
    Assert.IsTrue(timing.bindings.Any(b => b.path.Contains("buttonSouth")), "gamepad binding missing");
}
```
(`System.Linq` + `UnityEngine.InputSystem` already imported in that file.)
- [ ] **Step 2: Run test to verify it fails**

Run: `unity test "C:\Users\TRISTAN\FLYING FIH" --mode EditMode --filter InputAssetTests`
Expected: FAIL — action is null.

- [ ] **Step 3: Edit the JSON** — add the action + two bindings copying the `Pause` entries' exact field shape.
- [ ] **Step 4: Regenerate + verify** — run any `unity` command (triggers reimport); then `Select-String -Path Assets/Scripts/Input/PlayerInputActions.cs -Pattern TimingAction` MUST hit. If regen did not happen, rerun a `unity test` (forces domain reload); last resort hand-edit `.cs` mirroring the `Pause` members exactly.
- [ ] **Step 5: Run test to verify it passes** — same filter. Expected: PASS.
- [ ] **Step 6: Commit**

Run: `git add Assets/Input/PlayerInputActions.inputactions Assets/Scripts/Input/PlayerInputActions.cs Assets/Scripts/Input/PlayerInputActions.cs.meta Assets/Tests/EditMode/InputAssetTests.cs AGENTS.md; if ($?) { git commit -m "feat: TimingAction Space+gamepad binding (M2 task 4)" }`

### Task 5: TimingPromptSpawner core + scheduling tests

**Files:**
- Create: `Assets/Scripts/Timing/TimingPromptSpawner.cs`
- Create: `Assets/Tests/EditMode/TimingSpawnerTests.cs`

**Interfaces:**
- Consumes: `PlayerMovementController` (speed via `Momentum.CurrentSpeed`, input via `Input.Gameplay.TimingAction.WasPressedThisFrame()` — same polling style as `Move`), `PlayerMomentumController.ApplyTimingResult`, `FlightStateMachine.ActiveTier`, `TimingSettings`, `TimingEvaluator.Evaluate`, `CameraSpeedReactor.PlayTierUpKick/PlayMissShake` (existing).
- Produces: `public struct ActivePrompt { public bool Open; public float TargetTime; }`; `public ActivePrompt Active { get; private set; }`; `public TimingResult LastResult`; `public bool HasResolved`; `public void Tick(float now, float speed, bool pressed)` (test seam — `Update()` calls `Tick(Time.time, _momentum.CurrentSpeed, action.WasPressedThisFrame())`); `Configure(movement, momentum, sm, timing, camera)` stores refs (M1 direct-wiring pattern). Accumulator `_meters` resets on open and on resolve; no open while open. Pressed with nothing open: ignored. Resolve: pressed → `Evaluate(now - TargetTime, ...)` → `momentum.ApplyTimingResult(result, sm.ActiveTier)` → feedback (Perfect → `PlayTierUpKick`, Miss → `PlayMissShake`, Good → nothing) → `LastResult`/`HasResolved` set → close. Expiry (`now > TargetTime + goodAtCurrentSpeed + LateBuffer`, good recomputed via same window math — expose `TimingEvaluator.GoodWindowAt(speed, s, minSpeed, maxSpeed)` helper for both sides) → Miss path. Null-guard `Update` like M1 (`if (...) return;`).

- [ ] **Step 1: Write the failing tests** — spawner on a bare GameObject with a real momentum (helper pattern from `MomentumTests`: `AddComponent` + both settings + camera null → feedback calls must null-guard; helper calls `SetLimits(100f, 120f)` so the 50 → 66 boost is not clamped):
  - prompt opens after 60m at constant speed; NOT open at 59m;
  - second prompt suppressed while first open;
  - same distance schedule twice → identical open times;
  - press with nothing open → `HasResolved` false, speed unchanged;
  - frozen clock (same `now` twice) never resolves;
  - Perfect press applies Medium boost (speed 50 → 66 with real momentum).
- [ ] **Step 2: Run test to verify it fails**

Run: `unity test "C:\Users\TRISTAN\FLYING FIH" --mode EditMode --filter TimingSpawnerTests`
Expected: FAIL with exit code 2 — type does not exist.

- [ ] **Step 3: Write minimal implementation** — spawner exactly per the interface above.
- [ ] **Step 4: Run test to verify it passes** — same filter. Expected: PASS.
- [ ] **Step 5: Commit**

Run: `git add Assets/Scripts/Timing/TimingPromptSpawner.cs Assets/Tests/EditMode/TimingSpawnerTests.cs; if ($?) { git commit -m "test,feat: timing prompt spawner (M2 task 5)" }`

### Task 6: Ring visual + overlay text + scene wiring

**Files:**
- Create: `Assets/Scripts/Timing/TimingPromptRing.cs`
- Modify: `Assets/Scripts/Debug/M1DebugOverlay.cs` (spawner ref + 2 lines)
- Modify: `Assets/Editor/ProjectBootstrap/M1SceneBuilder.cs` (spawner + ring GameObjects, `Configure` calls)
- Rebuild scene headless; commit rebuilt scene/prefabs only.

**Interfaces:**
- Consumes: spawner `Active` + `TimingSettings.LeadTime`.
- Produces: `TimingPromptRing.Configure(spawner, target)`; `Update()` draws a 48-segment `LineRenderer` circle in the XZ plane at the fish, radius lerped 6 → 1.2 by `1 - (TargetTime - Time.time) / LeadTime`, enabled only while open; material `new Material(Shader.Find("Sprites/Default"))`, white. Overlay `Configure` gains trailing `TimingPromptSpawner spawner` param; `OnGUI` appends one line: result word if `HasResolved`, else `OPEN <seconds>f` countdown while open, else `-`. Builder: new `GameObject("Timing")` with spawner (`spawner.Configure(movement, momentum, sm, timingSettings, reactor)`) + ring child (`ring.Configure(spawner, player.transform)`); reuse the Task 3 `timingSettings` variable; overlay `Configure` call gains spawner arg.

- [ ] **Step 1: Write the failing PlayMode smoke test** (append to `MovementSceneTests.cs`):
```csharp
[UnityTest]
public IEnumerator PromptOpensInGame()
{
    var sm = Object.FindFirstObjectByType<FlightStateMachine>();
    var spawner = Object.FindFirstObjectByType<TimingPromptSpawner>();
    Assert.IsNotNull(spawner, "spawner not wired in scene");
    sm.SetTier(FlightTier.Medium); sm.Momentum.TargetSpeed = 50f;
    float t = 0f;
    while (!spawner.Active.Open && t < 10f) { t += Time.deltaTime; yield return null; }
    Assert.IsTrue(spawner.Active.Open, "no prompt after 60m of travel");
}
```
- [ ] **Step 2: Run test to verify it fails** — PlayMode filter `PromptOpensInGame`. Expected: FAIL (null spawner).
- [ ] **Step 3: Write minimal implementation** — ring + overlay + builder edits above; rebuild scene headless via the `Build` method (same invocation as M1 Task 7).
- [ ] **Step 4: Run test to verify it passes** — same filter. Expected: PASS. Then FULL PlayMode + EditMode suites (regression: new objects must not disturb movement tests).
- [ ] **Step 5: Commit** — verify `git status --short` shows only intended files (scene + prefabs + code + test).

Run: `git add Assets/Scripts/Timing/TimingPromptRing.cs Assets/Scripts/Debug/M1DebugOverlay.cs Assets/Editor/ProjectBootstrap/M1SceneBuilder.cs Assets/Scenes/M1_MovementProof.unity Assets/Prefabs/PlayerRoot.prefab Assets/Prefabs/CameraRig.prefab Assets/Tests/PlayMode/MovementSceneTests.cs; if ($?) { git commit -m "feat: prompt ring + overlay + scene wiring (M2 task 6)" }`

### Task 7: Delete digit keys, lock Medium, full regression

**Files:**
- Modify: `Assets/Scripts/Debug/M1DebugInput.cs` (delete digit lines 49–53; `Start` sets Medium once with an M2 scaffold comment)

**Interfaces:**
- Consumes: `FlightStateMachine.SetTier` (exists).
- Produces: no tier keys; tier is Medium from scene start. T/G camera keys + pause untouched.

- [ ] **Step 1: Make the edit** + verify no test references digit keys (`Select-String -Path Assets/Tests -Pattern digit` → empty; keys were human-only).
- [ ] **Step 2: Run FULL EditMode + PlayMode suites** — all green (every M1 test sets tiers explicitly or asserts spawn locomotion only).
- [ ] **Step 3: Commit**

Run: `git add Assets/Scripts/Debug/M1DebugInput.cs; if ($?) { git commit -m "chore: timing drives speed now, digits deleted, Medium locked (M2 task 7)" }`

### Task 8: In-game Perfect/Miss pins (manual drive, honest stepping)

**Files:**
- Modify: `Assets/Tests/PlayMode/MovementSceneTests.cs`

**Interfaces:**
- Consumes: spawner `Tick(now, speed, pressed)`, `mover.enabled = false` + `SetTier`-BEFORE-disable ordering (Global Constraints), fixed-step `1f/60f` determinism.

- [ ] **Step 1: Write the tests**
```csharp
[UnityTest]
public IEnumerator PerfectPressGainsSpeedInGame()
{
    // mover + spawner frozen (enabled = false, AFTER SetTier(Medium));
    // accumulate 60m via spawner.Tick(t, 50f, false) with rising t;
    // Tick(now: spawner.Active.TargetTime, pressed: true) → Perfect → speed 50 + 16 (±0.5).
}
[UnityTest]
public IEnumerator ExpiredPromptLosesSpeedInGame()
{
    // same setup; advance now past TargetTime + good + buffer with pressed: false
    // → Miss → speed 50 - 28 (±0.5), Active.Open false.
    // Then: breach to fly mid-open prompt (TickMove up through y=0), resolve Perfect —
    // spawner ignores locomotion, still Perfect.
}
```
- [ ] **Step 2: Run tests** — PlayMode filter for both names. Expected: PASS if behavior is right (pin tests — a first-run pass is fine and honest; record it). If red, check `Logs/Editor.log` CS errors first (test bug), then behavior gap.
- [ ] **Step 3: Run FULL PlayMode + EditMode suites** — all green.
- [ ] **Step 4: Commit**

Run: `git add Assets/Tests/PlayMode/MovementSceneTests.cs; if ($?) { git commit -m "test: in-game Perfect/Miss pins (M2 task 8)" }`

### Task 9: Human feel pass (closes M2)

No code. Human (user) plays 60–90s at locked Medium in `M1_MovementProof.unity`:
- [ ] Hit 5 prompts on purpose — Perfect boost felt, Good smaller boost felt, Miss drop felt
- [ ] Ring readable at all speeds, hit moment never ambiguous
- [ ] Camera kick on Perfect, shake on Miss, never loses fish
- [ ] FPS number noted
Report back in plain words; tunables adjusted in a follow-up if needed (no new mechanics).

### Task 10: Review + final suites

- [ ] Dispatch code reviewer subagent per `requesting-code-review` (range: first M2 commit..HEAD, requirements = this plan + spec).
- [ ] Fix Critical immediately, Important before proceeding, note Minor.
- [ ] FULL EditMode + PlayMode suites green, `git status` clean, push `origin main`, ledger entry.
