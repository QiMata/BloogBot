namespace GameData.Core.Models.Activities
{
    /// <summary>
    /// High-level family classifier for an <see cref="ActivityDefinition"/>.
    /// Mirrors the breakdown in <c>docs/Spec/04_ACTIVITIES.md#activitydefinition</c>.
    /// </summary>
    public enum ActivityFamily
    {
        StarterQuesting,
        ZoneQuesting,
        Dungeon,
        Raid,
        Battleground,
        ProfessionGathering,
        ProfessionCrafting,
        ProfessionLeveling,
        Economy,
        Reputation,
        Attunement,
        WorldEvent,
        WorldBoss,
    }
}
