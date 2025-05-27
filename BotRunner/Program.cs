using BloogBot.AI.StateMachine;
using BotRunner;
using Microsoft.Extensions.DependencyInjection;

internal class Program
{
    public static async Task Main(string[] args)
    {
        var services = new ServiceCollection();
        services.AddSingleton<BotRunnerService>();
        services.AddSingleton<BotActivityStateMachine>();
        services.AddSingleton<PluginCatalog>();
        services.AddSingleton<KernelCoordinator>();
        // Register other dependencies as needed

        var serviceProvider = services.BuildServiceProvider();

        var botRunner = serviceProvider.GetRequiredService<BotRunnerService>();

        // Start the bot or perform other operations
        botRunner.Start();
    }
}
