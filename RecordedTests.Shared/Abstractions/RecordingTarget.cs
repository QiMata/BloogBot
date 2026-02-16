namespace RecordedTests.Shared.Abstractions;

public sealed record RecordingTarget(RecordingTargetType TargetType, string? WindowTitle = null, int? ProcessId = null, nint? WindowHandle = null, int? ScreenIndex = null);