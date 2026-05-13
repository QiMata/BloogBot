using System.Collections.Generic;

namespace GameData.Core.Models.Activities
{
    /// <summary>
    /// Gating requirements consulted by both the autonomous progression
    /// planner (rejects) and the OnDemand launcher (fixes). Defaults are
    /// empty collections plus <see cref="LockoutPolicy.None"/>.
    /// </summary>
    public sealed record EntryRequirements
    {
        public IReadOnlyList<int> RequiredItems { get; init; } = [];
        public IReadOnlyList<int> RequiredQuests { get; init; } = [];
        public IReadOnlyList<FactionStanding> RequiredReputations { get; init; } = [];
        public IReadOnlyList<string> RequiredAttunements { get; init; } = [];
        public IReadOnlyList<string> RequiredCapabilities { get; init; } = [];
        public LockoutPolicy LockoutPolicy { get; init; } = LockoutPolicy.None();
    }
}
