namespace BotRunner.Constants;

/// <summary>
/// Shared timeout constants used across multiple bot tasks.
/// Task-specific timeouts that appear in only one file should remain local to that task.
/// </summary>
public static class BotTaskTimeouts
{
    /// <summary>
    /// How long to wait for a pathfinding result before giving up navigation.
    /// Used by GoToTask, RetrieveCorpseTask, and InteractWithUnitTask.
    /// </summary>
    public const double NoPathTimeoutSec = 30.0;

    /// <summary>Same value as <see cref="NoPathTimeoutSec"/> expressed in milliseconds.</summary>
    public const int NoPathTimeoutMs = 30_000;

    /// <summary>
    /// Maximum duration for a corpse retrieval run before the task is abandoned.
    /// </summary>
    public const int CorpseRetrievalMinutes = 12;

    /// <summary>
    /// Default interaction timeout (e.g., navigating to and interacting with an NPC).
    /// </summary>
    public const int InteractWithUnitSec = 30;
}
