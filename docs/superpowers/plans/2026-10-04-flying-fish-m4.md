# Flying Fish M4 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Endless run — RunManager streams hybrid chunk content ahead of the fish with run-clock difficulty, missions, and per-type looks, proving a 60–90s playable run.

**Architecture:** Data-driven spawn director (no prefabs): `RunManager` (clock + seeded type deck + mission tracking) drives `ChunkBuilder` (pooled primitives + recycled water segments + mood), which feeds the existing spawner wholesale; spawner judging, gauge, and scoring math stay untouched.

**Tech Stack:** Unity 6000.6.3f1, C#, Unity Input System (untouched), UPM InputSystem 1.20.0 (untouched).

**Spec:** `docs/superpowers/specs/2026-10-04-flying-fish-m4-design.md` — the plan argues from the spec; executors read both.

## Global Constraints

- Engine Unity 6000.6.3f1; assemblies `FlyingFishMomentum.Runtime`, `FlyingFishMomentum.Tests.EditMode`, `FlyingFishMomentum.Tests.PlayMode`.
- PowerShell only (no `grep`/`&&` — `Select-String`, `; if ($?)`).
- Tests: `unity test "C:\Users\TRISTAN\FLYING FIH" --mode EditMode` and `--mode PlayMode`; parse `test-results.xml`; compiler errors via `Select-String -Pattern "error CS"` on `Logs/Editor.log`.
- Scene rebuild: `unity run "C:\Users\TRISTAN\FLYING FIH" -- -executeMethod ProjectBootstrap.M1SceneBuilder.Build -logFile -`.
- Never commit `Library/`, `Temp/`, `Logs/`, `obj/`, `*.csproj`, `*.sln`; DO commit `.meta` and assets.
- Commits `feat|test|chore: <what> (M4 task N)`; exactly one `in_progress` todo; push `origin main` only when the task says so.
- SO-only tuning, seeded RNG only, dt-scaled determinism; evaluation independent from presentation; events/interfaces over tight coupling.
- Spawner judging semantics (windows evaluator, charge order/gains, slow-mo, rockets, coins) never change in M4 — only its *inputs* (rings/coins lists, difficulty scalar).
- PRD §12.4 safety must-prevents are pinned by tests, not comments.
- TDD red → green → commit per behavior; tests stay minimal (one small failing test per behavior).

## Review Focus

- Fish outruns content at Max speed (spawn horizon too short) — a long-run test holds content ≥500m ahead at 73u/s (Task 3).
- Recycled rings re-trigger or stay consumed (stale `Consumed` flags) — pool reset pinned (Task 3).
- Same seed must replay identical chunk order + layouts (seed-stream discipline) — determinism test (Task 2).
- Difficulty t=1 must never zero/negative windows (MinGood precedent) — floor asserts (Task 4).
- Mission progress leaking across chunk boundaries — reset-on-entry test (Task 5).

---

### Task 1: ChunkSpec + DifficultySettings + MissionBonus SOs

**Files:**
- Create: `Assets/Scripts/Run/ChunkSpec.cs`, `Assets/Scripts/Run/DifficultySettings.cs`
- Modify: `Assets/Scripts/Scoring/ScoringSettings.cs` (add `MissionBonus = 100f`), `Assets/Scripts/Scoring/ScoreSystem.cs` (add `AddBonus`)
- Create assets (after compile, read each script guid from its `.cs.meta`, write YAML like `Assets/Configs/TimingSettings.asset`): `Assets/Configs/ChunkSpec_Lagoon.asset`, `ChunkSpec_Gauntlet.asset`, `ChunkSpec_Storm.asset`, `ChunkSpec_Sky.asset`, `Assets/Configs/DifficultySettings.asset`
- Test: `Assets/Tests/EditMode/RunManagerTests.cs` (new file; data-table tests live here)

