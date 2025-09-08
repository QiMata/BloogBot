using BotRunner;
using BotRunner.Clients;
using PromptHandlingService;
using WoWSharpClient;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.Agent;
using WoWSharpClient.Networking.Agent.I;

namespace BackgroundBotRunner
{
    public class BackgroundBotWorker : BackgroundService
    {
        private readonly ILogger<BackgroundBotWorker> _logger;

        private readonly IPromptRunner _promptRunner;

        private readonly PathfindingClient _pathfindingClient;
        private readonly WoWClient _wowClient;
        private readonly CharacterStateUpdateClient _characterStateUpdateClient;

        private readonly BotRunnerService _botRunner;
        
        // Use all network agents through the allAgents pattern
        private readonly (
            ITargetingNetworkAgent TargetingAgent,
            IAttackNetworkAgent AttackAgent,
            IQuestNetworkAgent QuestAgent,
            ILootingNetworkAgent LootingAgent,
            IGameObjectNetworkAgent GameObjectAgent,
            IVendorNetworkAgent VendorAgent,
            IFlightMasterNetworkAgent FlightMasterAgent,
            IDeadActorAgent DeadActorAgent,
            IInventoryNetworkAgent InventoryAgent,
            IItemUseNetworkAgent ItemUseAgent,
            IEquipmentNetworkAgent EquipmentAgent,
            ISpellCastingNetworkAgent SpellCastingAgent,
            IAuctionHouseNetworkAgent AuctionHouseAgent,
            IBankNetworkAgent BankAgent,
            IMailNetworkAgent MailAgent,
            IGuildNetworkAgent GuildAgent
        ) _allAgents;
        
        private readonly CombatIntegrationExample _combatExample;

        private CancellationToken _stoppingToken;

        public BackgroundBotWorker(ILoggerFactory loggerFactory, IConfiguration configuration)
        {
            _logger = loggerFactory.CreateLogger<BackgroundBotWorker>();

            _promptRunner = PromptRunnerFactory.GetOllamaPromptRunner(new Uri(configuration["Ollama:BaseUri"]), configuration["Ollama:Model"]);

            _pathfindingClient = new PathfindingClient(configuration["PathfindingService:IpAddress"], int.Parse(configuration["PathfindingService:Port"]), loggerFactory.CreateLogger<PathfindingClient>());
            _characterStateUpdateClient = new CharacterStateUpdateClient(configuration["CharacterStateListener:IpAddress"], int.Parse(configuration["CharacterStateListener:Port"]), loggerFactory.CreateLogger<CharacterStateUpdateClient>());
            _wowClient = new();
            _wowClient.SetIpAddress(configuration["RealmEndpoint:IpAddress"]);
            WoWSharpObjectManager.Instance.Initialize(_wowClient, _pathfindingClient, loggerFactory.CreateLogger<WoWSharpObjectManager>());
            _botRunner = new BotRunnerService(WoWSharpObjectManager.Instance, _characterStateUpdateClient, _pathfindingClient);
            
            // Initialize all network agents including the new ones
            var worldClient = WoWClientFactory.CreateWorldClient();
            _allAgents = WoWClientFactory.CreateAllNetworkAgents(worldClient, loggerFactory);
            _combatExample = new CombatIntegrationExample(worldClient, loggerFactory);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _stoppingToken = stoppingToken;

            try
            {
                _botRunner.Start();

                while (!stoppingToken.IsCancellationRequested)
                {
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
                        
                        // Access individual agents like this:
                        // var targetingAgent = _allAgents.TargetingAgent;
                        // var attackAgent = _allAgents.AttackAgent;
                        // var questAgent = _allAgents.QuestAgent;
                        // var lootingAgent = _allAgents.LootingAgent;
                        // var gameObjectAgent = _allAgents.GameObjectAgent;
                        // var vendorAgent = _allAgents.VendorAgent;
                        // var flightMasterAgent = _allAgents.FlightMasterAgent;
                        // var deadActorAgent = _allAgents.DeadActorAgent;
                        // var inventoryAgent = _allAgents.InventoryAgent;
                        // var itemUseAgent = _allAgents.ItemUseAgent;
                        // var equipmentAgent = _allAgents.EquipmentAgent;
                        // var spellCastingAgent = _allAgents.SpellCastingAgent;
                        // var auctionHouseAgent = _allAgents.AuctionHouseAgent;
                        // var bankAgent = _allAgents.BankAgent;
                        // var mailAgent = _allAgents.MailAgent;
                        // var guildAgent = _allAgents.GuildAgent;
                    }

                    await Task.Delay(100, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in BackgroundBotWorker");
            }
        }
    }
}
