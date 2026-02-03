namespace BotRunner.Interfaces;

/// <summary>
/// Container for class-specific task factories.
/// </summary>
public interface IClassContainer
{
    /// <summary>
    /// Display name of the class/spec.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Factory to create rest tasks.
    /// </summary>
    Func<IBotContext, IBotTask> CreateRestTask { get; }

    /// <summary>
    /// Factory to create buff tasks.
    /// </summary>
    Func<IBotContext, IBotTask> CreateBuffTask { get; }

    /// <summary>
    /// Factory to create move-to-target tasks.
    /// </summary>
    Func<IBotContext, IBotTask> CreateMoveToTargetTask { get; }

    /// <summary>
    /// Factory to create PvE rotation tasks.
    /// </summary>
    Func<IBotContext, IBotTask> CreatePvERotationTask { get; }

    /// <summary>
    /// Factory to create PvP rotation tasks.
    /// </summary>
    Func<IBotContext, IBotTask> CreatePvPRotationTask { get; }
}
