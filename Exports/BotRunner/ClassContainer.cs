using BotRunner.Clients;
using BotRunner.Interfaces;
using Communication;
using System;

namespace BotRunner;

/// <summary>
/// Implementation of IClassContainer that holds task factories for a class/spec.
/// Also serves as the IDependencyContainer, providing pathfinding and repositories to tasks.
/// </summary>
public class ClassContainer : IClassContainer, IDependencyContainer
{
    public ClassContainer(
        string name,
        Func<IBotContext, IBotTask> createRestTask,
        Func<IBotContext, IBotTask> createBuffTask,
        Func<IBotContext, IBotTask> createMoveToTargetTask,
        Func<IBotContext, IBotTask> createPvERotationTask,
        Func<IBotContext, IBotTask> createPvPRotationTask,
        PathfindingClient pathfindingClient,
        IQuestRepository? questRepository = null)
    {
        Name = name;
        CreateRestTask = createRestTask;
        CreateBuffTask = createBuffTask;
        CreateMoveToTargetTask = createMoveToTargetTask;
        CreatePvERotationTask = createPvERotationTask;
        CreatePvPRotationTask = createPvPRotationTask;
        PathfindingClient = pathfindingClient;
        QuestRepository = questRepository;
    }

    // IClassContainer
    public string Name { get; }
    public Func<IBotContext, IBotTask> CreateRestTask { get; }
    public Func<IBotContext, IBotTask> CreateBuffTask { get; }
    public Func<IBotContext, IBotTask> CreateMoveToTargetTask { get; }
    public Func<IBotContext, IBotTask> CreatePvERotationTask { get; }
    public Func<IBotContext, IBotTask> CreatePvPRotationTask { get; }

    // IDependencyContainer
    public PathfindingClient PathfindingClient { get; }
    public IQuestRepository? QuestRepository { get; }
    IClassContainer IDependencyContainer.ClassContainer => this;
}
