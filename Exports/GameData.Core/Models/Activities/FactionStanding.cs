namespace GameData.Core.Models.Activities
{
    /// <summary>
    /// Minimum standing requirement against a specific MaNGOS faction id.
    /// </summary>
    public sealed record FactionStanding(int FactionId, ReputationStanding MinStanding);
}
