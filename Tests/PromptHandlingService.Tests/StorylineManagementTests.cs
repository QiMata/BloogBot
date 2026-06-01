using System.Text.Json;
using PromptHandlingService.Storylines;

namespace PromptHandlingService.Tests;

public sealed class StorylineManagementServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    [Fact]
    public async Task DraftPublish_PersonaProfile_WritesRuntimeAndMarksDraftPublished()
    {
        using var workspace = TempStorylineWorkspace.Create();
        using var repository = CreateRepository(workspace.DatabasePath);
        var service = CreateService(repository);
        var payload = new PersonaProfileDto("persona-a", "Persona A", "Test persona.", DateTime.UtcNow);

        var draft = await service.SaveDraftAsync(NewDraft(
            StorylineDraftKind.PersonaProfile,
            "persona-a",
            payload), CancellationToken.None);
        var result = await service.PublishDraftAsync(
            draft.DraftId,
            new PublishDraftRequest("tester", "publish profile"),
            CancellationToken.None);

        Assert.True(result.Published);
        Assert.Equal("Persona A", (await repository.GetPersonaProfileAsync("persona-a", CancellationToken.None))?.DisplayName);
        var published = await repository.GetStorylineDraftAsync(draft.DraftId, CancellationToken.None);
        Assert.Equal(StorylineDraftStatus.Published, published?.Status);
        Assert.Equal("tester", published?.PublishedBy);
    }

    [Fact]
    public async Task PublishNarrativeGraph_RejectsMissingTransitionNode()
    {
        using var workspace = TempStorylineWorkspace.Create();
        using var repository = CreateRepository(workspace.DatabasePath);
        var service = CreateService(repository);
        var graph = NewGraphDto("graph-a") with
        {
            Transitions = new[]
            {
                new NarrativeTransitionDto("t1", "graph-a", "start", "missing", "ask", string.Empty, 10)
            }
        };
        var draft = await service.SaveDraftAsync(NewDraft(
            StorylineDraftKind.NarrativeGraph,
            "graph-a",
            graph), CancellationToken.None);

        var result = await service.PublishDraftAsync(draft.DraftId, new PublishDraftRequest(), CancellationToken.None);

        Assert.False(result.Published);
        Assert.Contains(result.Errors, error => error.Code == "missing_node");
        Assert.Null(await repository.GetNarrativeGraphAsync("graph-a", CancellationToken.None));
    }

    [Fact]
    public async Task PublishGameplayArc_RejectsInvalidActivityId()
    {
        using var workspace = TempStorylineWorkspace.Create();
        using var repository = CreateRepository(workspace.DatabasePath);
        var service = CreateService(repository);
        var arc = new GameplayStoryArcDto(
            "arc-a",
            "v1",
            "Arc A",
            "Invalid activity.",
            false,
            DateTime.UtcNow,
            null,
            new[] { new GameplayArcStepDto("s1", "arc-a", "v1", 10, "missing.activity", "hook") });
        var draft = await service.SaveDraftAsync(NewDraft(
            StorylineDraftKind.GameplayArc,
            "arc-a:v1",
            arc), CancellationToken.None);

        var result = await service.PublishDraftAsync(draft.DraftId, new PublishDraftRequest(), CancellationToken.None);

        Assert.False(result.Published);
        Assert.Contains(result.Errors, error => error.Code == "invalid_activity_id");
    }

    [Fact]
    public async Task PublishNarrativeGraph_ReplacesSnapshotAndDeletesRemovedTransitions()
    {
        using var workspace = TempStorylineWorkspace.Create();
        using var repository = CreateRepository(workspace.DatabasePath);
        var service = CreateService(repository);
        var graph = NewGraphDto("graph-a") with
        {
            Nodes = new[]
            {
                NewNode("graph-a", "start"),
                NewNode("graph-a", "second")
            },
            Transitions = new[]
            {
                new NarrativeTransitionDto("t1", "graph-a", "start", "second", "go", string.Empty, 10),
                new NarrativeTransitionDto("t2", "graph-a", "second", "start", "back", string.Empty, 20)
            }
        };
        var firstDraft = await service.SaveDraftAsync(NewDraft(
            StorylineDraftKind.NarrativeGraph,
            "graph-a",
            graph), CancellationToken.None);
        Assert.True((await service.PublishDraftAsync(firstDraft.DraftId, new PublishDraftRequest(), CancellationToken.None)).Published);

        var secondDraft = await service.SaveDraftAsync(NewDraft(
            StorylineDraftKind.NarrativeGraph,
            "graph-a",
            graph with
            {
                Transitions = new[]
                {
                    new NarrativeTransitionDto("t1", "graph-a", "start", "second", "go", string.Empty, 10)
                }
            }), CancellationToken.None);
        Assert.True((await service.PublishDraftAsync(secondDraft.DraftId, new PublishDraftRequest(), CancellationToken.None)).Published);

        var transitions = await repository.ListNarrativeTransitionsAsync("graph-a", CancellationToken.None);
        Assert.Equal("t1", Assert.Single(transitions).TransitionId);
    }

    [Fact]
    public async Task PublishCharacterBinding_UpdatesAuthoringAndRuntimeRecords()
    {
        using var workspace = TempStorylineWorkspace.Create();
        using var repository = CreateRepository(workspace.DatabasePath);
        var service = CreateService(repository);
        await SeedPersonaAndGraphAsync(repository);
        await PublishValidArcAsync(service);
        var binding = new CharacterStoryBindingDto(
            "char-a",
            "Guide A",
            "Westworld-Test",
            "persona-a",
            "persona-a:v1",
            "graph-a",
            "start",
            string.Empty,
            "arc-a",
            "v1",
            "steady",
            DateTime.UtcNow);
        var draft = await service.SaveDraftAsync(NewDraft(
            StorylineDraftKind.CharacterBinding,
            "char-a",
            binding), CancellationToken.None);

        var result = await service.PublishDraftAsync(draft.DraftId, new PublishDraftRequest(), CancellationToken.None);

        Assert.True(result.Published);
        Assert.Equal("arc-a", (await repository.GetCharacterStoryBindingAsync("char-a", CancellationToken.None))?.GameplayArcId);
        Assert.Equal("start", (await repository.GetCharacterStateAsync("char-a", CancellationToken.None))?.ActiveNodeId);
        Assert.NotNull(await repository.GetConversationBindingAsync("char-a", null, CancellationToken.None));
    }

    [Fact]
    public async Task MemoryCandidateReview_ApprovesAndRejectsCandidates()
    {
        using var workspace = TempStorylineWorkspace.Create();
        using var repository = CreateRepository(workspace.DatabasePath);
        var service = CreateService(repository);
        await repository.AddMemoryCandidateAsync(new MemoryCandidate
        {
            CandidateId = "candidate-a",
            CharacterId = "char-a",
            PersonaId = "persona-a",
            SourceInput = "hello",
            CandidateText = "Player said hello.",
            Status = StorylineMemoryStatus.Pending,
            ActiveNodeId = "start"
        }, CancellationToken.None);
        await repository.AddMemoryCandidateAsync(new MemoryCandidate
        {
            CandidateId = "candidate-b",
            CharacterId = "char-a",
            PersonaId = "persona-a",
            SourceInput = "bye",
            CandidateText = "Player left.",
            Status = StorylineMemoryStatus.Pending,
            ActiveNodeId = "start"
        }, CancellationToken.None);

        await service.ReviewMemoryCandidateAsync(
            "candidate-a",
            new MemoryCandidateReviewDto("candidate-a", StorylineMemoryStatus.Approved, "tester", string.Empty),
            CancellationToken.None);
        await service.ReviewMemoryCandidateAsync(
            "candidate-b",
            new MemoryCandidateReviewDto("candidate-b", StorylineMemoryStatus.Rejected, "tester", string.Empty),
            CancellationToken.None);

        Assert.Equal(StorylineMemoryStatus.Approved, (await repository.GetMemoryCandidateAsync("candidate-a", CancellationToken.None))?.Status);
        Assert.Equal(StorylineMemoryStatus.Rejected, (await repository.GetMemoryCandidateAsync("candidate-b", CancellationToken.None))?.Status);
        Assert.Equal("Player said hello.", Assert.Single(await repository.GetApprovedMemoryFactsAsync("char-a", "persona-a", CancellationToken.None)).Text);
    }

    [Fact]
    public async Task GraphLayout_RoundTripsSeparatelyFromRuntimeGraph()
    {
        using var workspace = TempStorylineWorkspace.Create();
        using var repository = CreateRepository(workspace.DatabasePath);
        var service = CreateService(repository);

        await service.SaveGraphLayoutAsync(new GraphLayoutDto(
            "graph-a",
            new[] { new GraphLayoutNodeDto("start", 42, 84) },
            1.25,
            DateTime.UtcNow), CancellationToken.None);

        var layout = await service.GetGraphLayoutAsync("graph-a", CancellationToken.None);
        Assert.Equal(42, Assert.Single(layout?.Nodes ?? Array.Empty<GraphLayoutNodeDto>()).X);
        Assert.Null(await repository.GetNarrativeGraphAsync("graph-a", CancellationToken.None));
    }

    private static async Task SeedPersonaAndGraphAsync(SqliteStorylineRepository repository)
    {
        await repository.UpsertPersonaProfileAsync(new PersonaProfile
        {
            PersonaId = "persona-a",
            DisplayName = "Persona A"
        }, CancellationToken.None);
        await repository.UpsertPersonaVersionAsync(new PersonaVersion
        {
            PersonaId = "persona-a",
            PersonaVersionId = "persona-a:v1",
            Version = "v1",
            PromptSummary = "short"
        }, CancellationToken.None);
        await repository.UpsertNarrativeGraphSnapshotAsync(
            new NarrativeGraph { GraphId = "graph-a", Name = "Graph A" },
            new[] { new NarrativeNode { GraphId = "graph-a", NodeId = "start", Title = "Start" } },
            Array.Empty<NarrativeTransition>(),
            CancellationToken.None);
    }

    private static async Task PublishValidArcAsync(StorylineManagementService service)
    {
        var arc = new GameplayStoryArcDto(
            "arc-a",
            "v1",
            "Arc A",
            "Valid activity.",
            false,
            DateTime.UtcNow,
            null,
            new[] { new GameplayArcStepDto("s1", "arc-a", "v1", 10, FakeStorylineActivityCatalog.ValidActivityId, "hook") });
        var draft = await service.SaveDraftAsync(NewDraft(StorylineDraftKind.GameplayArc, "arc-a:v1", arc), CancellationToken.None);
        Assert.True((await service.PublishDraftAsync(draft.DraftId, new PublishDraftRequest(), CancellationToken.None)).Published);
    }

    private static SqliteStorylineRepository CreateRepository(string databasePath) => new(new StorylineRuntimeOptions
    {
        DatabasePath = databasePath,
        SeedPath = string.Empty,
        ImportSeedOnEmpty = false,
        MaxMemorySummaryCharacters = 1200
    });

    private static StorylineManagementService CreateService(IStorylineRepository repository) =>
        new(repository, new FakeStorylineActivityCatalog());

    private static StorylineDraftDto NewDraft<T>(string kind, string targetId, T payload) => new(
        string.Empty,
        kind,
        targetId,
        StorylineDraftStatus.Draft,
        JsonSerializer.Serialize(payload, JsonOptions),
        default,
        default,
        null,
        string.Empty,
        string.Empty);

    private static NarrativeGraphDto NewGraphDto(string graphId) => new(
        graphId,
        "Graph A",
        "Test graph.",
        DateTime.UtcNow,
        new[] { NewNode(graphId, "start") },
        Array.Empty<NarrativeTransitionDto>());

    private static NarrativeNodeDto NewNode(string graphId, string nodeId) =>
        new(nodeId, graphId, nodeId, "Summary.", "Fallback.", 10);
}

file sealed class FakeStorylineActivityCatalog : IStorylineActivityCatalog
{
    public const string ValidActivityId = "quest.starter.durotar";

    public IReadOnlyList<ActivityCatalogItemDto> List() => new[]
    {
        new ActivityCatalogItemDto(
            ValidActivityId,
            "Questing",
            "Durotar",
            1,
            10,
            "Horde",
            "Durotar starter questing")
    };

    public bool Contains(string activityId) =>
        string.Equals(activityId, ValidActivityId, StringComparison.Ordinal);
}

file sealed class TempStorylineWorkspace : IDisposable
{
    private TempStorylineWorkspace(string root)
    {
        Root = root;
        DatabasePath = Path.Combine(root, "storyline.sqlite");
    }

    public string Root { get; }
    public string DatabasePath { get; }

    public static TempStorylineWorkspace Create()
    {
        var root = Path.Combine(Path.GetTempPath(), "wwow-storyline-management-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return new TempStorylineWorkspace(root);
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