**Interfaces:**
- Consumes: nothing (first task).
- Produces: `ChunkSpec` fields (`string ChunkId; float Length = 300f; int RingCount; float RingSpacing = 150f; float RingSwimFraction = 1f; int CoinTrails; int CoinsPerTrail = 4; int IslandPairs; float MinDifficulty; float MaxDifficulty = 1f; float Weight = 1f; string MissionText; int MissionTarget; Color SkyTint; Color FogColor; float FogDensity; Color WaterTint;`) — Task 3 reads these exact names.
- Produces: `DifficultySettings` fields (`float RampSeconds = 240f; float SpeedCreep = 0.5f; float WindowTighten = 0.3f; float GapShrink = 0.5f; float StormWeightEnd = 3f; float LagoonFloor = 0.5f;`) — Tasks 2/4 read these.
- Produces: `ScoreSystem.AddBonus(float baseAmount)` → `Score += baseAmount * Multiplier` — Task 5 calls it.

- [ ] **Step 1: Write the failing tests** in `Assets/Tests/EditMode/RunManagerTests.cs`:
```csharp
[Test]
public void ChunkSpecDefaultsAreSane()
{
    var s = ScriptableObject.CreateInstance<ChunkSpec>();
    Assert.Greater(s.Length, 0f);
    Assert.GreaterOrEqual(s.Weight, 0f);
    Assert.LessOrEqual(s.MinDifficulty, s.MaxDifficulty);
    Assert.Greater(s.MissionTarget, 0);
}
[Test]
public void MissionBonusPaysTimesMultiplier()
{
    var go = new GameObject("score");
    var score = go.AddComponent<Scoring.ScoreSystem>();
    score.Configure(null, ScriptableObject.CreateInstance<Scoring.ScoringSettings>());
    score.AddBonus(100f);
    Assert.AreEqual(100f, score.Score, 0.001f);
    Object.DestroyImmediate(go);
}
```
- [ ] **Step 2: Run test to verify it fails** — Run: `unity test "C:\Users\TRISTAN\FLYING FIH" --mode EditMode --filter "ChunkSpecDefaultsAreSane|MissionBonusPaysTimesMultiplier"` — Expected: FAIL (compile errors: missing types).
- [ ] **Step 3: Write `ChunkSpec.cs`, `DifficultySettings.cs`, add `MissionBonus = 100f` + `AddBonus`** (minimal, defaults as in Interfaces).
- [ ] **Step 4: Re-run** — Expected: PASS.
- [ ] **Step 5: Write the 5 asset YAMLs** (read each `.cs.meta` guid first; copy the `TimingSettings.asset` header shape; Lagoon/Gauntlet/Storm/Sky values: lengths 300; rings 3/6/2/2; swim fractions 1/1/1/0.3; coins 2/2/1/3 trails; islands 0/0/3/0; bands Lagoon 0-0.6, Gauntlet 0.1-1, Storm 0.35-1, Sky 0.2-1; weights 1/1/0.3/0.7; missions "collect 30 coins"/"2 clean rings"/"pass 4 rock gates"/"stay airborne 20s" targets 30/2/4/20; moods per spec S2).
- [ ] **Step 6: Commit** — Run: `git add Assets/Scripts/Run Assets/Scripts/Scoring Assets/Configs/ChunkSpec_*.asset* Assets/Configs/DifficultySettings.asset* Assets/Tests/EditMode/RunManagerTests.cs*; git commit -m "feat: M4 chunk spec and difficulty settings (M4 task 1)"` (no push yet).

### Task 2: RunManager clock + deck + Tick seam

**Files:**
- Create: `Assets/Scripts/Run/RunManager.cs`
- Test: `Assets/Tests/EditMode/RunManagerTests.cs` (append)

**Interfaces:**
- Consumes: Task 1 (`ChunkSpec`, `DifficultySettings`).
- Produces: `RunManager.Configure(TimingPromptSpawner spawner, ScoreSystem score, DifficultySettings diff, List<ChunkSpec> deck)`; `RunManager.SetSeed(int)`; `RunManager.Tick(float playerZ, float dt)` (pure-ish seam like spawner.Tick — no scene needed); `float DifficultyT { get; }`; `ChunkSpec NextType()` (deck pick for current t) — Tasks 3/5 call `Tick`/`NextType`.

