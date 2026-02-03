using Microsoft.Extensions.Logging.Abstractions;

namespace ForegroundBotRunner;

public static class Program
{
    public static async Task Main(string[] args)
    {
        // Run the hosted service without the generic host to avoid extra dependencies.
        var service = new ForegroundBotHostedService(NullLogger<ForegroundBotHostedService>.Instance);
        await service.StartAsync(CancellationToken.None);

        // Keep the process alive.
        await Task.Delay(Timeout.InfiniteTimeSpan);
    }
}
