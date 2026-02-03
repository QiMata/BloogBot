namespace BotRunner.Interfaces;

/// <summary>
/// Interface for individual bot tasks that can be pushed onto the task stack.
/// </summary>
public interface IBotTask
{
    /// <summary>
    /// Called each tick to execute the task's logic.
    /// </summary>
    void Update();
}
