namespace GameData.Core.Models.Activities
{
    /// <summary>
    /// Selection weights for the OnDemand bot pool, per Spec/04. Defaults
    /// are homogeneous (per the 2026-05-12 design decision to ship
    /// uniform weights before measuring per-family signal).
    /// </summary>
    public sealed record BotSelectionPolicy(
        int RoleFitWeight = 100,
        int LevelFitWeight = 50,
        int InterruptibilityWeight = 40,
        int TravelEtaWeight = 30,
        int GearReadinessWeight = 20,
        int ClassUtilityWeight = 20,
        int ProgressionOpportunityWeight = 15,
        int RecentFailurePenaltyWeight = -25,
        int HumanPreferenceWeight = 50,
        LootPolicy LootPriority = LootPolicy.NeedBeforeGreed);
}
