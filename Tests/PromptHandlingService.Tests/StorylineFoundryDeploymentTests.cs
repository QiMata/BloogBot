using System.Runtime.CompilerServices;
using PromptHandlingService.Foundry;
using PromptHandlingService.Foundry.Deployment;
using PromptHandlingService.Storylines;

namespace PromptHandlingService.Tests;

public sealed class StorylineFoundryInstructionBuilderTests
{
    [Fact]
    public void Build_IsDeterministicAndIncludesStorylineBoundaryAndGraph()
    {
        var builder = new StorylineFoundryInstructionBuilder();
        var source = new StorylineFoundryInstructionSource(
            Profile(),
            Version(),
            Graph(),
            new[]
            {
                new NarrativeNode { GraphId = GraphId, NodeId = "node-b", Title = "B", Summary = "Second.", FallbackReply = "Fallback B.", SortOrder = 20 },
                new NarrativeNode { GraphId = GraphId, NodeId = "node-a", Title = "A", Summary = "First.", FallbackReply = "Fallback A.", SortOrder = 10 }
            },
            new[]
            {
                new NarrativeTransition { GraphId = GraphId, TransitionId = "transition-b", FromNodeId = "node-b", ToNodeId = "node-a", TriggerKind = "back", GuardExpression = "deterministic.back", SortOrder = 20 },
                new NarrativeTransition { GraphId = GraphId, TransitionId = "transition-a", FromNodeId = "node-a", ToNodeId = "node-b", TriggerKind = "next", GuardExpression = "deterministic.next", SortOrder = 10 }
            });

        var first = builder.Build(source);
        var second = builder.Build(source);

        Assert.Equal(first, second);
        Assert.Equal(builder.ComputeContentHash(first), builder.ComputeContentHash(second));
        Assert.Contains("advisory dialogue only", first);
        Assert.Contains("do not mutate state", first);
        Assert.Contains(PersonaPromptAssembler.OutputContract, first);
        Assert.Contains("disallowedIntents:", first);
        Assert.Contains("Durotar Guide", first);
        Assert.Contains("Test Graph", first);
        Assert.Contains("Fallback A.", first);
        Assert.True(first.IndexOf("node-a", StringComparison.Ordinal) < first.IndexOf("node-b", StringComparison.Ordinal));
        Assert.True(first.IndexOf("transition-a", StringComparison.Ordinal) < first.IndexOf("transition-b", StringComparison.Ordinal));
    }

    private const string GraphId = "graph-1";

    private static PersonaProfile Profile() => new()
    {
        PersonaId = "persona-a",
        DisplayName = "Durotar Guide",
        Description = "Helpful local guide."
    };

    private static PersonaVersion Version() => new()
    {
        PersonaId = "persona-a",
        PersonaVersionId = "persona-a:v1",
        Version = "v1",
        PromptSummary = "Short local guidance."
    };

    private static NarrativeGraph Graph() => new()
    {
        GraphId = GraphId,
        Name = "Test Graph",
        Description = "Graph description."
    };
}

public sealed class StorylineFoundryDeploymentServiceTests
{
    [Fact]
    public async Task PreviewDeploymentAsync_RejectsMissingTarget()
    {
        using var workspace = TempFoundryWorkspace.Create();
        using var repository = CreateRepository(workspace.DatabasePath);
        var service = CreateService(repository, new FakeDeploymentProvisioner());

        var preview = await service.PreviewDeploymentAsync(new FoundryDeploymentTargetRequest("", "", ""), CancellationToken.None);

        Assert.False(preview.IsValid);
        Assert.Empty(preview.Instructions);
        Assert.Contains(preview.Errors, error => error.Code == "required" && error.FieldPath == nameof(FoundryDeploymentTargetRequest.PersonaId));
        Assert.Contains(preview.Errors, error => error.Code == "required" && error.FieldPath == nameof(FoundryDeploymentTargetRequest.PersonaVersionId));
        Assert.Contains(preview.Errors, error => error.Code == "required" && error.FieldPath == nameof(FoundryDeploymentTargetRequest.GraphId));
    }

