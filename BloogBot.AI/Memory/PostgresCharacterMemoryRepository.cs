using System.Text.Json;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace BloogBot.AI.Memory;

/// <summary>
/// PostgreSQL implementation of character memory repository.
/// Uses JSONB columns for flexible memory storage.
/// </summary>
public sealed class PostgresCharacterMemoryRepository : ICharacterMemoryRepository
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresCharacterMemoryRepository>? _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public PostgresCharacterMemoryRepository(
        string connectionString,
        ILogger<PostgresCharacterMemoryRepository>? logger = null)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CharacterMemory?> LoadAsync(Guid characterId, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(@"
            SELECT character_name, realm, facts, recent_memories, long_term_goals, last_persisted
            FROM character_memory
            WHERE character_id = @id", conn);

        cmd.Parameters.AddWithValue("id", characterId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ParseFromReader(characterId, reader);
    }

    /// <inheritdoc />
    public async Task<CharacterMemory?> LoadByNameAsync(string characterName, string realm, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(@"
            SELECT character_id, character_name, realm, facts, recent_memories, long_term_goals, last_persisted
            FROM character_memory
            WHERE character_name = @name AND realm = @realm", conn);

        cmd.Parameters.AddWithValue("name", characterName);
        cmd.Parameters.AddWithValue("realm", realm);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        var characterId = reader.GetGuid(0);
        return ParseFromReader(characterId, reader, offset: 1);
    }

    /// <inheritdoc />
    public async Task PersistBatchAsync(IEnumerable<CharacterMemory> memories, CancellationToken cancellationToken = default)
    {
        var memoryList = memories.ToList();
        if (memoryList.Count == 0) return;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var transaction = await conn.BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (var memory in memoryList)
            {
                await UpsertMemoryAsync(conn, memory, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            _logger?.LogInformation("Batch persisted {Count} character memories", memoryList.Count);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task PersistAsync(CharacterMemory memory, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await UpsertMemoryAsync(conn, memory, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(Guid characterId, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(
            "SELECT 1 FROM character_memory WHERE character_id = @id LIMIT 1", conn);
        cmd.Parameters.AddWithValue("id", characterId);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result != null;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid characterId, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(
            "DELETE FROM character_memory WHERE character_id = @id", conn);
        cmd.Parameters.AddWithValue("id", characterId);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
        _logger?.LogInformation("Deleted character memory for {CharacterId}", characterId);
    }

    private async Task UpsertMemoryAsync(NpgsqlConnection conn, CharacterMemory memory, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO character_memory
                (character_id, character_name, realm, facts, recent_memories, long_term_goals, last_persisted)
            VALUES
                (@id, @name, @realm, @facts::jsonb, @memories::jsonb, @goals::jsonb, @persisted)
            ON CONFLICT (character_id)
            DO UPDATE SET
                character_name = EXCLUDED.character_name,
                realm = EXCLUDED.realm,
                facts = EXCLUDED.facts,
                recent_memories = EXCLUDED.recent_memories,
                long_term_goals = EXCLUDED.long_term_goals,
                last_persisted = EXCLUDED.last_persisted", conn);

        cmd.Parameters.AddWithValue("id", memory.CharacterId);
        cmd.Parameters.AddWithValue("name", memory.CharacterName);
        cmd.Parameters.AddWithValue("realm", memory.Realm);
        cmd.Parameters.AddWithValue("facts", JsonSerializer.Serialize(memory.Facts, JsonOptions));
        cmd.Parameters.AddWithValue("memories", JsonSerializer.Serialize(memory.RecentMemories, JsonOptions));
        cmd.Parameters.AddWithValue("goals", JsonSerializer.Serialize(memory.LongTermGoals, JsonOptions));
        cmd.Parameters.AddWithValue("persisted", DateTimeOffset.UtcNow);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private CharacterMemory ParseFromReader(Guid characterId, NpgsqlDataReader reader, int offset = 0)
    {
        var factsJson = reader.GetString(offset + 2);
        var memoriesJson = reader.GetString(offset + 3);
        var goalsJson = reader.GetString(offset + 4);

        return new CharacterMemory
        {
            CharacterId = characterId,
            CharacterName = reader.GetString(offset + 0),
            Realm = reader.GetString(offset + 1),
            Facts = JsonSerializer.Deserialize<Dictionary<string, string>>(factsJson, JsonOptions)
                    ?? new Dictionary<string, string>(),
            RecentMemories = JsonSerializer.Deserialize<List<MemoryEntry>>(memoriesJson, JsonOptions)
                             ?? new List<MemoryEntry>(),
            LongTermGoals = JsonSerializer.Deserialize<List<string>>(goalsJson, JsonOptions)
                            ?? new List<string>(),
            LastPersisted = reader.GetDateTime(offset + 5),
            LastLoaded = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Ensures the required database schema exists.
    /// Call this during application startup.
    /// </summary>
    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(@"
            CREATE TABLE IF NOT EXISTS character_memory (
                character_id UUID PRIMARY KEY,
                character_name VARCHAR(50) NOT NULL,
                realm VARCHAR(100) NOT NULL,
                facts JSONB NOT NULL DEFAULT '{}',
                recent_memories JSONB NOT NULL DEFAULT '[]',
                long_term_goals JSONB NOT NULL DEFAULT '[]',
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                last_persisted TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                CONSTRAINT character_memory_name_realm_idx UNIQUE (character_name, realm)
            );

            CREATE INDEX IF NOT EXISTS idx_character_memory_realm
                ON character_memory(realm);
            CREATE INDEX IF NOT EXISTS idx_character_memory_last_persisted
                ON character_memory(last_persisted);
        ", conn);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
        _logger?.LogInformation("Character memory schema ensured");
    }
}
