namespace BloogBot.AI.Memory;

/// <summary>
/// Repository contract for character memory persistence.
/// Supports lazy loading and batch persistence to PostgreSQL.
/// </summary>
public interface ICharacterMemoryRepository
{
    /// <summary>
    /// Loads memory for a character. Returns null if not found.
    /// This is a lazy load operation - only called when memory is needed.
    /// </summary>
    Task<CharacterMemory?> LoadAsync(Guid characterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads memory by character name and realm.
    /// </summary>
    Task<CharacterMemory?> LoadByNameAsync(string characterName, string realm, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a batch of character memories in a single transaction.
    /// More efficient than individual saves for multiple characters.
    /// </summary>
    Task PersistBatchAsync(IEnumerable<CharacterMemory> memories, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a single character memory.
    /// </summary>
    Task PersistAsync(CharacterMemory memory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if memory exists for a character without loading full data.
    /// </summary>
    Task<bool> ExistsAsync(Guid characterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes memory for a character.
    /// </summary>
    Task DeleteAsync(Guid characterId, CancellationToken cancellationToken = default);
}
