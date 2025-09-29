using BotRunner;
using BotRunner.Clients;
using BotRunner.Combat;
using BotRunner.Movement;
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

        private readonly BotRunnerService _botRunner;
        private readonly IAgentFactory _agentFactory;
        private readonly IWorldClient _worldClient;
        private readonly BotCombatState _botCombatState;

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
            _worldClient = WoWClientFactory.CreateWorldClient();
            _agentFactory = WoWClientFactory.CreateNetworkClientComponentFactory(_worldClient, loggerFactory);
            _botCombatState = new BotCombatState();
            _botRunner = new BotRunnerService(
                WoWSharpObjectManager.Instance,
                _characterStateUpdateClient,
                new TargetEngagementService(_agentFactory, _botCombatState),
                new LootingService(_agentFactory, _botCombatState),
                new TargetPositioningService(WoWSharpObjectManager.Instance, _pathfindingClient));
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
                        // var partyAgent = _allAgents.PartyAgent; // Accessing the party agent
                        // var trainerAgent = _allAgents.TrainerAgent; // Accessing the trainer agent
                        // var talentAgent = _allAgents.TalentAgent; // Accessing the talent agent
                        // var professionsAgent = _allAgents.ProfessionsAgent; // Accessing the professions agent
                        // var emoteAgent = _allAgents.EmoteAgent; // Accessing the emote agent
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
