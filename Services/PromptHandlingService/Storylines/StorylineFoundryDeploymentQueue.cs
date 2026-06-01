using System.Threading.Channels;
using Microsoft.Extensions.Hosting;

namespace PromptHandlingService.Storylines;

public interface IStorylineFoundryDeploymentQueue
{
    ValueTask QueueAsync(string deploymentId, CancellationToken cancellationToken);
    IAsyncEnumerable<string> ReadAllAsync(CancellationToken cancellationToken);
}

public sealed class StorylineFoundryDeploymentQueue : IStorylineFoundryDeploymentQueue
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    public ValueTask QueueAsync(string deploymentId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(deploymentId))
        {
            throw new ArgumentException("Deployment id is required.", nameof(deploymentId));
        }

        return _channel.Writer.WriteAsync(deploymentId.Trim(), cancellationToken);
    }

    public IAsyncEnumerable<string> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}

public sealed class StorylineFoundryDeploymentWorker : BackgroundService
{
    private readonly IStorylineFoundryDeploymentQueue _queue;
    private readonly IStorylineFoundryDeploymentService _deploymentService;

    public StorylineFoundryDeploymentWorker(
        IStorylineFoundryDeploymentQueue queue,
        IStorylineFoundryDeploymentService deploymentService)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _deploymentService = deploymentService ?? throw new ArgumentNullException(nameof(deploymentService));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var deploymentId in _queue.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            await _deploymentService.RunDeploymentAsync(deploymentId, stoppingToken).ConfigureAwait(false);
        }
    }
}
