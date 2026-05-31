using System.Text.RegularExpressions;
using PromptHandlingService.Foundry;
using PromptHandlingService.Storylines;
using SQLite;

namespace PromptHandlingService.Tests;

public sealed class StorylineRepositoryTests
{
    [Fact]
    public void Repository_CreatesSQLiteSchema()
    {
        using var workspace = TempWorkspace.Create();
        using var repository = CreateRepository(workspace.DatabasePath);

        using var connection = new SQLiteConnection(workspace.DatabasePath);
        var tableNames = connection.Query<SchemaTable>(
                "select name as Name from sqlite_master where type = 'table' order by name")
            .Select(table => table.Name)
            .ToArray();

        Assert.Contains("PersonaProfile", tableNames);
        Assert.Contains("PersonaVersion", tableNames);
        Assert.Contains("CharacterState", tableNames);
        Assert.Contains("MemoryFact", tableNames);
        Assert.Contains("MemoryEpisode", tableNames);
        Assert.Contains("MemoryCandidate", tableNames);
        Assert.Contains("NarrativeGraph", tableNames);
        Assert.Contains("NarrativeNode", tableNames);
        Assert.Contains("NarrativeTransition", tableNames);
        Assert.Contains("AgentBinding", tableNames);
        Assert.Contains("ConversationBinding", tableNames);
        Assert.Contains("StorylineDraft", tableNames);
        Assert.Contains("GameplayStoryArc", tableNames);
        Assert.Contains("GameplayArcStep", tableNames);
        Assert.Contains("CharacterStoryBinding", tableNames);
        Assert.Contains("GraphLayout", tableNames);
    }

    [Fact]
    public async Task SeedImport_IsIdempotent_WhenDatabaseAlreadyHasPersonaAndGraphRows()
    {
        using var workspace = TempWorkspace.Create();
        var seedPath = StorylineTestData.FindRepoFile("Config/foundry/storyline-seed.json");

        using (var repository = CreateRepository(workspace.DatabasePath, seedPath: seedPath, importSeed: true))
        {
            Assert.Equal(1, await repository.CountPersonasAsync(CancellationToken.None));
            Assert.Equal(1, await repository.CountNarrativeGraphsAsync(CancellationToken.None));
        }

        using (var repository = CreateRepository(workspace.DatabasePath, seedPath: seedPath, importSeed: true))
        {
            Assert.Equal(1, await repository.CountPersonasAsync(CancellationToken.None));
            Assert.Equal(1, await repository.CountNarrativeGraphsAsync(CancellationToken.None));
            var state = await repository.GetCharacterStateAsync("seed-durotar-host-001", CancellationToken.None);
            Assert.Equal("arrival-greeting", state?.ActiveNodeId);
        }
    }

