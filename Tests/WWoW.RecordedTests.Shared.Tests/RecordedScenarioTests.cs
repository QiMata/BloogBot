using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WWoW.RecordedTests.Shared;
using WWoW.RecordedTests.Shared.Abstractions;
using WWoW.RecordedTests.Shared.Abstractions.I;
using WWoW.RecordedTests.Shared.Tests.Scenarios;
using WWoW.RecordedTests.Shared.Tests.TestInfrastructure;
using Xunit;

namespace WWoW.RecordedTests.Shared.Tests;

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

        Func<IBotRunner> createForeground = () =>
        {
            var runner = new ScriptedBotRunner(
                "Foreground GM",
                scenario.CreateForegroundScript(state, log),
                log,
                state);
            foregroundRunners.Add(runner);
            return runner;
        };

        Func<IBotRunner> createBackground = () =>
        {
            var runner = new ScriptedBotRunner(
                "Background Adventurer",
                scenario.CreateBackgroundScript(state, log),
                log,
                state);
            backgroundRunners.Add(runner);
            return runner;
        };

        var description = new DefaultRecordedWoWTestDescription(
            scenario.Name,
            createForeground,
            createBackground,
            options: new OrchestrationOptions
            {
                ArtifactsRootDirectory = tempDir.Path,
                DoubleStopRecorderForSafety = false
            },
            logger: log);

        var result = await description.ExecuteAsync(server, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains(scenario.Name, result.Message, StringComparison.Ordinal);
        Assert.Null(result.RecordingArtifact);

        Assert.Single(Directory.GetDirectories(tempDir.Path));
        var scenarioRoot = Directory.GetDirectories(tempDir.Path).Single();
        Assert.Contains(SanitizeName(scenario.Name), Path.GetFileName(scenarioRoot), StringComparison.Ordinal);
        Assert.Single(Directory.GetDirectories(scenarioRoot));

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

    private static string SanitizeName(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(c, '_');
        }

        return value;
    }
}
