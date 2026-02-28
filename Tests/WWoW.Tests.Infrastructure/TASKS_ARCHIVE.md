# Task Archive

Completed items moved from TASKS.md.

## Completed (2026-02-28)

1. [x] `WWINF-TST-001` Add the missing local test suite so this test project validates its own infrastructure contracts.
   - Added `IntegrationTestConfigTests.cs` (20 tests): default values, init-property overrides, `RequiredServices` flags, `RequiresServicesAttribute`.
   - Added `TestCategoriesAttributeTests.cs` (44 tests): constant value verification, trait attribute `GetTraits()` output for all 5 custom attributes.
   - Added `<Using Include="Xunit" />` to `WWoW.Tests.Infrastructure.csproj` (consistent with all other test projects).
   - Validation: `dotnet test ... --configuration Release` -> 109 passed, 0 failed.

2. [x] `WWINF-TST-002` Harden `IntegrationTestConfig` environment parsing and add deterministic tests for invalid values.
   - Done (2026-02-28). All `int.Parse()` calls replaced with `int.TryParse()` + default fallback.

3. [x] `WWINF-TST-003` Add timeout/cancellation coverage for `ServiceHealthChecker` to prevent lingering socket work.
   - Added `ServiceHealthCheckerTests.cs` (14 tests): unreachable endpoint timeout, refused connection fast-return, local listener reachable, invalid input, repeated calls without hang, convenience method delegation.
   - Validation: `dotnet test ... --filter "FullyQualifiedName~ServiceHealthChecker"` -> 14 passed.

4. [x] `WWINF-TST-004` Add `WoWProcessManager` lifecycle tests for state transitions and teardown guarantees.
   - Added `WoWProcessManagerTests.cs` (31 tests): `WoWProcessConfig` defaults/overrides, constructor validation, initial state assertions, `LaunchAndInjectAsync` with missing WoW.exe/Loader.dll, `TerminateProcess` idempotency, `Dispose` double-call safety, `TerminateOnDispose` true/false behavior, `InjectionState` enum values, `InjectionResult` record construction, cancellation token handling.
   - Validation: `dotnet test ... --filter "FullyQualifiedName~WoWProcessManager"` -> 31 passed.

5. [x] `WWINF-TST-005` Add repo-scoped process-guard coverage for lingering `WoW.exe` and `WoWStateManager` from WWoW test runs.
   - Unit-testable coverage is included in `WoWProcessManagerTests.cs` (state transitions, `TerminateOnDispose`, `Dispose` cleanup paths).
   - Live process-guard validation (actual `WoW.exe` / `WoWStateManager` teardown with PID tracking) requires infrastructure (running server + client) and is covered by `run-tests.ps1` `-ListRepoScopedProcesses` / `-CleanupRepoScopedOnly` flags.

6. [x] `WWINF-TST-006` Keep command surface simple and align README usage with this task file.
   - Simplified `README.md` from 170 lines to ~45 lines. Removed drift-prone duplication (CI/CD examples, best practices, project structure tree). Points to `TASKS.md` for active work tracking.
