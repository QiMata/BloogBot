# WWoW.RecordedTests.Shared

`WWoW.RecordedTests.Shared` is a .NET 8 class library that provides the shared orchestration plumbing used by Westworld of Warcraft (WWoW) recorded integration tests. It coordinates foreground/background bot runners, manages screen recording, and discovers available private servers before a test starts.

## Highlights
- Encapsulates test orchestration with `RecordedTestOrchestrator` and `DefaultRecordedWoWTestDescription`.
- Lightweight abstractions for runners, recording, logging, orchestration context & results (`Abstractions/`).
- Polls TrueNAS Apps releases and automatically starts idle servers via `TrueNasAppServerAvailabilityChecker` (`ServerAvailability.cs`).
- Minimal `ObsRecorderStub` that can be swapped for a real OBS/WebSocket integration.
- Produces timestamped artifact folders for recorded test runs with simple sanitization helpers.
- Optional `LocalMangosDockerTrueNasAppsClient` to spin up Mangos via Docker when TrueNAS is unavailable.

## Project Layout
```
WWoW.RecordedTests.Shared/
  Abstractions/
    I/
    IRecordedTestContext.cs
    IScreenRecorder.cs
    IServerAvailabilityChecker.cs
    ITestDescription.cs
    ITestLogger.cs
    ITrueNasAppsClient.cs
    NullTestLogger.cs
    OrchestrationOptions.cs
    OrchestrationResult.cs
    RecordedTestContext.cs
    RecordingTarget.cs
    RecordingTargetType.cs
    ServerInfo.cs
    TestArtifact.cs
  DefaultRecordedWoWTestDescription.cs
  RecordedTestOrchestrator.cs          # Orchestrates server wait + test execution
  ServerAvailability.cs                # TrueNasAppServerAvailabilityChecker implementation
  TrueNasAppsClient.cs                 # Thin HTTP client for TrueNAS Apps API
  LocalMangosDockerTrueNasAppsClient.cs# Local Docker-backed TrueNAS client for Mangos
  TrueNasAppRelease.cs                 # Shared record describing release state
  ObsRecorderStub.cs                   # Example screen recorder implementation
  WWoW.RecordedTests.Shared.csproj
  README.md
```

## Getting Started
1. Reference the project from your test host:
   ```xml
   <ItemGroup>
     <ProjectReference Include="..\WWoW.RecordedTests.Shared\WWoW.RecordedTests.Shared.csproj" />
   </ItemGroup>
   ```
2. Implement concrete `IBotRunner` types (or reuse ones in `Services/ForegroundBotRunner` & `Services/BackgroundBotRunner`).
3. Provide an `IScreenRecorder` implementation (replace `ObsRecorderStub`).
4. Supply TrueNAS server definitions: `releaseName|host|port[|realm]`.
5. Instantiate `RecordedTestOrchestrator` with an `IServerAvailabilityChecker` implementation.

### Quick Orchestration Example
```csharp
using WWoW.RecordedTests.Shared;
using WWoW.RecordedTests.Shared.Abstractions.I;

var truenasClient = new TrueNasAppsClient(
    baseAddress: Environment.GetRequiredEnvironmentVariable("TRUENAS_API"),
    apiKey: Environment.GetRequiredEnvironmentVariable("TRUENAS_API_KEY"));

var serverChecker = new TrueNasAppServerAvailabilityChecker(
    client: truenasClient,
    serverDefinitions: new[]
    {
        "wow-classic|10.0.0.15|3724|Alliance",
        "wow-classic-backup|10.0.0.16|3724"
    });

var orchestrator = new RecordedTestOrchestrator(serverChecker);

var test = new DefaultRecordedWoWTestDescription(
    name: "ShadowfangKeep_Reset",
    createForegroundRunner: () => new ForegroundBotRunner(),
    createBackgroundRunner: () => new BackgroundBotRunner(),
    createRecorder: () => new ObsRecorderStub());

using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
var result = await orchestrator.RunAsync(test, cts.Token);

Console.WriteLine(result.Success
    ? $"Recorded artifact stored at {result.RecordingArtifact?.FullPath}"
    : $"Test failed: {result.Message}");
```

### Local Docker Mangos Example
```csharp
using System.Collections.Generic;
using WWoW.RecordedTests.Shared;

var dockerClient = new LocalMangosDockerTrueNasAppsClient(new[]
{
    new LocalMangosDockerConfiguration(
        releaseName: "mangos-local",
        image: "azerothcore/azerothcore-wotlk:latest",
        hostPort: 3724,
        containerPort: 3724,
        environment: new Dictionary<string, string>
        {
            ["AC_WORLD__REALMLIST"] = "127.0.0.1"
        })
});

var dockerBackedChecker = new TrueNasAppServerAvailabilityChecker(
    dockerClient,
    new[] { "mangos-local|127.0.0.1|3724" });
```

### Server Discovery
`TrueNasAppServerAvailabilityChecker` calls `TrueNasAppsClient.GetReleaseAsync` until it finds a release that is running, not checked out, and has connection info. If a release is idle it issues `StartReleaseAsync` and continues polling until `OrchestrationOptions.ServerAvailabilityTimeout` (default 5 minutes).

Definition format: `releaseName|host|port[|realm]`. Host/port act as fallbacks when release metadata is missing values.

### Recording Artifacts
`DefaultRecordedWoWTestDescription` creates per-run folders under `OrchestrationOptions.ArtifactsRootDirectory` (default `./TestLogs`). Each folder: `<SanitizedTestName>/<UTC_yyyyMMdd_HHmmss>` and will contain any `TestArtifact` returned by the recorder.

### Logging
All orchestration components accept an `ITestLogger`. Default is `NullTestLogger`. Integrate with Serilog or `Microsoft.Extensions.Logging` by adapting to `ITestLogger`.

## Automation Considerations
- The README currently guides you through wiring up the orchestration primitives, but a fully automated workflow also needs a host process (CI pipeline, scheduled job, or orchestrator service) that executes `RecordedTestOrchestrator` on a cadence and manages required secrets (TrueNAS API key, recorder credentials) via environment variables or a vault. Without this automation hook the process remains manual.

## Extending
- Replace `ObsRecorderStub` with OBS (obs-websocket), ShadowPlay, or ffmpeg-based recorder.
- Add new `IBotRunner` roles (e.g., multi-character coordination, spectator).
- Extend `TrueNasAppsClient` for stop/checkout endpoints if needed.
- Wrap `RecordedTestOrchestrator` in higher-level suite runner or scheduling framework.

## Key Abstractions (Summary)
- `ITestDescription` – Creates runners/recorder & executes ordered test flow.
- `IServerAvailabilityChecker` – Blocks until a suitable `ServerInfo` is available.
- `IScreenRecorder` – Launch/configure/start/stop/move recording artifacts.
- `IBotRunner` (external project) – Connects to server, prepares & resets state, executes test logic.
- `OrchestrationOptions` – Timeouts, artifact root path, recorder safety flags.

## Development Notes
- Follows repository coding standards in `CODING_STANDARDS.md`.
- Build: `dotnet build WestworldOfWarcraft.sln`.
- Use an integration test host to exercise orchestration end-to-end.



