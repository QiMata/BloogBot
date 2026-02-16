using RecordedTests.Shared.Abstractions.I;
using System;

namespace RecordedTests.Shared;

public sealed class DelegateBotRunnerFactory(Func<IBotRunner> factory) : IBotRunnerFactory
{
    private readonly Func<IBotRunner> _factory = factory ?? throw new ArgumentNullException(nameof(factory));

    public IBotRunner Create() => _factory();
}
