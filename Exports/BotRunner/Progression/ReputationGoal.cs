namespace BotRunner.Progression
{
    public enum ReputationStanding
    {
        Hated = 0,
        Hostile = 1,
        Unfriendly = 2,
        Neutral = 3,
        Friendly = 4,
        Honored = 5,
        Revered = 6,
        Exalted = 7
    }

    public record ReputationGoal(
        int FactionId,
        string FactionName,
        ReputationStanding TargetStanding,
        string GrindMethod);    // "Quests", "Dungeon:Stratholme", "Turnin:RuneclothBandage", "Mob:TimbermawFurbolg"

    public record ReputationProgress(
        int FactionId,
        string FactionName,
        ReputationStanding CurrentStanding,
        ReputationStanding TargetStanding,
        int CurrentRep,         // Raw reputation value
        bool IsComplete);
}
