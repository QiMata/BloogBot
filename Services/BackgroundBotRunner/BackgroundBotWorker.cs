using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using BackgroundBotRunner.Diagnostics;
using BotRunner;
using BotRunner.Clients;
using BotRunner.Combat;
using BotRunner.Constants;
using BotRunner.Interfaces;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WoWSharpClient;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents;
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
        private readonly BackgroundPacketTraceRecorder? _packetTraceRecorder;
        private readonly BackgroundPhysicsMode _physicsMode;

        /// <summary>P9.5: Per-bot ObjectManager instance (no longer using singleton).</summary>
        private readonly WoWSharpObjectManager _objectManager = new();
        private readonly object _agentFactoryLock = new();
        private CancellationToken _stoppingToken;
        private IAgentFactory? _agentFactory;
        private IWorldClient? _activeWorldClient;
        private IDisposable? _worldDisconnectSubscription;
        private IDisposable? _tradeAutoAcceptSubscription;
        private IDisposable? _logoutCompleteSubscription;

        public BackgroundBotWorker(ILoggerFactory loggerFactory, IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(loggerFactory);
            ArgumentNullException.ThrowIfNull(configuration);

            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<BackgroundBotWorker>();
            _physicsMode = BackgroundPhysicsModeResolver.Resolve(configuration);
            _logger.LogInformation("Background bot physics mode: {PhysicsMode} ({Description}). Set {EnvVar}=shared to force PathfindingService physics.",
                _physicsMode,
                BackgroundPhysicsModeResolver.Describe(_physicsMode),
                BackgroundPhysicsModeResolver.EnvironmentVariableName);

            var infrastructure = InitializeInfrastructure(configuration);

            _pathfindingClient = infrastructure.PathfindingClient;
            _characterStateUpdateClient = infrastructure.CharacterStateUpdateClient;
            _wowClient = infrastructure.WowClient;
            if (RecordingArtifactsFeature.IsEnabled())
            {
                _packetTraceRecorder = new BackgroundPacketTraceRecorder(_wowClient, _loggerFactory);
            }
            else
            {
                _logger.LogInformation("Recording artifacts disabled; background packet trace recorder is not active. Set {EnvVar}=1 to enable.",
                    RecordingArtifactsFeature.EnvironmentVariableName);
            }

            _agentFactory = infrastructure.AgentFactory;
            _activeWorldClient = infrastructure.InitialWorldClient;

            _botCombatState = new BotCombatState();
            var agentFactoryAccessor = new Func<IAgentFactory?>(() => _agentFactory);

            var accountName = Environment.GetEnvironmentVariable("WWOW_ACCOUNT_NAME");
            var container = CreateClassContainer(accountName, _pathfindingClient);

            var useGmCommands = string.Equals(Environment.GetEnvironmentVariable("WWOW_USE_GM_COMMANDS"), "1", StringComparison.Ordinal);
            var assignedActivity = Environment.GetEnvironmentVariable("WWOW_ASSIGNED_ACTIVITY");

            _botRunner = new BotRunnerService(
                infrastructure.ObjectManager,
                _characterStateUpdateClient,
                container,
                agentFactoryAccessor,
                accountName,
                talentService: new DynamicTalentService(agentFactoryAccessor),
                equipmentService: new EquipmentService(),
                behaviorConfig: LoadBehaviorConfig(configuration),
                diagnosticPacketTraceRecorder: _packetTraceRecorder,
                useGmCommands: useGmCommands,
                assignedActivity: assignedActivity);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _stoppingToken = stoppingToken;
            try
            {
                _botRunner.Start();

                while (!stoppingToken.IsCancellationRequested)
                {
                    MaintainAgentFactory();

                    await Task.Delay(100, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown — handled in StopAsync
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in BackgroundBotWorker");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("BackgroundBotWorker stopping — cleaning up bot runner and agent factory.");

            try
            {
                _botRunner.Stop();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping bot runner during shutdown.");
            }

            ResetAgentFactory();
            _packetTraceRecorder?.Dispose();

            _logger.LogInformation("BackgroundBotWorker cleanup complete.");
            await base.StopAsync(cancellationToken);
        }

        private static BotBehaviorConfig LoadBehaviorConfig(IConfiguration configuration)
        {
            var config = new BotBehaviorConfig();
            var section = configuration.GetSection("BotBehavior");
            if (!section.Exists()) return config;

            // Bind known properties from config section
            if (float.TryParse(section["MaxPullRange"], out var v1)) config.MaxPullRange = v1;
            if (int.TryParse(section["TargetLevelRangeBelow"], out var v2)) config.TargetLevelRangeBelow = v2;
            if (int.TryParse(section["TargetLevelRangeAbove"], out var v3)) config.TargetLevelRangeAbove = v3;
            if (int.TryParse(section["RestHpThresholdPct"], out var v4)) config.RestHpThresholdPct = v4;
            if (int.TryParse(section["RestManaThresholdPct"], out var v5)) config.RestManaThresholdPct = v5;
            if (int.TryParse(section["BagFullThreshold"], out var v6)) config.BagFullThreshold = v6;
            if (float.TryParse(section["GatherDetectRange"], out var v7)) config.GatherDetectRange = v7;
            if (float.TryParse(section["FishingPoolDetectRange"], out var v8)) config.FishingPoolDetectRange = v8;
            if (int.TryParse(section["StatsLogIntervalMs"], out var v9)) config.StatsLogIntervalMs = v9;
            return config;
        }

        private (PathfindingClient PathfindingClient, CharacterStateUpdateClient CharacterStateUpdateClient, WoWClient WowClient, IAgentFactory AgentFactory, IWorldClient? InitialWorldClient, IObjectManager ObjectManager) InitializeInfrastructure(IConfiguration configuration)
        {
            var pathfindingPort = ParseRequiredInt(configuration, "PathfindingService:Port");
            var pathfindingClient = new PathfindingClient(
                GetRequiredSetting(configuration, "PathfindingService:IpAddress"),
                pathfindingPort,
                _loggerFactory.CreateLogger<PathfindingClient>(),
                hasLocalPhysics: true);

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

            var objectManager = _objectManager;
            if (_physicsMode == BackgroundPhysicsMode.LocalInProcess)
            {
                WoWSharpClient.Movement.SceneDataClient? sceneDataClient = null;
                var sceneDataIp =
                    configuration["SceneDataService:IpAddress"]
                    ?? Environment.GetEnvironmentVariable("WWOW_SCENE_DATA_IP")
                    ?? "127.0.0.1";
                var sceneDataPortStr =
                    configuration["SceneDataService:Port"]
                    ?? Environment.GetEnvironmentVariable("WWOW_SCENE_DATA_PORT")
                    ?? "5003";
                var sceneDataEndpointValid = int.TryParse(sceneDataPortStr, out var sceneDataPort);
                var runtimeMode = BackgroundPhysicsRuntimeModeResolver.Resolve(_physicsMode, sceneDataEndpointValid);

                if (runtimeMode == BackgroundPhysicsRuntimeMode.LocalSceneSlices)
                {
                    try
                    {
                        sceneDataClient = new WoWSharpClient.Movement.SceneDataClient(
                            sceneDataIp, sceneDataPort, _loggerFactory.CreateLogger("SceneDataClient"));
                    }
                    catch (Exception ex)
                    {
                        runtimeMode = BackgroundPhysicsRuntimeMode.LocalPreloadedMaps;
                        _logger.LogWarning(ex, "SceneDataService client initialization failed at {Ip}:{Port}; BG movement will use local preloaded Navigation.dll physics instead.",
                            sceneDataIp, sceneDataPort);
                    }
                }

                if (!sceneDataEndpointValid)
                {
                    runtimeMode = BackgroundPhysicsRuntimeMode.LocalPreloadedMaps;
                    _logger.LogWarning("SceneDataService port '{Port}' is invalid; BG movement will use local preloaded Navigation.dll physics instead.",
                        sceneDataPortStr);
                }
                else if (runtimeMode == BackgroundPhysicsRuntimeMode.LocalSceneSlices)
                {
                    _logger.LogInformation("SceneDataService slices configured at {Ip}:{Port}; BG movement will establish the scene-data connection on demand and retry slice refreshes as the service becomes available.",
                        sceneDataIp, sceneDataPortStr);
                }
                else if (runtimeMode == BackgroundPhysicsRuntimeMode.LocalPreloadedMaps)
                {
                    _logger.LogWarning("SceneDataService endpoint is not configured; BG movement will use local Navigation.dll physics with on-demand per-map preload and without scene slices.");
                }

                _logger.LogInformation("Resolved BG physics runtime mode: {RuntimeMode} ({Description})",
                    runtimeMode,
                    BackgroundPhysicsRuntimeModeResolver.Describe(runtimeMode));

                // Physics is always local via NativeLocalPhysics — no remote physics fallback.
                objectManager.Initialize(wowClient, pathfindingClient,
                    _loggerFactory.CreateLogger<WoWSharpObjectManager>(),
                    sceneDataClient: runtimeMode == BackgroundPhysicsRuntimeMode.LocalSceneSlices ? sceneDataClient : null,
                    useLocalPhysics: true);
            }
            else
            {
                objectManager.Initialize(wowClient, pathfindingClient,
                    _loggerFactory.CreateLogger<WoWSharpObjectManager>(),
                    useLocalPhysics: true);
            }

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

            if (worldClient == null)
            {
                ResetAgentFactory();
                return;
            }

            if (!ReferenceEquals(worldClient, _activeWorldClient))
            {
                // WorldClient is created before the auth/world handshake completes.
                // Bind the factory immediately so early packets have their handlers registered.
                EnsureAgentFactory(worldClient);
                return;
            }

            if (worldClient.IsConnected != true && _worldDisconnectSubscription == null)
            {
                ResetAgentFactory();
            }
        }

        private void EnsureAgentFactory(IWorldClient worldClient)
        {
            lock (_agentFactoryLock)
            {
                if (_agentFactory != null && ReferenceEquals(worldClient, _activeWorldClient))
                {
                    return;
                }

                ResetAgentFactory();

                _agentFactory = WoWClientFactory.CreateNetworkClientComponentFactory(worldClient, _loggerFactory);
                _activeWorldClient = worldClient;

                // Eagerly initialize essential agents so their opcode handlers are registered
                // before login packets arrive (CharacterInit for ACTION_BUTTONS, Party for GROUP_INVITE)
                if (_agentFactory is NetworkClientComponentFactory concreteFactory)
                {
                    concreteFactory.InitializeEssentialAgents();
                }

                // Wire spell cooldown checker so IsSpellReady uses real cooldown data
                try
                {
                    var spellCasting = _agentFactory.SpellCastingAgent;
                    _objectManager.SetSpellCooldownChecker(
                        spellId => spellCasting.CanCastSpell(spellId));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to wire spell cooldown checker");
                }

                // Wire agent factory accessor for LootTargetAsync
                _objectManager.SetAgentFactoryAccessor(() => _agentFactory);

                // Wire auto-accept trading from party members
                try
                {
                    // Dispose old subscription before creating new one
                    _tradeAutoAcceptSubscription?.Dispose();
                    _tradeAutoAcceptSubscription = null;

                    var tradeAgent = _agentFactory.TradeAgent;
                    var partyAgent = _agentFactory.PartyAgent;
                    _tradeAutoAcceptSubscription = tradeAgent.TradesOpened
                        .Subscribe(_ =>
                        {
                            var tradingWith = tradeAgent.TradingWithGuid;
                            if (tradingWith == null) return;

                            // Auto-accept trades from party members
                            var om = _objectManager;
                            var guid = tradingWith.Value;
                            bool isPartyMember = guid == om.Party1Guid || guid == om.Party2Guid
                                || guid == om.Party3Guid || guid == om.Party4Guid;
                            if (!isPartyMember) return;

                            _logger.LogInformation("Auto-accepting trade from party member {Guid:X}", tradingWith.Value);
                            Task.Run(async () =>
                            {
                                try
                                {
                                    await Task.Delay(2000, _stoppingToken); // Wait for trader to set up
                                    if (tradeAgent.IsTradeOpen)
                                        await tradeAgent.AcceptTradeAsync();
                                }
                                catch (OperationCanceledException) { }
                                catch { }
                            }, _stoppingToken);
                        });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to wire trade auto-accept");
                }

                try
                {
                    // Dispose old subscription before creating new one
                    _worldDisconnectSubscription?.Dispose();
                    _worldDisconnectSubscription = null;

                    _worldDisconnectSubscription = worldClient.WhenDisconnected?.Subscribe(_ =>
                    {
                        _logger.LogInformation("World client disconnected. Resetting object manager world state and agent factory.");
                        _objectManager.ResetWorldSessionState("BackgroundBotWorker.WhenDisconnected");
                        ResetAgentFactory();
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to subscribe to world client disconnection notifications.");
                }

                // Auto-re-enter world after logout. When the server confirms logout
                // (SMSG_LOGOUT_COMPLETE), automatically re-login with the same character.
                // This enables tests to do .gm off → logout → relog to clear all GM state.
                try
                {
                    // Dispose old subscription before creating new one
                    _logoutCompleteSubscription?.Dispose();
                    _logoutCompleteSubscription = null;

                    _logoutCompleteSubscription = worldClient.LogoutComplete?.Subscribe(_ =>
                    {
                        var om = _objectManager;
                        var guid = om.PlayerGuid.FullGuid;
                        if (guid == 0)
                        {
                            _logger.LogWarning("Logout complete but no character GUID stored — cannot auto-re-enter.");
                            return;
                        }
                        _logger.LogInformation("Logout complete — auto-re-entering world with GUID 0x{Guid:X}", guid);
                        om.ResetWorldSessionState("BackgroundBotWorker.LogoutComplete");
                        Task.Run(async () =>
                        {
                            try
                            {
                                await Task.Delay(1500, _stoppingToken); // Let server finalize logout
                                om.EnterWorld(guid);
                            }
                            catch (OperationCanceledException) { }
                        }, _stoppingToken);
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to subscribe to logout complete notifications.");
                }

                _logger.LogInformation("Initialized network client component factory using active world client.");
            }
        }

        private void ResetAgentFactory()
        {
            lock (_agentFactoryLock)
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

                _tradeAutoAcceptSubscription?.Dispose();
                _tradeAutoAcceptSubscription = null;

                _logoutCompleteSubscription?.Dispose();
                _logoutCompleteSubscription = null;

                _logger.LogInformation("Cleared network client component factory state.");
            }
        }

        private static IDependencyContainer CreateClassContainer(string? accountName, PathfindingClient pathfindingClient)
        {
            var @class = WoWNameGenerator.ResolveClass(accountName);
            var specOverride = Environment.GetEnvironmentVariable("WWOW_CHARACTER_SPEC");

            var botProfile = BotProfiles.Common.BotProfileResolver.Resolve(specOverride, @class);

            return new ClassContainer(
                botProfile.Name,
                botProfile.CreateRestTask,
                botProfile.CreateBuffTask,
                botProfile.CreateMoveToTargetTask,
                botProfile.CreatePvERotationTask,
                botProfile.CreatePvPRotationTask,
                pathfindingClient,
                createPullTargetTask: botProfile.CreatePullTargetTask);
        }

    }
}
