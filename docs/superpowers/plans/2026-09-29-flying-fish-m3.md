# Flying Fish M3 (Gauge & Tiers) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a ring-filled flight gauge that moves the fish through speed-cap tiers with camera feedback, replacing the locked-Medium era.

**Architecture:** New `FlightGaugeSystem` MonoBehaviour with an explicit-clock `Tick` seam (same pattern as the spawner); thresholds call the existing `FlightStateMachine.SetTier`; spawner pushes charge fills and beat-Miss drains through direct refs (M1 wiring precedent, no event bus).

**Tech Stack:** Unity 6000.6.3f1, C#, Unity Input System, NUnit (EditMode + PlayMode batchmode via `unity` CLI).

**Spec:** `docs/superpowers/specs/2026-09-29-flying-fish-m3-design.md`

## Global Constraints

- PowerShell only: chain with `cmd1; if ($?) { cmd2 }`. No `grep` (use `Select-String`), no `&&`.
- Tests: `unity test "C:\Users\TRISTAN\FLYING FIH" --mode EditMode|PlayMode`; parse `test-results.xml` with the established python one-liner; results files are gitignored.
- Compiler errors: exact `file(line,col): error CSxxxx` lines live in `Logs/Editor.log`.
- Never commit `Library/`, `Temp/`, `Logs/`, `obj/`, `*.csproj`, `*.sln`.
- Commits: `feat|test|chore: <what> (M3 task N)`. Push `origin main` with feature commits.
- Gameplay: SO-only tuning, dt-scaled determinism, no `Random` (seeded `System.Random` only), red → green → commit, no exceptions.
- PlayMode batchmode: no synthetic input; drive via direct method calls; `SetTier` BEFORE `mover.enabled = false`; freeze autonomous systems (`mover`, `spawner`, `gauge` via `enabled = false`) when a test pins another system.
- New `.cs`/`.asset` files: commit their `.meta` files too.

## Review Focus

- Gauge parked exactly on a threshold (20.0) reads the UPPER tier or the lower one — pin it (Task 2).
- Miss at gauge 0 stays 0 and fires no tier churn — pin it (Task 2).
- Tier-down with speed above the new max clamps both target and current — pin it (Task 4).
- Charge jackpot overfills past 120 — pin the cap (Task 2).
- Spawner with null gauge never throws (defensive path) — pin it (Task 3).

---

### Task 1: FlightGaugeSettings SO + asset

**Files:**
- Create: `Assets/Scripts/Gauge/FlightGaugeSettings.cs`
- Create: `Assets/Configs/FlightGaugeSettings.asset` (hand YAML after `.meta` guid, M2 Task 1 precedent)
- Create: `Assets/Tests/EditMode/FlightGaugeSettingsTests.cs`

**Interfaces:**
- Consumes: nothing (leaf data).
- Produces: `FlightGaugeSettings` with `MaxGauge = 120f`, `StartGauge = 0f`, `ChargeStepFill = 2f`, `ChargeJackpotFill = 4f`, `FlyDrainPerSecond = 3.5f`, `MissDrain = 10f`, `float[] TierThresholds = { 20f, 40f, 70f, 100f }` (Low..Max floors; below 20 is None) + `public FlightTier TierFor(float gauge)` (half-open bands: boundary belongs UP — 20→Low, 70→High, 100→Max).

- [ ] **Step 1: Write the failing test**
```csharp
[Test]
public void TableMatchesSpec()
{
    var s = ScriptableObject.CreateInstance<FlightGaugeSettings>();
    Assert.AreEqual(120f, s.MaxGauge, 0.001f);
    Assert.AreEqual(0f, s.StartGauge, 0.001f);
    Assert.AreEqual(FlightTier.None, s.TierFor(0f));
    Assert.AreEqual(FlightTier.Low, s.TierFor(20f));
    Assert.AreEqual(FlightTier.High, s.TierFor(70f));
    Assert.AreEqual(FlightTier.Max, s.TierFor(120f));
}
[Test]
public void AssetFileLoadsWithSpecValues()
{
    var asset = AssetDatabase.LoadAssetAtPath<FlightGaugeSettings>("Assets/Configs/FlightGaugeSettings.asset");
    Assert.IsNotNull(asset, "FlightGaugeSettings.asset failed to import");
    Assert.AreEqual(3.5f, asset.FlyDrainPerSecond, 0.001f);
    Assert.AreEqual(10f, asset.MissDrain, 0.001f);
}
```
- [ ] **Step 2: Run test to verify it fails**

