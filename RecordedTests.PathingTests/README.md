# WWoW.RecordedTests.PathingTests

Automated pathing test suite for Westworld of Warcraft bot. This project validates bot navigation capabilities across various scenarios including point-to-point travel, transport usage (boats/zeppelins), cave navigation, and complex terrain handling.

## Overview

This test suite uses the orchestration framework from `WWoW.RecordedTests.Shared` to execute automated pathing tests with screen recording and artifact storage. Each test validates that the bot can successfully navigate from point A to point B under different conditions.

## Features

- **20 comprehensive pathing tests** covering basic to advanced scenarios
- **Transport integration** tests for boats and zeppelins
- **Cave navigation** tests for enclosed spaces and dungeons
- **Terrain challenges** including mountains, water, and bridges
- **Edge case handling** for stuck recovery, aggro avoidance, and path recalculation
- **Configurable via CLI, environment, or config files**
- **OBS-based screen recording** of all test executions
- **Multiple storage backends** (filesystem, S3, Azure Blob)

## Quick Start

### Prerequisites

1. .NET 8 SDK
2. OBS Studio with obs-websocket plugin (for recording)
3. Running Mangos/WoW server (TrueNAS-hosted or local Docker)
4. Configured foreground and background bot runners

### Running Tests

```bash
# Basic execution (uses environment variables for configuration)
dotnet run --project WWoW.RecordedTests.PathingTests

# With CLI arguments
dotnet run --project WWoW.RecordedTests.PathingTests \
  --truenas-api https://truenas.example.com/api/v2.0 \
  --truenas-api-key YOUR_API_KEY \
  --servers "wow-classic|10.0.0.15|3724|Alliance" \
  --artifacts-root ./TestRecordings \
  --server-timeout 10
```

### Configuration Options

#### CLI Arguments

- `--truenas-api <url>` - TrueNAS API base URL
- `--truenas-api-key <key>` - TrueNAS API authentication key
- `--servers <definitions>` - Semicolon-separated server definitions (format: `releaseName|host|port[|realm]`)
- `--artifacts-root <path>` - Root directory for test artifacts (default: `./TestLogs`)
- `--server-timeout <minutes>` - Server availability timeout in minutes (default: 5)
- `--double-stop-recorder` - Enable double-stop for recorder safety
- `--no-double-stop-recorder` - Disable double-stop

#### Environment Variables

