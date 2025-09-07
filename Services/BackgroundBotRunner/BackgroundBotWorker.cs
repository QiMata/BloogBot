using BotRunner;
using BotRunner.Clients;
using PromptHandlingService;
using WoWSharpClient;
using WoWSharpClient.Client;
using WoWSharpClient.Agent;

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
        
        // Add separated agents
        private readonly ITargetingAgent _targetingAgent;
        private readonly IAttackAgent _attackAgent;
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
            
            // Initialize separated targeting and attack functionality
            var worldClient = WoWClientFactory.CreateWorldClient();
            var (targetingAgent, attackAgent) = WoWClientFactory.CreateCombatAgents(worldClient, loggerFactory);
            _targetingAgent = targetingAgent;
            _attackAgent = attackAgent;
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
                    // Example: Demonstrate separated agent functionality
                    if (_wowClient.IsWorldConnected())
                    {
                        // This is where you would integrate targeting and attacking with your bot logic
                        // For example, find and attack enemies, assist players, etc.
                        
                        // Example usage (commented out to avoid actual targeting):
                        // await _combatExample.FindAndAttackNearestEnemyAsync();
                    }

                    await Task.Delay(100, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ActivityBackgroundMemberWorker");
            }
        }

        /// <summary>
        /// Example method showing how to use targeting in bot operations.
        /// Call this from your bot logic when you need to target something.
        /// </summary>
        /// <param name="targetGuid">The GUID of the target to select.</param>
        public async Task SetTargetAsync(ulong targetGuid)
        {
            try
            {
                await _targetingAgent.SetTargetAsync(targetGuid);
                _logger.LogInformation($"Set target to: {targetGuid:X}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during targeting");
            }
        }

        /// <summary>
        /// Example method showing how to use attacking in bot operations.
        /// Call this from your bot logic when you need to attack current target.
        /// </summary>
        public async Task StartAttackAsync()
        {
            try
            {
                await _attackAgent.StartAttackAsync();
                _logger.LogInformation("Started attacking current target");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting attack");
            }
        }

        /// <summary>
        /// Assists another player by targeting what they are targeting.
        /// </summary>
        /// <param name="playerGuid">The GUID of the player to assist.</param>
        public async Task AssistPlayerAsync(ulong playerGuid)
        {
            try
            {
                await _targetingAgent.AssistAsync(playerGuid);
                _logger.LogInformation($"Assisting player with GUID: {playerGuid:X}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error assisting player {playerGuid:X}");
            }
        }

        /// <summary>
        /// Sets target and immediately attacks (convenience method using both agents).
        /// </summary>
        /// <param name="targetGuid">The GUID of the target to attack.</param>
        public async Task AttackTargetAsync(ulong targetGuid)
        {
            try
            {
                await _attackAgent.AttackTargetAsync(targetGuid, _targetingAgent);
                _logger.LogInformation($"Set target and started attacking: {targetGuid:X}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error attacking target {targetGuid:X}");
            }
        }
    }
}
