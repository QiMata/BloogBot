namespace SceneDataService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private SceneDataSocketServer? _server;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var port = int.Parse(Environment.GetEnvironmentVariable("WWOW_SCENE_DATA_PORT") ?? "5003");
        var ip = Environment.GetEnvironmentVariable("WWOW_SCENE_DATA_IP") ?? "127.0.0.1";

        _logger.LogInformation("[SceneDataService] Starting on {Ip}:{Port}", ip, port);

        _server = new SceneDataSocketServer(ip, port, _logger);
        _server.InitializeNavigation();

        _logger.LogInformation("[SceneDataService] Ready and listening on {Ip}:{Port}", ip, port);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[SceneDataService] Shutting down...");
        }
        finally
        {
            _server?.Dispose();
        }
    }
}
