namespace BloogBot.AI.Memory;

/// <summary>
/// Represents persistent memory for a character.
/// Supports lazy loading and batch persistence to PostgreSQL.
/// </summary>
public sealed record CharacterMemory
{
    /// <summary>
    /// Unique identifier for the character.
    /// </summary>
    public Guid CharacterId { get; init; }

    /// <summary>
    /// Character's in-game name.
    /// </summary>
    public string CharacterName { get; init; } = string.Empty;

    /// <summary>
    /// Realm/server the character belongs to.
    /// </summary>
    public string Realm { get; init; } = string.Empty;

    /// <summary>
    /// Key-value facts about the character (e.g., "preferred_weapon": "sword").
    /// </summary>
    public IReadOnlyDictionary<string, string> Facts { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Recent memory entries with importance and expiration.
    /// </summary>
    public IReadOnlyList<MemoryEntry> RecentMemories { get; init; } =
        Array.Empty<MemoryEntry>();

    /// <summary>
    /// Long-term goals the character is working toward.
    /// </summary>
    public IReadOnlyList<string> LongTermGoals { get; init; } =
        Array.Empty<string>();

    /// <summary>
    /// When this memory was last persisted to storage.
    /// </summary>
    public DateTimeOffset LastPersisted { get; init; }

    /// <summary>
    /// When this memory was last loaded from storage.
    /// </summary>
    public DateTimeOffset LastLoaded { get; init; }

    /// <summary>
    /// Creates a new empty memory for a character.
    /// </summary>
    public static CharacterMemory CreateNew(Guid characterId, string name, string realm) =>
        new()
        {
            CharacterId = characterId,
            CharacterName = name,
            Realm = realm,
            LastLoaded = DateTimeOffset.UtcNow
        };

    /// <summary>
    /// Creates a copy with an additional fact.
    /// </summary>
    public CharacterMemory WithFact(string key, string value)
    {
        var newFacts = new Dictionary<string, string>(Facts) { [key] = value };
        return this with { Facts = newFacts };
    }

    /// <summary>
    /// Creates a copy with an additional memory entry.
    /// </summary>
    public CharacterMemory WithMemory(MemoryEntry entry)
    {
        var newMemories = RecentMemories.Append(entry).ToList();
        return this with { RecentMemories = newMemories };
    }

    /// <summary>
    /// Creates a copy with an additional goal.
    /// </summary>
    public CharacterMemory WithGoal(string goal)
    {
        var newGoals = LongTermGoals.Append(goal).ToList();
        return this with { LongTermGoals = newGoals };
    }
}

/// <summary>
/// A single memory entry with metadata for importance and expiration.
/// </summary>
public sealed record MemoryEntry(
    string Content,
    MemoryCategory Category,
    double Importance,
    DateTimeOffset Timestamp,
    DateTimeOffset? ExpiresAt)
{
    /// <summary>
    /// Returns true if this memory has expired.
    /// </summary>
    public bool IsExpired => ExpiresAt.HasValue && DateTimeOffset.UtcNow > ExpiresAt.Value;

    /// <summary>
    /// Creates a permanent memory with high importance.
    /// </summary>
    public static MemoryEntry CreatePermanent(string content, MemoryCategory category, double importance = 1.0) =>
        new(content, category, importance, DateTimeOffset.UtcNow, null);

    /// <summary>
    /// Creates a temporary memory that expires after the specified duration.
    /// </summary>
    public static MemoryEntry CreateTemporary(string content, MemoryCategory category, TimeSpan duration, double importance = 0.5) =>
        new(content, category, importance, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.Add(duration));
}

/// <summary>
/// Categories for organizing memory entries.
/// </summary>
public enum MemoryCategory
{
    /// <summary>Combat-related memories (tactics, enemy patterns).</summary>
    Combat,

    /// <summary>Quest-related memories (objectives, NPC locations).</summary>
    Quest,

    /// <summary>Social memories (player interactions, guild events).</summary>
    Social,

    /// <summary>Economic memories (prices, trade opportunities).</summary>
    Economic,

    /// <summary>Navigation memories (paths, discovered areas).</summary>
    Navigation,

    /// <summary>Learning memories (skill improvements, discoveries).</summary>
    Learning,

    /// <summary>Goal-related memories (progress toward objectives).</summary>
    Goal
}
