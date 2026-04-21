# Task Archive

Completed items moved from TASKS.md.

## Archived Snapshot (2026-04-15) - Foreground runner and command surface closeout

- [x] `RPTT-TST-005` Add foreground target selection and disconnect lifecycle tests.
  - Added foreground recording-target precedence coverage for config window title, config process ID, and environment fallback priority.
  - Added disconnect tests proving world/auth/resource teardown order, idempotence, and cleanup continuation after world disconnect failure.
- [x] `RPTT-TST-006` Validate the recorded pathing tests simple command set.
  - Revalidated the Program/Console command, runner lifecycle command, and bounded full local test command.
- Validation:
  - `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~ForegroundRecordedTestRunnerTests" --logger "console;verbosity=minimal"` -> `passed (6/6)`
  - `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~ProgramTests|FullyQualifiedName~ConsoleTestLoggerTests" --logger "console;verbosity=minimal"` -> `passed (34/34)`
  - `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~BackgroundRecordedTestRunnerTests|FullyQualifiedName~ForegroundRecordedTestRunnerTests" --logger "console;verbosity=minimal"` -> `passed (12/12)`
  - `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `passed (135/135)`

## Archived Snapshot (2026-04-15) - Program filter coverage

- [x] `RPTT-TST-001` Add direct tests for `Program.FilterTests` fail-fast behavior and exact-match filtering.
  - `Program.FilterTests` is now internal and exposed to `RecordedTests.PathingTests.Tests` through `InternalsVisibleTo`.
  - `ProgramTests` now pin exact case-insensitive name matching, exact case-insensitive category matching, combined filter behavior, and fail-fast no-match messaging.
- Validation:
  - `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~ProgramTests" --logger "console;verbosity=minimal"` -> `passed (26/26)`

## Archived Snapshot (2026-04-15) - Program pathfinding-service lifecycle coverage

- [x] `RPTT-TST-002` Add tests for in-process PathfindingService lifecycle start/stop and error cleanup path.
  - Added a test host factory seam for `Program.StartPathfindingServiceAsync`.
  - Added tests for host start, normal stop, stop failure cleanup, and start failure cleanup.
  - `Program.StopPathfindingServiceAsync` now clears and disposes the hosted service even when `StopAsync` throws.
- Validation:
  - `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~ProgramTests" --logger "console;verbosity=minimal"` -> `passed (30/30)`

## Archived Snapshot (2026-04-15) - Background runner timeout and disconnect lifecycle coverage

- [x] `RPTT-TST-003` Add timeout/teardown tests for `BackgroundRecordedTestRunner.RunTestAsync`.
  - Added test hooks for delay, navigation, current position/map, and stop movement.
  - Tests prove timeout and navigation-failure paths both stop movement.
- [x] `RPTT-TST-004` Add disconnect/game-loop lifecycle tests for `BackgroundRecordedTestRunner`.
  - Tests prove disconnect stops the game loop before network/resource teardown, is idempotent, and still disposes remaining resources when world disconnect fails.
  - `DisconnectAsync` now clears resources and logs non-cancellation disconnect/dispose failures instead of leaving partially disconnected state behind.
- Validation:
  - `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~BackgroundRecordedTestRunnerTests" --logger "console;verbosity=minimal"` -> `passed (6/6)`
