using BotRunner.Interfaces;
using Communication;

namespace BotRunner;

/// <summary>
/// Implementation of IClassContainer that holds task factories for a class/spec.
/// </summary>
public class ClassContainer : IClassContainer
{
    public string Name { get; }
    public Func<IBotContext, IBotTask> CreateRestTask { get; }
    public Func<IBotContext, IBotTask> CreateBuffTask { get; }
    public Func<IBotContext, IBotTask> CreateMoveToTargetTask { get; }
    public Func<IBotContext, IBotTask> CreatePvERotationTask { get; }
    public Func<IBotContext, IBotTask> CreatePvPRotationTask { get; }

    public ClassContainer(
        string name,
        Func<IBotContext, IBotTask> createRestTask,
        Func<IBotContext, IBotTask> createBuffTask,
        Func<IBotContext, IBotTask> createMoveToTargetTask,
        Func<IBotContext, IBotTask> createPvERotationTask,
        Func<IBotContext, IBotTask> createPvPRotationTask,
        WoWActivitySnapshot? probe = null)
    {
        Name = name;
        CreateRestTask = createRestTask;
        CreateBuffTask = createBuffTask;
        CreateMoveToTargetTask = createMoveToTargetTask;
        CreatePvERotationTask = createPvERotationTask;
        CreatePvPRotationTask = createPvPRotationTask;
    }
}
