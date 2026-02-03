using WWoW.RecordedTests.Shared.Abstractions.I;

namespace WWoW.RecordedTests.Shared;

public sealed class DelegateScreenRecorderFactory : IScreenRecorderFactory
{
    private readonly Func<IScreenRecorder> _factory;

    public DelegateScreenRecorderFactory(Func<IScreenRecorder> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public IScreenRecorder Create() => _factory();
}
