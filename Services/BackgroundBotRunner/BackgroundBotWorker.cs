using System.Reactive.Linq;
using BotRunner;
using BotRunner.Clients;
using BotRunner.Combat;
using BotRunner.Movement;
using GameData.Core.Interfaces;
using WoWSharpClient;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;

namespace BackgroundBotRunner
{
    public class BackgroundBotWorker : BackgroundService
    {
        private readonly ILogger<BackgroundBotWorker> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly PathfindingClient _pathfindingClient;
        private readonly CharacterStateUpdateClient _characterStateUpdateClient;
        private readonly WoWClient _wowClient;
        private readonly BotCombatState _botCombatState;
        private readonly BotRunnerService _botRunner;

        private IAgentFactory? _agentFactory;
        private IWorldClient? _activeWorldClient;
        private IDisposable? _worldDisconnectSubscription;

        public BackgroundBotWorker(ILoggerFactory loggerFactory, IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(loggerFactory);
            ArgumentNullException.ThrowIfNull(configuration);

            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<BackgroundBotWorker>();

            var infrastructure = InitializeInfrastructure(configuration);

            _pathfindingClient = infrastructure.PathfindingClient;
            _characterStateUpdateClient = infrastructure.CharacterStateUpdateClient;
            _wowClient = infrastructure.WowClient;

            _agentFactory = infrastructure.AgentFactory;
            _activeWorldClient = infrastructure.InitialWorldClient;

            _botCombatState = new BotCombatState();
            var agentFactoryAccessor = new Func<IAgentFactory?>(() => _agentFactory);

            _botRunner = new BotRunnerService(
                infrastructure.ObjectManager,
                _characterStateUpdateClient,
                new DynamicTargetEngagementService(agentFactoryAccessor, _botCombatState),
                new DynamicLootingService(agentFactoryAccessor, _botCombatState),
                new TargetPositioningService(infrastructure.ObjectManager, _pathfindingClient));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _botRunner.Start();

                while (!stoppingToken.IsCancellationRequested)
                {
                    MaintainAgentFactory();

                    await Task.Delay(100, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in BackgroundBotWorker");
            }
        }

        private (PathfindingClient PathfindingClient, CharacterStateUpdateClient CharacterStateUpdateClient, WoWClient WowClient, IAgentFactory AgentFactory, IWorldClient? InitialWorldClient, IObjectManager ObjectManager) InitializeInfrastructure(IConfiguration configuration)
        {
            var pathfindingPort = ParseRequiredInt(configuration, "PathfindingService:Port");
            var pathfindingClient = new PathfindingClient(
                GetRequiredSetting(configuration, "PathfindingService:IpAddress"),
                pathfindingPort,
                _loggerFactory.CreateLogger<PathfindingClient>());

            var characterStateListenerPort = ParseRequiredInt(configuration, "CharacterStateListener:Port");
            var characterStateUpdateClient = new CharacterStateUpdateClient(
                GetRequiredSetting(configuration, "CharacterStateListener:IpAddress"),
                characterStateListenerPort,
                _loggerFactory.CreateLogger<CharacterStateUpdateClient>());

            var wowClient = new WoWClient();
            var realmIp = configuration["RealmEndpoint:IpAddress"];
            if (!string.IsNullOrWhiteSpace(realmIp))
            {
                wowClient.SetIpAddress(realmIp);
            }

            var objectManager = WoWSharpObjectManager.Instance;
            objectManager.Initialize(wowClient, pathfindingClient, _loggerFactory.CreateLogger<WoWSharpObjectManager>());

            var initialWorldClient = WoWClientFactory.CreateWorldClient();
            var agentFactory = WoWClientFactory.CreateNetworkClientComponentFactory(initialWorldClient, _loggerFactory);

            return (pathfindingClient, characterStateUpdateClient, wowClient, agentFactory, initialWorldClient, objectManager);
        }

        private static string GetRequiredSetting(IConfiguration configuration, string key)
        {
            var value = configuration[key];

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Missing required configuration value '{key}'.");
            }

            return value;
        }

