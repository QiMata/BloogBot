using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RecordedTests.Shared;
using RecordedTests.Shared.Abstractions;
using RecordedTests.Shared.Abstractions.I;
using RecordedTests.Shared.Tests.Scenarios;
using RecordedTests.Shared.Tests.TestInfrastructure;
using Xunit;

namespace RecordedTests.Shared.Tests;

public sealed class RecordedScenarioTests
{
    public static IEnumerable<object[]> ScenarioData => new RecordedTestScenario[]
    {
        new NorthshireValleyHumanIntroScenario(),
        new ElwynnForestHoggerScenario(),
        new WestfallDeadminesAttunementScenario()
    }.Select(s => new object[] { s });

    [Theory]
    [MemberData(nameof(ScenarioData))]
    public async Task ExecuteScenario_CompletesScriptedPlan(RecordedTestScenario scenario)
    {
        using var tempDir = new TempDirectory();
        var log = new ScenarioLog();
        var state = new ScenarioState();
        var server = LoadServerInfo();

        var foregroundRunners = new List<ScriptedBotRunner>();
        var backgroundRunners = new List<ScriptedBotRunner>();

        IBotRunnerFactory createForegroundFactory = new DelegateBotRunnerFactory(() =>
        {
            var runner = new ScriptedBotRunner(
                "Foreground GM",
                scenario.CreateForegroundScript(state, log),
                log,
                state);
            foregroundRunners.Add(runner);
            return runner;
        });

        IBotRunnerFactory createBackgroundFactory = new DelegateBotRunnerFactory(() =>
        {
            var runner = new ScriptedBotRunner(
                "Background Adventurer",
                scenario.CreateBackgroundScript(state, log),
                log,
                state);
            backgroundRunners.Add(runner);
            return runner;
        });

        var initialDesiredState = new TestDesiredState("Initial", log);
        var baseDesiredState = new TestDesiredState("Base", log);

        var description = new DefaultRecordedWoWTestDescription(
            scenario.Name,
            createForegroundFactory,
            createBackgroundFactory,
            options: new OrchestrationOptions
            {
                DoubleStopRecorderForSafety = false
            },
            initialDesiredState: initialDesiredState,
            baseDesiredState: baseDesiredState,
            logger: log);

        var orchestrator = new RecordedTestOrchestrator(
            new ImmediateServerAvailabilityChecker(server),
            new OrchestrationOptions
            {
                ArtifactsRootDirectory = tempDir.Path
            },
            log);

        var result = await orchestrator.RunAsync(description, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains(scenario.Name, result.Message, StringComparison.Ordinal);
        Assert.Null(result.RecordingArtifact);

        Assert.NotNull(result.TestRunDirectory);
        Assert.True(Directory.Exists(result.TestRunDirectory));

        Assert.Single(Directory.GetDirectories(tempDir.Path));
        var scenarioRoot = Directory.GetDirectories(tempDir.Path).Single();
        Assert.Contains(ArtifactPathHelper.SanitizeName(scenario.Name), Path.GetFileName(scenarioRoot), StringComparison.Ordinal);
        var runDirectories = Directory.GetDirectories(scenarioRoot);
        Assert.Single(runDirectories);
        Assert.Equal(result.TestRunDirectory, runDirectories.Single());

        foreach (var runner in foregroundRunners.Concat(backgroundRunners))
        {
            runner.AssertPlanCompleted();
        }

        Assert.Equal(scenario.ExpectedStepIds.Count, state.CompletedSteps.Count);
        foreach (var expectedStep in scenario.ExpectedStepIds)
        {
            Assert.Contains(expectedStep, state.CompletedSteps);
        }

        var executedDescriptions = foregroundRunners
            .Concat(backgroundRunners)
            .SelectMany(r => r.ExecutedSteps);
        Assert.NotEmpty(executedDescriptions);

        Assert.Equal(foregroundRunners.Count, initialDesiredState.ApplyCalls);
        Assert.Equal(foregroundRunners.Count, baseDesiredState.ApplyCalls);
    }

    private static ServerInfo LoadServerInfo()
    {
        var host = Environment.GetEnvironmentVariable("WWOW_RECORDED_TEST_HOST");
        if (string.IsNullOrWhiteSpace(host))
        {
            host = "127.0.0.1";
        }

        var portValue = Environment.GetEnvironmentVariable("WWOW_RECORDED_TEST_PORT");
        var port = 3724;
        if (!string.IsNullOrWhiteSpace(portValue) && int.TryParse(portValue, out var parsed))
        {
            port = parsed;
        }

        var realm = Environment.GetEnvironmentVariable("WWOW_RECORDED_TEST_REALM");
        if (string.IsNullOrWhiteSpace(realm))
        {
            realm = "RecordedTestsRealm";
        }

        return new ServerInfo(host, port, realm);
    }

    private sealed class ImmediateServerAvailabilityChecker : IServerAvailabilityChecker
    {
        private readonly ServerInfo _serverInfo;

        public ImmediateServerAvailabilityChecker(ServerInfo serverInfo)
        {
            _serverInfo = serverInfo;
        }

        public Task<ServerInfo?> WaitForAvailableAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return Task.FromResult<ServerInfo?>(_serverInfo);
        }
    }

    private sealed class TestDesiredState : IServerDesiredState
        {
            private readonly ITestLogger _logger;

            public TestDesiredState(string name, ITestLogger logger)
            {
                Name = name;
                _logger = logger;
            }

            public string Name { get; }

            public int ApplyCalls { get; private set; }

            public Task ApplyAsync(IBotRunner gmRunner, IRecordedTestContext context, CancellationToken cancellationToken)
            {
                _logger.Info($"[State] {Name}: applying for '{context.TestName}'");
                ApplyCalls++;
                return Task.CompletedTask;
            }

            public Task RevertAsync(IBotRunner runner, IRecordedTestContext context, CancellationToken cancellationToken)
            {
                _logger.Info($"[State] {Name}: reverting for '{context.TestName}'");
                return Task.CompletedTask;
            }
        }
    }
