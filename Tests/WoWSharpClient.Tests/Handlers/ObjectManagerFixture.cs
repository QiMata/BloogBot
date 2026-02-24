using BotRunner.Clients;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WoWSharpClient.Client;

namespace WoWSharpClient.Tests.Handlers;

/// <summary>
/// xUnit fixture that initializes WoWSharpObjectManager with mock dependencies.
/// Shared across all handler test classes via the "Sequential ObjectManager tests" collection.
/// </summary>
public class ObjectManagerFixture : IDisposable
{
    public readonly Mock<WoWClient> _woWClient;
    public readonly Mock<PathfindingClient> _pathfindingClient;
    private readonly ILogger<WoWSharpObjectManager> _logger = NullLoggerFactory.Instance.CreateLogger<WoWSharpObjectManager>();

    public ObjectManagerFixture()
    {
        _woWClient = new();
        _pathfindingClient = new();

        WoWSharpObjectManager.Instance.Initialize(_woWClient.Object, _pathfindingClient.Object, _logger);
    }

    public void Dispose()
    {
        WoWSharpObjectManager.Instance.Initialize(_woWClient.Object, _pathfindingClient.Object, _logger);
    }
}

[CollectionDefinition("Sequential ObjectManager tests", DisableParallelization = true)]
public class SequentialCollection : ICollectionFixture<ObjectManagerFixture> { }