Run: `unity test "C:\Users\TRISTAN\FLYING FIH" --mode EditMode --filter FlightGaugeSettingsTests`
Expected: FAIL with exit code 1 (missing types).

- [ ] **Step 3: Write minimal implementation** — the SO exactly as specified (namespace `FlyingFishMomentum`, `[CreateAssetMenu(fileName = "FlightGaugeSettings", menuName = "FlyingFish/Gauge Settings")]`).
- [ ] **Step 4: Create the .asset** — run any `unity` command once, read `guid:` from `Assets/Scripts/Gauge/FlightGaugeSettings.cs.meta`, write the YAML (shape copied from `Assets/Configs/TimingSettings.asset`: header, `m_Script` with guid, 2-space keys, thresholds as 2-space dash items `20, 40, 70, 100`).
- [ ] **Step 5: Run test to verify it passes** — same filter, parse `test-results.xml`. Expected: PASS (if the asset fails to import, read `Logs/Editor.log`, fix YAML, rerun).
- [ ] **Step 6: Commit**

Run: `git add Assets/Scripts/Gauge/FlightGaugeSettings.cs Assets/Configs/FlightGaugeSettings.asset Assets/Tests/EditMode/FlightGaugeSettingsTests.cs Assets/Scripts/Gauge/FlightGaugeSettings.cs.meta Assets/Configs/FlightGaugeSettings.asset.meta Assets/Scripts/Gauge.meta; if ($?) { git commit -m "feat: gauge settings SO (M3 task 1)" }`

### Task 2: FlightGaugeSystem + threshold/drain tests

**Files:**
- Create: `Assets/Scripts/Gauge/FlightGaugeSystem.cs`
- Create: `Assets/Tests/EditMode/FlightGaugeTests.cs`

**Interfaces:**
- Consumes: `FlightGaugeSettings`, `FlightStateMachine.SetTier` (exists), `CameraSpeedReactor.PlayTierUpKick/PlayMissShake` (exist).
- Produces: `public float CurrentGauge { get; private set; }`; `Configure(sm, settings, camera)`; `Start()` sets `CurrentGauge = StartGauge`; `AddFill(float)` (clamp 0..Max); `DrainMiss()` (minus `MissDrain`, floor 0); `Tick(float dt, bool flying)` (drain `FlyDrainPerSecond` when flying, then reconcile); reconcile compares `TierFor` before/after, calls `sm.SetTier` on change, kick on up / shake on down (null-guarded camera).

- [ ] **Step 1: Write the failing tests** — build gauge on a bare GameObject with a REAL state machine + momentum (helper pattern from `MomentumTests`/`StateMachineTests`: `AddComponent`, `Configure` with tier profiles for None..Max, momentum with both settings):
```csharp
[Test] FillCrossingUpShiftsTier — AddFill(20) from 0 → tier Low, kick fired (camera stub? use real CameraSpeedReactor? needs camera component — instead assert sm.ActiveTier only; kick covered in PlayMode). Keep EditMode to tier math: assert ActiveTier transitions None→Low→Medium→High→Max at 20/40/70/100 exactly.
[Test] ExactThresholdReadsUpper — TierFor covered in Task 1; here: AddFill to exactly 70.0 → High (boundary belongs up).
[Test] FlyingDrainDropsTier — fill 25 (Low), Tick(2f, flying: true) ×2 (7.0 drain each... 3.5×2=7 per call, two calls = 14 → 11 <20) → tier None.
[Test] MissAtZeroStaysPut — DrainMiss at 0 → gauge 0, no tier call (fires counter stays 0 — subscribe OnTierChanged).
[Test] JackpotOverfillCaps — AddFill(200) → 120, tier Max.
[Test] SwimDoesNotDrain — fill 50, Tick(10f, flying: false) → still 50.
```
- [ ] **Step 2: Run test to verify it fails**

Run: `unity test "C:\Users\TRISTAN\FLYING FIH" --mode EditMode --filter FlightGaugeTests`
Expected: FAIL with exit code 1 (missing type).

- [ ] **Step 3: Write minimal implementation** — gauge exactly per the interface above.
- [ ] **Step 4: Run test to verify it passes** — same filter. Expected: PASS.
- [ ] **Step 5: Commit**