    [Fact]
    public async Task RunDeploymentAsync_RecordsQueuedRunningAndSucceededWithFakeProvisioner()
    {
        using var workspace = TempFoundryWorkspace.Create();
        using var repository = CreateRepository(workspace.DatabasePath);
        await SeedTargetAsync(repository);
        var deploymentId = string.Empty;
        var provisioner = new FakeDeploymentProvisioner(async (_, _) =>
        {
            var running = await repository.GetFoundryDeploymentAsync(deploymentId, CancellationToken.None);
            Assert.Equal(StorylineFoundryDeploymentStatus.Running, running?.Status);
            return new StorylineFoundryDeploymentProvisionResult("storyline-agent", "agent-v2", "agent-version-id", "gpt-test");
        });
        var queue = new RecordingDeploymentQueue();
        var service = CreateService(repository, provisioner, queue);

        var queued = await service.QueueDeploymentAsync(Target(), CancellationToken.None);
        deploymentId = queued.DeploymentId;
        var succeeded = await service.RunDeploymentAsync(queued.DeploymentId, CancellationToken.None);

        Assert.Equal(StorylineFoundryDeploymentStatus.Queued, queued.Status);
        Assert.Equal(queued.DeploymentId, Assert.Single(queue.QueuedDeploymentIds));
        Assert.Equal(StorylineFoundryDeploymentStatus.Succeeded, succeeded?.Status);
        Assert.Equal("agent-v2", succeeded?.AgentVersion);
        Assert.False(string.IsNullOrWhiteSpace(succeeded?.ContentHash));
        Assert.Contains("storyline-persona-dialogue-advisory", Assert.Single(provisioner.Requests).Instructions);
        Assert.NotNull((await repository.GetFoundryDeploymentAsync(queued.DeploymentId, CancellationToken.None))?.StartedAtUtc);
    }

    [Fact]
    public async Task RunDeploymentAsync_RecordsFailedWithProvisioningErrorText()
    {
        using var workspace = TempFoundryWorkspace.Create();
        using var repository = CreateRepository(workspace.DatabasePath);
        await SeedTargetAsync(repository);
        var service = CreateService(
            repository,
            new FakeDeploymentProvisioner((_, _) => Task.FromException<StorylineFoundryDeploymentProvisionResult>(
                new UnauthorizedAccessException("DefaultAzureCredential failed."))));

        var queued = await service.QueueDeploymentAsync(Target(), CancellationToken.None);
        var failed = await service.RunDeploymentAsync(queued.DeploymentId, CancellationToken.None);

        Assert.Equal(StorylineFoundryDeploymentStatus.Failed, failed?.Status);
        Assert.Contains("DefaultAzureCredential", failed?.ErrorText);
    }

    [Fact]
    public async Task PromoteDeploymentAsync_RejectsNonSucceededAndPromotesOnlyMatchingGraphBinding()
    {
        using var workspace = TempFoundryWorkspace.Create();
        using var repository = CreateRepository(workspace.DatabasePath);
        await SeedTargetAsync(repository);
        await repository.UpsertAgentBindingAsync(Binding("legacy-binding", string.Empty, "legacy-agent"), CancellationToken.None);
        await repository.UpsertAgentBindingAsync(Binding("old-graph-binding", GraphId, "old-graph-agent"), CancellationToken.None);
        await repository.UpsertAgentBindingAsync(Binding("other-graph-binding", OtherGraphId, "other-graph-agent"), CancellationToken.None);
        var service = CreateService(repository, new FakeDeploymentProvisioner());
        var queued = await service.QueueDeploymentAsync(Target(), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PromoteDeploymentAsync(queued.DeploymentId, new PromoteFoundryDeploymentRequest("tester"), CancellationToken.None));

        await service.RunDeploymentAsync(queued.DeploymentId, CancellationToken.None);
        var promoted = await service.PromoteDeploymentAsync(
            queued.DeploymentId,
            new PromoteFoundryDeploymentRequest("tester"),
            CancellationToken.None);

        var exact = await repository.GetAgentBindingAsync(PersonaId, PersonaVersionId, GraphId, CancellationToken.None);
        var otherGraph = await repository.GetAgentBindingAsync(PersonaId, PersonaVersionId, OtherGraphId, CancellationToken.None);
        var fallback = await repository.GetAgentBindingAsync(PersonaId, PersonaVersionId, "missing-graph", CancellationToken.None);

        Assert.Equal(StorylineFoundryDeploymentStatus.Promoted, promoted?.Status);
        Assert.Equal("tester", promoted?.PromotedBy);
        Assert.Equal($"foundry-{queued.DeploymentId}", exact?.BindingId);
        Assert.Equal("agent-v1", exact?.AgentVersion);
        Assert.Equal("other-graph-agent", otherGraph?.AgentName);
        Assert.Equal("legacy-agent", fallback?.AgentName);
    }