    [Fact]
    public async Task Repository_RoundTripsAllStorylineRecords()
    {
        using var workspace = TempWorkspace.Create();
        using var repository = CreateRepository(workspace.DatabasePath);
        await StorylineTestData.InsertCoreAsync(repository);

        var fact = new MemoryFact
        {
            FactId = "fact-1",
            CharacterId = StorylineTestData.CharacterId,
            PersonaId = StorylineTestData.PersonaId,
            Text = "Player prefers short directions.",
            Importance = 7,
            Status = StorylineMemoryStatus.Approved,
            CreatedAtUtc = new DateTime(2026, 5, 24, 1, 0, 0, DateTimeKind.Utc),
            ApprovedAtUtc = new DateTime(2026, 5, 24, 1, 1, 0, DateTimeKind.Utc)
        };
        var episode = new MemoryEpisode
        {
            EpisodeId = "episode-1",
            CharacterId = StorylineTestData.CharacterId,
            PersonaId = StorylineTestData.PersonaId,
            Summary = "Helped a traveler find the inn.",
            Importance = 6,
            Status = StorylineMemoryStatus.Approved,
            OccurredAtUtc = new DateTime(2026, 5, 24, 1, 2, 0, DateTimeKind.Utc)
        };
        var conversation = new ConversationBinding
        {
            CharacterId = StorylineTestData.CharacterId,
            GuestId = "guest-1",
            PersonaId = StorylineTestData.PersonaId,
            PersonaVersionId = StorylineTestData.PersonaVersionId,
            GraphId = StorylineTestData.GraphId,
            ActiveNodeId = StorylineTestData.NodeId
        };
        var candidate = new MemoryCandidate
        {
            CandidateId = "candidate-1",
            CharacterId = StorylineTestData.CharacterId,
            PersonaId = StorylineTestData.PersonaId,
            SourceInput = "Where is the inn?",
            CandidateText = "Guest asked about the inn.",
            Status = StorylineMemoryStatus.Pending,
            FoundryIntent = "answer",
            ActiveNodeId = StorylineTestData.NodeId
        };

        await repository.UpsertMemoryFactAsync(fact, CancellationToken.None);
        await repository.UpsertMemoryEpisodeAsync(episode, CancellationToken.None);
        await repository.UpsertConversationBindingAsync(conversation, CancellationToken.None);
        await repository.AddMemoryCandidateAsync(candidate, CancellationToken.None);

        Assert.Equal("Durotar Guide", (await repository.GetPersonaProfileAsync(StorylineTestData.PersonaId, CancellationToken.None))?.DisplayName);
        Assert.Equal("v1", (await repository.GetPersonaVersionAsync(StorylineTestData.PersonaId, StorylineTestData.PersonaVersionId, CancellationToken.None))?.Version);
        Assert.Equal(StorylineTestData.NodeId, (await repository.GetCharacterStateAsync(StorylineTestData.CharacterId, CancellationToken.None))?.ActiveNodeId);
        Assert.Equal("arrival-to-inn", Assert.Single(await repository.GetNarrativeTransitionsAsync(StorylineTestData.GraphId, StorylineTestData.NodeId, CancellationToken.None)).TransitionId);
        Assert.Equal("test-agent", (await repository.GetAgentBindingAsync(StorylineTestData.PersonaId, StorylineTestData.PersonaVersionId, CancellationToken.None))?.AgentName);
        Assert.Equal("guest-1", (await repository.GetConversationBindingAsync(StorylineTestData.CharacterId, "guest-1", CancellationToken.None))?.GuestId);
        Assert.Equal("Player prefers short directions.", Assert.Single(await repository.GetApprovedMemoryFactsAsync(StorylineTestData.CharacterId, StorylineTestData.PersonaId, CancellationToken.None)).Text);
        Assert.Equal("Helped a traveler find the inn.", Assert.Single(await repository.GetApprovedMemoryEpisodesAsync(StorylineTestData.CharacterId, StorylineTestData.PersonaId, CancellationToken.None)).Summary);
        Assert.Equal("Guest asked about the inn.", Assert.Single(await repository.GetMemoryCandidatesAsync(StorylineTestData.CharacterId, StorylineMemoryStatus.Pending, CancellationToken.None)).CandidateText);
    }

    private static SqliteStorylineRepository CreateRepository(string databasePath, string seedPath = "", bool importSeed = false) => new(new StorylineRuntimeOptions
    {
        DatabasePath = databasePath,
        SeedPath = seedPath,
        ImportSeedOnEmpty = importSeed,
        MaxMemorySummaryCharacters = 1200
    });

    public sealed class SchemaTable
    {
        public string Name { get; set; } = string.Empty;
    }
}

