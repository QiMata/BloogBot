# WWoW.RecordedTests.Shared

`WWoW.RecordedTests.Shared` is a .NET 8 class library that packages the common orchestration pieces used by Westworld of Warcraft (WWoW) recorded integration tests. It wires together bot runners, screen recording, and private server discovery so individual recorded tests can focus on scripting encounters instead of bootstrapping infrastructure.

## What it Provides

- **RecordedTestOrchestrator** – coordinates server availability checks, bot runner lifecycle, and artifact collection.
- **DefaultRecordedWoWTestDescription** – ready-made implementation of the `ITestDescription` contract for the common foreground/background runner pattern used in WWoW.
- **GmCommandServerDesiredState** – declaratively applies a list of GM commands through the foreground runner so scenarios can express preparation/reset logic as MaNGOS/Trinity-style commands.
- **Abstractions** – lightweight interfaces for loggers, recorders, orchestrator options, bot runners, and server availability.
- **TrueNAS integration** – clients and helpers that poll TrueNAS Apps releases, optionally starting idle releases before a test begins.
- **Artifact helpers** – the `ArtifactPathHelper` class for sanitizing names and creating timestamped artifact directories per test run.
- **OBS stub** – a minimal `IScreenRecorder` implementation that can be swapped for a real OBS/WebSocket integration.

## Project Layout

```
WWoW.RecordedTests.Shared/
  Abstractions/                 Interfaces and option classes consumed by tests
  DefaultRecordedWoWTestDescription.cs
  ObsRecorderStub.cs
  RecordedTestOrchestrator.cs
  ServerAvailability.cs        TrueNAS-backed availability checker
  TrueNasAppsClient.cs         Thin HTTP client for TrueNAS Apps API
  LocalMangosDockerTrueNasAppsClient.cs
  TrueNasAppRelease.cs
  WWoW.RecordedTests.Shared.csproj
  RECORDED_TEST_IDEAS.md       Brainstorming list for new scenarios
```

## Getting Started

1. **Reference the project** from your recorded test host:
   ```xml
   <ItemGroup>
     <ProjectReference Include="..\WWoW.RecordedTests.Shared\WWoW.RecordedTests.Shared.csproj" />
   </ItemGroup>
   ```
2. **Implement bot runner factories.** Tests expect factories that create foreground/background bot runners. Reuse the runners in `Services/ForegroundBotRunner` and `Services/BackgroundBotRunner` or provide your own `IBotRunner` implementations and wrap them with `DelegateBotRunnerFactory`.
3. **Provide a screen recorder factory.** Swap `ObsRecorderStub` with an implementation that talks to OBS WebSocket, ffmpeg, or your recorder of choice. Implement `IScreenRecorder` and surface it through an `IScreenRecorderFactory` (or `DelegateScreenRecorderFactory`).
4. **Describe desired server states.** Supply `IServerDesiredState` implementations for the initial state your scenario requires and the base state the realm should return to after execution. `GmCommandServerDesiredState` covers the common case of running a list of GM commands, while `DelegateServerDesiredState` makes it easy to wrap ad-hoc logic around the GM runner.
5. **Configure server discovery.** Supply release descriptors in the format `releaseName|host|port[|realm]`. The orchestrator waits until one of the releases is healthy before starting the test and will try to start idle TrueNAS releases automatically.
6. **Set orchestration options.** Override `OrchestrationOptions` if you need different artifact directories, server availability timeouts, or recording safeguards.

### End-to-End Runner Helper

`RecordedTestRunner` wraps the orchestration pieces into a single entry point that matches the end-to-end checklist. Supply the factories, desired states, and server discovery configuration through `RecordedTestE2EConfiguration`, then optionally persist results with `FileSystemRecordedTestStorage`.

```csharp
var configuration = new RecordedTestE2EConfiguration
{
    TestName = "ShadowfangKeep_Reset",
    ForegroundFactory = new DelegateBotRunnerFactory(() => new ForegroundBotRunner()),
    BackgroundFactory = new DelegateBotRunnerFactory(() => new BackgroundBotRunner()),
    RecorderFactory = new DelegateScreenRecorderFactory(() => new ObsRecorderStub()),
    InitialDesiredState = new DelegateServerDesiredState("ScenarioInitial", (gm, ctx, token) => gm.PrepareServerStateAsync(ctx, token)),
    BaseDesiredState = new DelegateServerDesiredState("RealmBaseline", (gm, ctx, token) => gm.ResetServerStateAsync(ctx, token)),
    MangosAppsClient = new TrueNasAppsClient(Environment.GetRequiredEnvironmentVariable("TRUENAS_API"), Environment.GetRequiredEnvironmentVariable("TRUENAS_API_KEY")),
    ServerDefinitions = new[] { "wow-classic|10.0.0.15|3724|Alliance" },
    OrchestrationOptions = new OrchestrationOptions { ArtifactsRootDirectory = "./TestLogs" },
    ArtifactStorage = new FileSystemRecordedTestStorage("./StoredRuns"),
    AutomationRunId = Environment.GetEnvironmentVariable("BUILD_ID"),
    Metadata = new Dictionary<string, string?>
    {
        ["branch"] = Environment.GetEnvironmentVariable("GIT_BRANCH")
    }
};

var runner = new RecordedTestRunner(configuration);
var result = await runner.RunAsync(cts.Token);

Console.WriteLine(result.Success
    ? $"Artifacts stored to {result.TestRunDirectory}"
    : $"Test failed: {result.Message}");
```

