# Pathfinding Tests Implementation Status

## ✅ Completed Components

### 1. Native Build & Data Infrastructure
- **[Navigation.vcxproj](../Exports/Navigation/Navigation.vcxproj)** - Output paths normalized for all configurations
  - Release|x64 now outputs to `..\..\Bot\$(Configuration)\net8.0\`
- **[DllMain.cpp](../Exports/Navigation/DllMain.cpp)** - `BLOOGBOT_DATA_DIR` environment variable support
  - Checks env var for maps/ and vmaps/ paths before falling back to DLL-relative
- **[Navigation.cpp](../Exports/Navigation/Navigation.cpp)** - `GetMmapsPath()` updated
  - Prioritizes `BLOOGBOT_DATA_DIR` for mmaps/ path resolution
- **[setup.ps1](../setup.ps1)** - Updated instructions for setting environment variable

### 2. PathfindingService.Tests Preflight Checks
- **[PathingAndOverlapTests.cs](../Tests/PathfindingService.Tests/PathingAndOverlapTests.cs)** - Enhanced `NavigationFixture`
  - `VerifyNavigationDll()` - Validates Navigation.dll exists in test output
  - `VerifyNavDataExists()` - Validates mmaps/ directory with clear error messages
  - Supports both `BLOOGBOT_DATA_DIR` and DLL-relative paths

### 3. Test Definition Infrastructure
- **[PathingTestDefinition.cs](Models/PathingTestDefinition.cs)** - Complete model
  - Includes TransportMode enum for boat/zeppelin tests
  - All 20 test properties: name, category, positions, commands, duration
- **[PathingTestDefinitions.cs](Models/PathingTestDefinitions.cs)** - All 20 tests defined
  - 3 Basic point-to-point tests
  - 4 Transport tests (boats & zeppelins)
  - 3 Cave navigation tests
  - 3 Terrain challenge tests
  - 3 Advanced multi-segment tests
  - 4 Edge case/stress tests
  - Helper methods: `GetByName()`, `GetByCategory()`, `GetCategories()`

### 4. Test Context & Description
- **[PathingRecordedTestContext.cs](Context/PathingRecordedTestContext.cs)** - Test execution context
  - Extends `RecordedTestContext` with `PathingTestDefinition`
- **[PathingRecordedTestDescription.cs](Descriptions/PathingRecordedTestDescription.cs)** - Test orchestration
  - Extends `DefaultRecordedWoWTestDescription`
  - Wires up GM command execution via `IGmCommandExecutor`

### 5. GM Command Execution
- **[IGmCommandExecutor.cs](../WWoW.RecordedTests.Shared/Abstractions/I/IGmCommandExecutor.cs)** - Interface
  - `ExecuteCommandAsync(string command, CancellationToken cancellationToken)`
- **[GmCommandServerDesiredState.cs](../WWoW.RecordedTests.Shared/DesiredState/GmCommandServerDesiredState.cs)** - Updated
  - `SetExecutor(IGmCommandExecutor executor)` method added
  - `ApplyAsync()` and `RevertAsync()` now use executor
  - Clear error messages if executor not set

### 6. Bot Runners
- **[ForegroundRecordedTestRunner.cs](Runners/ForegroundRecordedTestRunner.cs)** - GM bot ✅
  - Implements `IBotRunner` and `IGmCommandExecutor`
  - Uses `WoWClientOrchestrator` for connection and chat commands
  - Provides recording target via environment variables
  - Full GM command execution support
- **[BackgroundRecordedTestRunner.cs](Runners/BackgroundRecordedTestRunner.cs)** - Test bot ✅
  - Implements `IBotRunner` for test execution
  - Uses `PathfindingClient` for path calculation
  - Movement logic with stuck detection and repathing
  - Transport handling hooks (implementation pending object manager integration)
  - **NOTE**: Object manager integration is stubbed - needs `WoWSharpObjectManager` wiring

### 7. Project Configuration
- **[WWoW.RecordedTests.PathingTests.csproj](WWoW.RecordedTests.PathingTests.csproj)** - Updated
  - Added `obs-websocket-dotnet` v5.2.0
  - Added Microsoft.Extensions.Configuration packages

---

## 🚧 Remaining Work

### Phase 1: OBS Recording Implementation
**Status**: Package added, implementation needed

The `ObsWebSocketScreenRecorder.cs` in WWoW.RecordedTests.Shared needs the TODO stubs replaced with actual obs-websocket-dotnet implementation.

**File**: `../WWoW.RecordedTests.Shared/Recording/ObsWebSocketScreenRecorder.cs`

**Required Methods**:
```csharp
using OBSWebsocketDotNet;

