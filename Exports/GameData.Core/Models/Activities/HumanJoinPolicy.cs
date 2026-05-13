using System;

namespace GameData.Core.Models.Activities
{
    /// <summary>
    /// How a human participates in an OnDemand activity. Catalog shards use
    /// two construction styles:
    /// <list type="bullet">
    ///   <item>Object initializer (<c>new HumanJoinPolicy { ... }</c>) per
    ///         shards 1 and 4.</item>
    ///   <item>Positional constructor with five core arguments per shards 2,
    ///         3, 5.</item>
    /// </list>
    /// Both are supported. The optional <see cref="BotRaidLeader"/> and
    /// <see cref="GearHuman"/> properties match the additional fields named
    /// in <c>Spec/04_ACTIVITIES.md#human-join-semantics-ondemand-only</c>.
    /// </summary>
    public sealed record HumanJoinPolicy
    {
        public bool HumanCanInitiate { get; init; }
        public HumanGroupRole HumanRole { get; init; }
        public bool BotRaidLeader { get; init; }
        public bool RequireFactionMatch { get; init; }
        public bool LootPriorityToHuman { get; init; }
        public bool GearHuman { get; init; }
        public TimeSpan HumanIdleTimeout { get; init; }

        public HumanJoinPolicy()
        {
        }

        public HumanJoinPolicy(
            bool HumanCanInitiate,
            HumanGroupRole HumanRole,
            bool RequireFactionMatch,
            bool LootPriorityToHuman,
            TimeSpan HumanIdleTimeout)
        {
            this.HumanCanInitiate = HumanCanInitiate;
            this.HumanRole = HumanRole;
            this.RequireFactionMatch = RequireFactionMatch;
            this.LootPriorityToHuman = LootPriorityToHuman;
            this.HumanIdleTimeout = HumanIdleTimeout;
        }
    }
}