Run: `git add Assets/Scripts/Gauge/FlightGaugeSystem.cs Assets/Tests/EditMode/FlightGaugeTests.cs Assets/Scripts/Gauge/FlightGaugeSystem.cs.meta Assets/Tests/EditMode/FlightGaugeTests.cs.meta; if ($?) { git commit -m "test,feat: flight gauge system (M3 task 2)" }`

### Task 3: Spawner gauge wiring (charge fills, beat-Miss drains)

**Files:**
- Modify: `Assets/Scripts/Timing/TimingPromptSpawner.cs` (`Configure` gains `FlightGaugeSystem gauge`; charge path fills; beat-Miss drains)
- Modify: `Assets/Tests/EditMode/TimingSpawnerTests.cs` (helpers pass gauge or null)

**Interfaces:**
- Consumes: `FlightGaugeSystem.AddFill/DrainMiss` (Task 2), existing spawner `Configure(movement, momentum, sm, timing, momSettings, camera)`.
- Produces: `Configure(..., camera, gauge)` (gauge may be null in tests → skip). Charge path: `if (gain > 0f) { _momentum.AddChargeGain(gain); if (_gauge != null) _gauge.AddFill(gain); }`. Beat-Miss resolve: `if (_gauge != null) _gauge.DrainMiss();` (charge-Miss path untouched — no penalties in charge).

- [ ] **Step 1: Write the failing tests**
```csharp
[Test] ChargeGainFillsGauge — spawner with REAL gauge (helper builds gauge+sm like Task 2, or minimal: gauge with stub sm? Use real sm with None+Low profiles): trigger ring, drive a full hold (adaptive driver pattern from ChargeHoldBanksViaMomentum), assert gauge rose by the banked amount.
[Test] BeatMissDrainsGauge — gauge pre-filled to 50 via AddFill, open beat prompt, expire it → gauge 40, tier still Medium... wait Medium floor is 40: 50-10=40 → stays Medium (boundary!). Use pre-fill 45 → 35 → tier Low. Assert tier.
[Test] TapsDoNotFillGauge — Perfect beat resolve → gauge unchanged.
[Test] NullGaugeNeverThrows — helper WITHOUT gauge (existing NewSpawner path, Configure without gauge? No — signature grows; pass null): full hold → speed banks, no exception.
```
- [ ] **Step 2: Run test to verify it fails**

Run: `unity test "C:\Users\TRISTAN\FLYING FIH" --mode EditMode --filter TimingSpawnerTests`
Expected: FAIL — `AddFill`/7-arg `Configure` do not exist... wait, `AddFill`/`DrainMiss` WILL exist (Task 2 done). What fails: the new test methods reference `gauge` behaviors through spawner that don't exist → the tests fail on missing spawner members (`SetGauge`? no — 7-arg Configure). Precisely: update the test helper FIRST to call 7-arg Configure (compile fail = RED), then the new tests fail on behavior. Either order is honest RED; do helper + new tests together, run once, confirm failures are missing-member/behavior (not typos).
- [ ] **Step 3: Write minimal implementation** — 7th Configure param + the three call sites above.
- [ ] **Step 4: Run FULL EditMode suite** (helper signature change ripples to every spawner test).

Run: `unity test "C:\Users\TRISTAN\FLYING FIH" --mode EditMode`, parse `test-results.xml`. Expected: all PASS.
- [ ] **Step 5: Commit**

Run: `git add Assets/Scripts/Timing/TimingPromptSpawner.cs Assets/Tests/EditMode/TimingSpawnerTests.cs; if ($?) { git commit -m "feat: spawner feeds gauge, beat-Miss drains (M3 task 3)" }`

### Task 4: SetTier down-clamp + spawn None

**Files:**
- Modify: `Assets/Scripts/Player/FlightStateMachine.cs` (down-clamp TargetSpeed in `SetTier`)
- Modify: `Assets/Tests/EditMode/StateMachineTests.cs` (down-clamp test, up-no-gift test)
- Modify: `Assets/Scripts/Debug/M1DebugInput.cs` (delete `SetTier(Medium)` line; keep 10/10 spawn speeds)

