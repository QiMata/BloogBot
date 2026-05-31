# AGENTS.md - Westworld of Warcraft (WWoW) Agent Playbook

This file is the operational guide for coding agents in this repository.

## Entry point: [docs/SPEC.md](docs/SPEC.md)

`docs/SPEC.md` is the single entry point for all autonomous work. It links
to Spec contracts (`docs/Spec/`), the phased Plan (`docs/Plan/`), the
per-activity implementation slots (`docs/Plan/Activities/`), and the
rolling task board ([docs/TASKS.md](docs/TASKS.md)).

The decisions of record from the 2026-05-11 design session are in
[`docs/SPEC.md#decisions-of-record`](docs/SPEC.md#decisions-of-record).

## Monorepo Shared Contract

- Also follow the root monorepo rules in [../AGENTS.md](../AGENTS.md) and [../CLAUDE.md](../CLAUDE.md).
- Runtime StateManager/BotRunner traffic is protobuf/TCP with length framing.
- ActivitySnapshot should carry major state deltas, not full enemy/object payloads.
- FG work must be state-gated and should not steal focus or capture the cursor.
- BG work must be validated against FG packet/event recordings when parity matters.
- Live tests must poll StateManager APIs, fail fast on disconnect/crash, and capture latest screenshots/state dumps.
- See [../docs/TEST_PATTERNS.md](../docs/TEST_PATTERNS.md), [../docs/TEST_SCREENSHOTS.md](../docs/TEST_SCREENSHOTS.md), and [../docs/SKILL_DEVELOPMENT_PLAN.md](../docs/SKILL_DEVELOPMENT_PLAN.md).

## 1. Project Snapshot

- Project names in repo/docs may use both `Westworld of Warcraft`, `WWoW`, and legacy `BloogBot`.
- Tech stack is mixed:
- C#/.NET 8 services, libraries, WPF UI, and tests.
- Native C++ components for loader, fast calls, and navigation/physics.
- Primary runtime modes:
- `ForegroundBotRunner`: injected into `WoW.exe` (in-process memory + Lua).
- `BackgroundBotRunner`: headless protocol-driven bot (no client rendering).
- Supported WoW clients:
- Vanilla `1.12.1`
- TBC `2.4.3`
- WotLK `3.3.5a`

## 2. First-Minute Checklist

- **Before any broad or cross-layer change**, read the practical hubs:
  [docs/architecture.md](docs/architecture.md) (where code lives + dependency
  rules), [docs/testing.md](docs/testing.md), [docs/security.md](docs/security.md)
  (process-safety + SOAP-only guardrails), and [docs/troubleshooting.md](docs/troubleshooting.md).
  These link to the canonical `docs/Spec/` contracts.
- Read [docs/TASKS.md](docs/TASKS.md) before starting implementation work that is task-tracked.
- **If the work touches pathfinding** (`Services/PathfindingService`, `Exports/Navigation`, `Exports/BotRunner` movement/transport code, `Tests/PathfindingService.Tests`, `tools/NavDataAudit`): read [docs/physics/README.md](docs/physics/README.md) **before editing**. The stack is in a 2026-05-06 architectural freeze; mesh fixes go in [tools/MmapGen/](tools/MmapGen/) instead of new managed repair logic.
- Identify the relevant local `TASKS.md` and `TASKS_ARCHIVE.md` in the subsystem you touch.
- Preserve existing uncommitted work; do not revert unrelated changes.
- Choose smallest-scope validation commands that prove your change.
- Use repo-scoped cleanup commands only (see Process Safety).

## 3. Architecture Boundaries (Do Not Violate)

Dependency flow is strict:

`GameData.Core -> BotCommLayer -> BotRunner -> WoWSharpClient -> Services -> UI`

Rules:

- Keep interfaces/contracts in lower layers, implementations in higher layers.
- Do not add upward dependencies (for example, Exports project depending on Services/UI).
- If you must alter cross-layer contracts, update all impacted consumers and tests in one change.
- Static world collision belongs in generated navigation data. Do not hardcode
  route-specific blocker coordinates, clearance cylinders, detour waypoints, or
  live-position guards in production pathfinding/BotRunner code to make a route
  pass. If a generated route clips a bonfire, tree, corner, support, or other
  static gameobject, fix the GO-aware mmap generation/data and keep the offline
  route gate red until the regenerated mmaps avoid it naturally.

## 4. Repository Map

Core areas:

- `Exports/GameData.Core`: domain interfaces and shared contracts.
- `Exports/BotCommLayer`: protobuf IPC models and transport.
- `Exports/BotRunner`: orchestration, task logic, behavior flow.
- `Exports/WoWSharpClient`: WoW protocol client, packets, networking.
- `Exports/Navigation`: C++ physics/navigation engine.
- `Exports/Loader`: C++ injection/bootstrap.
- `Services/*`: runnable workers (`ForegroundBotRunner`, `BackgroundBotRunner`, `PathfindingService`, `WoWStateManager`, `DecisionEngineService`, `PromptHandlingService`).
- `UI/WoWStateManagerUI`: WPF desktop UI.
- `UI/Systems/*`: Aspire/service defaults orchestration.
- `Tests/*`: unit/integration/regression suites.
- `BotProfiles/*`: class/spec behavior profiles.

### Path-specific instructions

Targeted, area-scoped rules live in `.github/instructions/*.instructions.md`,
each applied by an `applyTo:` glob when you edit a matching file (and surfaced to
`applyTo`-aware agents such as GitHub Copilot). They hold conventions +
validation commands + do-not-edit rules per area without bloating this file. See
[`.github/instructions/README.md`](.github/instructions/README.md):

- `shared-libraries` (`Exports/*` C# libs) · `services` (`Services/**`) ·
  `native` (C++ + `*.vcxproj`) · `bot-profiles` (`BotProfiles/**`) ·
  `tests` (`Tests/**`) · `ui` (`UI/**`) · `protobuf` (`.proto` + generated
  `*.cs`) · `config` (`Config/**/*.json`) · `docs` (`docs/**/*.md`, task trackers).

Per-directory `CLAUDE.md` files cover component context (what each project is and
depends on); the instruction files cover rules-by-file-type. Neither restates
this playbook.

## 5. Canonical Build and Test Commands

### Stable script interface (preferred)

**Use `scripts/` instead of guessing stack-specific commands.** These are thin,
strict wrappers over the commands below; they print progress, exit non-zero on
failure, and run from anywhere in the tree. Full reference:
[`scripts/README.md`](scripts/README.md) and
[`docs/local-development.md`](docs/local-development.md).

```powershell
.\scripts\bootstrap.ps1          # verify .NET 8 SDK + dotnet restore
.\scripts\build.ps1              # dotnet build the solution (Debug); -Configuration Release; -Native for C++
.\scripts\format.ps1             # dotnet format (mutates files)
.\scripts\lint.ps1               # dotnet format --verify-no-changes (read-only)
.\scripts\test-fast.ps1          # unit tests only (Layer 3, no server)
.\scripts\test.ps1               # full layered suite (all layers)
.\scripts\test-integration.ps1   # live integration tests (Layer 4; needs MaNGOS stack)
.\scripts\check.ps1              # pre-PR gate: lint (advisory) -> build -> fast tests
.\scripts\clean.ps1              # remove build artifacts (bin/obj, Bot/<config>, ...)
```

From bash/git-bash the same names work via extensionless shims: `./scripts/build`,
`./scripts/test-fast`, etc. The raw commands these wrap are documented below.

### Build .NET solution

```powershell
dotnet build WestworldOfWarcraft.sln --configuration Debug
```

### Run layered tests (preferred)

```powershell
.\run-tests.ps1
```

Useful variants:

```powershell
.\run-tests.ps1 -Layer 1
.\run-tests.ps1 -Layer 2
.\run-tests.ps1 -Layer 3
.\run-tests.ps1 -Layer 4
.\run-tests.ps1 -SkipBuild
.\run-tests.ps1 -TestTimeoutMinutes 10
```

Layer map from `run-tests.ps1`:

- Layer 1: native DLL availability checks.
- Layer 2: navigation physics + pathfinding tests.
- Layer 3: WoWSharpClient/BotRunner unit tests (+ PromptHandlingService tests when present).
- Layer 4: integration tests.

### Targeted tests

```powershell
dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --settings Tests/BotRunner.Tests/test.runsettings
dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings
```

### Native C++ builds (when needed)

```powershell
$MSBUILD = "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe"
& $MSBUILD Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal
& $MSBUILD Exports/Loader/Loader.vcxproj -p:Configuration=Release -p:Platform=x86 -p:PlatformToolset=v145 -v:minimal
& $MSBUILD Exports/FastCall/FastCall.vcxproj -p:Configuration=Release -p:Platform=x86 -p:PlatformToolset=v145 -v:minimal
```

Notes:

- `Directory.Build.props` centralizes non-x64 output to `Bot/<Configuration>/net8.0/`.
- `Directory.Build.targets` overrides x64 output to `Bot/<Configuration>/x64/`.

## 6. Process Safety (Critical)

Never kill all processes by image name.

Forbidden patterns:

- `taskkill /F /IM dotnet.exe`
- `Stop-Process -Name dotnet`
- `taskkill /F /IM Game.exe`
- any equivalent blanket process kill

Required approach:

- Kill only explicit PIDs you started.
- Use repo-scoped helpers in `run-tests.ps1`:

```powershell
.\run-tests.ps1 -ListRepoScopedProcesses
.\run-tests.ps1 -CleanupRepoScopedOnly
```

Pre-build reminder:

- If `WoW.exe` locks binaries, identify PID first and kill only that PID.

## 7. MaNGOS Data Policy

All mutable server operations must use SOAP, not direct MySQL writes.

- SOAP endpoint: `http://127.0.0.1:7878/`
- Credentials: `ADMINISTRATOR:PASSWORD`
- Preferred test helpers:
- `ExecuteGMCommandAsync(...)`
- `SendGmChatCommandAsync(...)`

Allowed DB access:

- Read-only queries for verification/fixtures.
- One bootstrap exception to enable GM commands may write as documented in tests.

Important behavior:

- Online character state is authoritative in memory; DB may be stale until save/logout.
- Do not trust direct DB reads for live state assertions.

## 8. Task Tracking and Handoff

Task tracking is mandatory and hierarchical:

- Master: `docs/TASKS.md`
- Local execution files: `*/TASKS.md`
- Completed work archives: `*/TASKS_ARCHIVE.md`

When you complete or partially complete work:

- Update `docs/TASKS.md` handoff block.
- Update impacted local `TASKS.md` handoff block.
- Move completed items into the matching `TASKS_ARCHIVE.md` and renumber remaining open items.

Minimum handoff content:

- What was completed.
- Exact commands run + outcomes.
- Evidence references (logs/snapshots/responses) when relevant.
- Files changed.
- Exact next command to run.

## 9. Documentation Update Rules

Before asking for clarification, check existing docs under `docs/`.

Update docs when you add or change:

- executable commands/scripts,
- service behavior or contract flow,
- dependencies/tools,
- protocol/schema definitions,
- task workflow rules.

High-value references:

- `docs/architecture.md` (practical orientation) and `docs/Spec/01_ARCHITECTURE.md` (formal contract)
- `docs/IPC_COMMUNICATION.md` / `docs/api-contracts.md`
- `docs/TECHNICAL_NOTES.md`
- `docs/DEVELOPMENT_GUIDE.md` / `docs/local-development.md`
- `docs/testing.md`, `docs/security.md`, `docs/troubleshooting.md`
- `docs/physics/README.md`
- `docs/server-protocol/*`

## 10. Symptom-to-Code Navigation

- Bot does not move/path correctly:
- `Services/PathfindingService`
- `Exports/Navigation/PathFinder.cpp`
- Physics oddities (falling/sliding/clipping):
- `Exports/Navigation/PhysicsEngine.cpp`
- `Exports/Navigation/PhysicsCollideSlide.cpp`
- Login/connection/protocol failures:
- `Exports/WoWSharpClient/Client`
- `Exports/WoWSharpClient/Networking`
- State machine stuck:
- `Services/WoWStateManager/StateManagerWorker.cs`
- IPC/service comm issues:
- `Exports/BotCommLayer/*`
- DLL injection failures:
- `Exports/Loader/dllmain.cpp`
- `Exports/Loader/simple_loader.cpp`
- Wrong combat rotation/spells:
- `BotProfiles/<ClassSpec>/`

## 11. Proto and Generated Code

If you modify `.proto` files in `Exports/BotCommLayer/Models/ProtoDef`:

- Regenerate C# outputs with `protocsharp.bat`.
- Verify generated files compile and tests pass.
- Keep generated code and source `.proto` changes in the same commit.

## 12. Large-File and Search Discipline

- Prefer targeted search (`rg`) over broad scanning.
- Read large files in chunks.
- For C++ physics/protocol-heavy areas, locate symbol first, then open surrounding lines.

## 13. Accuracy Notes for This Repo

- The `scripts/` interface (see §5 and [`scripts/README.md`](scripts/README.md)) now
  exists and is the preferred entry point for build/test/lint/check. Earlier docs that
  referenced `scripts/build.ps1` before it existed are now valid.
- Treat actual files in the repository as source of truth when docs and filesystem diverge.
- Prefer `scripts/`, `run-tests.ps1`, solution/project files, and current task docs over outdated historical instructions.

## 14. Done Criteria for Agent Changes

Before handing off:

- Build/test scope matches changed components.
- No architecture boundary violations introduced.
- Task trackers updated (`docs/TASKS.md` + local `TASKS.md`/`TASKS_ARCHIVE.md` as needed).
- Process cleanup done repo-scoped only.
- Next command for follow-up work is explicit.

## 15. Calibration Anti-Loop Rule (Mandatory)

For iterative tuning work (especially PhysicsEngine replay calibration), do this before editing code:

1. Read the latest calibration handoff doc for that subsystem (for PhysicsEngine: `docs/physicsengine-calibration.md`).
2. Review recent run logs in `logs/` and identify:
   - best known metrics,
   - latest regression,
   - explicit "Do Not Repeat" hypotheses.
3. Confirm the next planned tweak is new and single-scope.

Additional required behavior:

- One behavioral code change per calibration run.
- Append every run outcome to the calibration doc immediately after running.
- If a tweak regresses metrics, record it under "Do Not Repeat" before trying anything else.