private OBSWebsocket? _obs;

public override async Task LaunchAsync(CancellationToken cancellationToken)
{
    if (_config.AutoLaunch && !string.IsNullOrEmpty(_config.ObsExecutablePath))
    {
        Process.Start(_config.ObsExecutablePath);
        await Task.Delay(_config.ObsStartupDelayMs, cancellationToken);
    }

    _obs = new OBSWebsocket();
    _obs.ConnectAsync(_config.WebSocketUrl, _config.WebSocketPassword);
    await Task.Delay(1000, cancellationToken); // Wait for connection
}

public override async Task StartAsync(CancellationToken cancellationToken)
{
    if (_obs == null || !_obs.IsConnected)
        throw new InvalidOperationException("OBS not connected");

    await _obs.StartRecording();
}

public override async Task StopAsync(CancellationToken cancellationToken)
{
    if (_obs == null) return;

    await _obs.StopRecording();
    await Task.Delay(_config.RecordingFinalizationDelayMs, cancellationToken);
}

// Implement ConfigureTargetAsync, MoveLastRecordingAsync based on OBS API
```

### Phase 2: Configuration System
**Status**: Not started

**Files to Create**:
1. `Configuration/TestConfiguration.cs` - Configuration model
2. `Configuration/ConfigurationParser.cs` - CLI/env/config file parsing

**TestConfiguration Properties**:
- Server: `ServerInfo`, `TrueNasApiUrl`, `TrueNasApiKey`, `ServerTimeout`
- Accounts: `GmAccount`, `GmPassword`, `GmCharacter`, `TestAccount`, `TestPassword`, `TestCharacter`
- PathfindingService: `PathfindingServiceIp`, `PathfindingServicePort`
- OBS: `ObsExecutablePath`, `ObsWebSocketUrl`, `ObsWebSocketPassword`, `ObsRecordingPath`, `ObsAutoLaunch`
- Recording: `WowWindowTitle`, `WowProcessId`, `WowWindowHandle`
- Test execution: `TestFilter`, `CategoryFilter`, `StopOnFirstFailure`, `ArtifactsRoot`

**Parsing Priority**: CLI args > Environment variables > appsettings.json > Defaults

### Phase 3: Program.cs Orchestration
**Status**: Not started

**File**: `Program.cs`

**Main Responsibilities**:
1. Parse configuration from all sources
2. Start PathfindingService in-process
3. Create server availability checker (TrueNAS or Docker)
4. For each test definition:
   - Create `PathingRecordedTestContext`
   - Create `ForegroundRecordedTestRunner` (GM)
   - Create `BackgroundRecordedTestRunner` (test)
   - Create `ObsWebSocketScreenRecorder` (if configured)
   - Create `PathingRecordedTestDescription`
   - Create `RecordedTestOrchestrator`
   - Run test and collect results
5. Print summary of test results
6. Stop PathfindingService
7. Return exit code (0 = success, 1 = failure)

**Key Methods Needed**:
- `ParseConfiguration(string[] args)` - Build configuration from all sources
- `FilterTests(Configuration config, IReadOnlyList<PathingTestDefinition> all)` - Apply test/category filters
- `CreateServerAvailabilityChecker(Configuration config)` - TrueNAS or local Docker
- `CreateRecorder(Configuration config)` - OBS recorder or null
- `PrintSummary(List<OrchestrationResult> results)` - Test results summary

### Phase 4: Unit Tests
**Status**: Not started

**Test Projects to Create**:
1. `Tests/WWoW.RecordedTests.PathingTests.Tests/` - New test project
2. `Tests/WWoW.RecordedTests.Shared.Tests/` - Shared component tests (if doesn't exist)

**Test Files Needed**:

#### `Tests/WWoW.RecordedTests.PathingTests.Tests/ProgramTests.cs`
```csharp
[Fact]
public void DefinePathingTests_Returns20Tests()
{
    Assert.Equal(20, PathingTestDefinitions.All.Count);
}

[Fact]
public void DefinePathingTests_AllNamesUnique()
{
    var names = PathingTestDefinitions.All.Select(t => t.Name);
    Assert.Equal(20, names.Distinct().Count());
}