**Interfaces:**
- Consumes: `PlayerMomentumController.TargetSpeed` (settable property, exists), tier profiles (exist).
- Produces: `SetTier` after applying limits: `if (_momentum != null && _momentum.TargetSpeed > profile.MaxSpeed) _momentum.TargetSpeed = profile.MaxSpeed;` (down only; never raises).

- [ ] **Step 1: Write the failing tests** (append to `StateMachineTests.cs`, reuse `NewStateMachineWithLowAndHigh`: Low max 23, High max 47):
```csharp
[Test] TierDownClampsTarget — set High (Target 47 via direct set to 47), SetTier(Low) → TargetSpeed 23, ActiveTier Low.
[Test] TierUpNeverGiftsSpeed — set Low, TargetSpeed 10, SetTier(High) → TargetSpeed still 10, tier High.
```
- [ ] **Step 2: Run test to verify it fails**

Run: `unity test "C:\Users\TRISTAN\FLYING FIH" --mode EditMode --filter StateMachineTests`
Expected: FAIL — target stays 47 after down-shift.
- [ ] **Step 3: Write minimal implementation** — the two-line clamp in `SetTier` + delete the Medium line in `M1DebugInput.Start` (keep the 10/10 spawn block).
- [ ] **Step 4: Run FULL EditMode suite** (every momentum/state test touches these paths).

Run: `unity test "C:\Users\TRISTAN\FLYING FIH" --mode EditMode`. Expected: all PASS.
- [ ] **Step 5: Commit**

Run: `git add Assets/Scripts/Player/FlightStateMachine.cs Assets/Tests/EditMode/StateMachineTests.cs Assets/Scripts/Debug/M1DebugInput.cs; if ($?) { git commit -m "feat: tier down-clamp, spawn None (M3 task 4)" }`

### Task 5: Scene wiring (gauge GO, overlay line, spawner gauge) + rebuild

**Files:**
- Modify: `Assets/Editor/ProjectBootstrap/M1SceneBuilder.cs` (gauge GameObject under Timing, `Configure(sm, gaugeSettings, reactor)`, spawner `Configure` 7th arg, overlay `Configure` gauge arg)
- Modify: `Assets/Scripts/Debug/M1DebugOverlay.cs` (gauge ref + `Gauge {Current:F0} Tier {ActiveTier}` line)
- Rebuild scene headless via the `Build` method; commit rebuilt scene/prefabs/materials only.

**Interfaces:**
- Consumes: gauge `Configure`, spawner 7-arg `Configure`, overlay extended `Configure(momentum, sm, momSettings, movement, spawner, gauge)`.
- Produces: scene containing wired `Gauge` GameObject; smoke test below passes.

