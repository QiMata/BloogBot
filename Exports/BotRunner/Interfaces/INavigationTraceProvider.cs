using BotRunner.Movement;

namespace BotRunner.Interfaces;

/// <summary>
/// Exposes a task-owned navigation trace snapshot for live diagnostics and test recording.
/// </summary>
public interface INavigationTraceProvider
{
    NavigationTraceSnapshot? GetNavigationTraceSnapshot();
}
