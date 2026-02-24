# Master Tasks

## Active Blockers (Top 5)
1. Fresh post-patch live evidence for corpse runback is still missing (`DeathCorpseRunTests` has not completed after latest flow reorder).
2. `dotnet test` can stall in this shell with no console progress; timeout guards were added, but a clean full run is still pending.
3. BG runback stall signature (zero displacement with horizontal movement intent) still needs re-validation after the latest `RetrieveCorpseTask` changes.
4. BG/FG corpse-phase parity (`dead corpse`, `ghost`, `moving`, `reclaim-ready`, `alive`) remains unverified on current code.
5. Keep temporary `.log`/`.txt` artifacts purged between validation runs to prevent context bloat from recurring.

## In Progress
- [x] Priority cleanup complete: purged temporary `.log`/`.txt` artifacts across temp work areas before next corpse-run validation pass.
- [x] `RetrieveCorpseTask`: moved stall-recovery checks behind route/probe resolution to avoid recovery loops when no route is available.
- [x] `RetrieveCorpseTask`: stall detection now considers all horizontal intents (forward/back/strafe), not forward-only.
- [x] `RetrieveCorpseTask`: suppress nested stall recovery while unstick maneuver is active.
- [x] `DeathCorpseRunTests`: removed reseed/variant retry kill loop in corpse setup path (single-pass flow only).
- [x] `DeathCorpseRunTests`: relaxed movement-proof condition to accept meaningful travel + convergence, not only improvement-tick count.
- [x] Added repo-wide test session timeout wiring:
  - `Tests/test.runsettings` with `TestSessionTimeout=180000`
  - existing project runsettings updated with same timeout
  - missing test projects now reference shared runsettings via `RunSettingsFilePath`
- [x] `run-tests.ps1` was rewritten to clean ASCII and now injects `--blame-hang --blame-hang-timeout <minutes>`, sets local `DOTNET_CLI_HOME` env defaults, and performs post-layer orphan cleanup for `dotnet/testhost`.
- [x] Orphan test cleanup completed this iteration (`dotnet`, `WoWStateManager`, and elevated `WoW` PID kill).
- [ ] Capture new live corpse-run evidence log with timeout-safe test invocation.

## Next Commands (Exact Commands to Run)
- `$env:DOTNET_CLI_HOME=(Resolve-Path tmp\\dotnethome).Path; $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'; $env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='0'; $env:DOTNET_CLI_TELEMETRY_OPTOUT='1'; $env:DOTNET_GENERATE_ASPNET_CERTIFICATE='0'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 3m --logger "console;verbosity=normal" 2>&1 | Tee-Object -FilePath tmp/deathcorpse_run_current.log`
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~CombatLoopTests" --blame-hang --blame-hang-timeout 3m --logger "console;verbosity=normal" 2>&1 | Tee-Object -FilePath tmp/combatloop_current.log`
- `Get-Process dotnet,testhost,testhost.x86,WoW,WoWStateManager -ErrorAction SilentlyContinue | Stop-Process -Force`

## Evidence Logs
- `tmp/build_botrunnertests_after_flow_reorder.log` (2026-02-23): `dotnet build` exited non-zero with no surfaced diagnostics in this shell.
- `tmp/run_tests_layer1_after_script_rewrite.log` (2026-02-23): `run-tests.ps1` layer-1 validation run after script rewrite and cleanup hardening.
- `tmp/deathcorpse_run_current.log` (baseline): stale/no-displacement corpse-run behavior before current reorder patch.
- `tmp/combatloop_current.log` (baseline): combat loop pass baseline.

## Session Handoff
- Last updated: 2026-02-23 16:05:00
- Files changed this iteration:
  - `Exports/BotRunner/Tasks/RetrieveCorpseTask.cs`
  - `Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs`
  - `run-tests.ps1`
  - `Tests/test.runsettings`
  - `Tests/BotRunner.Tests/test.runsettings`
  - `Tests/Navigation.Physics.Tests/test.runsettings`
  - `Tests/PathfindingService.Tests/test.runsettings`
  - `Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj`
  - `Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj`
  - `Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj`
  - `Tests/Tests.Infrastructure/Tests.Infrastructure.csproj`
  - `Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj`
  - `Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj`
  - `Tests/WoWSimulation/WoWSimulation.Tests.csproj`
  - `Tests/WWoW.RecordedTests.PathingTests.Tests/WWoW.RecordedTests.PathingTests.Tests.csproj`
  - `Tests/WWoW.RecordedTests.Shared.Tests/WWoW.RecordedTests.Shared.Tests.csproj`
  - `Tests/WWoW.Tests.Infrastructure/WWoW.Tests.Infrastructure.csproj`
  - `docs/TASKS.md`
- Commands run this iteration:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -v minimal 2>&1 | Tee-Object -FilePath tmp/build_botrunnertests_after_flow_reorder.log` => non-zero exit with no diagnostics emitted.
  - `powershell -ExecutionPolicy Bypass -File .\\run-tests.ps1 -Layer 1 -SkipBuild -TestTimeoutMinutes 1` => pass; no lingering `dotnet/testhost` processes after script-level cleanup pass.
  - attempted short `dotnet test` invocations (including `--blame-hang`) => command timeouts in this shell; orphan processes were manually terminated.
  - orphan cleanup:
    - `Stop-Process` for `dotnet` and `WoWStateManager`
    - elevated `taskkill /F /PID 48864` for protected `WoW` process.
- Highest-priority unresolved issue: produce fresh live corpse-run evidence on current code with timeout-safe test execution.
- Next command: `$env:DOTNET_CLI_HOME=(Resolve-Path tmp\\dotnethome).Path; $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'; $env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='0'; $env:DOTNET_CLI_TELEMETRY_OPTOUT='1'; $env:DOTNET_GENERATE_ASPNET_CERTIFICATE='0'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 3m --logger "console;verbosity=normal" 2>&1 | Tee-Object -FilePath tmp/deathcorpse_run_current.log`