    private const string PersonaId = "persona-a";
    private const string PersonaVersionId = "persona-a:v1";
    private const string GraphId = "graph-a";
    private const string OtherGraphId = "graph-b";

    private static FoundryDeploymentTargetRequest Target() => new(PersonaId, PersonaVersionId, GraphId, "tester");

    private static StorylineFoundryDeploymentService CreateService(
        SqliteStorylineRepository repository,
        IStorylineFoundryDeploymentProvisioner provisioner,
        IStorylineFoundryDeploymentQueue? queue = null) =>
        new(
            repository,
            new StorylineFoundryInstructionBuilder(),
            provisioner,
            queue ?? new RecordingDeploymentQueue(),
            new FoundryPersonaRuntimeOptions
            {
                ProjectEndpoint = "https://example.test/api/projects/test",
                Model = "gpt-test",
                AgentName = "storyline-agent",
                TimeoutMs = 1000,
                MaxOutputTokens = 321
            });

    private static SqliteStorylineRepository CreateRepository(string databasePath) => new(new StorylineRuntimeOptions
    {
        DatabasePath = databasePath,
        SeedPath = string.Empty,
        ImportSeedOnEmpty = false,
        MaxMemorySummaryCharacters = 1200
    });

    private static async Task SeedTargetAsync(SqliteStorylineRepository repository)
    {
        await repository.UpsertPersonaProfileAsync(new PersonaProfile
        {
            PersonaId = PersonaId,
            DisplayName = "Persona A",
            Description = "Test persona."
        }, CancellationToken.None);
        await repository.UpsertPersonaVersionAsync(new PersonaVersion
        {
            PersonaId = PersonaId,
            PersonaVersionId = PersonaVersionId,
            Version = "v1",
            PromptSummary = "Test summary."
        }, CancellationToken.None);
        await repository.UpsertNarrativeGraphSnapshotAsync(
            new NarrativeGraph { GraphId = GraphId, Name = "Graph A", Description = "Test graph." },
            new[]
            {
                new NarrativeNode
                {
                    GraphId = GraphId,
                    NodeId = "start",
                    Title = "Start",
                    Summary = "Start summary.",
                    FallbackReply = "Fallback.",
                    SortOrder = 10
                }
            },
            Array.Empty<NarrativeTransition>(),
            CancellationToken.None);
    }

    private static AgentBinding Binding(string bindingId, string graphId, string agentName) => new()
    {
        BindingId = bindingId,
        PersonaId = PersonaId,
        PersonaVersionId = PersonaVersionId,
        GraphId = graphId,
        Model = "gpt-test",
        AgentName = agentName,
        AgentVersion = "old",
        MaxOutputTokens = 128,
        IsDefault = true,
        CreatedAtUtc = new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc)
    };

    private sealed class FakeDeploymentProvisioner : IStorylineFoundryDeploymentProvisioner
    {
        private readonly Func<StorylineFoundryDeploymentProvisionRequest, CancellationToken, Task<StorylineFoundryDeploymentProvisionResult>> _handler;

        public FakeDeploymentProvisioner()
            : this((_, _) => Task.FromResult(new StorylineFoundryDeploymentProvisionResult(
                "storyline-agent",
                "agent-v1",
                "agent-version-id",
                "gpt-test")))
        {
        }

        public FakeDeploymentProvisioner(Func<StorylineFoundryDeploymentProvisionRequest, CancellationToken, Task<StorylineFoundryDeploymentProvisionResult>> handler)
        {
            _handler = handler;
        }

        public List<StorylineFoundryDeploymentProvisionRequest> Requests { get; } = new();

        public async Task<StorylineFoundryDeploymentProvisionResult> DeployAsync(
            StorylineFoundryDeploymentProvisionRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return await _handler(request, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class RecordingDeploymentQueue : IStorylineFoundryDeploymentQueue
    {
        public List<string> QueuedDeploymentIds { get; } = new();

        public ValueTask QueueAsync(string deploymentId, CancellationToken cancellationToken)
        {
            QueuedDeploymentIds.Add(deploymentId);
            return ValueTask.CompletedTask;
        }

        public async IAsyncEnumerable<string> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class TempFoundryWorkspace : IDisposable
    {
        private TempFoundryWorkspace(string root)
        {
            Root = root;
            DatabasePath = Path.Combine(root, "storyline.sqlite");
        }

        public string Root { get; }
        public string DatabasePath { get; }

        public static TempFoundryWorkspace Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "wwow-storyline-foundry-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new TempFoundryWorkspace(root);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
