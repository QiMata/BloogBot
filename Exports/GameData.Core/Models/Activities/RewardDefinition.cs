namespace GameData.Core.Models.Activities
{
    /// <summary>
    /// Summary reward shape for the ProgressionPlanner's value scoring
    /// (XP/hour vs gold/hour vs rep gain). Per-quest reward CHOICE is
    /// handled by <c>RewardSelector</c> at turn-in time. Per-encounter
    /// drop tables live in a separate <c>LootTable</c> model (deferred).
    /// </summary>
    public sealed record RewardDefinition(
        RewardKind Kind,
        int Min,
        int Max,
        int? ItemId,
        int? FactionId);

    public enum RewardKind
    {
        XpRange,
        Gold,
        ItemId,
        FactionRep,
        Honor,
    }
}
