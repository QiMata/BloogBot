using System.Text.Json;
using SQLite;

namespace PromptHandlingService.Storylines;

public sealed class SqliteStorylineRepository : IStorylineRepository, IDisposable
{
    private static readonly JsonSerializerOptions SeedJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly SQLiteConnection _connection;
    private readonly object _sync = new();
    private bool _disposed;

    public SqliteStorylineRepository(StorylineRuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        DatabasePath = options.DatabasePath;
        EnsureDirectoryExists(DatabasePath);
        _connection = new SQLiteConnection(DatabasePath);
        InitializeSchema();

        if (options.ImportSeedOnEmpty && File.Exists(options.SeedPath) && HasNoSeedTargetRows())
        {
            ImportSeedFile(options.SeedPath);
        }
    }

    public string DatabasePath { get; }

    public Task<int> CountPersonasAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            EnsureNotDisposed();
            return Task.FromResult(_connection.Table<PersonaProfileRow>().Count());
        }
    }

    public Task<int> CountNarrativeGraphsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            EnsureNotDisposed();
            return Task.FromResult(_connection.Table<NarrativeGraphRow>().Count());
        }
    }

    public Task UpsertPersonaProfileAsync(PersonaProfile profile, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateRequired(profile.PersonaId, nameof(profile.PersonaId));
        lock (_sync)
        {
            EnsureNotDisposed();
            _connection.InsertOrReplace(ToRow(profile));
        }

        return Task.CompletedTask;
    }

    public Task<PersonaProfile?> GetPersonaProfileAsync(string personaId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            EnsureNotDisposed();
            var row = _connection.Find<PersonaProfileRow>(personaId);
            return Task.FromResult(row is null ? null : FromRow(row));
        }
    }

    public Task<IReadOnlyList<PersonaProfile>> ListPersonaProfilesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            EnsureNotDisposed();
            var rows = _connection.Query<PersonaProfileRow>(
                "select * from PersonaProfile order by DisplayName asc, PersonaId asc");
            return Task.FromResult<IReadOnlyList<PersonaProfile>>(rows.Select(FromRow).ToArray());
        }
    }

    public Task UpsertPersonaVersionAsync(PersonaVersion version, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateRequired(version.PersonaVersionId, nameof(version.PersonaVersionId));
        ValidateRequired(version.PersonaId, nameof(version.PersonaId));
        lock (_sync)
        {
            EnsureNotDisposed();
            _connection.InsertOrReplace(ToRow(version));
        }

        return Task.CompletedTask;
    }

    public Task<PersonaVersion?> GetPersonaVersionAsync(string personaId, string personaVersionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            EnsureNotDisposed();
            var rows = _connection.Query<PersonaVersionRow>(
                "select * from PersonaVersion where PersonaId = ? and PersonaVersionId = ? limit 1",
                personaId,
                personaVersionId);
            var row = rows.FirstOrDefault();
            return Task.FromResult(row is null ? null : FromRow(row));
        }
    }

    public Task<IReadOnlyList<PersonaVersion>> ListPersonaVersionsAsync(string? personaId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            EnsureNotDisposed();
            var rows = string.IsNullOrWhiteSpace(personaId)
                ? _connection.Query<PersonaVersionRow>(
                    "select * from PersonaVersion order by PersonaId asc, CreatedAtUtc desc, PersonaVersionId asc")
                : _connection.Query<PersonaVersionRow>(
                    "select * from PersonaVersion where PersonaId = ? order by CreatedAtUtc desc, PersonaVersionId asc",
                    personaId);
            return Task.FromResult<IReadOnlyList<PersonaVersion>>(rows.Select(FromRow).ToArray());
        }
    }

    public Task UpsertCharacterStateAsync(CharacterState state, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateRequired(state.CharacterId, nameof(state.CharacterId));
        lock (_sync)
        {
            EnsureNotDisposed();
            _connection.InsertOrReplace(ToRow(state));
        }

        return Task.CompletedTask;
    }

    public Task<CharacterState?> GetCharacterStateAsync(string characterId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            EnsureNotDisposed();
            var row = _connection.Find<CharacterStateRow>(characterId);
            return Task.FromResult(row is null ? null : FromRow(row));
        }
    }

    public Task UpsertMemoryFactAsync(MemoryFact fact, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateRequired(fact.FactId, nameof(fact.FactId));
        lock (_sync)
        {
            EnsureNotDisposed();
            _connection.InsertOrReplace(ToRow(fact));
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MemoryFact>> GetApprovedMemoryFactsAsync(string characterId, string personaId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            EnsureNotDisposed();
            var rows = _connection.Query<MemoryFactRow>(
                "select * from MemoryFact where CharacterId = ? and PersonaId = ? and Status = ? order by Importance desc, CreatedAtUtc desc, FactId asc",
                characterId,
                personaId,
                StorylineMemoryStatus.Approved);
            return Task.FromResult<IReadOnlyList<MemoryFact>>(rows.Select(FromRow).ToArray());
        }
    }

    public Task UpsertMemoryEpisodeAsync(MemoryEpisode episode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateRequired(episode.EpisodeId, nameof(episode.EpisodeId));
        lock (_sync)
        {
            EnsureNotDisposed();
            _connection.InsertOrReplace(ToRow(episode));
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MemoryEpisode>> GetApprovedMemoryEpisodesAsync(string characterId, string personaId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            EnsureNotDisposed();
            var rows = _connection.Query<MemoryEpisodeRow>(
                "select * from MemoryEpisode where CharacterId = ? and PersonaId = ? and Status = ? order by Importance desc, OccurredAtUtc desc, EpisodeId asc",
                characterId,
                personaId,
                StorylineMemoryStatus.Approved);
            return Task.FromResult<IReadOnlyList<MemoryEpisode>>(rows.Select(FromRow).ToArray());
        }
    }

    public Task AddMemoryCandidateAsync(MemoryCandidate candidate, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var row = ToRow(candidate with
        {
            CandidateId = string.IsNullOrWhiteSpace(candidate.CandidateId) ? Guid.NewGuid().ToString("N") : candidate.CandidateId,
            Status = string.IsNullOrWhiteSpace(candidate.Status) ? StorylineMemoryStatus.Pending : candidate.Status
        });

        lock (_sync)
        {
            EnsureNotDisposed();
            _connection.Insert(row);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MemoryCandidate>> GetMemoryCandidatesAsync(string characterId, string status, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            EnsureNotDisposed();
            var rows = _connection.Query<MemoryCandidateRow>(
                "select * from MemoryCandidate where CharacterId = ? and Status = ? order by ProposedAtUtc asc, CandidateId asc",
                characterId,
                status);
            return Task.FromResult<IReadOnlyList<MemoryCandidate>>(rows.Select(FromRow).ToArray());
        }
    }

    public Task<MemoryCandidate?> GetMemoryCandidateAsync(string candidateId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            EnsureNotDisposed();
            var row = _connection.Find<MemoryCandidateRow>(candidateId);
            return Task.FromResult(row is null ? null : FromRow(row));
        }
    }

    public Task UpdateMemoryCandidateStatusAsync(string candidateId, string status, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateRequired(candidateId, nameof(candidateId));
        ValidateRequired(status, nameof(status));
        lock (_sync)
        {
            EnsureNotDisposed();
            var row = _connection.Find<MemoryCandidateRow>(candidateId);
            if (row is null)
            {
                return Task.CompletedTask;
            }

            row.Status = status;
            _connection.Update(row);
        }

        return Task.CompletedTask;
    }

    public Task UpsertNarrativeGraphAsync(NarrativeGraph graph, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateRequired(graph.GraphId, nameof(graph.GraphId));
        lock (_sync)
        {
            EnsureNotDisposed();
            _connection.InsertOrReplace(ToRow(graph));
        }

        return Task.CompletedTask;
    }

    public Task<NarrativeGraph?> GetNarrativeGraphAsync(string graphId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            EnsureNotDisposed();
            var row = _connection.Find<NarrativeGraphRow>(graphId);
            return Task.FromResult(row is null ? null : FromRow(row));
        }
    }

    public Task<IReadOnlyList<NarrativeGraph>> ListNarrativeGraphsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            EnsureNotDisposed();
            var rows = _connection.Query<NarrativeGraphRow>(
                "select * from NarrativeGraph order by Name asc, GraphId asc");
            return Task.FromResult<IReadOnlyList<NarrativeGraph>>(rows.Select(FromRow).ToArray());
        }
    }

    public Task UpsertNarrativeNodeAsync(NarrativeNode node, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateRequired(node.NodeId, nameof(node.NodeId));
        ValidateRequired(node.GraphId, nameof(node.GraphId));
        lock (_sync)
        {
            EnsureNotDisposed();
            _connection.InsertOrReplace(ToRow(node));
        }

        return Task.CompletedTask;
    }

    public Task<NarrativeNode?> GetNarrativeNodeAsync(string graphId, string nodeId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            EnsureNotDisposed();
            var rows = _connection.Query<NarrativeNodeRow>(
                "select * from NarrativeNode where GraphId = ? and NodeId = ? limit 1",
                graphId,
                nodeId);
            var row = rows.FirstOrDefault();
            return Task.FromResult(row is null ? null : FromRow(row));
        }
    }

    public Task<IReadOnlyList<NarrativeNode>> ListNarrativeNodesAsync(string graphId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            EnsureNotDisposed();
            var rows = _connection.Query<NarrativeNodeRow>(
                "select * from NarrativeNode where GraphId = ? order by SortOrder asc, NodeId asc",
                graphId);
            return Task.FromResult<IReadOnlyList<NarrativeNode>>(rows.Select(FromRow).ToArray());
        }
    }

    public Task UpsertNarrativeTransitionAsync(NarrativeTransition transition, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateRequired(transition.TransitionId, nameof(transition.TransitionId));
        lock (_sync)
        {
            EnsureNotDisposed();
            _connection.InsertOrReplace(ToRow(transition));
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<NarrativeTransition>> GetNarrativeTransitionsAsync(string graphId, string fromNodeId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            EnsureNotDisposed();
            var rows = _connection.Query<NarrativeTransitionRow>(
                "select * from NarrativeTransition where GraphId = ? and FromNodeId = ? order by SortOrder asc, TransitionId asc",
                graphId,
                fromNodeId);
            return Task.FromResult<IReadOnlyList<NarrativeTransition>>(rows.Select(FromRow).ToArray());
        }
    }

    public Task<IReadOnlyList<NarrativeTransition>> ListNarrativeTransitionsAsync(string graphId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            EnsureNotDisposed();
            var rows = _connection.Query<NarrativeTransitionRow>(
                "select * from NarrativeTransition where GraphId = ? order by SortOrder asc, TransitionId asc",
                graphId);
            return Task.FromResult<IReadOnlyList<NarrativeTransition>>(rows.Select(FromRow).ToArray());
        }
    }

    public Task UpsertNarrativeGraphSnapshotAsync(
        NarrativeGraph graph,
        IReadOnlyList<NarrativeNode> nodes,
        IReadOnlyList<NarrativeTransition> transitions,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateRequired(graph.GraphId, nameof(graph.GraphId));
        lock (_sync)
        {
            EnsureNotDisposed();
            _connection.RunInTransaction(() =>
            {
                _connection.InsertOrReplace(ToRow(graph));

                var nodeIds = nodes.Select(node => node.NodeId).ToArray();
                DeleteRowsNotIn(
                    "NarrativeNode",
                    "GraphId",
                    graph.GraphId,
                    "NodeId",
                    nodeIds);
                foreach (var node in nodes)
                {
                    _connection.InsertOrReplace(ToRow(node));
                }

                var transitionIds = transitions.Select(transition => transition.TransitionId).ToArray();
                DeleteRowsNotIn(
                    "NarrativeTransition",
                    "GraphId",
                    graph.GraphId,
                    "TransitionId",
                    transitionIds);
                foreach (var transition in transitions)
                {
                    _connection.InsertOrReplace(ToRow(transition));
                }
            });
        }

        return Task.CompletedTask;
    }

    public Task UpsertAgentBindingAsync(AgentBinding binding, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateRequired(binding.BindingId, nameof(binding.BindingId));
        lock (_sync)
        {
            EnsureNotDisposed();
            _connection.InsertOrReplace(ToRow(binding));
        }

        return Task.CompletedTask;
    }

    public Task<AgentBinding?> GetAgentBindingAsync(string personaId, string personaVersionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            EnsureNotDisposed();
            var rows = _connection.Query<AgentBindingRow>(
                "select * from AgentBinding where PersonaId = ? and PersonaVersionId = ? order by IsDefault desc, CreatedAtUtc desc, BindingId asc limit 1",
                personaId,
                personaVersionId);
            var row = rows.FirstOrDefault();
            return Task.FromResult(row is null ? null : FromRow(row));
        }
    }

    public Task UpsertConversationBindingAsync(ConversationBinding binding, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateRequired(binding.CharacterId, nameof(binding.CharacterId));
        lock (_sync)
        {
            EnsureNotDisposed();
            _connection.InsertOrReplace(ToRow(binding));
        }

        return Task.CompletedTask;
    }

    public Task<ConversationBinding?> GetConversationBindingAsync(string characterId, string? guestId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            EnsureNotDisposed();
            var row = _connection.Find<ConversationBindingRow>(ConversationKey(characterId, guestId));
            return Task.FromResult(row is null ? null : FromRow(row));
        }
    }

    public Task UpsertStorylineDraftAsync(StorylineDraft draft, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateRequired(draft.DraftId, nameof(draft.DraftId));
        ValidateRequired(draft.Kind, nameof(draft.Kind));
        ValidateRequired(draft.TargetId, nameof(draft.TargetId));
        lock (_sync)
        {
            EnsureNotDisposed();
            _connection.InsertOrReplace(ToRow(draft));
        }

        return Task.CompletedTask;
    }

    public Task<StorylineDraft?> GetStorylineDraftAsync(string draftId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            EnsureNotDisposed();
            var row = _connection.Find<StorylineDraftRow>(draftId);
            return Task.FromResult(row is null ? null : FromRow(row));
        }
    }

    public Task<IReadOnlyList<StorylineDraft>> ListStorylineDraftsAsync(string? kind, string? status, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var kindFilter = string.IsNullOrWhiteSpace(kind) ? null : kind.Trim();
        var statusFilter = string.IsNullOrWhiteSpace(status) ? null : status.Trim();
        lock (_sync)
        {
            EnsureNotDisposed();
            var rows = (kindFilter, statusFilter) switch
            {
                ({ Length: > 0 }, { Length: > 0 }) => _connection.Query<StorylineDraftRow>(
                    "select * from StorylineDraft where Kind = ? and Status = ? order by UpdatedAtUtc desc, DraftId asc",
                    kindFilter,
                    statusFilter),
                ({ Length: > 0 }, _) => _connection.Query<StorylineDraftRow>(
                    "select * from StorylineDraft where Kind = ? order by UpdatedAtUtc desc, DraftId asc",
                    kindFilter),
                (_, { Length: > 0 }) => _connection.Query<StorylineDraftRow>(
                    "select * from StorylineDraft where Status = ? order by UpdatedAtUtc desc, DraftId asc",
                    statusFilter),
                _ => _connection.Query<StorylineDraftRow>(
                    "select * from StorylineDraft order by UpdatedAtUtc desc, DraftId asc")
            };
            return Task.FromResult<IReadOnlyList<StorylineDraft>>(rows.Select(FromRow).ToArray());
        }
    }

    public Task MarkStorylineDraftPublishedAsync(
        string draftId,
        DateTime publishedAtUtc,
        string publishedBy,
        string publishMessage,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            EnsureNotDisposed();
            var row = _connection.Find<StorylineDraftRow>(draftId);
            if (row is null)
            {
                return Task.CompletedTask;
            }

            row.Status = StorylineDraftStatus.Published;
            row.PublishedAtUtc = NormalizeDate(publishedAtUtc);
            row.PublishedBy = publishedBy;
            row.PublishMessage = publishMessage;
            row.UpdatedAtUtc = row.PublishedAtUtc.Value;
            _connection.Update(row);
        }

        return Task.CompletedTask;
    }

    public Task UpsertGameplayArcSnapshotAsync(
        GameplayStoryArc arc,
        IReadOnlyList<GameplayArcStep> steps,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateRequired(arc.ArcId, nameof(arc.ArcId));
        ValidateRequired(arc.VersionId, nameof(arc.VersionId));
        lock (_sync)
        {
            EnsureNotDisposed();
            _connection.RunInTransaction(() =>
            {
                _connection.InsertOrReplace(ToRow(arc));
                var stepIds = steps.Select(step => step.StepId).ToArray();
                DeleteRowsNotIn(
                    "GameplayArcStep",
                    "ArcVersionKey",
                    ArcVersionKey(arc.ArcId, arc.VersionId),
                    "StepId",
                    stepIds);
                foreach (var step in steps)
                {
                    _connection.InsertOrReplace(ToRow(step));
                }
            });
        }

        return Task.CompletedTask;
    }

    public Task<GameplayStoryArc?> GetGameplayArcAsync(string arcId, string versionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            EnsureNotDisposed();
            var row = _connection.Find<GameplayStoryArcRow>(ArcVersionKey(arcId, versionId));
            return Task.FromResult(row is null ? null : FromRow(row));
        }
    }

    public Task<IReadOnlyList<GameplayStoryArc>> ListGameplayArcsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            EnsureNotDisposed();
            var rows = _connection.Query<GameplayStoryArcRow>(
                "select * from GameplayStoryArc order by Name asc, ArcId asc, VersionId asc");
            return Task.FromResult<IReadOnlyList<GameplayStoryArc>>(rows.Select(FromRow).ToArray());
        }
    }

    public Task<IReadOnlyList<GameplayArcStep>> ListGameplayArcStepsAsync(string arcId, string versionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            EnsureNotDisposed();
            var rows = _connection.Query<GameplayArcStepRow>(
                "select * from GameplayArcStep where ArcVersionKey = ? order by StepOrder asc, StepId asc",
                ArcVersionKey(arcId, versionId));
            return Task.FromResult<IReadOnlyList<GameplayArcStep>>(rows.Select(FromRow).ToArray());
        }
    }

    public Task UpsertCharacterStoryBindingAsync(CharacterStoryBinding binding, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateRequired(binding.CharacterId, nameof(binding.CharacterId));
        lock (_sync)
        {
            EnsureNotDisposed();
            _connection.InsertOrReplace(ToRow(binding));
        }

        return Task.CompletedTask;
    }

    public Task<CharacterStoryBinding?> GetCharacterStoryBindingAsync(string characterId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            EnsureNotDisposed();
            var row = _connection.Find<CharacterStoryBindingRow>(characterId);
            return Task.FromResult(row is null ? null : FromRow(row));
        }
    }

    public Task<IReadOnlyList<CharacterStoryBinding>> ListCharacterStoryBindingsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            EnsureNotDisposed();
            var rows = _connection.Query<CharacterStoryBindingRow>(
                "select * from CharacterStoryBinding order by Realm asc, CharacterName asc, CharacterId asc");
            return Task.FromResult<IReadOnlyList<CharacterStoryBinding>>(rows.Select(FromRow).ToArray());
        }
    }

    public Task UpsertGraphLayoutAsync(GraphLayout layout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateRequired(layout.GraphId, nameof(layout.GraphId));
        lock (_sync)
        {
            EnsureNotDisposed();
            _connection.InsertOrReplace(ToRow(layout));
        }

        return Task.CompletedTask;
    }

    public Task<GraphLayout?> GetGraphLayoutAsync(string graphId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            EnsureNotDisposed();
            var row = _connection.Find<GraphLayoutRow>(graphId);
            return Task.FromResult(row is null ? null : FromRow(row));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _connection.Dispose();
        _disposed = true;
    }

    private void InitializeSchema()
    {
        lock (_sync)
        {
            _connection.CreateTable<PersonaProfileRow>();
            _connection.CreateTable<PersonaVersionRow>();
            _connection.CreateTable<CharacterStateRow>();
            _connection.CreateTable<MemoryFactRow>();
            _connection.CreateTable<MemoryEpisodeRow>();
            _connection.CreateTable<MemoryCandidateRow>();
            _connection.CreateTable<NarrativeGraphRow>();
            _connection.CreateTable<NarrativeNodeRow>();
            _connection.CreateTable<NarrativeTransitionRow>();
            _connection.CreateTable<AgentBindingRow>();
            _connection.CreateTable<ConversationBindingRow>();
            _connection.CreateTable<StorylineDraftRow>();
            _connection.CreateTable<GameplayStoryArcRow>();
            _connection.CreateTable<GameplayArcStepRow>();
            _connection.CreateTable<CharacterStoryBindingRow>();
            _connection.CreateTable<GraphLayoutRow>();
        }
    }

    private bool HasNoSeedTargetRows()
    {
        lock (_sync)
        {
            return _connection.Table<PersonaProfileRow>().Count() == 0 &&
                _connection.Table<NarrativeGraphRow>().Count() == 0;
        }
    }

    private void ImportSeedFile(string seedPath)
    {
        var json = File.ReadAllText(seedPath);
        var seed = JsonSerializer.Deserialize<StorylineSeedDocument>(json, SeedJsonOptions) ??
            throw new InvalidDataException($"Storyline seed file '{seedPath}' is empty or invalid.");

        lock (_sync)
        {
            foreach (var profile in seed.PersonaProfiles)
            {
                _connection.InsertOrReplace(ToRow(profile));
            }

            foreach (var version in seed.PersonaVersions)
            {
                _connection.InsertOrReplace(ToRow(version));
            }

            foreach (var state in seed.CharacterStates)
            {
                _connection.InsertOrReplace(ToRow(state));
            }

            foreach (var fact in seed.MemoryFacts)
            {
                _connection.InsertOrReplace(ToRow(fact));
            }

            foreach (var episode in seed.MemoryEpisodes)
            {
                _connection.InsertOrReplace(ToRow(episode));
            }

            foreach (var graph in seed.NarrativeGraphs)
            {
                _connection.InsertOrReplace(ToRow(graph));
            }

            foreach (var node in seed.NarrativeNodes)
            {
                _connection.InsertOrReplace(ToRow(node));
            }

            foreach (var transition in seed.NarrativeTransitions)
            {
                _connection.InsertOrReplace(ToRow(transition));
            }

            foreach (var binding in seed.AgentBindings)
            {
                _connection.InsertOrReplace(ToRow(binding));
            }

            foreach (var binding in seed.ConversationBindings)
            {
                _connection.InsertOrReplace(ToRow(binding));
            }
        }
    }

    private static void EnsureDirectoryExists(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SqliteStorylineRepository));
        }
    }

    private static void ValidateRequired(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldName} is required.", fieldName);
        }
    }

    private static string ConversationKey(string characterId, string? guestId)
    {
        var participant = string.IsNullOrWhiteSpace(guestId) ? "self" : guestId.Trim();
        return $"{characterId.Trim()}::{participant}";
    }

    private static string ArcVersionKey(string arcId, string versionId) => $"{arcId.Trim()}::{versionId.Trim()}";

    private void DeleteRowsNotIn(
        string tableName,
        string scopeColumn,
        string scopeValue,
        string idColumn,
        IReadOnlyList<string> retainedIds)
    {
        if (retainedIds.Count == 0)
        {
            _connection.Execute($"delete from {tableName} where {scopeColumn} = ?", scopeValue);
            return;
        }

        var placeholders = string.Join(", ", retainedIds.Select(_ => "?"));
        var args = new List<object> { scopeValue };
        args.AddRange(retainedIds);
        _connection.Execute(
            $"delete from {tableName} where {scopeColumn} = ? and {idColumn} not in ({placeholders})",
            args.ToArray());
    }

    private static DateTime NormalizeDate(DateTime value) => value == default ? DateTime.UtcNow : value;

    private static PersonaProfileRow ToRow(PersonaProfile profile) => new()
    {
        PersonaId = profile.PersonaId,
        DisplayName = profile.DisplayName,
        Description = profile.Description,
        CreatedAtUtc = NormalizeDate(profile.CreatedAtUtc)
    };

    private static PersonaProfile FromRow(PersonaProfileRow row) => new()
    {
        PersonaId = row.PersonaId,
        DisplayName = row.DisplayName,
        Description = row.Description,
        CreatedAtUtc = row.CreatedAtUtc
    };

    private static PersonaVersionRow ToRow(PersonaVersion version) => new()
    {
        PersonaVersionId = version.PersonaVersionId,
        PersonaId = version.PersonaId,
        Version = version.Version,
        PromptSummary = version.PromptSummary,
        IsActive = version.IsActive,
        CreatedAtUtc = NormalizeDate(version.CreatedAtUtc)
    };

    private static PersonaVersion FromRow(PersonaVersionRow row) => new()
    {
        PersonaVersionId = row.PersonaVersionId,
        PersonaId = row.PersonaId,
        Version = row.Version,
        PromptSummary = row.PromptSummary,
        IsActive = row.IsActive,
        CreatedAtUtc = row.CreatedAtUtc
    };

    private static CharacterStateRow ToRow(CharacterState state) => new()
    {
        CharacterId = state.CharacterId,
        CharacterName = state.CharacterName,
        Realm = state.Realm,
        PersonaId = state.PersonaId,
        PersonaVersionId = state.PersonaVersionId,
        ActiveGraphId = state.ActiveGraphId,
        ActiveNodeId = state.ActiveNodeId,
        MoodState = state.MoodState,
        UpdatedAtUtc = NormalizeDate(state.UpdatedAtUtc)
    };

    private static CharacterState FromRow(CharacterStateRow row) => new()
    {
        CharacterId = row.CharacterId,
        CharacterName = row.CharacterName,
        Realm = row.Realm,
        PersonaId = row.PersonaId,
        PersonaVersionId = row.PersonaVersionId,
        ActiveGraphId = row.ActiveGraphId,
        ActiveNodeId = row.ActiveNodeId,
        MoodState = row.MoodState,
        UpdatedAtUtc = row.UpdatedAtUtc
    };

    private static MemoryFactRow ToRow(MemoryFact fact) => new()
    {
        FactId = fact.FactId,
        CharacterId = fact.CharacterId,
        PersonaId = fact.PersonaId,
        Text = fact.Text,
        Importance = fact.Importance,
        Status = fact.Status,
        CreatedAtUtc = NormalizeDate(fact.CreatedAtUtc),
        ApprovedAtUtc = fact.ApprovedAtUtc
    };

    private static MemoryFact FromRow(MemoryFactRow row) => new()
    {
        FactId = row.FactId,
        CharacterId = row.CharacterId,
        PersonaId = row.PersonaId,
        Text = row.Text,
        Importance = row.Importance,
        Status = row.Status,
        CreatedAtUtc = row.CreatedAtUtc,
        ApprovedAtUtc = row.ApprovedAtUtc
    };

    private static MemoryEpisodeRow ToRow(MemoryEpisode episode) => new()
    {
        EpisodeId = episode.EpisodeId,
        CharacterId = episode.CharacterId,
        PersonaId = episode.PersonaId,
        Summary = episode.Summary,
        Importance = episode.Importance,
        Status = episode.Status,
        OccurredAtUtc = NormalizeDate(episode.OccurredAtUtc),
        CreatedAtUtc = NormalizeDate(episode.CreatedAtUtc)
    };

    private static MemoryEpisode FromRow(MemoryEpisodeRow row) => new()
    {
        EpisodeId = row.EpisodeId,
        CharacterId = row.CharacterId,
        PersonaId = row.PersonaId,
        Summary = row.Summary,
        Importance = row.Importance,
        Status = row.Status,
        OccurredAtUtc = row.OccurredAtUtc,
        CreatedAtUtc = row.CreatedAtUtc
    };

    private static MemoryCandidateRow ToRow(MemoryCandidate candidate) => new()
    {
        CandidateId = candidate.CandidateId,
        CharacterId = candidate.CharacterId,
        PersonaId = candidate.PersonaId,
        SourceInput = candidate.SourceInput,
        CandidateText = candidate.CandidateText,
        Status = candidate.Status,
        FoundryIntent = candidate.FoundryIntent,
        ActiveNodeId = candidate.ActiveNodeId,
        ProposedAtUtc = NormalizeDate(candidate.ProposedAtUtc)
    };

    private static MemoryCandidate FromRow(MemoryCandidateRow row) => new()
    {
        CandidateId = row.CandidateId,
        CharacterId = row.CharacterId,
        PersonaId = row.PersonaId,
        SourceInput = row.SourceInput,
        CandidateText = row.CandidateText,
        Status = row.Status,
        FoundryIntent = row.FoundryIntent,
        ActiveNodeId = row.ActiveNodeId,
        ProposedAtUtc = row.ProposedAtUtc
    };

    private static NarrativeGraphRow ToRow(NarrativeGraph graph) => new()
    {
        GraphId = graph.GraphId,
        Name = graph.Name,
        Description = graph.Description,
        CreatedAtUtc = NormalizeDate(graph.CreatedAtUtc)
    };

    private static NarrativeGraph FromRow(NarrativeGraphRow row) => new()
    {
        GraphId = row.GraphId,
        Name = row.Name,
        Description = row.Description,
        CreatedAtUtc = row.CreatedAtUtc
    };

    private static NarrativeNodeRow ToRow(NarrativeNode node) => new()
    {
        NodeId = node.NodeId,
        GraphId = node.GraphId,
        Title = node.Title,
        Summary = node.Summary,
        FallbackReply = node.FallbackReply,
        SortOrder = node.SortOrder
    };

    private static NarrativeNode FromRow(NarrativeNodeRow row) => new()
    {
        NodeId = row.NodeId,
        GraphId = row.GraphId,
        Title = row.Title,
        Summary = row.Summary,
        FallbackReply = row.FallbackReply,
        SortOrder = row.SortOrder
    };

    private static NarrativeTransitionRow ToRow(NarrativeTransition transition) => new()
    {
        TransitionId = transition.TransitionId,
        GraphId = transition.GraphId,
        FromNodeId = transition.FromNodeId,
        ToNodeId = transition.ToNodeId,
        TriggerKind = transition.TriggerKind,
        GuardExpression = transition.GuardExpression,
        SortOrder = transition.SortOrder
    };

    private static NarrativeTransition FromRow(NarrativeTransitionRow row) => new()
    {
        TransitionId = row.TransitionId,
        GraphId = row.GraphId,
        FromNodeId = row.FromNodeId,
        ToNodeId = row.ToNodeId,
        TriggerKind = row.TriggerKind,
        GuardExpression = row.GuardExpression,
        SortOrder = row.SortOrder
    };

    private static AgentBindingRow ToRow(AgentBinding binding) => new()
    {
        BindingId = binding.BindingId,
        PersonaId = binding.PersonaId,
        PersonaVersionId = binding.PersonaVersionId,
        Model = binding.Model,
        AgentName = binding.AgentName,
        AgentVersion = binding.AgentVersion,
        MaxOutputTokens = binding.MaxOutputTokens,
        IsDefault = binding.IsDefault,
        CreatedAtUtc = NormalizeDate(binding.CreatedAtUtc)
    };

    private static AgentBinding FromRow(AgentBindingRow row) => new()
    {
        BindingId = row.BindingId,
        PersonaId = row.PersonaId,
        PersonaVersionId = row.PersonaVersionId,
        Model = row.Model,
        AgentName = row.AgentName,
        AgentVersion = row.AgentVersion,
        MaxOutputTokens = row.MaxOutputTokens,
        IsDefault = row.IsDefault,
        CreatedAtUtc = row.CreatedAtUtc
    };

    private static ConversationBindingRow ToRow(ConversationBinding binding) => new()
    {
        ConversationBindingId = string.IsNullOrWhiteSpace(binding.ConversationBindingId)
            ? ConversationKey(binding.CharacterId, binding.GuestId)
            : binding.ConversationBindingId,
        CharacterId = binding.CharacterId,
        GuestId = binding.GuestId,
        PersonaId = binding.PersonaId,
        PersonaVersionId = binding.PersonaVersionId,
        GraphId = binding.GraphId,
        ActiveNodeId = binding.ActiveNodeId,
        CreatedAtUtc = NormalizeDate(binding.CreatedAtUtc),
        UpdatedAtUtc = NormalizeDate(binding.UpdatedAtUtc)
    };

    private static ConversationBinding FromRow(ConversationBindingRow row) => new()
    {
        ConversationBindingId = row.ConversationBindingId,
        CharacterId = row.CharacterId,
        GuestId = row.GuestId,
        PersonaId = row.PersonaId,
        PersonaVersionId = row.PersonaVersionId,
        GraphId = row.GraphId,
        ActiveNodeId = row.ActiveNodeId,
        CreatedAtUtc = row.CreatedAtUtc,
        UpdatedAtUtc = row.UpdatedAtUtc
    };

    private static StorylineDraftRow ToRow(StorylineDraft draft) => new()
    {
        DraftId = draft.DraftId,
        Kind = draft.Kind,
        TargetId = draft.TargetId,
        Status = string.IsNullOrWhiteSpace(draft.Status) ? StorylineDraftStatus.Draft : draft.Status,
        PayloadJson = draft.PayloadJson,
        CreatedAtUtc = NormalizeDate(draft.CreatedAtUtc),
        UpdatedAtUtc = NormalizeDate(draft.UpdatedAtUtc),
        PublishedAtUtc = draft.PublishedAtUtc,
        PublishedBy = draft.PublishedBy,
        PublishMessage = draft.PublishMessage
    };

    private static StorylineDraft FromRow(StorylineDraftRow row) => new()
    {
        DraftId = row.DraftId,
        Kind = row.Kind,
        TargetId = row.TargetId,
        Status = row.Status,
        PayloadJson = row.PayloadJson,
        CreatedAtUtc = row.CreatedAtUtc,
        UpdatedAtUtc = row.UpdatedAtUtc,
        PublishedAtUtc = row.PublishedAtUtc,
        PublishedBy = row.PublishedBy,
        PublishMessage = row.PublishMessage
    };

    private static GameplayStoryArcRow ToRow(GameplayStoryArc arc) => new()
    {
        ArcVersionKey = ArcVersionKey(arc.ArcId, arc.VersionId),
        ArcId = arc.ArcId,
        VersionId = arc.VersionId,
        Name = arc.Name,
        Description = arc.Description,
        IsPublished = arc.IsPublished,
        CreatedAtUtc = NormalizeDate(arc.CreatedAtUtc),
        PublishedAtUtc = arc.PublishedAtUtc
    };

    private static GameplayStoryArc FromRow(GameplayStoryArcRow row) => new()
    {
        ArcId = row.ArcId,
        VersionId = row.VersionId,
        Name = row.Name,
        Description = row.Description,
        IsPublished = row.IsPublished,
        CreatedAtUtc = row.CreatedAtUtc,
        PublishedAtUtc = row.PublishedAtUtc
    };

    private static GameplayArcStepRow ToRow(GameplayArcStep step) => new()
    {
        StepId = string.IsNullOrWhiteSpace(step.StepId)
            ? $"{ArcVersionKey(step.ArcId, step.VersionId)}::{step.StepOrder}"
            : step.StepId,
        ArcVersionKey = ArcVersionKey(step.ArcId, step.VersionId),
        ArcId = step.ArcId,
        VersionId = step.VersionId,
        StepOrder = step.StepOrder,
        ActivityId = step.ActivityId,
        NarrativeHook = step.NarrativeHook
    };

    private static GameplayArcStep FromRow(GameplayArcStepRow row) => new()
    {
        StepId = row.StepId,
        ArcId = row.ArcId,
        VersionId = row.VersionId,
        StepOrder = row.StepOrder,
        ActivityId = row.ActivityId,
        NarrativeHook = row.NarrativeHook
    };

    private static CharacterStoryBindingRow ToRow(CharacterStoryBinding binding) => new()
    {
        CharacterId = binding.CharacterId,
        CharacterName = binding.CharacterName,
        Realm = binding.Realm,
        PersonaId = binding.PersonaId,
        PersonaVersionId = binding.PersonaVersionId,
        ActiveGraphId = binding.ActiveGraphId,
        ActiveNodeId = binding.ActiveNodeId,
        ConversationBindingId = binding.ConversationBindingId,
        GameplayArcId = binding.GameplayArcId,
        GameplayArcVersionId = binding.GameplayArcVersionId,
        MoodState = binding.MoodState,
        UpdatedAtUtc = NormalizeDate(binding.UpdatedAtUtc)
    };

    private static CharacterStoryBinding FromRow(CharacterStoryBindingRow row) => new()
    {
        CharacterId = row.CharacterId,
        CharacterName = row.CharacterName,
        Realm = row.Realm,
        PersonaId = row.PersonaId,
        PersonaVersionId = row.PersonaVersionId,
        ActiveGraphId = row.ActiveGraphId,
        ActiveNodeId = row.ActiveNodeId,
        ConversationBindingId = row.ConversationBindingId,
        GameplayArcId = row.GameplayArcId,
        GameplayArcVersionId = row.GameplayArcVersionId,
        MoodState = row.MoodState,
        UpdatedAtUtc = row.UpdatedAtUtc
    };

    private static GraphLayoutRow ToRow(GraphLayout layout) => new()
    {
        GraphId = layout.GraphId,
        LayoutJson = layout.LayoutJson,
        UpdatedAtUtc = NormalizeDate(layout.UpdatedAtUtc)
    };

    private static GraphLayout FromRow(GraphLayoutRow row) => new()
    {
        GraphId = row.GraphId,
        LayoutJson = row.LayoutJson,
        UpdatedAtUtc = row.UpdatedAtUtc
    };

    internal sealed class StorylineSeedDocument
    {
        public List<PersonaProfile> PersonaProfiles { get; set; } = new();
        public List<PersonaVersion> PersonaVersions { get; set; } = new();
        public List<CharacterState> CharacterStates { get; set; } = new();
        public List<MemoryFact> MemoryFacts { get; set; } = new();
        public List<MemoryEpisode> MemoryEpisodes { get; set; } = new();
        public List<NarrativeGraph> NarrativeGraphs { get; set; } = new();
        public List<NarrativeNode> NarrativeNodes { get; set; } = new();
        public List<NarrativeTransition> NarrativeTransitions { get; set; } = new();
        public List<AgentBinding> AgentBindings { get; set; } = new();
        public List<ConversationBinding> ConversationBindings { get; set; } = new();
    }

    [Table("PersonaProfile")]
    internal sealed class PersonaProfileRow
    {
        [PrimaryKey]
        public string PersonaId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
    }

    [Table("PersonaVersion")]
    internal sealed class PersonaVersionRow
    {
        [PrimaryKey]
        public string PersonaVersionId { get; set; } = string.Empty;
        [Indexed]
        public string PersonaId { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string PromptSummary { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    [Table("CharacterState")]
    internal sealed class CharacterStateRow
    {
        [PrimaryKey]
        public string CharacterId { get; set; } = string.Empty;
        public string CharacterName { get; set; } = string.Empty;
        [Indexed]
        public string Realm { get; set; } = string.Empty;
        [Indexed]
        public string PersonaId { get; set; } = string.Empty;
        public string PersonaVersionId { get; set; } = string.Empty;
        public string ActiveGraphId { get; set; } = string.Empty;
        public string ActiveNodeId { get; set; } = string.Empty;
        public string MoodState { get; set; } = string.Empty;
        public DateTime UpdatedAtUtc { get; set; }
    }

    [Table("MemoryFact")]
    internal sealed class MemoryFactRow
    {
        [PrimaryKey]
        public string FactId { get; set; } = string.Empty;
        [Indexed]
        public string CharacterId { get; set; } = string.Empty;
        [Indexed]
        public string PersonaId { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public int Importance { get; set; }
        [Indexed]
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? ApprovedAtUtc { get; set; }
    }

    [Table("MemoryEpisode")]
    internal sealed class MemoryEpisodeRow
    {
        [PrimaryKey]
        public string EpisodeId { get; set; } = string.Empty;
        [Indexed]
        public string CharacterId { get; set; } = string.Empty;
        [Indexed]
        public string PersonaId { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public int Importance { get; set; }
        [Indexed]
        public string Status { get; set; } = string.Empty;
        public DateTime OccurredAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    [Table("MemoryCandidate")]
    internal sealed class MemoryCandidateRow
    {
        [PrimaryKey]
        public string CandidateId { get; set; } = string.Empty;
        [Indexed]
        public string CharacterId { get; set; } = string.Empty;
        [Indexed]
        public string PersonaId { get; set; } = string.Empty;
        public string SourceInput { get; set; } = string.Empty;
        public string CandidateText { get; set; } = string.Empty;
        [Indexed]
        public string Status { get; set; } = string.Empty;
        public string FoundryIntent { get; set; } = string.Empty;
        public string ActiveNodeId { get; set; } = string.Empty;
        public DateTime ProposedAtUtc { get; set; }
    }

    [Table("NarrativeGraph")]
    internal sealed class NarrativeGraphRow
    {
        [PrimaryKey]
        public string GraphId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
    }

    [Table("NarrativeNode")]
    internal sealed class NarrativeNodeRow
    {
        [PrimaryKey]
        public string NodeId { get; set; } = string.Empty;
        [Indexed]
        public string GraphId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string FallbackReply { get; set; } = string.Empty;
        public int SortOrder { get; set; }
    }

    [Table("NarrativeTransition")]
    internal sealed class NarrativeTransitionRow
    {
        [PrimaryKey]
        public string TransitionId { get; set; } = string.Empty;
        [Indexed]
        public string GraphId { get; set; } = string.Empty;
        [Indexed]
        public string FromNodeId { get; set; } = string.Empty;
        public string ToNodeId { get; set; } = string.Empty;
        public string TriggerKind { get; set; } = string.Empty;
        public string GuardExpression { get; set; } = string.Empty;
        public int SortOrder { get; set; }
    }

    [Table("AgentBinding")]
    internal sealed class AgentBindingRow
    {
        [PrimaryKey]
        public string BindingId { get; set; } = string.Empty;
        [Indexed]
        public string PersonaId { get; set; } = string.Empty;
        [Indexed]
        public string PersonaVersionId { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string AgentName { get; set; } = string.Empty;
        public string? AgentVersion { get; set; }
        public int MaxOutputTokens { get; set; }
        public bool IsDefault { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    [Table("ConversationBinding")]
    internal sealed class ConversationBindingRow
    {
        [PrimaryKey]
        public string ConversationBindingId { get; set; } = string.Empty;
        [Indexed]
        public string CharacterId { get; set; } = string.Empty;
        public string? GuestId { get; set; }
        public string PersonaId { get; set; } = string.Empty;
        public string PersonaVersionId { get; set; } = string.Empty;
        public string GraphId { get; set; } = string.Empty;
        public string ActiveNodeId { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    [Table("StorylineDraft")]
    internal sealed class StorylineDraftRow
    {
        [PrimaryKey]
        public string DraftId { get; set; } = string.Empty;
        [Indexed]
        public string Kind { get; set; } = string.Empty;
        [Indexed]
        public string TargetId { get; set; } = string.Empty;
        [Indexed]
        public string Status { get; set; } = string.Empty;
        public string PayloadJson { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public DateTime? PublishedAtUtc { get; set; }
        public string PublishedBy { get; set; } = string.Empty;
        public string PublishMessage { get; set; } = string.Empty;
    }

    [Table("GameplayStoryArc")]
    internal sealed class GameplayStoryArcRow
    {
        [PrimaryKey]
        public string ArcVersionKey { get; set; } = string.Empty;
        [Indexed]
        public string ArcId { get; set; } = string.Empty;
        [Indexed]
        public string VersionId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsPublished { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? PublishedAtUtc { get; set; }
    }

    [Table("GameplayArcStep")]
    internal sealed class GameplayArcStepRow
    {
        [PrimaryKey]
        public string StepId { get; set; } = string.Empty;
        [Indexed]
        public string ArcVersionKey { get; set; } = string.Empty;
        [Indexed]
        public string ArcId { get; set; } = string.Empty;
        public string VersionId { get; set; } = string.Empty;
        public int StepOrder { get; set; }
        public string ActivityId { get; set; } = string.Empty;
        public string NarrativeHook { get; set; } = string.Empty;
    }

    [Table("CharacterStoryBinding")]
    internal sealed class CharacterStoryBindingRow
    {
        [PrimaryKey]
        public string CharacterId { get; set; } = string.Empty;
        public string CharacterName { get; set; } = string.Empty;
        [Indexed]
        public string Realm { get; set; } = string.Empty;
        [Indexed]
        public string PersonaId { get; set; } = string.Empty;
        public string PersonaVersionId { get; set; } = string.Empty;
        public string ActiveGraphId { get; set; } = string.Empty;
        public string ActiveNodeId { get; set; } = string.Empty;
        public string ConversationBindingId { get; set; } = string.Empty;
        public string GameplayArcId { get; set; } = string.Empty;
        public string GameplayArcVersionId { get; set; } = string.Empty;
        public string MoodState { get; set; } = string.Empty;
        public DateTime UpdatedAtUtc { get; set; }
    }

    [Table("GraphLayout")]
    internal sealed class GraphLayoutRow
    {
        [PrimaryKey]
        public string GraphId { get; set; } = string.Empty;
        public string LayoutJson { get; set; } = string.Empty;
        public DateTime UpdatedAtUtc { get; set; }
    }
}