[Fact]
public void DefinePathingTests_AllNamesNonEmpty()
{
    Assert.All(PathingTestDefinitions.All, t =>
        Assert.False(string.IsNullOrWhiteSpace(t.Name)));
}

[Fact]
public void GetByCategory_ReturnsCorrectTests()
{
    var basicTests = PathingTestDefinitions.GetByCategory("Basic");
    Assert.Equal(3, basicTests.Count());
}
```

#### `Tests/WWoW.RecordedTests.Shared.Tests/GmCommandServerDesiredStateTests.cs`
```csharp
public class FakeGmCommandExecutor : IGmCommandExecutor
{
    public List<string> ExecutedCommands { get; } = new();
    public Task ExecuteCommandAsync(string command, CancellationToken ct = default)
    {
        ExecutedCommands.Add(command);
        return Task.CompletedTask;
    }
}

[Fact]
public async Task ApplyAsync_ExecutesAllSetupCommands()
{
    var setup = new[] { ".level 10", ".teleport Goldshire" };
    var teardown = new[] { ".character delete" };
    var executor = new FakeGmCommandExecutor();
    var state = new GmCommandServerDesiredState(setup, teardown, NullTestLogger.Instance);
    state.SetExecutor(executor);

    await state.ApplyAsync(null!, null!, CancellationToken.None);

    Assert.Equal(setup, executor.ExecutedCommands);
}
```

### Phase 5: CI Pipeline
**Status**: Not started

**File**: `.github/workflows/pathfinding-tests.yml`

**Jobs**:
1. `build-native` - Build Navigation.dll (x64 Debug) and upload artifact
2. `unit-tests` - Run PathfindingService.Tests and PathingTests.Tests
3. `recorded-tests` - Run recorded tests on self-hosted Windows agent with OBS

**Secrets Required**:
- `NAV_DATA_PATH` - Path to navigation data on CI agents
- `GM_ACCOUNT`, `GM_PASSWORD`, `GM_CHARACTER`
- `TEST_ACCOUNT`, `TEST_PASSWORD`, `TEST_CHARACTER`
- `OBS_WEBSOCKET_PASSWORD`
- `SERVER_DEFINITIONS` - Server connection strings

---

## 🔧 Known Limitations & TODOs

### BackgroundRecordedTestRunner
- **Object Manager Integration**: Currently stubbed
  - `GetCurrentPosition()` - Returns start position (needs `_objectManager.Player.Position`)
  - `MoveTowardWaypoint()` - Logs only (needs `_objectManager.MoveToward(waypoint)`)
  - `StopMovement()` - Logs only (needs `_objectManager.StopAllMovement()`)
- **Transport Handling**: Hooks in place but not implemented
  - Need to detect `MOVEFLAG_ONTRANSPORT` flag
  - Need to wait for mapId change or position change
  - Need to continue pathfinding after transport arrival

### ObsWebSocketScreenRecorder
- All methods are TODO stubs that need obs-websocket-dotnet implementation
- Need to implement scene/source configuration
- Need to implement recording output path management

### Configuration System
- Complete implementation needed for CLI/env/config parsing
- appsettings.json schema not yet defined
- Environment variable naming conventions need documentation

---

## 📝 Next Steps (Priority Order)

1. ✅ **Complete OBS Recording** - Replace TODO stubs in `ObsWebSocketScreenRecorder.cs`
2. ✅ **Create Configuration System** - `TestConfiguration.cs` + `ConfigurationParser.cs`
3. ✅ **Implement Program.cs** - Main orchestration logic
4. ✅ **Add Unit Tests** - ProgramTests, GmCommandServerDesiredStateTests
5. ✅ **Create CI Pipeline** - `.github/workflows/pathfinding-tests.yml`
6. ✅ **Wire Object Manager** - Update `BackgroundRecordedTestRunner` with real movement
7. ✅ **Implement Transport Handling** - Boat/zeppelin detection and waiting
8. ✅ **Documentation** - Update README.md with usage instructions

---

## 🚀 Testing the Implementation

### Prerequisites
1. Build Navigation.dll for x64:
   ```powershell
   msbuild Exports/Navigation/Navigation.vcxproj /p:Configuration=Debug /p:Platform=x64
   ```

2. Set environment variables:
   ```powershell
   $env:BLOOGBOT_DATA_DIR = "D:\WoWData"
   $env:WWOW_GM_ACCOUNT = "admin"
   $env:WWOW_GM_PASSWORD = "password"
   $env:WWOW_GM_CHARACTER = "GMChar"
   $env:WWOW_TEST_ACCOUNT = "test"
   $env:WWOW_TEST_PASSWORD = "password"
   $env:WWOW_TEST_CHARACTER = "TestChar"
   $env:OBS_WEBSOCKET_PASSWORD = "your_obs_password"
   $env:SERVER_DEFINITIONS = "mangos-local|127.0.0.1|3724|mangos"
   ```

3. Ensure nav data exists:
   ```powershell
   Test-Path "$env:BLOOGBOT_DATA_DIR\mmaps\*.mmtile"
   ```

### Run Single Test
```powershell
dotnet run --project WWoW.RecordedTests.PathingTests -- --test-filter Northshire
```

### Run All Tests in Category
```powershell
dotnet run --project WWoW.RecordedTests.PathingTests -- --category Basic
```

### Run All Tests
```powershell
dotnet run --project WWoW.RecordedTests.PathingTests
```

---

## 📚 Architecture Reference

**Test Orchestration Flow**:
```
Program.cs
  ├─ Parse Configuration (CLI → env → config → defaults)
  ├─ Start PathfindingService in-process
  ├─ For each test definition:
  │   ├─ Create PathingRecordedTestContext
  │   ├─ Create ForegroundRecordedTestRunner (GM)
  │   ├─ Create BackgroundRecordedTestRunner (test)
  │   ├─ Create ObsWebSocketScreenRecorder (optional)
  │   ├─ Create PathingRecordedTestDescription
  │   ├─ Create RecordedTestOrchestrator
  │   └─ Run test:
  │       ├─ Wait for server availability
  │       ├─ Connect both runners
  │       ├─ Launch & configure recorder
  │       ├─ Prepare server state (GM setup commands)
  │       ├─ Start recording
  │       ├─ Execute test (pathfinding & movement)
  │       ├─ Stop recording
  │       ├─ Reset server state (GM teardown commands)
  │       ├─ Move recording to artifacts
  │       └─ Cleanup
  ├─ Stop PathfindingService
  └─ Return exit code
