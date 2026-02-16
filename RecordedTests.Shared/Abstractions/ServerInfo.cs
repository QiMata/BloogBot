namespace RecordedTests.Shared.Abstractions;

// Core models
public sealed record ServerInfo(string Host, int Port, string? Realm = null);