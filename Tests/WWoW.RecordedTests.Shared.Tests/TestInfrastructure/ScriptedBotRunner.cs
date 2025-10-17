using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WWoW.RecordedTests.Shared;
using WWoW.RecordedTests.Shared.Abstractions;
using WWoW.RecordedTests.Shared.Abstractions.I;

namespace WWoW.RecordedTests.Shared.Tests.TestInfrastructure;

internal sealed class ScriptedBotRunner : IBotRunner, IGmCommandHost
{
    private readonly string _role;
    private readonly BotScript _script;
    private readonly ScenarioLog _log;
    private readonly ScenarioState _state;
    private readonly List<string> _executed = new();
    private readonly List<string> _gmCommands = new();
    private bool _disposed;

    public ScriptedBotRunner(string role, BotScript script, ScenarioLog log, ScenarioState state)
    {
        _role = role ?? throw new ArgumentNullException(nameof(role));
        _script = script ?? throw new ArgumentNullException(nameof(script));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public IReadOnlyList<string> ExecutedSteps => _executed;

    public BotScript Script => _script;

    public bool Disposed => _disposed;

    public IReadOnlyList<string> ExecutedGmCommands => _gmCommands;

    public Task ConnectAsync(ServerInfo server, CancellationToken cancellationToken)
    {
        _log.Info($"[{_role}] Connecting to {server.Host}:{server.Port} ({server.Realm})");
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken)
    {
        _log.Info($"[{_role}] Disconnecting");
        return Task.CompletedTask;
    }

    public async Task PrepareServerStateAsync(IRecordedTestContext context, CancellationToken cancellationToken)
    {
        await ExecuteStepsAsync(_script.Prepare, "Prepare", context, cancellationToken).ConfigureAwait(false);
    }

    public async Task ResetServerStateAsync(IRecordedTestContext context, CancellationToken cancellationToken)
    {
        await ExecuteStepsAsync(_script.Reset, "Reset", context, cancellationToken).ConfigureAwait(false);
    }

    public Task<RecordingTarget> GetRecordingTargetAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(new RecordingTarget(RecordingTargetType.Screen, ScreenIndex: 0));
    }

    public async Task RunTestAsync(IRecordedTestContext context, CancellationToken cancellationToken)
    {
        await ExecuteStepsAsync(_script.Run, "Execute", context, cancellationToken).ConfigureAwait(false);
    }

    public async Task ShutdownUiAsync(CancellationToken cancellationToken)
    {
        if (_lastContext is null)
        {
            return;
        }

        foreach (var step in _script.Shutdown)
        {
            _log.Info($"[{_role}] Shutdown: {step.Description}");
            await step.ExecuteAsync(_lastContext, cancellationToken).ConfigureAwait(false);
            _executed.Add($"Shutdown::{step.StepId}");
        }
    }

    public Task<GmCommandExecutionResult> ExecuteGmCommandAsync(string command, CancellationToken cancellationToken)
    {
        if (command is null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        _log.Info($"[{_role}] GM Command: {command}");
        _gmCommands.Add(command);
        return Task.FromResult(GmCommandExecutionResult.Succeeded);
    }

    async Task<TResult> IBotRunner.AcceptVisitorAsync<TResult>(IBotRunnerVisitor<TResult> visitor, CancellationToken cancellationToken)
    {
        if (visitor is IGmCommandRunnerVisitor<TResult> gmVisitor)
        {
            return await gmVisitor.VisitAsync(this, cancellationToken).ConfigureAwait(false);
        }

        return await visitor.VisitAsync(this, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return ValueTask.CompletedTask;
    }

    public void AssertPlanCompleted()
    {
        if (!_disposed)
        {
            throw new InvalidOperationException($"Runner '{_role}' should be disposed after execution.");
        }

        foreach (var step in _script.Prepare)
        {
            if (!_executed.Contains($"Prepare::{step.StepId}"))
            {
                throw new InvalidOperationException($"Runner '{_role}' did not execute preparation step '{step.StepId}'.");
            }
        }

        foreach (var step in _script.Run)
        {
            if (!_executed.Contains($"Execute::{step.StepId}"))
            {
                throw new InvalidOperationException($"Runner '{_role}' did not execute execution step '{step.StepId}'.");
            }
        }

        foreach (var step in _script.Reset)
        {
            if (!_executed.Contains($"Reset::{step.StepId}"))
            {
                throw new InvalidOperationException($"Runner '{_role}' did not execute reset step '{step.StepId}'.");
            }
        }

        foreach (var step in _script.Shutdown)
        {
            if (!_executed.Contains($"Shutdown::{step.StepId}"))
            {
                throw new InvalidOperationException($"Runner '{_role}' did not execute shutdown step '{step.StepId}'.");
            }
        }
    }

    private TestStepExecutionContext? _lastContext;

    private async Task ExecuteStepsAsync(IReadOnlyList<TestStep> steps, string stage, IRecordedTestContext context, CancellationToken cancellationToken)
    {
        _lastContext = new TestStepExecutionContext(context, _state, _log, _role);
        foreach (var step in steps)
        {
            _log.Info($"[{_role}] {stage}: {step.Description}");
            await step.ExecuteAsync(_lastContext, cancellationToken).ConfigureAwait(false);
            _executed.Add($"{stage}::{step.StepId}");
        }
    }
}
