namespace WWoW.RecordedTests.Shared;

public sealed record TrueNasAppRelease(string Name, bool IsRunning, bool IsCheckedOut, string? Host, int? Port, string? Realm)
{
    public bool HasConnectionInfo => !string.IsNullOrWhiteSpace(Host) && Port.HasValue;
}
