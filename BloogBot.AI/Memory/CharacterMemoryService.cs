using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace BloogBot.AI.Memory;

/// <summary>
/// Service for managing character memory with lazy loading and batch persistence.
/// Caches loaded memories and periodically persists dirty entries.
/// </summary>
public sealed class CharacterMemoryService : IDisposable
{
    private readonly ICharacterMemoryRepository _repository;
    private readonly ILogger<CharacterMemoryService>? _logger;
    private readonly ConcurrentDictionary<Guid, CharacterMemory> _cache = new();
    private readonly ConcurrentDictionary<Guid, bool> _dirty = new();
    private readonly Timer _persistTimer;
    private readonly TimeSpan _persistInterval;
    private bool _disposed;

    /// <summary>
    /// Creates a new CharacterMemoryService with the specified repository.
    /// </summary>
    /// <param name="repository">The repository for persistence.</param>
    /// <param name="persistInterval">Interval between batch persistence operations.</param>
    /// <param name="logger">Optional logger.</param>
    public CharacterMemoryService(
        ICharacterMemoryRepository repository,
        TimeSpan? persistInterval = null,
        ILogger<CharacterMemoryService>? logger = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger;
        _persistInterval = persistInterval ?? TimeSpan.FromMinutes(1);

        _persistTimer = new Timer(
            PersistDirtyMemoriesCallback,
            null,
            _persistInterval,
            _persistInterval);
    }

    /// <summary>
    /// Gets or loads memory for a character.
    /// If memory exists in cache, returns cached version.
    /// If not in cache, loads from repository.
    /// If not in repository, creates new empty memory.
    /// </summary>
    public async Task<CharacterMemory> GetOrLoadAsync(
        Guid characterId,
        string? characterName = null,
        string? realm = null,
        CancellationToken cancellationToken = default)
    {
        // Check cache first (lazy load)
        if (_cache.TryGetValue(characterId, out var cached))
            return cached;

        // Load from repository
        var loaded = await _repository.LoadAsync(characterId, cancellationToken);
        if (loaded != null)
        {
            _cache[characterId] = loaded;
            _logger?.LogDebug("Loaded memory for character {CharacterId}", characterId);
            return loaded;
        }

        // Create new memory if not found
        var newMemory = new CharacterMemory
        {
            CharacterId = characterId,
            CharacterName = characterName ?? "Unknown",
            Realm = realm ?? "Unknown",
            LastLoaded = DateTimeOffset.UtcNow
        };

        _cache[characterId] = newMemory;
        _dirty[characterId] = true;
        _logger?.LogDebug("Created new memory for character {CharacterId}", characterId);

        return newMemory;
    }

    /// <summary>
    /// Gets memory by character name and realm.
    /// </summary>
    public async Task<CharacterMemory?> GetByNameAsync(
        string characterName,
        string realm,
        CancellationToken cancellationToken = default)
    {
        // Check cache first
        var cached = _cache.Values.FirstOrDefault(m =>
            m.CharacterName.Equals(characterName, StringComparison.OrdinalIgnoreCase) &&
            m.Realm.Equals(realm, StringComparison.OrdinalIgnoreCase));

        if (cached != null)
            return cached;

        // Load from repository
        var loaded = await _repository.LoadByNameAsync(characterName, realm, cancellationToken);
        if (loaded != null)
        {
            _cache[loaded.CharacterId] = loaded;
            return loaded;
        }

        return null;
    }

    /// <summary>
    /// Updates memory for a character and marks it as dirty for persistence.
    /// </summary>
    public void UpdateMemory(Guid characterId, Func<CharacterMemory, CharacterMemory> update)
    {
        if (!_cache.TryGetValue(characterId, out var current))
        {
            _logger?.LogWarning("Cannot update memory for {CharacterId} - not loaded", characterId);
            return;
        }

        var updated = update(current);
        _cache[characterId] = updated;
        _dirty[characterId] = true;

        _logger?.LogDebug("Updated memory for character {CharacterId}", characterId);
    }

    /// <summary>
    /// Adds a fact to character memory.
    /// </summary>
    public void AddFact(Guid characterId, string key, string value)
    {
        UpdateMemory(characterId, m => m.WithFact(key, value));
    }

    /// <summary>
    /// Adds a memory entry to character memory.
    /// </summary>
    public void AddMemoryEntry(Guid characterId, MemoryEntry entry)
    {
        UpdateMemory(characterId, m => m.WithMemory(entry));
    }

    /// <summary>
    /// Adds a goal to character memory.
    /// </summary>
    public void AddGoal(Guid characterId, string goal)
    {
        UpdateMemory(characterId, m => m.WithGoal(goal));
    }

    /// <summary>
    /// Forces immediate persistence of all dirty memories.
    /// </summary>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        await PersistDirtyMemoriesAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the number of memories currently cached.
    /// </summary>
    public int CachedCount => _cache.Count;

    /// <summary>
    /// Gets the number of memories pending persistence.
    /// </summary>
    public int DirtyCount => _dirty.Count;

    private async void PersistDirtyMemoriesCallback(object? state)
    {
        if (_disposed) return;

        try
        {
            await PersistDirtyMemoriesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during batch memory persistence");
        }
    }

    private async Task PersistDirtyMemoriesAsync(CancellationToken cancellationToken)
    {
        var dirtyIds = _dirty.Keys.ToList();
        if (dirtyIds.Count == 0) return;

        var toSave = dirtyIds
            .Where(id => _cache.TryGetValue(id, out _))
            .Select(id => _cache[id])
            .ToList();

        if (toSave.Count == 0) return;

        try
        {
            await _repository.PersistBatchAsync(toSave, cancellationToken);

            // Clear dirty flags for successfully persisted memories
            foreach (var id in dirtyIds)
            {
                _dirty.TryRemove(id, out _);
            }

            _logger?.LogInformation("Batch persisted {Count} character memories", toSave.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to persist {Count} character memories - will retry", toSave.Count);
            // Don't clear dirty flags - will retry on next interval
        }
    }

    /// <summary>
    /// Disposes the service, flushing any pending changes.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _persistTimer.Dispose();

        // Best effort final flush
        try
        {
            PersistDirtyMemoriesAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during final memory flush on dispose");
        }
    }
}
