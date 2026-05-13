namespace GameData.Core.Models.Activities
{
    /// <summary>
    /// Group loot policy hint used by <see cref="BotSelectionPolicy"/> and
    /// the OnDemand launcher per Spec/04.
    /// </summary>
    public enum LootPolicy
    {
        FreeForAll,
        GroupLoot,
        NeedBeforeGreed,
        MasterLoot,
    }
}
