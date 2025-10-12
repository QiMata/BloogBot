using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;

namespace ForegroundBotRunner;

public static class Program
{
    public static async Task Main(string[] args)
    {
        using var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices(static services =>
            {
                services.AddHostedService<ForegroundBotHostedService>();
            })
            .Build();

        await host.RunAsync();
    }
}
