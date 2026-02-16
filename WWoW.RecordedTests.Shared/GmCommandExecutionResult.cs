namespace WWoW.RecordedTests.Shared;

public readonly record struct GmCommandExecutionResult(bool Success, string? ErrorMessage = null)
{
    public static GmCommandExecutionResult Succeeded { get; } = new(true, null);

    public static GmCommandExecutionResult Failed(string? errorMessage = null)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            errorMessage = "GM command execution failed.";
        }

        return new GmCommandExecutionResult(false, errorMessage);
    }
}