```

**Key Interfaces**:
- `IBotRunner` - Bot lifecycle (connect, prepare, run, disconnect)
- `IGmCommandExecutor` - GM command execution capability
- `IServerDesiredState` - Server state setup/teardown
- `IScreenRecorder` - Recording lifecycle
- `ITestLogger` - Test logging abstraction

---

## 📄 File Manifest

### Created Files
- `Models/PathingTestDefinition.cs`
- `Models/PathingTestDefinitions.cs`
- `Context/PathingRecordedTestContext.cs`
- `Descriptions/PathingRecordedTestDescription.cs`
- `Runners/ForegroundRecordedTestRunner.cs`
- `Runners/BackgroundRecordedTestRunner.cs`
- `WWoW.RecordedTests.PathingTests.csproj` (updated)
- `../WWoW.RecordedTests.Shared/Abstractions/I/IGmCommandExecutor.cs`

### Modified Files
- `../WWoW.RecordedTests.Shared/DesiredState/GmCommandServerDesiredState.cs`
- `../Exports/Navigation/Navigation.vcxproj`
- `../Exports/Navigation/DllMain.cpp`
- `../Exports/Navigation/Navigation.cpp`
- `../Tests/PathfindingService.Tests/PathingAndOverlapTests.cs`
- `../setup.ps1`

### Files Pending Creation
- `Configuration/TestConfiguration.cs`
- `Configuration/ConfigurationParser.cs`
- `Program.cs`
- `Tests/WWoW.RecordedTests.PathingTests.Tests/ProgramTests.cs`
- `Tests/WWoW.RecordedTests.Shared.Tests/GmCommandServerDesiredStateTests.cs`
- `.github/workflows/pathfinding-tests.yml`

---

## 📞 Support & Contribution

For issues or questions, refer to:
- [PATHING_TEST_LIST.md](PATHING_TEST_LIST.md) - All 20 test definitions
- [README.md](README.md) - Project overview and usage
- [CODING_STANDARDS.md](../CODING_STANDARDS.md) - Project coding conventions
- [.github/copilot-instructions.md](../.github/copilot-instructions.md) - AI development guidelines
