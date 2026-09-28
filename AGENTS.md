# AGENTS.md — Flying Fish Momentum (Unity)

**Engine:** Unity 6000.6.3f1 (installed 2026-09-27 via Hub; accepted in place of
6000.3.25f1 LTS per ledger ruling — M1 uses stable APIs only).
**PRD (source of truth):** `c:\Users\TRISTAN\Downloads\FlyingFishMomentum_Master_PRD_v3.0.md`
**Approved spec:** `docs/superpowers/specs/2026-09-27-flying-fish-m1-design.md`
**Current scope:** Milestone 1 — Movement Proof (PRD §29). Timing, gauge,
tiers-as-progression, scoring, menus, audio, VFX, and RunManager are NOT M1.

## Required skills — invoke BEFORE acting whenever they apply

| Skill | When (mandatory) |
|-------|------------------|
| `brainstorming` | Before any new feature, subsystem, or behavior change. Classify (spike/bounded/architectural), get approval before code. |
| `test-driven-development` | Before writing ANY implementation code. Red → green → commit, no exceptions. |
| `systematic-debugging` | On ANY bug or test failure, before proposing a fix. |
| `verification-before-completion` | Before claiming ANY task done. Run its checks, show output. |
| `requesting-code-review` | After each task group, before moving to the next. |
| `writing-plans` / `executing-plans` / `subagent-driven-development` | Plans live in `docs/superpowers/plans/`; implement task-by-task with checkboxes. |
| `using-git-worktrees` | When feature work needs isolation from the working tree. |
| `unity-cli` (global: `~/.config/opencode/skills/unity-cli`, plus `unity skill show`) | For Unity CLI commands, Editor management, and Unity API usage. |
| `ponytail` (global) | On EVERY coding task: YAGNI ladder — reuse existing > Unity API/stdlib > one-liner > minimal new code. No unrequested abstractions, no scaffold "for later". Governs scope, NOT rigor: it never overrides TDD or PRD rules below. |
| `unity-csharp-scripting` | Before writing MonoBehaviours: lifecycle (Awake/OnEnable/Start/Update/LateUpdate), serialization rules, coroutines. |
| `unity-input-system` | Before touching input: actions, maps, bindings, PlayerInput, polling vs callbacks. |
| `unity-physics` | Before colliders/triggers/layers/raycasts. CharacterController context: skin width, slide on contact, swept move (no tunneling). |
| `unity-scriptableobjects` | Before config/data work: SO config assets, event channels (`OnTierChanged` style), runtime sets. No magic numbers outside SOs. |
| `unity-package-management` | Before adding/upgrading UPM packages. Headless-safe installs only. |
| `frontend-design` | M5 only: main menu, HUD, results screen UI work. Not before. |

Unity MCP (`unity mcp`) is configured globally in opencode, but requires a running
Editor with the Pipeline package — M1 uses the Unity CLI only.

## PRD agent rules (§34) — always binding

1. Preserve system boundaries unless a change is necessary.
2. Keep gameplay parameters configurable — ScriptableObjects only, no magic numbers.
3. Prefer deterministic gameplay logic (dt-scaled, no frame-rate dependence, no `Random` in gameplay).
4. Keep timing evaluation independent from visual presentation (M2+).
5. Keep UI independent from core gameplay state.
6. Events/interfaces over tight coupling.
7. No new gameplay mechanics without documenting them (update PRD/spec).
8. Debug visibility for gameplay-critical systems (overlay fields per §24.1).
9. Update PRD/spec on intentional divergence, using the §34.1 change protocol.
10. Never silently change gameplay rules.

## Long-session rules (against drift over many turns)

- Re-read this file + the approved spec after every context compaction, before continuing.
- Exactly one `in_progress` todo at a time; mark `completed` immediately, with evidence.
- Missing skill? Install globally, then record it in the table above:
  `npx -y skills add <owner/repo> --skill <name> --agent opencode --global --yes`,
  then mirror the folder to `~/.config/opencode/skills/` and `~/.claude/skills/`.
- Unity operations via the `unity` CLI only (editors, projects, auth, logs, builds, tests).
- Never commit `Library/`, `Temp/`, `Logs/`, `obj/`, `*.csproj`, `*.sln`.
- Verify with real runs (EditMode/PlayMode batchmode tests, `unity doctor` when the
  environment is suspect). No "should work" claims — output of the check, or it didn't happen.
- Hidden complexity discovered mid-task upgrades the approach: stop, say so, re-plan.
  Never silently expand scope.
- `ponytail` vs TDD: ponytail forbids speculative code, never tests. Gameplay logic is
  always red → green → commit; the tests themselves stay minimal (one small failing
  test per behavior).

## Conventions

- **Layout:** repo root IS the Unity project root (`Assets/`, `Packages/`, `ProjectSettings/`).
- **Assemblies/namespaces:** `FlyingFishMomentum.Runtime`,
  `FlyingFishMomentum.Tests.EditMode`, `FlyingFishMomentum.Tests.PlayMode`.
- **Tests:** `Assets/Tests/EditMode|PlayMode/*.cs`. Real asserts only, no placeholder tests.
- **Scaffold:** temporary code marked `[M1-SCAFFOLD]`, tracked for removal (M5). No other throwaway code.
- **Commits:** `feat|test|chore: <what> (M1 task N)`.
- **Input:** Unity Input System asset at `Assets/Input/PlayerInputActions.inputactions`.
  M1 actions only: `Gameplay/Move`, `Gameplay/Pause`. `TimingAction` is M2 (YAGNI).
