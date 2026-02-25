# WoWSharpClient Tasks

## Scope
- Project: `Exports/WoWSharpClient`
- Owns BG-side object model parsing, packet send/receive flow, and network client components.
- This file tracks concrete missing-implementation items tied to source files and test coverage.
- Master tracker: `MASTER-SUB-009`.

## Execution Rules
1. Work only the top unchecked task unless blocked.
2. Keep model/packet changes paired with deterministic tests in `Tests/WoWSharpClient.Tests` and/or `Tests/WowSharpClient.NetworkTests`.
3. Keep commands simple and one-line.
4. Record `Last delta` and `Next command` in `Session Handoff` every pass.
5. Move completed tasks to `Exports/WoWSharpClient/TASKS_ARCHIVE.md` in the same session.
6. Loop-break guard: if two consecutive passes produce no file delta, log blocker + exact next command and move to next queue file.
7. `Session Handoff` must include `Pass result` (`delta shipped` or `blocked`) and exactly one executable `Next command`.

## Environment Checklist
- [x] `Exports/WoWSharpClient/WoWSharpClient.csproj` builds cleanly in `Release`.
- [ ] Opcode tests and object-update tests run before marking parser/send-path work complete.
- [ ] BG behavior changes are validated against FG parity expectations in the same scenario cycle.

## Evidence Snapshot (2026-02-25)
- `WSC-MISS-001` evidence is still present in object manager mapping:
  - `Exports/WoWSharpClient/WoWSharpObjectManager.cs:2000-2039` contains explicit `not implemented` notes for player fields (`ChosenTitle`, `KnownTitles`, `ModHealingDonePos`, `ModTargetResistance`, `FieldBytes`, `OffhandCritPercentage`, `SpellCritPercentage`, `ModManaRegen`, `ModManaRegenInterrupt`, `MaxLevel`, `DailyQuests`).
- `WSC-MISS-002` remains a TODO stub:
  - `Exports/WoWSharpClient/Models/WoWUnit.cs:270` says `TODO: Send CMSG_CANCEL_AURA`.
- `WSC-MISS-003` remains unresolved:
  - `Exports/WoWSharpClient/Networking/ClientComponents/GossipNetworkClientComponent.cs:249` logs `"Custom navigation strategy not implemented"`.
- `WSC-MISS-004` still uses placeholder reward-selection behavior:
  - `Exports/WoWSharpClient/Networking/ClientComponents/GossipNetworkClientComponent.cs:266` comment indicates placeholder selection of first reward via `SelectGossipOptionAsync(0, ...)`.
- Build/test inventory baseline in this shell:
  - `dotnet build Exports/WoWSharpClient/WoWSharpClient.csproj -c Release` succeeded.
  - `dotnet test ...WoWSharpClient.Tests... --list-tests` and `dotnet test ...WowSharpClient.NetworkTests... --list-tests` both enumerate tests (with warning noise in current environment).

## P0 Active Tasks (Ordered)

### WSC-MISS-001 Implement missing `WoWPlayer` field coverage referenced in object manager notes
- [ ] Problem: `WoWSharpObjectManager` still contains explicit "property not implemented" notes for active `EPlayerFields` mappings.
- [ ] Target files:
  - `Exports/WoWSharpClient/WoWSharpObjectManager.cs`
  - `Exports/WoWSharpClient/Models/WoWPlayer.cs`
- [ ] Required change: add missing `WoWPlayer` properties and mapping logic for `ChosenTitle`, `KnownTitles`, `ModHealingDonePos`, `ModTargetResistance`, `FieldBytes`, `OffhandCritPercentage`, `SpellCritPercentage`, `ModManaRegen`, `ModManaRegenInterrupt`, `MaxLevel`, and `DailyQuests`.
- [ ] Validation command: `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~SMSG_UPDATE_OBJECT|FullyQualifiedName~ObjectManager" --logger "console;verbosity=minimal"`.
- [ ] Acceptance: parser notes are removed/replaced by implemented assignments and tests assert populated values when fields are present.

