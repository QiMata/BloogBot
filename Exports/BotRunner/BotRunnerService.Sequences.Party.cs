using GameData.Core.Enums;
using Serilog;
using System.Linq;
using Xas.FluentBehaviourTree;

namespace BotRunner
{
    public partial class BotRunnerService
    {
        /// <summary>
        /// Sequence to promote another player to group leader.
        /// </summary>
        /// <param name="playerGuid">The GUID of the player to promote to leader.</param>
        /// <returns>IBehaviourTreeNode that manages promoting the player to group leader.</returns>
        private IBehaviourTreeNode BuildPromoteLeaderSequence(ulong playerGuid) => new BehaviourTreeBuilder()
            .Sequence("Promote Leader Sequence")
                // Ensure the bot is in a group with the specified player
                .Condition("Is In Group with Player", time => _objectManager.PartyMembers.Any(x => x.Guid == playerGuid))

                // Promote the player to group leader
                .Do("Promote Leader", time =>
                {
                    _objectManager.PromoteLeader(playerGuid);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to promote another player to group assistant.
        /// </summary>
        /// <param name="playerGuid">The GUID of the player to promote to assistant.</param>
        /// <returns>IBehaviourTreeNode that manages promoting the player to group assistant.</returns>
        private IBehaviourTreeNode BuildPromoteAssistantSequence(ulong playerGuid) => new BehaviourTreeBuilder()
            .Sequence("Promote Assistant Sequence")
                // Ensure the bot is in a group with the specified player
                .Condition("Is In Group with Player", time => _objectManager.PartyMembers.Any(x => x.Guid == playerGuid))

                // Promote the player to group assistant
                .Do("Promote Assistant", time =>
                {
                    _objectManager.PromoteAssistant(playerGuid);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to promote another player to loot manager in the group.
        /// </summary>
        /// <param name="playerGuid">The GUID of the player to promote to loot manager.</param>
        /// <returns>IBehaviourTreeNode that manages promoting the player to loot manager.</returns>
        private IBehaviourTreeNode BuildPromoteLootManagerSequence(ulong playerGuid) => new BehaviourTreeBuilder()
            .Sequence("Promote Loot Manager Sequence")
                // Ensure the bot is in a group with the specified player
                .Condition("Is In Group with Player", time => _objectManager.PartyMembers.Any(x => x.Guid == playerGuid))

                // Promote the player to loot manager
                .Do("Promote Loot Manager", time =>
                {
                    _objectManager.PromoteLootManager(playerGuid);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to set group loot rules for distributing loot in a group.
        /// </summary>
        /// <param name="setting">The group loot setting to apply (e.g., free-for-all, round-robin).</param>
        /// <returns>IBehaviourTreeNode that manages setting the group loot rules.</returns>
        private IBehaviourTreeNode BuildSetGroupLootSequence(GroupLootSetting setting) => new BehaviourTreeBuilder()
            .Sequence("Set Group Loot Sequence")
                // Ensure the bot is in a group and has permission to change loot rules
                .Condition("Can Set Loot Rules", time => _objectManager.PartyLeaderGuid == _objectManager.Player.Guid)

                // Set the group loot rule
                .Do("Set Group Loot", time =>
                {
                    _objectManager.SetGroupLoot(setting);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to assign specific loot to a player in the group.
        /// </summary>
        /// <param name="itemId">The ID of the loot item to assign.</param>
        /// <param name="playerGuid">The GUID of the player to assign the loot to.</param>
        /// <returns>IBehaviourTreeNode that manages assigning the loot.</returns>
        private IBehaviourTreeNode BuildAssignLootSequence(int itemId, ulong playerGuid) => new BehaviourTreeBuilder()
            .Sequence("Assign Loot Sequence")
                // Ensure the bot has permission to assign loot
                .Condition("Can Assign Loot", time => _objectManager.PartyMembers.Any(x => x.Guid == playerGuid))

                // Assign the loot to the specified player
                .Do("Assign Loot", time =>
                {
                    _objectManager.AssignLoot(itemId, playerGuid);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to roll "Need" on a specific loot item during group loot distribution.
        /// </summary>
        /// <param name="itemId">The ID of the item to roll "Need" on.</param>
        /// <returns>IBehaviourTreeNode that manages rolling "Need" for the item.</returns>
        private IBehaviourTreeNode BuildLootRollNeedSequence(int itemId) => new BehaviourTreeBuilder()
            .Sequence("Loot Roll Need Sequence")
                // Ensure the bot can roll "Need" on the item
                .Condition("Can Roll Need", time => _objectManager.HasLootRollWindow(itemId))

                // Roll "Need" for the item
                .Do("Roll Need", time =>
                {
                    _objectManager.LootRollNeed(itemId);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to roll "Greed" on a specific loot item during group loot distribution.
        /// </summary>
        /// <param name="itemId">The ID of the item to roll "Greed" on.</param>
        /// <returns>IBehaviourTreeNode that manages rolling "Greed" for the item.</returns>
        private IBehaviourTreeNode BuildLootRollGreedSequence(int itemId) => new BehaviourTreeBuilder()
            .Sequence("Loot Roll Greed Sequence")
                // Ensure the bot can roll "Greed" on the item
                .Condition("Can Roll Greed", time => _objectManager.HasLootRollWindow(itemId))

                // Roll "Greed" for the item
                .Do("Roll Greed", time =>
                {
                    _objectManager.LootRollGreed(itemId);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to pass on a specific loot item during group loot distribution.
        /// </summary>
        /// <param name="itemId">The ID of the item to pass on.</param>
        /// <returns>IBehaviourTreeNode that manages passing on the item.</returns>
        private IBehaviourTreeNode BuildLootPassSequence(int itemId) => new BehaviourTreeBuilder()
            .Sequence("Loot Pass Sequence")
                // Ensure the bot can pass on the item
                .Condition("Can Pass Loot", time => _objectManager.HasLootRollWindow(itemId))

                // Pass on the loot item
                .Do("Pass Loot", time =>
                {
                    _objectManager.LootPass(itemId);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to send a group invite to another player.
        /// </summary>
        /// <param name="playerGuid">The GUID of the player to invite to the group.</param>
        /// <returns>IBehaviourTreeNode that manages sending the group invite.</returns>
        private IBehaviourTreeNode BuildSendGroupInviteSequence(ulong playerGuid) => new BehaviourTreeBuilder()
            .Sequence("Send Group Invite Sequence")
                // Ensure the player is not already in a group and can be invited
                .Condition("Can Send Group Invite", time => !_objectManager.PartyMembers.Any(x => x.Guid == playerGuid))

                // Send the group invite
                .Do("Send Group Invite", time =>
                {
                    _objectManager.InviteToGroup(playerGuid);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to send a group invite by player name (used by headless clients via PartyNetworkClientComponent).
        /// </summary>
        private IBehaviourTreeNode BuildSendGroupInviteByNameSequence(string playerName) => new BehaviourTreeBuilder()
            .Sequence("Send Group Invite By Name Sequence")
                .Do("Send Group Invite By Name", time =>
                {
                    var factory = _agentFactoryAccessor?.Invoke();
                    if (factory != null)
                    {
                        Log.Information($"[BOT RUNNER] Inviting player '{playerName}' to group via network agent");
                        _ = factory.PartyAgent.InvitePlayerAsync(playerName);
                        return BehaviourTreeStatus.Success;
                    }

                    // Fallback to IObjectManager for foreground bots (uses Lua InviteByName)
                    Log.Information($"[BOT RUNNER] Inviting player '{playerName}' to group via ObjectManager");
                    _objectManager.InviteByName(playerName);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to accept a group invite from another player.
        /// </summary>
        /// <returns>IBehaviourTreeNode that manages accepting the group invite.</returns>
        private IBehaviourTreeNode AcceptGroupInviteSequence
        {
            get
            {
                int pollCount = 0;
                return new BehaviourTreeBuilder()
                    .Sequence("Accept Group Invite Sequence")
                        .Do("Accept Group Invite", time =>
                        {
                            pollCount++;
                            var factory = _agentFactoryAccessor?.Invoke();
                            if (factory != null && factory.PartyAgent.HasPendingInvite)
                            {
                                Log.Information("[BOT RUNNER] Accepting group invite via network agent");
                                _ = factory.PartyAgent.AcceptInviteAsync();
                                return BehaviourTreeStatus.Success;
                            }

                            // Fallback to IObjectManager for foreground bots
                            if (_objectManager.HasPendingGroupInvite())
                            {
                                _objectManager.AcceptGroupInvite();
                                return BehaviourTreeStatus.Success;
                            }

                            if (pollCount % 10 == 1)
                                Log.Information($"[BOT RUNNER] Waiting for group invite... (poll {pollCount}, HasPendingInvite={factory?.PartyAgent?.HasPendingInvite})");

                            // Timeout after ~10 seconds (100 ticks at 100ms)
                            if (pollCount >= 100)
                            {
                                Log.Warning("[BOT RUNNER] Timed out waiting for group invite after 10s");
                                return BehaviourTreeStatus.Failure;
                            }

                            return BehaviourTreeStatus.Running;
                        })
                    .End()
                    .Build();
            }
        }
        /// <summary>
        /// Sequence to decline a group invite from another player.
        /// </summary>
        /// <returns>IBehaviourTreeNode that manages declining the group invite.</returns>
        private IBehaviourTreeNode DeclineGroupInviteSequence => new BehaviourTreeBuilder()
            .Sequence("Decline Group Invite Sequence")
                // Ensure the bot has a pending invite to decline
                .Condition("Has Pending Invite", time => _objectManager.HasPendingGroupInvite())

                // Decline the group invite
                .Do("Decline Group Invite", time =>
                {
                    _objectManager.DeclineGroupInvite();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to kick a player from the group.
        /// </summary>
        /// <param name="playerGuid">The GUID of the player to kick from the group.</param>
        /// <returns>IBehaviourTreeNode that manages kicking the player from the group.</returns>
        private IBehaviourTreeNode BuildKickPlayerSequence(ulong playerGuid) => new BehaviourTreeBuilder()
            .Sequence("Kick Player Sequence")
                // Ensure the bot has permission to kick players and the target is valid
                .Condition("Can Kick Player", time => _objectManager.Player.Guid == _objectManager.PartyLeaderGuid)

                // Kick the player from the group
                .Do("Kick Player", time =>
                {
                    _objectManager.KickPlayer(playerGuid);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to leave the current group.
        /// </summary>
        /// <returns>IBehaviourTreeNode that manages leaving the group.</returns>
        private IBehaviourTreeNode LeaveGroupSequence => new BehaviourTreeBuilder()
            .Sequence("Leave Group Sequence")
                // Leave the group
                .Do("Leave Group", time =>
                {
                    var factory = _agentFactoryAccessor?.Invoke();
                    if (factory != null)
                    {
                        Log.Information("[BOT RUNNER] Leaving group via network agent");
                        _ = factory.PartyAgent.LeaveGroupAsync();
                        return BehaviourTreeStatus.Success;
                    }

                    // Fallback to IObjectManager for foreground bots
                    if (_objectManager.PartyLeaderGuid != 0)
                    {
                        Log.Information("[BOT RUNNER] Leaving group via ObjectManager");
                        _objectManager.LeaveGroup();
                        return BehaviourTreeStatus.Success;
                    }

                    Log.Information("[BOT RUNNER] Not in a group, nothing to leave");
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to disband the current group the bot is leading.
        /// </summary>
        /// <returns>IBehaviourTreeNode that manages disbanding the group.</returns>
        private IBehaviourTreeNode DisbandGroupSequence => new BehaviourTreeBuilder()
            .Sequence("Disband Group Sequence")
                // Ensure the bot is the leader of the group
                .Condition("Is Group Leader", time => _objectManager.Player.Guid == _objectManager.PartyLeaderGuid)

                // Disband the group
                .Do("Disband Group", time =>
                {
                    _objectManager.DisbandGroup();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
    }
}
