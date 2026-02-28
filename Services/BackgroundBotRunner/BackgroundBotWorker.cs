using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        private IAgentFactory? _agentFactory;
        private IWorldClient? _activeWorldClient;
        private IDisposable? _worldDisconnectSubscription;
        private IDisposable? _tradeAutoAcceptSubscription;

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

            var accountName = Environment.GetEnvironmentVariable("WWOW_ACCOUNT_NAME");
            var container = CreateClassContainer(accountName, _pathfindingClient);

            _botRunner = new BotRunnerService(
                infrastructure.ObjectManager,
                _characterStateUpdateClient,
                container,
                agentFactoryAccessor,
                accountName,
                talentService: new DynamicTalentService(agentFactoryAccessor),
                equipmentService: new EquipmentService(),
                behaviorConfig: LoadBehaviorConfig(configuration));
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
                WoWSharpObjectManager.Instance.SetSpellCooldownChecker(
                    spellId => spellCasting.CanCastSpell(spellId));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to wire spell cooldown checker");
            }

            // Wire agent factory accessor for LootTargetAsync
            WoWSharpObjectManager.Instance.SetAgentFactoryAccessor(() => _agentFactory);

            // Wire auto-accept trading from party members
            try
            {
                var tradeAgent = _agentFactory.TradeAgent;
                var partyAgent = _agentFactory.PartyAgent;
                _tradeAutoAcceptSubscription = tradeAgent.TradesOpened
                    .Subscribe(_ =>
                    {
                        var tradingWith = tradeAgent.TradingWithGuid;
                        if (tradingWith == null) return;

                        // Auto-accept trades from party members
                        var om = WoWSharpObjectManager.Instance;
                        var guid = tradingWith.Value;
                        bool isPartyMember = guid == om.Party1Guid || guid == om.Party2Guid
                            || guid == om.Party3Guid || guid == om.Party4Guid;
                        if (!isPartyMember) return;

                        _logger.LogInformation("Auto-accepting trade from party member {Guid:X}", tradingWith.Value);
                        Task.Run(async () =>
                        {
                            try
                            {
                                await Task.Delay(2000); // Wait for trader to set up
                                if (tradeAgent.IsTradeOpen)
                                    await tradeAgent.AcceptTradeAsync();
                            }
                            catch { }
                        });
                    });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to wire trade auto-accept");
            }

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

            _tradeAutoAcceptSubscription?.Dispose();
            _tradeAutoAcceptSubscription = null;

            _logger.LogInformation("Cleared network client component factory state.");
        }

        private static IDependencyContainer CreateClassContainer(string? accountName, PathfindingClient pathfindingClient)
        {
            BotProfiles.Common.BotBase botProfile;

            if (!string.IsNullOrEmpty(accountName) && accountName.Length >= 4)
            {
                var classCode = accountName.Substring(2, 2);
                var @class = WoWNameGenerator.ParseClassCode(classCode);

                botProfile = @class switch
                {
                    Class.Warrior => new WarriorArms.WarriorArms(),
                    Class.Paladin => new PaladinRetribution.PaladinRetribution(),
                    Class.Rogue => new RogueCombat.RogueCombat(),
                    Class.Hunter => new HunterBeastMastery.HunterBeastMastery(),
                    Class.Priest => new PriestDiscipline.PriestDiscipline(),
                    Class.Shaman => new ShamanEnhancement.ShamanEnhancement(),
                    Class.Mage => new MageArcane.MageArcane(),
                    Class.Warlock => new WarlockAffliction.WarlockAffliction(),
                    Class.Druid => new DruidRestoration.DruidRestoration(),
                    _ => new WarriorArms.WarriorArms()
                };
            }
            else
            {
                botProfile = new WarriorArms.WarriorArms();
            }

            return new ClassContainer(
                botProfile.Name,
                botProfile.CreateRestTask,
                botProfile.CreateBuffTask,
                botProfile.CreateMoveToTargetTask,
                botProfile.CreatePvERotationTask,
                botProfile.CreatePvPRotationTask,
                pathfindingClient);
        }

    }
}
