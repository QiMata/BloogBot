using Communication;

namespace BotRunner.Interfaces;

/// <summary>
/// Interface for bot profile plugins that define class-specific behaviors.
/// </summary>
public interface IBot
{
    /// <summary>
    /// Display name of the bot profile.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Filename of the bot profile DLL.
    /// </summary>
    string FileName { get; }

    /// <summary>
    /// Creates a class container with task factories for the given activity member state.
    /// </summary>
    IClassContainer GetClassContainer(WoWActivitySnapshot probe);
}
