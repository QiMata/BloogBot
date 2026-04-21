using GameData.Core.Enums;
using GameData.Core.Interfaces;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace BotRunner
{
    public partial class BotRunnerService
    {
        private static readonly TimeSpan DesiredPartyInviteCooldown = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan DesiredPartyAcceptCooldown = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan DesiredPartyGroupExitCooldown = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan DesiredPartyRaidUpgradeCooldown = TimeSpan.FromSeconds(5);

        private readonly Dictionary<string, DateTime> _desiredPartyInviteSentAt = new(StringComparer.OrdinalIgnoreCase);
        private DateTime _lastDesiredPartyAcceptAt = DateTime.MinValue;
        private DateTime _lastDesiredPartyGroupExitAt = DateTime.MinValue;
        private DateTime _lastDesiredPartyRaidUpgradeAt = DateTime.MinValue;
        private bool _desiredPartyRaidUpgradeAssumed;

        private enum DesiredPartyGroupAlignment
        {
            NotInGroup,
            GroupedToDesiredLeader,
            GroupedToWrongLeader,
            LeadingUnexpectedGroup,
        }

        private bool TryBuildDesiredPartyBehaviorTree(Communication.WoWActivitySnapshot? incomingActivityMemberState)
        {
            if (incomingActivityMemberState == null
                || string.IsNullOrWhiteSpace(incomingActivityMemberState.DesiredPartyLeaderName))
            {
                ResetDesiredPartyReconciliationState();
                return false;
            }

            var player = _objectManager.Player;
            if (player == null)
                return false;

            var selfName = ResolveCurrentCharacterName(incomingActivityMemberState);
            if (string.IsNullOrWhiteSpace(selfName))
                return false;

            var desiredLeaderName = incomingActivityMemberState.DesiredPartyLeaderName.Trim();
            var currentGroupAlignment = GetCurrentDesiredPartyGroupAlignment(player, selfName, desiredLeaderName);
            if (!selfName.Equals(desiredLeaderName, StringComparison.OrdinalIgnoreCase))
            {
                _desiredPartyRaidUpgradeAssumed = false;
                return TryBuildDesiredPartyFollowerBehaviorTree(currentGroupAlignment);
            }

            if (currentGroupAlignment == DesiredPartyGroupAlignment.GroupedToWrongLeader)
            {
                _desiredPartyRaidUpgradeAssumed = false;
                return TryBuildDesiredPartyGroupExitBehaviorTree(disbandGroup: false);
            }

            if (currentGroupAlignment == DesiredPartyGroupAlignment.LeadingUnexpectedGroup)
            {
                _desiredPartyRaidUpgradeAssumed = false;
                return TryBuildDesiredPartyGroupExitBehaviorTree(disbandGroup: true);
            }

            var desiredMembers = incomingActivityMemberState.DesiredPartyMembers
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var localGroupSize = GetCurrentGroupSize(player);
            var isRaid = IsCurrentGroupRaid();

            if (incomingActivityMemberState.DesiredPartyIsRaid
                && localGroupSize >= 5
                && !isRaid)
            {
                var now = DateTime.UtcNow;
                if (now - _lastDesiredPartyRaidUpgradeAt < DesiredPartyRaidUpgradeCooldown)
                    return false;

                _lastDesiredPartyRaidUpgradeAt = now;
                _desiredPartyRaidUpgradeAssumed = true;
                return StartDesiredPartyBehavior(CharacterAction.ConvertToRaid);
            }

            if (desiredMembers.Count == 0)
                return false;

            var nextMember = desiredMembers.FirstOrDefault(ShouldInviteDesiredPartyMember);
            if (nextMember == null)
                return false;

            _desiredPartyInviteSentAt[nextMember] = DateTime.UtcNow;
            return StartDesiredPartyBehavior(CharacterAction.SendGroupInvite, nextMember);
        }

        private bool TryBuildDesiredPartyFollowerBehaviorTree(DesiredPartyGroupAlignment currentGroupAlignment)
        {
            if (currentGroupAlignment == DesiredPartyGroupAlignment.GroupedToWrongLeader)
                return TryBuildDesiredPartyGroupExitBehaviorTree(disbandGroup: false);

            if (currentGroupAlignment == DesiredPartyGroupAlignment.LeadingUnexpectedGroup)
                return TryBuildDesiredPartyGroupExitBehaviorTree(disbandGroup: true);

            if (!HasPendingDesiredPartyInvite())
                return false;

            var now = DateTime.UtcNow;
            if (now - _lastDesiredPartyAcceptAt < DesiredPartyAcceptCooldown)
                return false;

            _lastDesiredPartyAcceptAt = now;
            return StartDesiredPartyBehavior(CharacterAction.AcceptGroupInvite);
        }

        private bool TryBuildDesiredPartyGroupExitBehaviorTree(bool disbandGroup)
        {
            var now = DateTime.UtcNow;
            if (now - _lastDesiredPartyGroupExitAt < DesiredPartyGroupExitCooldown)
                return false;

            _lastDesiredPartyGroupExitAt = now;
            return StartDesiredPartyBehavior(disbandGroup
                ? CharacterAction.DisbandGroup
                : CharacterAction.LeaveGroup);
        }

        private void ResetDesiredPartyReconciliationState()
        {
            _desiredPartyRaidUpgradeAssumed = false;
            _desiredPartyInviteSentAt.Clear();
            _lastDesiredPartyAcceptAt = DateTime.MinValue;
            _lastDesiredPartyGroupExitAt = DateTime.MinValue;
            _lastDesiredPartyRaidUpgradeAt = DateTime.MinValue;
        }

        private string ResolveCurrentCharacterName(Communication.WoWActivitySnapshot incomingActivityMemberState)
        {
            if (!string.IsNullOrWhiteSpace(incomingActivityMemberState.CharacterName))
                return incomingActivityMemberState.CharacterName.Trim();

            if (!string.IsNullOrWhiteSpace(_activitySnapshot.CharacterName))
                return _activitySnapshot.CharacterName.Trim();

            var generatedName = ResolveGeneratedCharacterName(
                !string.IsNullOrWhiteSpace(incomingActivityMemberState.AccountName)
                    ? incomingActivityMemberState.AccountName
                    : _activitySnapshot.AccountName);

            if (!string.IsNullOrWhiteSpace(generatedName))
                _activitySnapshot.CharacterName = generatedName;

            return generatedName;
        }

        private static string ResolveGeneratedCharacterName(string? accountName)
        {
            if (string.IsNullOrWhiteSpace(accountName))
                return string.Empty;

            var characterClass = WoWNameGenerator.ResolveClass(accountName);
            var race = WoWNameGenerator.ResolveRace(accountName);
            var gender = WoWNameGenerator.ResolveGender(characterClass);
            var attemptOffset = ResolveCharacterNameAttemptOffset();
            var uniquenessSeed = BuildCharacterUniquenessSeed(accountName, createAttempts: 0, attemptOffset);
            return WoWNameGenerator.GenerateName(race, gender, uniquenessSeed);
        }

        private ulong GetCurrentPartyLeaderGuid(IWoWLocalPlayer player)
        {
            var partyAgent = _agentFactoryAccessor?.Invoke()?.PartyAgent;
            if (partyAgent != null)
            {
                if (partyAgent.LeaderGuid != 0)
                    return partyAgent.LeaderGuid;

                if (partyAgent.IsGroupLeader
                    && (partyAgent.GroupSize > 0 || partyAgent.GetGroupMembers().Count > 0))
                {
                    return player.Guid;
                }
            }

            return _objectManager.PartyLeaderGuid;
        }

        private DesiredPartyGroupAlignment GetCurrentDesiredPartyGroupAlignment(
            IWoWLocalPlayer player,
            string selfName,
            string desiredLeaderName)
        {
            var partyAgent = _agentFactoryAccessor?.Invoke()?.PartyAgent;
            if (partyAgent != null)
            {
                var groupMembers = partyAgent.GetGroupMembers();
                var currentLeaderGuid = GetCurrentPartyLeaderGuid(player);
                var isInGroup = partyAgent.IsInGroup
                    || partyAgent.IsGroupLeader
                    || currentLeaderGuid != 0
                    || partyAgent.GroupSize > 0
                    || groupMembers.Count > 0;

                if (!isInGroup)
                    return DesiredPartyGroupAlignment.NotInGroup;

                if (currentLeaderGuid == player.Guid)
                {
                    return selfName.Equals(desiredLeaderName, StringComparison.OrdinalIgnoreCase)
                        ? DesiredPartyGroupAlignment.GroupedToDesiredLeader
                        : DesiredPartyGroupAlignment.LeadingUnexpectedGroup;
                }

                var currentLeaderName = currentLeaderGuid != 0
                    ? groupMembers.FirstOrDefault(member => member.Guid == currentLeaderGuid)?.Name
                    : null;

                if (!string.IsNullOrWhiteSpace(currentLeaderName)
                    && currentLeaderName.Equals(desiredLeaderName, StringComparison.OrdinalIgnoreCase))
                {
                    return DesiredPartyGroupAlignment.GroupedToDesiredLeader;
                }

                return DesiredPartyGroupAlignment.GroupedToWrongLeader;
            }

            var fallbackLeaderGuid = _objectManager.PartyLeaderGuid;
            if (fallbackLeaderGuid == 0)
                return DesiredPartyGroupAlignment.NotInGroup;

            if (fallbackLeaderGuid == player.Guid)
            {
                return selfName.Equals(desiredLeaderName, StringComparison.OrdinalIgnoreCase)
                    ? DesiredPartyGroupAlignment.GroupedToDesiredLeader
                    : DesiredPartyGroupAlignment.LeadingUnexpectedGroup;
            }

            var fallbackLeaderName = _objectManager.PartyLeader?.Name;
            if (!string.IsNullOrWhiteSpace(fallbackLeaderName)
                && fallbackLeaderName.Equals(desiredLeaderName, StringComparison.OrdinalIgnoreCase))
            {
                return DesiredPartyGroupAlignment.GroupedToDesiredLeader;
            }

            return DesiredPartyGroupAlignment.GroupedToWrongLeader;
        }

        private int GetCurrentGroupSize(IWoWLocalPlayer player)
        {
            var partyAgent = _agentFactoryAccessor?.Invoke()?.PartyAgent;
            if (partyAgent != null)
            {
                var members = partyAgent.GetGroupMembers();
                var memberCount = members.Count;
                var groupSize = (int)partyAgent.GroupSize;
                if (groupSize > 0 || memberCount > 0)
                {
                    var knownCount = Math.Max(groupSize, memberCount);
                    var includesSelf = members.Any(member => member.Guid == player.Guid)
                        || groupSize > memberCount;
                    return includesSelf ? knownCount : knownCount + 1;
                }
            }

            var count = player.Guid != 0 ? 1 : 0;
            if (_objectManager.PartyLeaderGuid == 0)
                return count;

            if (_objectManager.Party1Guid != 0) count++;
            if (_objectManager.Party2Guid != 0) count++;
            if (_objectManager.Party3Guid != 0) count++;
            if (_objectManager.Party4Guid != 0) count++;
            return count;
        }

        private bool IsCurrentGroupRaid()
        {
            var partyAgent = _agentFactoryAccessor?.Invoke()?.PartyAgent;
            if (partyAgent != null)
                return partyAgent.IsInRaid || _desiredPartyRaidUpgradeAssumed;

            return _desiredPartyRaidUpgradeAssumed;
        }

        private bool HasPendingDesiredPartyInvite()
        {
            var partyAgent = _agentFactoryAccessor?.Invoke()?.PartyAgent;
            if (partyAgent?.HasPendingInvite == true)
                return true;

            return _objectManager.HasPendingGroupInvite();
        }

        private bool ShouldInviteDesiredPartyMember(string characterName)
        {
            if (string.IsNullOrWhiteSpace(characterName))
                return false;

            if (!_desiredPartyInviteSentAt.TryGetValue(characterName, out var lastSentAt))
                return true;

            return DateTime.UtcNow - lastSentAt >= DesiredPartyInviteCooldown;
        }

        private bool StartDesiredPartyBehavior(CharacterAction action, params object[] parameters)
        {
            _currentActionCorrelationId = $"desired-party-{Interlocked.Increment(ref _actionSequenceNumber)}";
            Log.Information("[BOT RUNNER] Reconciling desired party state with {Action} [{CorrelationId}]", action, _currentActionCorrelationId);
            DiagLog($"[DESIRED-PARTY] action={action} params={parameters.Length} corr={_currentActionCorrelationId}");

            var actionList = new List<(CharacterAction, List<object>)>
            {
                (action, new List<object>(parameters))
            };

            _behaviorTree = BuildBehaviorTreeFromActions(actionList);
            _behaviorTreeStatus = Xas.FluentBehaviourTree.BehaviourTreeStatus.Running;
            return true;
        }
    }
}
