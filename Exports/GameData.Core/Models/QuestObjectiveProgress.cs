namespace GameData.Core.Models
{
    /// <summary>
    /// Tracks real-time progress for a single quest objective, updated from
    /// SMSG_QUESTUPDATE_ADD_KILL and SMSG_QUESTUPDATE_ADD_ITEM packets.
    /// </summary>
    public record QuestObjectiveProgress(
        uint QuestId,
        uint ObjectId,
        uint CurrentCount,
        uint RequiredCount,
        QuestObjectiveTypes Type);
}
