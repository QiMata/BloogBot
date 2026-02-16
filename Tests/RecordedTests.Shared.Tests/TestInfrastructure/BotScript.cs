using System;
using System.Collections.Generic;

namespace RecordedTests.Shared.Tests.TestInfrastructure;

public sealed class BotScript(
    IReadOnlyList<TestStep> prepare,
    IReadOnlyList<TestStep> run,
    IReadOnlyList<TestStep> reset,
    IReadOnlyList<TestStep> shutdown)
{
    public IReadOnlyList<TestStep> Prepare { get; } = prepare ?? throw new ArgumentNullException(nameof(prepare));

    public IReadOnlyList<TestStep> Run { get; } = run ?? throw new ArgumentNullException(nameof(run));

    public IReadOnlyList<TestStep> Reset { get; } = reset ?? throw new ArgumentNullException(nameof(reset));

    public IReadOnlyList<TestStep> Shutdown { get; } = shutdown ?? throw new ArgumentNullException(nameof(shutdown));
}
