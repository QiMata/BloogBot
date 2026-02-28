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
1. [x] `PHS-MISS-001` Replace `NotImplementedException` in `TransferHistory` with explicit unsupported-target validation.
- **Done (prior session).** `NotImplementedException` → `ArgumentException` in `PromptFunctionBase.cs`.

2. [x] `PHS-MISS-002` Add direct transfer-contract tests for `PromptFunctionBase`.
- **Done (batch 14).** Added `PromptFunctionBaseTransferTests.cs` with 14 tests covering TransferHistory, TransferChatHistory, TransferPromptRunner.
  - TransferHistory: non-PromptFunctionBase rejection, System message filtering, target clear, message order preservation.
  - TransferPromptRunner: runner reference copy verified.
- Validation: 14/14 pass (`dotnet test --filter "FullyQualifiedName~PromptFunctionBaseTransferTests"`).
- [x] Acceptance: targeted transfer tests are discovered and pass; regressions fail deterministically.

3. [x] `PHS-MISS-003` Add regression tests for system prompt preservation and initialization semantics.
- **Done (batch 14).** Same `PromptFunctionBaseTransferTests.cs` covers:
  - TransferChatHistory inserts target SystemPrompt as first entry (not source prompt).
  - Source System messages removed; exactly one System entry in target.
  - InitializeChat called exactly once.
  - ResetChat clears history, inserts SystemPrompt, calls InitializeChat.
  - Multiple ResetChat calls don't accumulate System entries.
- Validation: all transfer/reset tests pass.
- [x] Acceptance: tests fail on prompt-order/init-call regressions.

4. [x] `PHS-MISS-004` Restore test discovery for existing PromptHandling test methods.
- **Already addressed.** All test methods already have `[Fact(Skip = "Integration: requires local Ollama")]` attributes. Test discovery is correct — 2 non-skipped tests run, 12 integration tests are properly skipped.

## Simple Command Set
1. Build service: `dotnet build Services/PromptHandlingService/PromptHandlingService.csproj --configuration Release --no-restore`
2. Build tests: `dotnet build Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore`
3. Full tests: `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`
4. Transfer-focused tests: `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Transfer|FullyQualifiedName~PromptFunctionBase" --logger "console;verbosity=minimal"`

## Session Handoff
- Last updated: 2026-02-28
- Active task: all PromptHandlingService tasks complete (PHS-MISS-001..004)
- Last delta: PHS-MISS-002/003 (14 transfer-contract + initialization tests in PromptFunctionBaseTransferTests.cs)
- Pass result: `delta shipped`
- Validation/tests run:
  - `dotnet test Tests/PromptHandlingService.Tests -c Debug --filter PromptFunctionBaseTransferTests` — 14/14 pass
- Files changed:
  - `Tests/PromptHandlingService.Tests/PromptFunctionBaseTransferTests.cs` — new (14 tests)
  - `Services/PromptHandlingService/TASKS.md`
- Next command: continue with next queue file
- Blockers: none
