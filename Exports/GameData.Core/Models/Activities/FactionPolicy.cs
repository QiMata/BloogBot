namespace GameData.Core.Models.Activities
{
    /// <summary>
    /// Faction gating for an activity. <see cref="AllowCrossFaction"/> permits
    /// bots to escort an opposing-faction human (e.g. neutral OnDemand
    /// flow per Spec/04).
    /// </summary>
    public sealed record FactionPolicy(
        FactionRequirement Requirement,
        bool AllowCrossFaction);

    public enum FactionRequirement
    {
        Horde,
        Alliance,
        Either,
    }
}
