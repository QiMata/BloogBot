using System;
using System.Collections.Generic;

namespace GameData.Core.Models.Activities
{
    /// <summary>
    /// Compiled catalog row. The full contract is defined in
    /// <c>docs/Spec/04_ACTIVITIES.md#activitydefinition</c>; the catalog is
    /// authored as paste-ready literals across
    /// <c>docs/Plan/Activities/_catalog_rows/*.md</c> and concatenated by
    /// <c>ActivityCatalog</c> (S0.3).
    /// </summary>
    public sealed record ActivityDefinition
    {
        public required string Id { get; init; }
        public required ActivityFamily Family { get; init; }
        public required string Activity { get; init; }
        public required string Location { get; init; }
        public required LevelRange LevelRange { get; init; }
        public required FactionPolicy FactionPolicy { get; init; }
        public required int MinPlayers { get; init; }
        public required int MaxPlayers { get; init; }
        public required RoleTemplate RoleTemplate { get; init; }
        public required EntryRequirements EntryRequirements { get; init; }
        public required TravelTarget TravelTarget { get; init; }
        public required TimeSpan ExpectedDuration { get; init; }
        public required HumanJoinPolicy HumanJoinPolicy { get; init; }
        public required BotSelectionPolicy BotSelectionPolicy { get; init; }
        public required IReadOnlyList<string> ProgressionTags { get; init; }
        public required IReadOnlyList<RewardDefinition> Rewards { get; init; }
        public required string TaskFamily { get; init; }
    }
}