### WSC-MISS-002 Implement `CMSG_CANCEL_AURA` send path for `WoWUnit.DismissBuff`
- [ ] Problem: `WoWUnit.DismissBuff` is a TODO stub and always returns `false`.
- [ ] Target files:
  - `Exports/WoWSharpClient/Models/WoWUnit.cs`
  - `Exports/WoWSharpClient/Networking` packet send components used by unit actions
- [ ] Required change: wire client access for `DismissBuff`, send correct opcode/payload, and return deterministic success/failure.
- [ ] Validation command: `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Aura|FullyQualifiedName~Cancel" --logger "console;verbosity=minimal"`.
- [ ] Acceptance: dismissing an active buff emits `CMSG_CANCEL_AURA` with expected payload and test coverage catches regressions.

### WSC-MISS-003 Resolve `GossipNavigationStrategy.Custom` runtime warning path
- [ ] Problem: `GossipNetworkClientComponent` logs "Custom navigation strategy not implemented" and skips behavior.
- [ ] Target files:
  - `Exports/WoWSharpClient/Networking/ClientComponents/GossipNetworkClientComponent.cs`
  - Any supporting custom-strategy interface/handler files under `Exports/WoWSharpClient/Networking`
- [ ] Required change: implement custom navigation strategy hook or remove dead branch with explicit, tested fallback behavior.
- [ ] Validation command: `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Gossip" --logger "console;verbosity=minimal"`.
- [ ] Acceptance: supported gossip flows no longer rely on an unimplemented strategy branch and behavior is deterministic under test.

### WSC-MISS-004 Replace placeholder quest reward selection strategy logic
- [ ] Problem: `SelectOptimalQuestRewardAsync` currently uses a placeholder that always selects index `0`.
- [ ] Target files:
  - `Exports/WoWSharpClient/Networking/ClientComponents/GossipNetworkClientComponent.cs`
  - Related quest/gossip test files under `Tests/WoWSharpClient.Tests`
- [ ] Required change: implement strategy-aware reward selection and assert behavior for at least one non-trivial selection case.
- [ ] Validation command: `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Gossip|FullyQualifiedName~Quest" --logger "console;verbosity=minimal"`.
- [ ] Acceptance: quest reward selection respects requested strategy and no placeholder-first-option behavior remains.

## Simple Command Set
1. `dotnet build Exports/WoWSharpClient/WoWSharpClient.csproj -c Release`
2. `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`
3. `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`
4. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
5. `rg --line-number "TODO|FIXME|NotImplemented|not implemented" Exports/WoWSharpClient -g "*.cs"`

## Session Handoff
- Last updated: 2026-02-25
- Active task: `MASTER-SUB-009` (`Exports/WoWSharpClient/TASKS.md`)
- Current focus: `WSC-MISS-001`
- Last delta: added evidence-backed symbol inventory for unresolved player field mappings, aura cancel send path, custom gossip strategy branch, and placeholder quest reward selection.
- Pass result: `delta shipped`
- Validation/tests run:
  - `rg --line-number "not implemented yet|ChosenTitle|KnownTitles|ModHealingDonePos|ModTargetResistance|FieldBytes|OffhandCritPercentage|SpellCritPercentage|ModManaRegen|ModManaRegenInterrupt|MaxLevel|DailyQuests" Exports/WoWSharpClient/WoWSharpObjectManager.cs Exports/WoWSharpClient/Models/WoWPlayer.cs`
  - `rg --line-number "DismissBuff|CMSG_CANCEL_AURA|TODO" Exports/WoWSharpClient/Models/WoWUnit.cs Exports/WoWSharpClient/Networking -g "*.cs"`
  - `rg --line-number "Custom navigation strategy not implemented|GossipNavigationStrategy.Custom|SelectOptimalQuestRewardAsync|return 0|placeholder" Exports/WoWSharpClient/Networking/ClientComponents/GossipNetworkClientComponent.cs`
  - `dotnet build Exports/WoWSharpClient/WoWSharpClient.csproj -c Release`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore --list-tests`
  - `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-restore --list-tests`
- Files changed:
  - `Exports/WoWSharpClient/TASKS.md`
- Next command: `Get-Content -Path 'RecordedTests.PathingTests/TASKS.md' -TotalCount 360`
- Loop Break: if two passes produce no delta, record blocker + exact next command and move to next queued file.
