using System;

namespace BotRunner;

public static class RecordingArtifactsFeature
{
    public const string EnvironmentVariableName = "WWOW_ENABLE_RECORDING_ARTIFACTS";

    public static bool IsEnabled()
    {
        var value = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value.Equals("on", StringComparison.OrdinalIgnoreCase);
    }
}
