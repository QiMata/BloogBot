namespace RecordedTests.Shared;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RecordedTests.Shared.Abstractions;
using RecordedTests.Shared.Abstractions.I;

public sealed class GmCommandServerDesiredState : IServerDesiredState
{
    private readonly IReadOnlyList<GmCommandStep> _commands;
    private readonly ITestLogger _logger;

    public GmCommandServerDesiredState(string name, IEnumerable<GmCommandStep> commands, ITestLogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("State name is required.", nameof(name));
        }

        if (commands is null)
        {
            throw new ArgumentNullException(nameof(commands));
        }

        var materialized = commands.ToArray();
        if (materialized.Length == 0)
        {
            throw new ArgumentException("At least one GM command must be provided.", nameof(commands));
        }

        foreach (var step in materialized)
        {
            if (step is null)
            {
                throw new ArgumentException("GM command steps cannot be null.", nameof(commands));
            }
        }

        Name = name;
        _commands = new ReadOnlyCollection<GmCommandStep>(materialized);
        _logger = logger ?? new NullTestLogger();
    }

    public string Name { get; }

    public async Task ApplyAsync(IBotRunner gmRunner, IRecordedTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gmRunner);
        ArgumentNullException.ThrowIfNull(context);

        var resolvedCommands = new List<(string Command, string Description)>();

        foreach (var command in _commands)
        {
            var text = command.Resolve(context);
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException($"GM command resolved to an empty value for state '{Name}'.");
            }

            var description = string.IsNullOrWhiteSpace(command.Description)
                ? text
                : $"{command.Description}: {text}";
            resolvedCommands.Add((text, description));
        }

        // Check if the runner supports GM commands directly
        if (gmRunner is IGmCommandHost gmHost)
        {
            foreach (var (command, description) in resolvedCommands)
            {
                _logger.Info($"[DesiredState:{Name}] Executing GM command {description}");
                var result = await gmHost.ExecuteGmCommandAsync(command, cancellationToken).ConfigureAwait(false);
                if (!result.Success)
                {
                    throw new InvalidOperationException($"GM command '{command}' failed while applying desired state '{Name}': {result.ErrorMessage}");
                }
            }
        }
        else
        {
            throw new InvalidOperationException("GM runner must support GM command execution to use this desired state.");
        }
    }

    public Task RevertAsync(IBotRunner runner, IRecordedTestContext context, CancellationToken cancellationToken)
    {
        // GM commands typically don't have automatic revert logic
        // Subclasses can override if they need specific revert behavior
        return Task.CompletedTask;
    }

    public sealed class GmCommandStep
    {
        private readonly Func<IRecordedTestContext, string> _commandFactory;

        public GmCommandStep(string command, string? description = null)
            : this(_ => command ?? throw new ArgumentNullException(nameof(command)), description)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                throw new ArgumentException("Command must be provided.", nameof(command));
            }
        }

        public GmCommandStep(Func<IRecordedTestContext, string> commandFactory, string? description = null)
        {
            _commandFactory = commandFactory ?? throw new ArgumentNullException(nameof(commandFactory));
            Description = description;
        }

        public string? Description { get; }

        public string Resolve(IRecordedTestContext context)
                {
                    ArgumentNullException.ThrowIfNull(context);
                    return _commandFactory(context);
                }
            }
        }