- [ ] **Step 1: Write the failing tests** (append):
```csharp
[Test]
public void DeckReplaysPerSeed()
{
    var a = NewManager(7); var b = NewManager(7);
    Assert.AreEqual(a.NextType().ChunkId, b.NextType().ChunkId);
}
[Test]
public void DifficultyCapsAtOne()
{
    var m = NewManager(1);
    m.Tick(0f, 9999f);
    Assert.AreEqual(1f, m.DifficultyT, 0.001f);
}
```
(`NewManager` helper builds RunManager + 4 specs via CreateInstance + Configure with nulls; destroy `manager` GameObjects in TearDown.)
- [ ] **Step 2: Run test to verify it fails** — same `unity test --mode EditMode --filter` pattern — Expected: FAIL (missing type).
- [ ] **Step 3: Implement** `RunManager`: `DifficultyT = Clamp01(elapsed / RampSeconds)`; deck = weighted pick among specs whose `[MinDifficulty, MaxDifficulty]` contains t using a dedicated `System.Random` stream (never shares beat/charge streams); Storm weight scales toward `StormWeightEnd` with t; Lagoon keeps `LagoonFloor` weight; if roll misses everything, return Lagoon.
- [ ] **Step 4: Re-run** — Expected: PASS.
- [ ] **Step 5: Commit** — Run: `git add Assets/Scripts/Run/RunManager.cs* Assets/Tests/EditMode/RunManagerTests.cs*; git commit -m "feat: M4 run clock and type deck (M4 task 2)"` (no push yet).

### Task 3: ChunkBuilder spawn/reclaim + pools + water recycle

**Files:**
- Create: `Assets/Scripts/Run/ChunkBuilder.cs`
- Modify: `Assets/Scripts/Timing/ChargeRing.cs` (add `Reset()` clearing `Consumed` + re-enabling renderers — pooled rings must reset)
- Test: `Assets/Tests/EditMode/RunManagerTests.cs` (builder data tests) + PlayMode long-run test in `Assets/Tests/PlayMode/MovementSceneTests.cs`

**Interfaces:**
- Consumes: Task 1 (`ChunkSpec` exact field names), Task 2 (`RunManager.Tick` drives builder — builder exposes `BuildChunk(ChunkSpec spec, float zStart, int seed)` and `ReclaimBefore(float z)`; water `RecycleSegments(float fishZ)`).
- Produces: live `List<ChargeRing>`/`List<CoinPickup>` per chunk + segment layout — Tasks 4/7 consume the lists.

- [ ] **Step 1: Write the failing tests** (EditMode: build Lagoon spec at z=0 → ring/coin counts match spec, z within `[zStart, zStart+Length]`, swim fraction honored; reclaim removes past-line content; `ChargeRing.Reset()` clears consumed. PlayMode `EndlessContentStaysAhead`: fly Max speed 120s sim-time, assert newest content z minus fish z ≥ 500 throughout. Safety (PRD §12.4, Storm gates): island gate gaps ≥ 20m (M1 slalom precedent — steerable at top speed); ring z-spacing ≥ 100m so a beat gap always fits between rings at band speed; prompts never spawn inside island volumes).
- [ ] **Step 2: Run test to verify it fails** — Expected: FAIL (missing types).
- [ ] **Step 3: Implement** `ChunkBuilder` (pooled `GameObject` roots per chunk; rings via the same LineRenderer pattern as `M1SceneBuilder.AddChargeRing`; coins via the `AddCoin` pattern; islands as scaled cubes with colliders; 4× water + 4× seabed 1000m segments repositioned ahead when behind; pooled rings/coins reset on reuse via `Reset()`/`Collect` state).
- [ ] **Step 4: Re-run** — Expected: PASS.
- [ ] **Step 5: Commit** — Run: `git add Assets/Scripts/Run/ChunkBuilder.cs* Assets/Scripts/Timing/ChargeRing.cs Assets/Tests/EditMode/RunManagerTests.cs Assets/Tests/PlayMode/MovementSceneTests.cs; git commit -m "feat: M4 chunk builder with pools and water recycle (M4 task 3)"` (no push yet).

### Task 4: Spawner feed + difficulty scalar

**Files:**
- Modify: `Assets/Scripts/Timing/TimingPromptSpawner.cs` (add `SetDifficulty(float)`, private runtime `_view` clone of `_timing`, route ALL internal timing reads through `_view`; add `EffectiveSettings` getter), `Assets/Scripts/Timing/TimingPromptDial.cs` (read windows from `spawner.EffectiveSettings`, not `Settings`)
- Test: `Assets/Tests/EditMode/TimingSpawnerTests.cs` (append)