- [ ] **Step 1: Write the failing PlayMode smoke test** (append to `MovementSceneTests.cs`):
```csharp
[UnityTest]
public IEnumerator GaugeWiredInScene()
{
    var gauge = Object.FindFirstObjectByType<FlightGaugeSystem>();
    Assert.IsNotNull(gauge, "gauge not wired in scene");
    Assert.AreEqual(0f, gauge.CurrentGauge, 0.001f);
    Assert.AreEqual(FlightTier.None, gauge.StateMachine.ActiveTier);
    yield break;
}
```
(Needs a `StateMachine` getter on the gauge — add `public FlightStateMachine StateMachine => _sm;`, mirroring `PlayerMomentumController.Momentum` precedent. That getter is the RED: without it the test doesn't compile.)
- [ ] **Step 2: Run test to verify it fails** — PlayMode filter `GaugeWiredInScene`. Expected: FAIL (null gauge — old scene).
- [ ] **Step 3: Write minimal implementation** — builder edits + overlay line + gauge getter; rebuild scene headless (`unity run ... -- -executeMethod ProjectBootstrap.M1SceneBuilder.Build -logFile -`, exit 0 + "Scene + prefabs saved").
- [ ] **Step 4: Run test to verify it passes** — same filter. Expected: PASS.
- [ ] **Step 5: Commit** — verify `git status --short` shows only intended files (scene + prefabs + maybe material churn).

Run: `git add Assets/Scripts/Gauge/FlightGaugeSystem.cs Assets/Scripts/Debug/M1DebugOverlay.cs Assets/Editor/ProjectBootstrap/M1SceneBuilder.cs Assets/Scenes/M1_MovementProof.unity Assets/Prefabs/PlayerRoot.prefab Assets/Prefabs/CameraRig.prefab Assets/Tests/PlayMode/MovementSceneTests.cs Assets/Scripts/Gauge/FlightGaugeSystem.cs.meta; if ($?) { git commit -m "feat: gauge scene wiring + overlay (M3 task 5)" }`

### Task 6: Freeze gauge in movement-pinning tests + climb test

**Files:**
- Modify: `Assets/Tests/PlayMode/MovementSceneTests.cs` (freeze lines + new climb test)

**Interfaces:**
- Consumes: `FlightGaugeSystem.enabled` (MonoBehaviour flag, Update-driven gauge).
- Produces: every test below freezes the gauge right after its `SetTier` call: `Object.FindFirstObjectByType<FlightGaugeSystem>().enabled = false;` — MaxSpeedRun, SwimFlySwim, climb, slalom, cone-edge, AirTurns, glide-out, LongRun, PromptOpens, Perfect/Miss pins, breach pin, dial visibility, needle pitched, DialZones, ChargeBar visible, RingSwim, ChargeSteps, Pause (every test that calls `SetTier`, plus LongRun; SpawnStartsAtTen needs none — spawn state can't leave None in one frame).
- New climb test (gauge LIVE, mover frozen after teleport-free swim):
```csharp
[UnityTest]
public IEnumerator TwoCleanRingsReachLowGear()
{
    // Manual drive, honest stepping: teleport to ring 1, trigger via one
    // live frame... (mover frozen AFTER teleport like RingSwim Macht nichts —
    // spawner stays LIVE for trigger+charge start, then freeze spawner and
    // drive charge manually with the adaptive driver (copy the ~12-line
    // driver from ChargeHoldBanksViaMomentum; no shared test utils).
    // Repeat on ring 2. Assert gauge >= 20 and ActiveTier == Low.
    // Assert camera KickEnvelope == 1 immediately after the tier-up resolve.
}
```
(Write the full ~40 lines inline in the plan executor's implementation following the RingSwim + ChargeHold patterns exactly: teleport, 2 yields, spawner stays enabled for trigger, then `spawner.enabled = false`, manual `spawner.Tick` + sequencer driver, repeat for ring index 1. Speeds: Current/Target 10/10 spawn values. Jackpot math: 10 clean gains ≈ 2 rings × (3×2+4) = 20 → Low floor exactly; use `GreaterOrEqual(gauge, 20)`. Camera kick: `Object.FindFirstObjectByType<CameraSpeedReactor>().KickEnvelope` equals 1 within the same synchronous block after the resolving Tick.)

- [ ] **Step 1: Add freeze lines** (no new behavior — existing tests must stay green; this step has no RED).
- [ ] **Step 2: Write the climb test** — run it: expected PASS first run is fine and honest (pin test, record it); if red, debug (check `Logs/Editor.log` CS errors first).
- [ ] **Step 3: Run FULL PlayMode + EditMode suites** — all green.
- [ ] **Step 4: Commit**

Run: `git add Assets/Tests/PlayMode/MovementSceneTests.cs; if ($?) { git commit -m "test: freeze gauge in movement pins, ring climb test (M3 task 6)" }`

### Task 7: Human feel pass (closes M3)

No code. Human (user) plays 60–90s from spawn (gauge 0 / None / speed 10):
- [ ] Ring hunting readable, charge bar/tap pulse unchanged in value
- [ ] Every gear change felt (kick + speed-cap lift), tiers distinguishable
- [ ] Leak pressure fair, gauge line readable, camera never lost, FPS noted
Report back in plain words; tunables adjusted in a follow-up if needed (no new mechanics).

### M2 follow-up batch (ship + launch + arrow glyphs, approved 2026-09-30)

Implements the M2-spec ship/launch/arrow records above the M3 line:
ship swim (depth lock, pitch lock), gauge-full launch (rising edge),
arrow-glyph charge markers, 4-step sequences. TDD red→green→commit per
item, full suites green, spec records updated here as written.

### Task 8: Review + final suites

- [ ] Dispatch code reviewer subagent per `requesting-code-review` (range: first M3 commit..HEAD, requirements = this plan + spec + the plan's Review Focus verbatim + ledger `Ruling:` lines).
- [ ] Fix Critical immediately, Important before proceeding (each RED→GREEN + green suite), note Minor.
- [ ] FULL EditMode + PlayMode suites green, `git status` clean, push `origin main`, ledger entry.
