using RecordedTests.Shared.Abstractions.I;
using System;

namespace RecordedTests.Shared;

public sealed class DelegateScreenRecorderFactory(Func<IScreenRecorder> factory) : IScreenRecorderFactory
{
    private readonly Func<IScreenRecorder> _factory = factory ?? throw new ArgumentNullException(nameof(factory));

    public IScreenRecorder Create() => _factory();
}