**Interfaces:**
- Consumes: Task 2 (`DifficultyT` value fed in per frame by RunManager — wiring lands in Task 7; this task only adds the receiver).
- Produces: `SetDifficulty(float t)`; `TimingSettings EffectiveSettings { get; }`; unchanged judging semantics at t=0.

- [ ] **Step 1: Write the failing tests** (append):
```csharp
[Test]
public void DifficultyTightensWindowsAndGap()
{
    var a = NewSpawner(out _); // t=0 default
    var b = NewSpawner(out _); b.SetDifficulty(1f);
    Assert.Less(b.EffectiveSettings.GoodWindow, a.EffectiveSettings.GoodWindow);
    Assert.GreaterOrEqual(b.EffectiveSettings.GoodWindow, 0.01f); // never zero (MinGood precedent)
    Assert.Less(b.EffectiveSettings.BeatMaxMeters, a.EffectiveSettings.BeatMaxMeters);
}
[Test]
public void ZeroDifficultyMatchesLegacyBehavior()
{
    // One perfect press banks identically with and without SetDifficulty(0).
    var a = NewSpawner(out var ma); ma.CurrentSpeed = 50f; ma.TargetSpeed = 50f;
    var b = NewSpawner(out var mb); mb.CurrentSpeed = 50f; mb.TargetSpeed = 50f;
    b.SetDifficulty(0f);
    // open a prompt on each (16 travel ticks) and tap TargetTime exactly
    // ... assert equal CurrentSpeed and equal LastResult
}
```
- [ ] **Step 2: Run test to verify it fails** — Expected: FAIL (missing members).
- [ ] **Step 3: Implement** (`_view = Object.Instantiate(_timing)` in `Configure`; `SetDifficulty` rescales `BeatMin/MaxMeters ×(1−0.5t)`, `PerfectWindow/GoodWindow ×(1−0.3t)` with ≥0.01f floors; route every `_timing` read in spawner + dial through `_view`; `Settings` keeps returning the original asset).
- [ ] **Step 4: Re-run** — Expected: PASS (existing `DialZonesFollowStreak` must stay green — it runs at t=0).
- [ ] **Step 5: Commit** — Run: `git add Assets/Scripts/Timing/TimingPromptSpawner.cs Assets/Scripts/Timing/TimingPromptDial.cs Assets/Tests/EditMode/TimingSpawnerTests.cs; git commit -m "feat: M4 spawner difficulty scalar (M4 task 4)"` (no push yet).

### Task 5: Missions (tracking + bonus + overlay line)

**Files:**
- Modify: `Assets/Scripts/Run/RunManager.cs` (mission state), `Assets/Scripts/Debug/M1DebugOverlay.cs` (mission line; `Configure` gains `RunManager` — builder-only caller, safe like the score param before it)
- Test: `Assets/Tests/EditMode/RunManagerTests.cs` (mission unit tests via `Tick` + stub score/sm doubles or real components)

**Interfaces:**
- Consumes: Task 2 (`RunManager.Tick`, score via `AddBonus`), spawner public state (`Charge.StepIndex/StepCount`, `ChargeActive`), sm `Locomotion`, score `Coins`.
- Produces: `string MissionText { get; }`, `float MissionProgress01 { get; }` — Task 7 wires the overlay.

- [ ] **Step 1: Write the failing tests** (per mission kind, driven through `RunManager.Tick`: coins-delta completes collect missions; charge full-completion transitions (active→inactive with `StepIndex >= StepCount`) complete ring missions; Flying-seconds accumulate airtime; gate z-crossings complete gate missions; completion calls `AddBonus`; new chunk entry resets progress).
- [ ] **Step 2: Run test to verify it fails** — Expected: FAIL.
- [ ] **Step 3: Implement** (poll deltas in `Tick`; bonus = `MissionBonus × Multiplier` via `score.AddBonus`; overlay line `Mission <text> <pct>`).
- [ ] **Step 4: Re-run** — Expected: PASS.
- [ ] **Step 5: Commit** — Run: `git add Assets/Scripts/Run/RunManager.cs* Assets/Scripts/Debug/M1DebugOverlay.cs Assets/Tests/EditMode/RunManagerTests.cs; git commit -m "feat: M4 chunk missions (M4 task 5)"` (no push yet).

### Task 6: Mood (sky/fog/water/light per chunk type)

