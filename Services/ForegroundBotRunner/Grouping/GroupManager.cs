using ForegroundBotRunner.Mem;
using ForegroundBotRunner.Objects;
using ForegroundBotRunner.Statics;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Serilog;
using System;
using System.Linq;

namespace ForegroundBotRunner.Grouping
{
    /// <summary>
    /// Manages party state: invite handling, role assignment, group-aware target selection,
    /// and follow-leader behavior. Works alongside GrindBot for group grinding.
    /// </summary>
    public class GroupManager(ObjectManager objectManager)
    {
        private readonly ObjectManager _objectManager = objectManager;
        private GroupRole _myRole = GroupRole.DPS;
        private bool _autoAcceptInvites = true;
        private int _lastInviteCheckTick;
        private const int INVITE_CHECK_INTERVAL_MS = 2000;
        private const float FOLLOW_DISTANCE = 15f;
        private const float FOLLOW_TOO_FAR = 40f;

        public GroupRole MyRole => _myRole;
        public bool IsInGroup => _objectManager.Party1Guid != 0;
        public bool IsLeader => IsInGroup && _objectManager.PartyLeaderGuid == _objectManager.Player?.Guid;
        public int PartySize => _objectManager.PartyMembers.Count();
        public bool AutoAcceptInvites { get => _autoAcceptInvites; set => _autoAcceptInvites = value; }

        /// <summary>
        /// Call every tick from bot main loop. Handles invite auto-accept and role detection.
        /// </summary>
        public void Update()
        {
            var now = Environment.TickCount;

            // Periodically check for pending invites
            if (_autoAcceptInvites && now - _lastInviteCheckTick > INVITE_CHECK_INTERVAL_MS)
            {
                _lastInviteCheckTick = now;
                CheckAndAcceptInvite();
            }

            // Update role based on class
            if (_objectManager.Player is LocalPlayer lp)
            {
                _myRole = DetermineRole(lp.Class);
            }
        }

        /// <summary>
        /// Determines the role for a given class. Simple heuristic:
        /// Warriors → Tank, Priests → Healer, everything else → DPS.
        /// Could be enhanced with talent inspection.
        /// </summary>
        public static GroupRole DetermineRole(Class playerClass)
        {
            return playerClass switch
            {
                Class.Warrior => GroupRole.Tank,
                Class.Paladin => GroupRole.Healer, // Holy by default for groups
                Class.Priest => GroupRole.Healer,
                Class.Druid => GroupRole.Healer,   // Resto by default for groups
                Class.Shaman => GroupRole.Healer,  // Resto by default for groups
                _ => GroupRole.DPS
            };
        }

        /// <summary>
        /// Invite a nearby player to the group by name.
        /// </summary>
        public void InvitePlayer(string name)
        {
            ObjectManager.InviteToGroup(name);
            Log.Information("[GroupManager] Invited {Name} to group", name);
        }

        /// <summary>
        /// Get the party leader's position for follow behavior.
        /// Returns null if not in group, or if we are the leader.
        /// </summary>
        public Position? GetLeaderPosition()
        {
            if (!IsInGroup || IsLeader) return null;

            var leader = _objectManager.PartyLeader;
            return leader?.Position;
        }

        /// <summary>
        /// Returns true if we should follow the leader (too far away).
        /// </summary>
        public bool ShouldFollowLeader()
        {
            if (!IsInGroup || IsLeader) return false;

            var leader = _objectManager.PartyLeader;
            if (leader == null) return false;

            var player = _objectManager.Player;
            if (player == null) return false;

            var dist = player.Position.DistanceTo(leader.Position);
            return dist > FOLLOW_DISTANCE;
        }

        /// <summary>
        /// Returns true if we're so far from the leader we should abandon current activity.
        /// </summary>
        public bool IsTooFarFromLeader()
        {
            if (!IsInGroup || IsLeader) return false;

            var leader = _objectManager.PartyLeader;
            if (leader == null) return false;

            var player = _objectManager.Player;
            if (player == null) return false;

            return player.Position.DistanceTo(leader.Position) > FOLLOW_TOO_FAR;
        }

        /// <summary>
        /// Find the best target for group combat. Priorities:
        /// 1. Skull-marked target (kill first)
        /// 2. Cross-marked target (kill second)
        /// 3. Target attacking a party healer
        /// 4. Target attacking the tank
        /// 5. Nearest aggressor
        /// </summary>
        public WoWUnit? FindGroupTarget()
        {
            if (!IsInGroup) return null;

            var player = _objectManager.Player;
            if (player == null) return null;

            // Check raid markers (skull = kill first, cross = kill second)
            var skullGuid = _objectManager.SkullTargetGuid;
            var crossGuid = _objectManager.CrossTargetGuid;

            // Get all units attacking any party member
            var partyGuids = _objectManager.PartyMembers.Select(m => m.Guid).ToHashSet();
            var groupAggressors = _objectManager.Units
                .OfType<WoWUnit>()
                .Where(u => u is not WoWPlayer &&
                           u.Health > 0 &&
                           u.IsInCombat &&
                           partyGuids.Contains(u.TargetGuid))
                .ToList();

            if (groupAggressors.Count == 0) return null;

            // Priority 1: Skull-marked target
            if (skullGuid != 0)
            {
                var skull = groupAggressors.FirstOrDefault(u => u.Guid == skullGuid);
                if (skull != null) return skull;
            }

            // Priority 2: Cross-marked target
            if (crossGuid != 0)
            {
                var cross = groupAggressors.FirstOrDefault(u => u.Guid == crossGuid);
                if (cross != null) return cross;
            }

            // Role-specific targeting
            if (_myRole == GroupRole.Tank)
            {
                // Tank: pick up mobs NOT targeting us (loose adds)
                var loose = groupAggressors
                    .Where(u => u.TargetGuid != player.Guid)
                    .OrderBy(u => u.Position.DistanceTo(player.Position))
                    .FirstOrDefault();
                if (loose != null) return loose;
            }

            // Default: nearest aggressor
            return groupAggressors
                .OrderBy(u => u.Position.DistanceTo(player.Position))
                .FirstOrDefault();
        }

        /// <summary>
        /// For healers: find the party member with the lowest health percentage.
        /// Returns null if everyone is above threshold.
        /// </summary>
        public IWoWPlayer? FindHealTarget(float threshold = 70f)
        {
            if (_myRole != GroupRole.Healer) return null;

            return _objectManager.PartyMembers
                .Where(m => m.Health > 0 && m.MaxHealth > 0)
                .Select(m => new { Member = m, HpPct = (float)m.Health / m.MaxHealth * 100 })
                .Where(x => x.HpPct < threshold)
                .OrderBy(x => x.HpPct)
                .Select(x => x.Member)
                .FirstOrDefault();
        }

        /// <summary>
        /// Check if any party member is in combat.
        /// </summary>
        public bool IsPartyInCombat()
        {
            return _objectManager.PartyMembers.Any(m => m.IsInCombat);
        }

        /// <summary>
        /// Auto-accept group invites via Lua popup detection.
        /// </summary>
        private void CheckAndAcceptInvite()
        {
            try
            {
                // Check if the party invite popup is visible
                var result = Functions.LuaCallWithResult(
                    "{0} = ''; if StaticPopup1 and StaticPopup1:IsVisible() then {0} = StaticPopup1.which or '' end");

                if (result.Length > 0 && result[0] == "PARTY_INVITE")
                {
                    Log.Information("[GroupManager] Auto-accepting group invite");
                    _objectManager.AcceptGroupInvite();
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "[GroupManager] CheckAndAcceptInvite error");
            }
        }
    }
}
