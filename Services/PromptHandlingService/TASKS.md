# PromptHandlingService Tasks

Master tracker: `MASTER-SUB-019`

## Scope
- Directory: `Services/PromptHandlingService`
- Project: `PromptHandlingService.csproj`
- Focus: enforce explicit transfer contracts in `PromptFunctionBase` and add deterministic test coverage that is actually discovered by xUnit.
- Queue dependency: `docs/TASKS.md` controls execution order and handoff pointers.

## Execution Rules
1. Execute tasks in order unless blocked by a recorded dependency.
2. Keep transfer semantics explicit: supported targets transfer; unsupported targets fail with deterministic validation errors.
3. Never blanket-kill `dotnet`; use repo-scoped cleanup only.
4. Every task completion must include at least one simple validation command recorded in `Session Handoff`.
5. Archive completed items to `Services/PromptHandlingService/TASKS_ARCHIVE.md` in the same session.
6. Every pass must write one-line `Pass result` (`delta shipped` or `blocked`) and exactly one executable `Next command`.

## Evidence Snapshot (2026-02-25)
- Build checks pass:
  - `dotnet build Services/PromptHandlingService/PromptHandlingService.csproj --configuration Release --no-restore` -> `0 Error(s)`, `0 Warning(s)`.
  - `dotnet build Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore` -> `0 Error(s)`, `0 Warning(s)`.
- Test baseline is green but narrow:
  - `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"` -> `Passed: 2, Failed: 0`.
  - Transfer-focused filter returns no discovered tests: `No test matches ... FullyQualifiedName~Transfer|FullyQualifiedName~PromptFunctionBase`.
- Transfer code path evidence in `PromptFunctionBase.cs`:
  - `TransferHistory` throws `NotImplementedException` for unsupported targets (`PromptFunctionBase.cs:47`).
  - Transfer helper methods exist (`TransferHistory` line 43, `TransferChatHistory` line 147, `TransferPromptRunner` line 156).
  - System prompt reset/init behavior is embedded in transfer/reset flow (`SystemPrompt` line 64, `InitializeChat` line 129, transfer reset logic line 152-153).
- Discovery-gap evidence in test project:
  - `rg -n "\\[Fact\\]|\\[Theory\\]" Tests/PromptHandlingService.Tests -g "*.cs"` only returns two tests:
    - `DecisionEngineReadBinFileTests.cs:16`
    - `PromptCacheTests.cs:9`
  - Multiple additional test methods exist but are not attributed, so they are not executed by xUnit.

## P0 Active Tasks (Ordered)
1. [ ] `PHS-MISS-001` Replace `NotImplementedException` in `TransferHistory` with explicit unsupported-target validation.
- Problem: unsupported targets currently throw `NotImplementedException`, which is non-contractual and non-deterministic for callers.
- Target files: `Services/PromptHandlingService/PromptFunctionBase.cs`.
- Required change: replace throw-only guard with explicit argument/validation exception contract (for example `ArgumentException`/`InvalidCastException` with stable message).
- Validation command: `rg -n "throw new NotImplementedException\\(" Services/PromptHandlingService/PromptFunctionBase.cs`
- Acceptance criteria: no `NotImplementedException` remains in transfer paths.

2. [ ] `PHS-MISS-002` Add direct transfer-contract tests for `PromptFunctionBase`.
- Problem: no tests currently reference `TransferHistory`, `TransferChatHistory`, or `TransferPromptRunner`.
- Target files: `Tests/PromptHandlingService.Tests` (new focused test file recommended), `Services/PromptHandlingService/PromptFunctionBase.cs` (if internals exposure needed).
- Required change: cover supported-transfer behavior plus unsupported-target behavior with explicit assertions.
- Validation command: `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Transfer|FullyQualifiedName~PromptFunctionBase" --logger "console;verbosity=minimal"`
- Acceptance criteria: targeted transfer tests are discovered and pass; regressions fail deterministically.

3. [ ] `PHS-MISS-003` Add regression tests for system prompt preservation and initialization semantics.
- Problem: current suite does not gate that `TransferChatHistory` re-inserts target system prompt and calls `InitializeChat()` exactly once.
- Target files: `Tests/PromptHandlingService.Tests` (new transfer semantics tests), `Services/PromptHandlingService/PromptFunctionBase.cs`.
- Required change: assert target system prompt is first entry post-transfer, source system prompt is not copied, and init-call count is stable.
- Validation command: `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~TransferChatHistory|FullyQualifiedName~InitializeChat" --logger "console;verbosity=minimal"`
- Acceptance criteria: tests fail on prompt-order/init-call regressions.

4. [ ] `PHS-MISS-004` Restore test discovery for existing PromptHandling test methods.
- Problem: many methods in `Tests/PromptHandlingService.Tests` are not discovered because they are missing `[Fact]`/`[Theory]`.
- Target files: `Tests/PromptHandlingService.Tests/*.cs` (non-generated files only).
- Required change: convert intended unit tests to discovered xUnit tests or move integration-only methods behind explicit categories/skip attributes with rationale.
- Validation command: `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`
- Acceptance criteria: discovered test count increases beyond baseline `Total: 2` with deterministic pass/fail behavior.

## Simple Command Set
1. Build service: `dotnet build Services/PromptHandlingService/PromptHandlingService.csproj --configuration Release --no-restore`
2. Build tests: `dotnet build Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore`
3. Full tests: `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`
4. Transfer-focused tests: `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Transfer|FullyQualifiedName~PromptFunctionBase" --logger "console;verbosity=minimal"`

## Session Handoff
- Last updated: 2026-02-25
- Pass result: `delta shipped`
- Last delta: converted to execution-card format with refreshed build/test evidence, added xUnit discovery-gap tasking, and pinned deterministic validation commands.
- Next task: `PHS-MISS-001`
- Next command: `Get-Content -Path 'Services/WoWStateManager/TASKS.md' -TotalCount 320`
- Blockers: none
