using BloogBot.AI.Observable;

namespace BloogBot.AI.Summary;

/// <summary>
/// Context provided to the summary pipeline containing all data to be summarized.
/// </summary>
public sealed record SummaryContext(
    IReadOnlyList<StateChangeEvent> RecentStates,
    CharacterSnapshot Character,
    IReadOnlyList<string> RecentChatMessages,
    IReadOnlyList<QuestProgress> ActiveQuests,
    EnvironmentSnapshot Environment);

/// <summary>
/// Snapshot of character information for summarization.
/// </summary>
public sealed record CharacterSnapshot(
    string Name,
    string Class,
    int Level,
    int HealthPercent,
    int ManaPercent,
    string Zone,
    string SubZone,
    bool InCombat,
    bool InParty,
    int GoldCopper);

/// <summary>
/// Progress on a quest for summarization.
/// </summary>
public sealed record QuestProgress(
    string QuestName,
    string CurrentObjective,
    int ObjectivesCompleted,
    int ObjectivesTotal);

/// <summary>
/// Snapshot of environment information for summarization.
/// </summary>
public sealed record EnvironmentSnapshot(
    int NearbyHostileCount,
    int NearbyFriendlyCount,
    int NearbyNpcCount,
    string? CurrentTarget,
    string? NearestVendor,
    string? NearestFlightMaster);
