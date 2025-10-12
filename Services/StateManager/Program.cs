using BackgroundBotRunner;
using DecisionEngineService;
using PromptHandlingService;

namespace StateManager
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            await CreateHostBuilder(args)
                .Build()
                .RunAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, builder) =>
                {
                    builder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                    builder.AddEnvironmentVariables();

                    if (args != null)
                        builder.AddCommandLine(args);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.Configure<PathfindingServiceOptions>(hostContext.Configuration.GetSection("PathfindingService"));
                    services.AddHostedService<PathfindingServiceBootstrapper>();
                    services.AddPromptHandlingServices(hostContext.Configuration);
                    services.AddHostedService<StateManagerWorker>();
                    services.AddHostedService<DecisionEngineWorker>();
                    services.AddHostedService<PromptHandlingServiceWorker>();
                    services.AddTransient<BackgroundBotWorker>();
                });
    }
}
