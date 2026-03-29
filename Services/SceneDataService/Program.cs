using SceneDataService;

// SceneDataService reads config from environment variables (WWOW_SCENE_DATA_PORT, etc.),
// not from appsettings.json. Use minimal host builder to avoid conflicts with the
// shared appsettings.json in the output directory (which contains StateManager config).
var builder = new HostBuilder()
    .ConfigureServices(services =>
    {
        services.AddLogging(logging => logging.AddConsole());
        services.AddHostedService<Worker>();
    });

var host = builder.Build();
host.Run();
