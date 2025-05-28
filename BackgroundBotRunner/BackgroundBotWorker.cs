using BloogBot.AI.StateMachine;
using BotRunner;
using BotRunner.Clients;
using GameData.Core.Interfaces;
using PromptHandlingService;
using WoWSharpClient;

namespace BackgroundBotRunner
{
    public class BackgroundBotWorker : BackgroundService
    {
        private readonly ILogger<BackgroundBotWorker> _logger;
        private readonly IPromptRunner _promptRunner;
        private readonly PathfindingClient _pathfindingClient;
        private readonly CharacterStateUpdateClient _characterStateUpdateClient;
        private readonly BotActivityStateMachine _botActivityStateMachine;
        private BotRunnerService _botRunner;
        private KernelCoordinator _kernelCoordinator;
        private CancellationToken _stoppingToken;

        public BackgroundBotWorker(ILoggerFactory loggerFactory, IConfiguration configuration)
        {
            _logger = loggerFactory.CreateLogger<BackgroundBotWorker>();
            _promptRunner = PromptRunnerFactory.GetOllamaPromptRunner(
                new Uri(configuration["Ollama:BaseUri"]),
                configuration["Ollama:Model"]
            );
            _pathfindingClient = new PathfindingClient(
                configuration["PathfindingService:IpAddress"],
                int.Parse(configuration["PathfindingService:Port"]),
                loggerFactory.CreateLogger<PathfindingClient>()
            );
            _characterStateUpdateClient = new CharacterStateUpdateClient(
                configuration["CharacterStateListener:IpAddress"],
                int.Parse(configuration["CharacterStateListener:Port"]),
                loggerFactory.CreateLogger<CharacterStateUpdateClient>()
            );
            _botRunner = new BotRunnerService(
                new WoWSharpObjectManager(
                    configuration["RealmEndpoint:IpAddress"],
                    loggerFactory.CreateLogger<WoWSharpObjectManager>()
                ),
                _characterStateUpdateClient,
                _pathfindingClient,
                _botActivityStateMachine,
                _kernelCoordinator
            );
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _stoppingToken = stoppingToken;

            try
            {
                _botRunner.Start();

                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(100, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ActivityBackgroundMemberWorker");
            }
        }
    }
}