### GM Command Desired States

The foreground GM runner often needs to execute a predictable sequence of commands (summoning bots, setting reputations, clearing encounters) before and after a scenario. `GmCommandServerDesiredState` captures those commands declaratively and executes them by visiting the foreground runner. Runners that support GM commands should implement `IGmCommandHost` and override `IBotRunner.AcceptVisitorAsync` so the visitor can drive command execution:

```csharp
var initialState = new GmCommandServerDesiredState(
    "ScarletMonastery_Setup",
    new[]
    {
        new GmCommandServerDesiredState.GmCommandStep(".gm on"),
        new GmCommandServerDesiredState.GmCommandStep(ctx => $".tele {ctx.TestName.Replace(' ', '_')} Tirisfal", "Teleport to arena"),
        new GmCommandServerDesiredState.GmCommandStep(".additem 12345", "Grant quest key")
    });

var baseState = new GmCommandServerDesiredState(
    "Realm_Reset",
    new[]
    {
        new GmCommandServerDesiredState.GmCommandStep(".respawn"),
        new GmCommandServerDesiredState.GmCommandStep(".server restart cancel")
    });
```

Each command can either be a literal string or a factory that inspects `IRecordedTestContext` to build dynamic arguments (for example, referencing the sanitized test name). The helper logs each command before execution, throws if a command resolves to an empty value, and stops the workflow if a GM command host reports a failure so orchestration issues surface immediately.

### Minimal Orchestration Example

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
    foregroundFactory: new DelegateBotRunnerFactory(() => new ForegroundBotRunner()),
    backgroundFactory: new DelegateBotRunnerFactory(() => new BackgroundBotRunner()),
    recorderFactory: new DelegateScreenRecorderFactory(() => new ObsRecorderStub()),
    initialDesiredState: new DelegateServerDesiredState(
        "ScenarioInitial",
        (gm, ctx, token) => gm.PrepareServerStateAsync(ctx, token)),
    baseDesiredState: new DelegateServerDesiredState(
        "RealmBaseline",
        (gm, ctx, token) => gm.ResetServerStateAsync(ctx, token)));

using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
var result = await orchestrator.RunAsync(test, cts.Token);

Console.WriteLine(result.Success
    ? $"Artifacts written to {result.TestRunDirectory} (primary recording: {result.RecordingArtifact?.FullPath})"
    : $"Test failed: {result.Message}");
```

### Local Docker TrueNAS Substitute

If you are developing locally and do not have access to a TrueNAS instance, you can emulate the API through Docker:

```csharp
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

### Recording Artifacts

`RecordedTestOrchestrator` generates folders under `OrchestrationOptions.ArtifactsRootDirectory` (default `./TestLogs`). Each test run is stored at `<SanitizedTestName>/<UTC_yyyyMMdd_HHmmss>` alongside any `TestArtifact` returned by the recorder, and the resulting path is surfaced via `OrchestrationResult.TestRunDirectory`. Use `ArtifactPathHelper.SanitizeName` or `ArtifactPathHelper.PrepareArtifactDirectories` if you need to integrate custom tooling with the orchestrated folder structure.

### Logging

Every orchestration component accepts an `ITestLogger`. The default `NullTestLogger` is intentionally silent. To integrate with Serilog or `Microsoft.Extensions.Logging`, wrap your logger and implement `ITestLogger` so the orchestrator can emit structured events.

## Automation Checklist

When promoting a recorded test to automation, make sure to:

- Supply secrets (TrueNAS API key, recorder credentials) via environment variables or a secret store.
- Run the orchestrator from a scheduled host process, CI job, or monitoring agent.
- Persist artifacts to durable storage and surface failures through telemetry or alerts.

## Extending the Library

- Swap `ObsRecorderStub` with a recorder tied to OBS WebSocket, ShadowPlay, or a custom ffmpeg pipeline.
- Implement new `IServerAvailabilityChecker` variants for other infrastructure providers.
- Build higher-level runners that execute suites of recorded tests and aggregate results.

## Related Documents

- [`RECORDED_TEST_IDEAS.md`](./RECORDED_TEST_IDEAS.md) captures scenario ideas that can be scripted with this library.
- Browse the `Services/*BotRunner` projects for concrete bot runner implementations that plug into the orchestrator.
- Review the repository root `README.md` for an overview of the larger WWoW simulation platform.
