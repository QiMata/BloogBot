using System;
using System.Collections.Generic;

namespace RecordedTests.Shared.Tests.TestInfrastructure;

public sealed class BotScript
{
    public BotScript(
        IReadOnlyList<TestStep> prepare,
        IReadOnlyList<TestStep> run,
        IReadOnlyList<TestStep> reset,
        IReadOnlyList<TestStep> shutdown)
    {
        Prepare = prepare ?? throw new ArgumentNullException(nameof(prepare));
        Run = run ?? throw new ArgumentNullException(nameof(run));
        Reset = reset ?? throw new ArgumentNullException(nameof(reset));
        Shutdown = shutdown ?? throw new ArgumentNullException(nameof(shutdown));
    }

    public IReadOnlyList<TestStep> Prepare { get; }

    public IReadOnlyList<TestStep> Run { get; }

    public IReadOnlyList<TestStep> Reset { get; }

    public IReadOnlyList<TestStep> Shutdown { get; }
}
