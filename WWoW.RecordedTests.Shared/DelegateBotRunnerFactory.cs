using System;
using WWoW.RecordedTests.Shared.Abstractions.I;

namespace WWoW.RecordedTests.Shared;

public sealed class DelegateBotRunnerFactory : IBotRunnerFactory
{
    private readonly Func<IBotRunner> _factory;

    public DelegateBotRunnerFactory(Func<IBotRunner> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public IBotRunner Create() => _factory();
}