**Files:**
- Modify: `Assets/Scripts/Run/ChunkBuilder.cs` (apply mood on chunk entry), `Assets/Editor/ProjectBootstrap/M1SceneBuilder.cs` (create shared mood materials: sky tint, fog setup, water tint instances)
- Test: EditMode mood-apply test in `Assets/Tests/EditMode/RunManagerTests.cs` (colors/fog set from spec); PlayMode untouched (visual only)

**Interfaces:**
- Consumes: Task 1 mood fields, Task 3 builder.
- Produces: mood applied on `BuildChunk` of a new type (instant switch; smoothing explicitly deferred).

- [ ] **Step 1: Write the failing test** (build Storm spec → `RenderSettings.fogColor`/`fogDensity` + water material color match spec).
- [ ] **Step 2: Run test to verify it fails** — Expected: FAIL.
- [ ] **Step 3: Implement** (per-type `SkyTint/FogColor/FogDensity/WaterTint` applied to shared instances; builder creates materials once).
- [ ] **Step 4: Re-run** — Expected: PASS.
- [ ] **Step 5: Commit** — Run: `git add Assets/Scripts/Run/ChunkBuilder.cs* Assets/Editor/ProjectBootstrap/M1SceneBuilder.cs Assets/Tests/EditMode/RunManagerTests.cs; git commit -m "feat: M4 per-chunk mood (M4 task 6)"` (no push yet).

### Task 7: Starter + scene rebuild + tune lock + speed creep

**Files:**
- Modify: `Assets/Editor/ProjectBootstrap/M1SceneBuilder.cs` (starter Lagoon×2 deterministic content z 0..600; KEEP baked slalom islands z<120; water as 4+4 recycled 1000m segments; wire RunManager + builder + spawner feed + overlay mission line; remove baked ring/coin loops — RunManager owns all content past spawn), `Assets/Scripts/Run/RunManager.cs` (speed creep: `TargetSpeed += SpeedCreep*dt` capped at `SpeedCap`, only while playing), `Assets/Configs/FlightGaugeSettings.asset` (tank 120, bands 20/40/70/100 — keep drain 1.5, MissDrain 3), `Assets/Configs/TimingSettings.asset` (slow-mo 0.4/1.2 — keep flat gains, timeout 8)
- Modify tests: `FlightGaugeSettingsTests` asset pins (120/bands), `TwoCleanRingsReachLowGear` → `FourCleanRingsReachLow` (4×6=24 ≥ 20), DELETE `RunwayFilledWithRings` (replaced by streaming tests), `Launch*` tests (AddFill 120 still full — unchanged), keep `SoarHold/DiveBuilds/CameraDistance` (speeds only — unchanged)
- Rebuild scene headless; full suites green

**Interfaces:**
- Consumes: Tasks 1–6.
- Produces: playable endless scene; tune-locked assets.

**Open decision (flagged for plan review):** flat gains (6/ring) × reverted 120-tank = ~20 rings to launch — unplayable for feel-testing. Recommendation: KEEP the 48 testing tank (amend spec S4 at review) until M6 tuning. If rejected, launch pacing becomes its own task.

- [ ] **Step 1: Update asset-pin tests to the lock values** (run → FAIL on current 48-tank asset).
- [ ] **Step 2: Write streaming tests**: `ChunksStreamEndlessly` (PlayMode: travel 3000m+, content always ahead, reclaim behind, no exceptions), starter determinism (same seed → same starter).
- [ ] **Step 3: Implement builder/starter/feed/creep/reverts.**
- [ ] **Step 4: Rebuild scene** (`unity run ... -executeMethod ...Build`), run FULL EditMode + PlayMode suites, expect green.
- [ ] **Step 5: Commit** `feat: M4 starter, streaming feed, tune lock (M4 task 7)` (+ push `origin main`).

### Task 8: Review + final suites + ledger

- [ ] **Step 1: Dispatch code review** per `requesting-code-review` (range: first M4 commit..HEAD; requirements = this plan + M4 spec + ledger rulings).
- [ ] **Step 2: Fix Critical immediately, Important before proceeding** (each RED→GREEN + green suite).
- [ ] **Step 3: FULL EditMode + PlayMode suites green**, `git status` clean, push `origin main`, ledger entry.
- [ ] **Step 4: Commit** `chore: M4 review fixes (M4 task 8)` as needed.
