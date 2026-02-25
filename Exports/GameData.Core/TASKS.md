# GameData.Core Tasks

## Scope
- Project: `Exports/GameData.Core`
- Owns shared enums/interfaces/models consumed by FG, BG, BotRunner, and snapshot layers.
- This file tracks direct implementation tasks bound to concrete files/symbols.
- Master tracker: `MASTER-SUB-005`.

## Execution Rules
1. Work only the top unchecked task unless blocked.
2. Keep corpse lifecycle contract aligned to canonical flow: `.tele name {NAME} Orgrimmar` -> kill -> release -> runback -> reclaim-ready -> resurrect.
3. Keep validation commands simple and one-line, with `--no-restore` where possible.
4. Record `Last delta` and `Next command` in `Session Handoff` every pass.
5. Move completed tasks to `Exports/GameData.Core/TASKS_ARCHIVE.md` in the same session.
6. Loop-break guard: if two consecutive passes produce no file delta, log blocker + exact next command and move to next queue file.
7. `Session Handoff` must include `Pass result` (`delta shipped` or `blocked`) and exactly one executable `Next command`.
8. Resume-first guard: start each pass by running the prior `Session Handoff -> Next command` verbatim before new scans.
9. After shipping one local delta, set `Session Handoff -> Next command` to the next queue-file read command and execute it in the same pass.

## Environment Checklist
- [x] `Exports/GameData.Core/GameData.Core.csproj` builds in `Release`.
- [x] Corpse lifecycle consumer checks are discoverable from `Tests/BotRunner.Tests`.
- [ ] No stale `FIXME`/ambiguous death-state comments remain in contract files.

## Evidence Snapshot (2026-02-25)
- `dotnet build Exports/GameData.Core/GameData.Core.csproj --configuration Release --no-restore` passes.
- `Exports/GameData.Core/Enums/DeathState.cs` still contains an ambiguity marker/FIXME at `DeathState.DEAD` for player `CORPSE/DEAD` switching semantics.
- Corpse lifecycle interface contract fields are present for normalization:
  - `IWoWLocalPlayer`: `CorpsePosition`, `InGhostForm`, `CanResurrect`, `CorpseRecoveryDelaySeconds`.
  - `IWoWCorpse`: `OwnerGuid`, `Type`, `CorpseFlags`.
  - `IObjectManager`: `RetrieveCorpse()`.
- BotRunner corpse lifecycle test set is discoverable via list-tests filter:
  - `ReleaseCorpseTaskTests`
  - `RetrieveCorpseTaskTests`
  - `DeathCorpseRunTests`
- Snapshot interface drift evidence:
  - `IActivitySnapshot` currently exposes only `Timestamp` and `AccountName`.
  - `IWoWActivitySnapshot` adds `CharacterName` and `ScreenState`.

## P0 Active Tasks (Ordered)

### GDC-MISS-001 Resolve ambiguous player death-state contract in `DeathState`
- [ ] Problem: `DeathState.DEAD` still carries an inline `FIXME` with unclear player `CORPSE` vs `DEAD` semantics.
- [ ] Target file: `Exports/GameData.Core/Enums/DeathState.cs`.
- [ ] Required change: replace ambiguous comment contract with explicit player semantics tied to reclaim-delay/ghost handling and remove the raw `FIXME` marker.
- [ ] Validation command: `dotnet build Exports/GameData.Core/GameData.Core.csproj --configuration Release --no-restore`.
- [ ] Acceptance: no ambiguity marker remains and consumers can interpret player death states deterministically.

### GDC-MISS-002 Lock corpse lifecycle interface contract across player/object/corpse
- [ ] Problem: corpse-run gating depends on a small field set, but contract expectations are not explicitly normalized in interface docs.
- [ ] Target files:
  - `Exports/GameData.Core/Interfaces/IWoWLocalPlayer.cs`
  - `Exports/GameData.Core/Interfaces/IWoWCorpse.cs`
  - `Exports/GameData.Core/Interfaces/IObjectManager.cs`
- [ ] Required change: document and align reclaim-critical fields (`CorpsePosition`, `InGhostForm`, `CanResurrect`, `CorpseRecoveryDelaySeconds`, corpse owner/type/flags) so FG and BG implementations are contract-equivalent.
- [ ] Validation command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~ReleaseCorpseTaskTests|FullyQualifiedName~RetrieveCorpseTaskTests|FullyQualifiedName~DeathCorpseRunTests" --logger "console;verbosity=minimal"`.
- [ ] Acceptance: corpse lifecycle decisions in BotRunner no longer need implementation-specific drift guards for these contract members.

### GDC-MISS-003 Remove snapshot contract drift between `IActivitySnapshot` and `IWoWActivitySnapshot`
- [ ] Problem: overlapping snapshot interfaces exist with divergent fields, which can cause mapper drift.
- [ ] Target files:
  - `Exports/GameData.Core/Interfaces/IActivitySnapshot.cs`
  - `Exports/GameData.Core/Interfaces/IWoWActivitySnapshot.cs`
- [ ] Required change: define and document intended boundary for each snapshot interface (or converge them) so downstream mapping is deterministic.
- [ ] Validation command: `dotnet build Exports/GameData.Core/GameData.Core.csproj --configuration Release --no-restore`.
- [ ] Acceptance: snapshot interface ownership is explicit and there is one deterministic contract path per consumer use-case.

## Simple Command Set
1. `dotnet build Exports/GameData.Core/GameData.Core.csproj --configuration Release --no-restore`
2. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~ReleaseCorpseTaskTests|FullyQualifiedName~RetrieveCorpseTaskTests|FullyQualifiedName~DeathCorpseRunTests" --logger "console;verbosity=minimal"`
3. `powershell -ExecutionPolicy Bypass -File .\\run-tests.ps1 -CleanupRepoScopedOnly`

## Session Handoff
- Last updated: 2026-02-25
- Current focus: `GDC-MISS-001`
- Last delta: executed prior handoff command (`Get-Content Exports/Loader/TASKS.md`) and added resume-first/next-file continuity guards for one-by-one queue traversal.
- Pass result: `delta shipped`
- Validation/tests run:
  - `Get-Content -Path 'Exports/Loader/TASKS.md' -TotalCount 380`
- Files changed:
  - `Exports/GameData.Core/TASKS.md`
- Next queue file: `Exports/Loader/TASKS.md`
- Next command: `Get-Content -Path 'Exports/Loader/TASKS.md' -TotalCount 380`
- Loop Break: if two passes produce no delta, record blocker + exact next command and move to next queued file.