- `TRUENAS_API` - TrueNAS API base URL
- `TRUENAS_API_KEY` - TrueNAS API key
- `SERVER_DEFINITIONS` - Semicolon-separated server definitions
- `ARTIFACTS_ROOT` - Artifact storage root directory
- `SERVER_TIMEOUT_MINUTES` - Server availability timeout
- `DOUBLE_STOP_RECORDER` - Enable/disable double-stop (true/false)
- `OBS_EXECUTABLE_PATH` - Path to OBS executable
- `OBS_WEBSOCKET_URL` - OBS WebSocket URL (default: ws://localhost:4455)
- `OBS_WEBSOCKET_PASSWORD` - OBS WebSocket password
- `OBS_RECORDING_PATH` - OBS recording output directory
- `OBS_AUTO_LAUNCH` - Auto-launch OBS (true/false)

#### Configuration Precedence

Configuration is resolved in the following order (highest to lowest priority):
1. **CLI arguments** - Highest priority
2. **Environment variables** - Medium priority
3. **Config file** - Lowest priority
4. **Defaults** - Fallback values

## Test Categories

See [PATHING_TEST_LIST.md](PATHING_TEST_LIST.md) for detailed test definitions.

### Basic Point-to-Point (3 tests)
- Short, medium, and long-distance navigation
- Road following and simple terrain

### Transport Tests (4 tests)
- Boat travel (Menethil-Auberdine, Ratchet-BootyBay)
- Zeppelin travel (Orgrimmar-Undercity, Undercity-GromGol)

### Cave Navigation (3 tests)
- Simple caves (Fargodeep Mine)
- Complex dungeons (Deadmines, Wailing Caverns)

### Terrain Challenges (3 tests)
- Mountain climbing
- Water navigation and swimming
- Bridge crossing

### Advanced Multi-Segment (3 tests)
- Alliance capital cities tour
- Horde capital cities tour
- Cross-continent long-distance travel

### Edge Cases (4 tests)
- Stuck recovery
- Aggro avoidance
- Night/low visibility navigation
- Rapid path recalculation

## Architecture

### Project Structure

```
WWoW.RecordedTests.PathingTests/
├── Program.cs                    # Orchestration entry point
├── PATHING_TEST_LIST.md          # Detailed test definitions
├── README.md                     # This file
└── WWoW.RecordedTests.PathingTests.csproj
```

### Dependencies

- **WWoW.RecordedTests.Shared** - Core orchestration framework
- **ForegroundBotRunner** (implicit) - GM-capable bot for setup/teardown
- **BackgroundBotRunner** (implicit) - Test execution bot

### Workflow

```
Program.cs
    ↓
Parse Configuration (CLI → env → config)
    ↓
Create Mangos Client (TrueNAS or Local Docker)
    ↓
Create Server Availability Checker
    ↓
Create Orchestrator
    ↓
For Each Test:
    ↓
RecordedTestOrchestrator.RunAsync()
    ↓
    ├─ Wait for Available Server
    ├─ Create Foreground Runner (GM)
    ├─ Create Background Runner (Test)
    ├─ Create Screen Recorder (OBS)
    ├─ Connect Runners
    ├─ Launch & Configure Recorder
    ├─ Prepare Server State (GM Commands)
    ├─ Start Recording
    ├─ Execute Test (Pathing)
    ├─ Stop Recording
    ├─ Reset Server State (GM Commands)
    ├─ Move Recording to Artifacts
    └─ Cleanup
    ↓
Exit with Success/Failure Code
```

## Extending the Test Suite

### Adding a New Test

1. Define test in `PATHING_TEST_LIST.md`:
   - Setup commands
   - Start/end positions
   - Expected behavior
   - Teardown commands

2. Implement test description in `Program.cs`:
```csharp
private static IEnumerable<ITestDescription> DefinePathingTests(PathingTestConfiguration configuration)
{
    yield return new DefaultRecordedWoWTestDescription(
        name: "MyNewPathingTest",
        createForegroundRunner: () => new ForegroundBotRunner(),
        createBackgroundRunner: () => new BackgroundBotRunner(),
        createRecorder: () => ObsScreenRecorderFactory.CreateFromEnvironment(),
        options: configuration.OrchestrationOptions,
        logger: new ConsoleTestLogger());
}
```

### Custom Test Descriptions

For tests requiring special handling, implement `ITestDescription`:

```csharp
public class CustomPathingTest : ITestDescription
{
    public string Name => "CustomTest";

    public async Task<OrchestrationResult> ExecuteAsync(
        ServerInfo server,
        CancellationToken cancellationToken)
    {
        // Custom test logic
    }
}
```

## Storage Backends

### Filesystem (Default)

Artifacts stored locally in hierarchical structure:
```
TestLogs/
└── TestName/
    └── 20260119_143022/
        └── TestName.mkv
```

### S3-Compatible Storage

Requires `AWSSDK.S3` package (not yet installed):
```bash
dotnet add package AWSSDK.S3
```

Configuration via environment:
- `S3_BUCKET_NAME`
- `AWS_ACCESS_KEY_ID`
- `AWS_SECRET_ACCESS_KEY`
- `S3_SERVICE_URL` (for MinIO, etc.)

### Azure Blob Storage

Requires `Azure.Storage.Blobs` package (not yet installed):
```bash
dotnet add package Azure.Storage.Blobs
```

Configuration via environment:
- `AZURE_STORAGE_CONNECTION_STRING`
- `AZURE_STORAGE_CONTAINER_NAME`

## Troubleshooting

### OBS Not Recording

1. Verify OBS is installed and obs-websocket plugin is enabled
2. Check `OBS_WEBSOCKET_URL` environment variable
3. Ensure OBS WebSocket server is running (Tools → WebSocket Server Settings)
4. Verify recording output path in OBS settings

### Server Not Found

1. Check TrueNAS API credentials
2. Verify server definitions format: `releaseName|host|port[|realm]`
3. Ensure servers are running and accessible
4. Check firewall rules for server ports

### Test Failures

1. Check test logs in artifacts directory
2. Review recorded video for visual debugging
3. Verify GM commands executed correctly
4. Check bot runner logs for navigation errors

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Pathing Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
      - name: Run Pathing Tests
        env:
          TRUENAS_API: ${{ secrets.TRUENAS_API }}
          TRUENAS_API_KEY: ${{ secrets.TRUENAS_API_KEY }}
          SERVER_DEFINITIONS: ${{ secrets.SERVER_DEFINITIONS }}
        run: dotnet run --project WWoW.RecordedTests.PathingTests
      - name: Upload Artifacts
        uses: actions/upload-artifact@v3
        with:
          name: test-recordings
          path: TestLogs/
```

## Performance Considerations

- **Sequential execution**: Tests run one at a time to avoid resource conflicts
- **Server pooling**: Multiple servers can be configured for faster test cycles
- **Recording overhead**: OBS recording adds ~5-10% CPU overhead
- **Estimated runtime**: Full suite takes 6-8 hours sequentially

## Future Enhancements

- [ ] Parallel test execution with server pooling
- [ ] Test result database for historical analysis
- [ ] Automatic failure screenshot capture
- [ ] Performance metrics collection (FPS, memory, CPU)
- [ ] Test retry logic for flaky tests
- [ ] Custom bot profiles per test
- [ ] Real-time test progress dashboard

## Contributing

When adding new tests:
1. Document in `PATHING_TEST_LIST.md` first
2. Use consistent naming conventions
3. Include setup/teardown commands
4. Specify expected duration
5. Add to appropriate test category

## License

Same as parent WWoW project.

---

*This component is part of the WWoW (Westworld of Warcraft) simulation platform.*
