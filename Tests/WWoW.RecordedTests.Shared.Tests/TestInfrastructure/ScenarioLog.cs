using System;
using System.Collections.Generic;
using WWoW.RecordedTests.Shared.Abstractions.I;

namespace WWoW.RecordedTests.Shared.Tests.TestInfrastructure;

public sealed class ScenarioLog : ITestLogger
{
    private readonly List<string> _info = new();
    private readonly List<string> _warnings = new();
    private readonly List<string> _errors = new();

    public IReadOnlyList<string> InfoMessages => _info;
    public IReadOnlyList<string> WarningMessages => _warnings;
    public IReadOnlyList<string> ErrorMessages => _errors;

    public void Info(string message)
    {
        if (message is null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        _info.Add(message);
    }

    public void Warn(string message)
    {
        if (message is null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        _warnings.Add(message);
    }

    public void Error(string message, Exception? ex = null)
    {
        if (message is null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        var formatted = ex is null ? message : $"{message} :: {ex.Message}";
        _errors.Add(formatted);
    }
}