public sealed class StorylineContextResolverTests
{
    [Fact]
    public async Task ResolveAsync_AssemblesPromptInDeterministicOrder()
    {
        using var workspace = TempWorkspace.Create();
        using var repository = StorylineTestData.CreateRepository(workspace.DatabasePath);
        await StorylineTestData.InsertCoreAsync(repository);
        await repository.UpsertMemoryFactAsync(new MemoryFact
        {
            FactId = "fact-a",
            CharacterId = StorylineTestData.CharacterId,
            PersonaId = StorylineTestData.PersonaId,
            Text = "Knows the inn is near the central road.",
            Importance = 10,
            Status = StorylineMemoryStatus.Approved,
            CreatedAtUtc = new DateTime(2026, 5, 24, 2, 0, 0, DateTimeKind.Utc)
        }, CancellationToken.None);

        var resolver = new StorylineContextResolver(repository, StorylineTestData.Options(workspace.DatabasePath));
        var context = await resolver.ResolveAsync(StorylineTestData.Input(), CancellationToken.None);
        var prompt = new PersonaPromptAssembler().Assemble(context.PromptRequest);

        Assert.Equal("durotar-guide", context.PromptRequest.PersonaId);
        Assert.Equal("A concise Razor Hill guide.", context.PromptRequest.PersonaDescription);
        Assert.Equal("Speak in short local guidance.", context.PromptRequest.PersonaPromptSummary);
        Assert.Contains("arrival-greeting", context.PromptRequest.ActiveNarrativeNode);
        Assert.Equal("arrival-to-inn", Assert.Single(context.OutboundTransitions).TransitionId);
        Assert.True(prompt.IndexOf("persona:", StringComparison.Ordinal) < prompt.IndexOf("character:", StringComparison.Ordinal));
        Assert.True(prompt.IndexOf("narrative:", StringComparison.Ordinal) < prompt.IndexOf("memory:", StringComparison.Ordinal));
        Assert.True(prompt.IndexOf("memory:", StringComparison.Ordinal) < prompt.IndexOf("input:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ResolveAsync_FailsDeterministically_WhenRequiredStateIsMissing()
    {
        using var workspace = TempWorkspace.Create();
        using var repository = StorylineTestData.CreateRepository(workspace.DatabasePath);
        var resolver = new StorylineContextResolver(repository, StorylineTestData.Options(workspace.DatabasePath));

        var ex = await Assert.ThrowsAsync<StorylineRuntimeException>(
            () => resolver.ResolveAsync(StorylineTestData.Input(), CancellationToken.None));
        Assert.Contains("Missing character state", ex.Message);
    }

    [Fact]
    public async Task ResolveAsync_FailsDeterministically_WhenPersonaVersionIsMissing()
    {
        using var workspace = TempWorkspace.Create();
        using var repository = StorylineTestData.CreateRepository(workspace.DatabasePath);
        await repository.UpsertPersonaProfileAsync(StorylineTestData.Profile(), CancellationToken.None);
        await repository.UpsertCharacterStateAsync(StorylineTestData.State(), CancellationToken.None);
        var resolver = new StorylineContextResolver(repository, StorylineTestData.Options(workspace.DatabasePath));

        var ex = await Assert.ThrowsAsync<StorylineRuntimeException>(
            () => resolver.ResolveAsync(StorylineTestData.Input(), CancellationToken.None));
        Assert.Contains("Missing persona version", ex.Message);
    }

    [Fact]
    public async Task ResolveAsync_FailsDeterministically_WhenNarrativeNodeIsMissing()
    {
        using var workspace = TempWorkspace.Create();
        using var repository = StorylineTestData.CreateRepository(workspace.DatabasePath);
        await repository.UpsertPersonaProfileAsync(StorylineTestData.Profile(), CancellationToken.None);
        await repository.UpsertPersonaVersionAsync(StorylineTestData.Version(), CancellationToken.None);
        await repository.UpsertCharacterStateAsync(StorylineTestData.State(), CancellationToken.None);
        var resolver = new StorylineContextResolver(repository, StorylineTestData.Options(workspace.DatabasePath));

        var ex = await Assert.ThrowsAsync<StorylineRuntimeException>(
            () => resolver.ResolveAsync(StorylineTestData.Input(), CancellationToken.None));
        Assert.Contains("Missing narrative node", ex.Message);
    }

    [Fact]
    public async Task ResolveAsync_FailsDeterministically_WhenAgentBindingIsMissing()
    {
        using var workspace = TempWorkspace.Create();
        using var repository = StorylineTestData.CreateRepository(workspace.DatabasePath);
        await repository.UpsertPersonaProfileAsync(StorylineTestData.Profile(), CancellationToken.None);
        await repository.UpsertPersonaVersionAsync(StorylineTestData.Version(), CancellationToken.None);
        await repository.UpsertCharacterStateAsync(StorylineTestData.State(), CancellationToken.None);
        await repository.UpsertNarrativeGraphAsync(StorylineTestData.Graph(), CancellationToken.None);
        await repository.UpsertNarrativeNodeAsync(StorylineTestData.Node(), CancellationToken.None);
        var resolver = new StorylineContextResolver(repository, StorylineTestData.Options(workspace.DatabasePath));

        var ex = await Assert.ThrowsAsync<StorylineRuntimeException>(
            () => resolver.ResolveAsync(StorylineTestData.Input(), CancellationToken.None));
        Assert.Contains("Missing agent binding", ex.Message);
    }

    [Fact]
    public async Task ResolveAsync_MemorySummary_UsesApprovedMemoryInDeterministicOrderAndLimit()
    {
        using var workspace = TempWorkspace.Create();
        using var repository = StorylineTestData.CreateRepository(workspace.DatabasePath);
        await StorylineTestData.InsertCoreAsync(repository);
        await repository.UpsertMemoryFactAsync(new MemoryFact
        {
            FactId = "fact-low",
            CharacterId = StorylineTestData.CharacterId,
            PersonaId = StorylineTestData.PersonaId,
            Text = "low approved memory should be last",
            Importance = 1,
            Status = StorylineMemoryStatus.Approved,
            CreatedAtUtc = new DateTime(2026, 5, 24, 1, 0, 0, DateTimeKind.Utc)
        }, CancellationToken.None);
        await repository.UpsertMemoryFactAsync(new MemoryFact
        {
            FactId = "fact-high",
            CharacterId = StorylineTestData.CharacterId,
            PersonaId = StorylineTestData.PersonaId,
            Text = "high approved memory should be first",
            Importance = 10,
            Status = StorylineMemoryStatus.Approved,
            CreatedAtUtc = new DateTime(2026, 5, 24, 2, 0, 0, DateTimeKind.Utc)
        }, CancellationToken.None);
        await repository.UpsertMemoryEpisodeAsync(new MemoryEpisode
        {
            EpisodeId = "episode-pending",
            CharacterId = StorylineTestData.CharacterId,
            PersonaId = StorylineTestData.PersonaId,
            Summary = "pending episode must not appear",
            Importance = 99,
            Status = StorylineMemoryStatus.Pending,
            OccurredAtUtc = new DateTime(2026, 5, 24, 3, 0, 0, DateTimeKind.Utc)
        }, CancellationToken.None);

        var options = new StorylineRuntimeOptions
        {
            DatabasePath = workspace.DatabasePath,
            SeedPath = string.Empty,
            ImportSeedOnEmpty = false,
            MaxMemorySummaryCharacters = 90
        };
        var resolver = new StorylineContextResolver(repository, options);
        var context = await resolver.ResolveAsync(StorylineTestData.Input(), CancellationToken.None);

        Assert.True(context.PromptRequest.CompactMemorySummary.Length <= 90);
        Assert.StartsWith("fact: high approved memory should be first", context.PromptRequest.CompactMemorySummary);
        Assert.Contains("low approved", context.PromptRequest.CompactMemorySummary);
        Assert.DoesNotContain("pending episode", context.PromptRequest.CompactMemorySummary);
    }
}

public sealed class StorylinePersonaRuntimeTests
{
    [Fact]
    public async Task GenerateAsync_StoresFoundryMemoryCandidatesAsPendingOnly()
    {
        using var workspace = TempWorkspace.Create();
        using var repository = StorylineTestData.CreateRepository(workspace.DatabasePath);
        await StorylineTestData.InsertCoreAsync(repository);
        var foundry = new FakeFoundryPersonaRuntime(new PersonaPromptResult(
            "The inn is just off the main road.",
            "answer",
            new[] { "Guest asked for inn directions.", "Guest prefers short answers." },
            "local answer",
            "test-agent",
            "v2",
            "gpt-test"));
        var runtime = StorylineTestData.CreateRuntime(repository, foundry, workspace.DatabasePath);

        var result = await runtime.GenerateAsync(StorylineTestData.Input(), CancellationToken.None);
        var pending = await repository.GetMemoryCandidatesAsync(StorylineTestData.CharacterId, StorylineMemoryStatus.Pending, CancellationToken.None);
        var approvedFacts = await repository.GetApprovedMemoryFactsAsync(StorylineTestData.CharacterId, StorylineTestData.PersonaId, CancellationToken.None);

        Assert.False(result.UsedDeterministicFallback);
        Assert.Equal(2, pending.Count);
        Assert.All(pending, candidate => Assert.Equal(StorylineMemoryStatus.Pending, candidate.Status));
        Assert.Equal(2, result.PendingMemoryCandidateIds.Count);
        Assert.Empty(approvedFacts);
    }

    [Fact]
    public async Task GenerateAsync_RejectsDisallowedFoundryIntentWithDeterministicFallback()
    {
        using var workspace = TempWorkspace.Create();
        using var repository = StorylineTestData.CreateRepository(workspace.DatabasePath);
        await StorylineTestData.InsertCoreAsync(repository);
        var foundry = new FakeFoundryPersonaRuntime(new PersonaPromptResult(
            "I will trade you supplies.",
            "trade",
            new[] { "should not be stored" },
            "bad intent",
            "test-agent",
            "v2",
            "gpt-test"));
        var runtime = StorylineTestData.CreateRuntime(repository, foundry, workspace.DatabasePath);

        var result = await runtime.GenerateAsync(StorylineTestData.Input(), CancellationToken.None);
        var pending = await repository.GetMemoryCandidatesAsync(StorylineTestData.CharacterId, StorylineMemoryStatus.Pending, CancellationToken.None);

        Assert.True(result.UsedDeterministicFallback);
        Assert.Equal("fallback", result.Intent);
        Assert.Equal("The guide can only offer directions and conversation.", result.ReplyText);
        Assert.Equal("disallowed-intent:trade", result.FallbackReason);
        Assert.Empty(pending);
    }

    [Fact]
    public async Task GenerateAsync_PassesPerPersonaRuntimeBindingToFoundry()
    {
        using var workspace = TempWorkspace.Create();
        using var repository = StorylineTestData.CreateRepository(workspace.DatabasePath);
        await StorylineTestData.InsertCoreAsync(repository, StorylineTestData.Binding() with
        {
            Model = "gpt-persona",
            AgentName = "durotar-agent",
            AgentVersion = "agent-v7",
            MaxOutputTokens = 321
        });
        var foundry = new FakeFoundryPersonaRuntime(new PersonaPromptResult(
            "Welcome to Razor Hill.",
            "greeting",
            Array.Empty<string>(),
            "ok",
            "durotar-agent",
            "agent-v7",
            "gpt-persona"));
        var runtime = StorylineTestData.CreateRuntime(repository, foundry, workspace.DatabasePath);

        await runtime.GenerateAsync(StorylineTestData.Input(), CancellationToken.None);

        Assert.NotNull(foundry.CapturedBinding);
        Assert.Equal("gpt-persona", foundry.CapturedBinding!.Model);
        Assert.Equal("durotar-agent", foundry.CapturedBinding.AgentName);
        Assert.Equal("agent-v7", foundry.CapturedBinding.AgentVersion);
        Assert.Equal(321, foundry.CapturedBinding.MaxOutputTokens);
    }

    [Fact]
    public async Task GenerateAsync_RejectsEmptyInputText()
    {
        using var workspace = TempWorkspace.Create();
        using var repository = StorylineTestData.CreateRepository(workspace.DatabasePath);
        await StorylineTestData.InsertCoreAsync(repository);
        var runtime = StorylineTestData.CreateRuntime(repository, new FakeFoundryPersonaRuntime(), workspace.DatabasePath);

        await Assert.ThrowsAsync<ArgumentException>(
            () => runtime.GenerateAsync(StorylineTestData.Input() with { InputText = " " }, CancellationToken.None));
    }
}

public sealed class StorylineConfigTests
{
    [Fact]
    public void StorylineSeedAndConfig_DoNotContainSecrets()
    {
        var runtimePath = StorylineTestData.FindRepoFile("Config/foundry/storyline-runtime.json");
        var seedPath = StorylineTestData.FindRepoFile("Config/foundry/storyline-seed.json");
        var combined = File.ReadAllText(runtimePath) + "\n" + File.ReadAllText(seedPath);

        Assert.DoesNotMatch(
            new Regex("(api[_-]?key|secret|password|bearer|access[_-]?token)", RegexOptions.IgnoreCase),
            combined);
    }
}

public sealed class StorylinePersonaLiveSmokeTests
{
    [Fact(Skip = "Live Foundry storyline smoke. Remove skip and set WWOW_FOUNDRY_LIVE=1 to run.")]
    public async Task SeededStorylineRuntimeSmoke_UsesConfiguredFoundryProject()
    {
        Assert.Equal("1", Environment.GetEnvironmentVariable("WWOW_FOUNDRY_LIVE"));
        using var workspace = TempWorkspace.Create();
        using var repository = new SqliteStorylineRepository(new StorylineRuntimeOptions
        {
            DatabasePath = workspace.DatabasePath,
            SeedPath = StorylineTestData.FindRepoFile("Config/foundry/storyline-seed.json"),
            ImportSeedOnEmpty = true,
            MaxMemorySummaryCharacters = 1200
        });
        var foundry = new FoundryPersonaRuntime(LiveOptions());
        var runtime = StorylineTestData.CreateRuntime(repository, foundry, workspace.DatabasePath);

        var result = await runtime.GenerateAsync(new StorylinePromptInput(
            "seed-durotar-host-001",
            "Grask Trailwatch",
            "Westworld-Test",
            null,
            "Where is the inn?",
            "ask-inn"), CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(result.ReplyText));
    }

    private static FoundryPersonaRuntimeOptions LiveOptions() => new()
    {
        ProjectEndpoint = "https://atlsqlsattest-resource.services.ai.azure.com/api/projects/atlsqlsattest",
        Model = "gpt-5-mini",
        AgentName = "wwow-persona-runtime-dev",
        TimeoutMs = 30_000,
        MaxOutputTokens = 512
    };
}

file static class StorylineTestData
{
    public const string PersonaId = "durotar-guide";
    public const string PersonaVersionId = "durotar-guide:v1";
    public const string CharacterId = "char-1";
    public const string GraphId = "graph-1";
    public const string NodeId = "arrival-greeting";

    public static SqliteStorylineRepository CreateRepository(string databasePath) => new(Options(databasePath));

    public static StorylineRuntimeOptions Options(string databasePath) => new()
    {
        DatabasePath = databasePath,
        SeedPath = string.Empty,
        ImportSeedOnEmpty = false,
        MaxMemorySummaryCharacters = 1200
    };

    public static async Task InsertCoreAsync(SqliteStorylineRepository repository, AgentBinding? binding = null)
    {
        await repository.UpsertPersonaProfileAsync(Profile(), CancellationToken.None);
        await repository.UpsertPersonaVersionAsync(Version(), CancellationToken.None);
        await repository.UpsertCharacterStateAsync(State(), CancellationToken.None);
        await repository.UpsertNarrativeGraphAsync(Graph(), CancellationToken.None);
        await repository.UpsertNarrativeNodeAsync(Node(), CancellationToken.None);
        await repository.UpsertNarrativeTransitionAsync(Transition(), CancellationToken.None);
        await repository.UpsertAgentBindingAsync(binding ?? Binding(), CancellationToken.None);
    }

    public static StorylinePersonaRuntime CreateRuntime(
        SqliteStorylineRepository repository,
        IFoundryPersonaRuntime foundry,
        string databasePath)
    {
        var options = Options(databasePath);
        var resolver = new StorylineContextResolver(repository, options);
        return new StorylinePersonaRuntime(resolver, repository, foundry);
    }

    public static StorylinePromptInput Input() => new(
        CharacterId,
        "Grask Trailwatch",
        "Westworld-Test",
        "guest-1",
        "Where is the inn?",
        "ask-inn");

    public static PersonaProfile Profile() => new()
    {
        PersonaId = PersonaId,
        DisplayName = "Durotar Guide",
        Description = "A concise Razor Hill guide.",
        CreatedAtUtc = new DateTime(2026, 5, 24, 0, 0, 0, DateTimeKind.Utc)
    };

    public static PersonaVersion Version() => new()
    {
        PersonaVersionId = PersonaVersionId,
        PersonaId = PersonaId,
        Version = "v1",
        PromptSummary = "Speak in short local guidance.",
        IsActive = true,
        CreatedAtUtc = new DateTime(2026, 5, 24, 0, 0, 0, DateTimeKind.Utc)
    };

    public static CharacterState State() => new()
    {
        CharacterId = CharacterId,
        CharacterName = "Grask Trailwatch",
        Realm = "Westworld-Test",
        PersonaId = PersonaId,
        PersonaVersionId = PersonaVersionId,
        ActiveGraphId = GraphId,
        ActiveNodeId = NodeId,
        MoodState = "steady",
        UpdatedAtUtc = new DateTime(2026, 5, 24, 0, 0, 0, DateTimeKind.Utc)
    };

    public static NarrativeGraph Graph() => new()
    {
        GraphId = GraphId,
        Name = "Durotar loop",
        Description = "Small test graph."
    };

    public static NarrativeNode Node() => new()
    {
        GraphId = GraphId,
        NodeId = NodeId,
        Title = "Arrival greeting",
        Summary = "Welcome and point to town services.",
        FallbackReply = "The guide can only offer directions and conversation.",
        SortOrder = 10
    };

    public static NarrativeTransition Transition() => new()
    {
        TransitionId = "arrival-to-inn",
        GraphId = GraphId,
        FromNodeId = NodeId,
        ToNodeId = "inn-directions",
        TriggerKind = "ask-inn",
        GuardExpression = "deterministic",
        SortOrder = 10
    };

    public static AgentBinding Binding() => new()
    {
        BindingId = "binding-1",
        PersonaId = PersonaId,
        PersonaVersionId = PersonaVersionId,
        Model = "gpt-test",
        AgentName = "test-agent",
        AgentVersion = "v2",
        MaxOutputTokens = 256,
        IsDefault = true,
        CreatedAtUtc = new DateTime(2026, 5, 24, 0, 0, 0, DateTimeKind.Utc)
    };

    public static string FindRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(relativePath);
    }
}

file sealed class FakeFoundryPersonaRuntime : IFoundryPersonaRuntime
{
    private readonly PersonaPromptResult _result;

    public FakeFoundryPersonaRuntime()
        : this(new PersonaPromptResult(
            "Welcome.",
            "greeting",
            Array.Empty<string>(),
            string.Empty,
            "test-agent",
            "v2",
            "gpt-test"))
    {
    }

    public FakeFoundryPersonaRuntime(PersonaPromptResult result)
    {
        _result = result;
    }

    public PersonaPromptRequest? CapturedRequest { get; private set; }
    public PersonaPromptRuntimeBinding? CapturedBinding { get; private set; }

    public Task<PersonaPromptResult> GenerateAsync(PersonaPromptRequest request, CancellationToken cancellationToken)
    {
        return GenerateAsync(request, new PersonaPromptRuntimeBinding("gpt-test", "test-agent", "v2", 256), cancellationToken);
    }

    public Task<PersonaPromptResult> GenerateAsync(
        PersonaPromptRequest request,
        PersonaPromptRuntimeBinding binding,
        CancellationToken cancellationToken)
    {
        CapturedRequest = request;
        CapturedBinding = binding;
        return Task.FromResult(_result);
    }
}

file sealed class TempWorkspace : IDisposable
{
    private TempWorkspace(string root)
    {
        Root = root;
        DatabasePath = Path.Combine(root, "storyline.sqlite");
    }

    public string Root { get; }
    public string DatabasePath { get; }

    public static TempWorkspace Create()
    {
        var root = Path.Combine(Path.GetTempPath(), "wwow-storyline-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return new TempWorkspace(root);
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