        private static int ParseRequiredInt(IConfiguration configuration, string key)
        {
            var value = GetRequiredSetting(configuration, key);
            if (!int.TryParse(value, out var result))
            {
                throw new InvalidOperationException($"Configuration value for '{key}' must be a valid integer.");
            }

            return result;
        }

        private void MaintainAgentFactory()
        {
            var worldClient = _wowClient.WorldClient;

            if (worldClient?.IsConnected == true)
            {
                EnsureAgentFactory(worldClient);
            }
            else
            {
                ResetAgentFactory();
            }
        }

        private void EnsureAgentFactory(IWorldClient worldClient)
        {
            if (_agentFactory != null && ReferenceEquals(worldClient, _activeWorldClient))
            {
                return;
            }

            ResetAgentFactory();

            _agentFactory = WoWClientFactory.CreateNetworkClientComponentFactory(worldClient, _loggerFactory);
            _activeWorldClient = worldClient;

            try
            {
                _worldDisconnectSubscription = worldClient.WhenDisconnected?.Subscribe(_ =>
                {
                    _logger.LogInformation("World client disconnected. Resetting agent factory.");
                    ResetAgentFactory();
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to subscribe to world client disconnection notifications.");
            }

            _logger.LogInformation("Initialized network client component factory using active world client.");
        }

        private void ResetAgentFactory()
        {
            if (_agentFactory == null && _worldDisconnectSubscription == null && _activeWorldClient == null)
            {
                return;
            }

            if (_agentFactory is IDisposable disposableFactory)
            {
                try
                {
                    disposableFactory.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing agent factory.");
                }
            }

            _agentFactory = null;
            _activeWorldClient = null;

            _worldDisconnectSubscription?.Dispose();
            _worldDisconnectSubscription = null;

            _logger.LogInformation("Cleared network client component factory state.");
        }

        private sealed class DynamicTargetEngagementService(Func<IAgentFactory?> factoryAccessor, BotCombatState combatState)
            : ITargetEngagementService
        {
            private readonly Func<IAgentFactory?> _factoryAccessor = factoryAccessor;
            private readonly BotCombatState _combatState = combatState;

            public ulong? CurrentTargetGuid => _combatState.CurrentTargetGuid;

            public async Task EngageAsync(IWoWUnit target, CancellationToken cancellationToken)
            {
                ArgumentNullException.ThrowIfNull(target);

                var factory = _factoryAccessor() ?? throw new InvalidOperationException("Agent factory is not available.");
                var targetGuid = target.Guid;

                if (!factory.TargetingAgent.IsTargeted(targetGuid))
                {
                    await factory.AttackAgent.AttackTargetAsync(targetGuid, factory.TargetingAgent, cancellationToken);
                }
                else if (!factory.AttackAgent.IsAttacking)
                {
                    await factory.AttackAgent.StartAttackAsync(cancellationToken);
                }

                _combatState.SetCurrentTarget(targetGuid);
            }
        }

        private sealed class DynamicLootingService(Func<IAgentFactory?> factoryAccessor, BotCombatState combatState)
            : ILootingService
        {
            private readonly Func<IAgentFactory?> _factoryAccessor = factoryAccessor;
            private readonly BotCombatState _combatState = combatState;

            public async Task<bool> TryLootAsync(ulong targetGuid, CancellationToken cancellationToken)
            {
                if (targetGuid == 0 || _combatState.HasLooted(targetGuid))
                {
                    return false;
                }

                var factory = _factoryAccessor() ?? throw new InvalidOperationException("Agent factory is not available.");

                await factory.LootingAgent.QuickLootAsync(targetGuid, cancellationToken);

                if (factory.AttackAgent.IsAttacking)
                {
                    await factory.AttackAgent.StopAttackAsync(cancellationToken);
                }

                if (factory.TargetingAgent.HasTarget() && factory.TargetingAgent.IsTargeted(targetGuid))
                {
                    await factory.TargetingAgent.ClearTargetAsync(cancellationToken);
                }

                return _combatState.TryMarkLooted(targetGuid);
            }
        }
    }
}
