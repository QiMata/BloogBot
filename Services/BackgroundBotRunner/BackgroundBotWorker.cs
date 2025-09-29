using System;
using System.Reactive.Linq;
using BotRunner;
using BotRunner.Clients;
using PromptHandlingService;
using WoWSharpClient;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;

namespace BackgroundBotRunner
{
    public class BackgroundBotWorker : BackgroundService
    {
        private readonly ILogger<BackgroundBotWorker> _logger;

        private readonly IPromptRunner _promptRunner;

        private readonly PathfindingClient _pathfindingClient;
        private readonly WoWClient _wowClient;
        private readonly CharacterStateUpdateClient _characterStateUpdateClient;
        private readonly ILoggerFactory _loggerFactory;

        private readonly BotRunnerService _botRunner;
        
        // Use all network agents through the allAgents pattern
        private readonly (
            ITargetingNetworkClientComponent TargetingAgent,
            IAttackNetworkClientComponent AttackAgent,
            IChatNetworkClientComponent ChatAgent,
            IQuestNetworkClientComponent QuestAgent,
            ILootingNetworkClientComponent LootingAgent,
            IGameObjectNetworkClientComponent GameObjectAgent,
            IVendorNetworkClientComponent VendorAgent,
            IFlightMasterNetworkClientComponent FlightMasterAgent,
            IDeadActorNetworkClientComponent DeadActorAgent,
            IInventoryNetworkClientComponent InventoryAgent,
            IItemUseNetworkClientComponent ItemUseAgent,
            IEquipmentNetworkClientComponent EquipmentAgent,
            ISpellCastingNetworkClientComponent SpellCastingAgent,
            IAuctionHouseNetworkClientComponent AuctionHouseAgent,
            IBankNetworkClientComponent BankAgent,
            IMailNetworkClientComponent MailAgent,
            IGuildNetworkClientComponent GuildAgent,
            IPartyNetworkClientComponent PartyAgent,
            ITrainerNetworkClientComponent TrainerAgent,
            ITalentNetworkClientComponent TalentAgent,
            IProfessionsNetworkClientComponent ProfessionsAgent,
            IEmoteNetworkClientComponent EmoteAgent,
            IGossipNetworkClientComponent GossipAgent,
            IFriendNetworkClientComponent FriendAgent,
            IIgnoreNetworkClientComponent IgnoreAgent,
            ITradeNetworkClientComponent TradeAgent
        ) _allAgents;

        private CancellationToken _stoppingToken;

        public BackgroundBotWorker(ILoggerFactory loggerFactory, IConfiguration configuration)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<BackgroundBotWorker>();

            _promptRunner = PromptRunnerFactory.GetOllamaPromptRunner(new Uri(configuration["Ollama:BaseUri"]), configuration["Ollama:Model"]);

            _pathfindingClient = new PathfindingClient(configuration["PathfindingService:IpAddress"], int.Parse(configuration["PathfindingService:Port"]), loggerFactory.CreateLogger<PathfindingClient>());
            _characterStateUpdateClient = new CharacterStateUpdateClient(configuration["CharacterStateListener:IpAddress"], int.Parse(configuration["CharacterStateListener:Port"]), loggerFactory.CreateLogger<CharacterStateUpdateClient>());
            _wowClient = new();
            _wowClient.SetIpAddress(configuration["RealmEndpoint:IpAddress"]);
            WoWSharpObjectManager.Instance.Initialize(_wowClient, _pathfindingClient, loggerFactory.CreateLogger<WoWSharpObjectManager>());
            _botRunner = new BotRunnerService(WoWSharpObjectManager.Instance, _characterStateUpdateClient, _pathfindingClient);
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

                    // Example: Demonstrate agent functionality through internal logic
                    if (_wowClient.IsWorldConnected())
                    {
                        // This is where you would integrate targeting, attacking, questing, looting, game object interaction,
                        // vendor operations, flight master usage, and death handling with your bot logic

                        // Example usage (commented out to avoid actual actions):
                        // await _combatExample.EngageCombatAsync(enemyGuid);
                        // await InternalProcessQuestsAsync();
                        // await InternalLootNearbyBodiesAsync();
                        // await InternalGatherFromNodesAsync();
                        // await InternalBuySuppliesAsync();
                        // await InternalTakeFlight();
                        // await InternalHandleDeathAsync();

                        // Access individual agents like this once the factory has been initialized:
                        // var targetingAgent = _agentFactory?.TargetingAgent;
                        // var attackAgent = _agentFactory?.AttackAgent;
                        // var questAgent = _agentFactory?.QuestAgent;
                        // var lootingAgent = _agentFactory?.LootingAgent;
                        // var gameObjectAgent = _agentFactory?.GameObjectAgent;
                        // var vendorAgent = _agentFactory?.VendorAgent;
                        // var flightMasterAgent = _agentFactory?.FlightMasterAgent;
                        // var deadActorAgent = _agentFactory?.DeadActorAgent;
                        // var inventoryAgent = _agentFactory?.InventoryAgent;
                        // var itemUseAgent = _agentFactory?.ItemUseAgent;
                        // var equipmentAgent = _agentFactory?.EquipmentAgent;
                        // var spellCastingAgent = _agentFactory?.SpellCastingAgent;
                        // var auctionHouseAgent = _agentFactory?.AuctionHouseAgent;
                        // var bankAgent = _agentFactory?.BankAgent;
                        // var mailAgent = _agentFactory?.MailAgent;
                        // var guildAgent = _agentFactory?.GuildAgent;
                        // var partyAgent = _agentFactory?.PartyAgent; // Accessing the party agent
                        // var trainerAgent = _agentFactory?.TrainerAgent; // Accessing the trainer agent
                        // var talentAgent = _agentFactory?.TalentAgent; // Accessing the talent agent
                        // var professionsAgent = _agentFactory?.ProfessionsAgent; // Accessing the professions agent
                        // var emoteAgent = _agentFactory?.EmoteAgent; // Accessing the emote agent
                    }

                    await Task.Delay(100, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in BackgroundBotWorker");
            }
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
    }
}
