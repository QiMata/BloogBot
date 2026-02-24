using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WoWSharpClient.Models
{
    public class WoWLocalPlayer(HighGuid highGuid, WoWObjectType objectType = WoWObjectType.Player)
        : WoWPlayer(highGuid, objectType),
            IWoWLocalPlayer
    {
        // Battleground map IDs for vanilla 1.12.1
        private static readonly HashSet<uint> BattlegroundMapIds = [30, 489, 529, 566];

        // Warrior stance buff names
        private const string BattleStance = "Battle Stance";
        private const string DefensiveStance = "Defensive Stance";
        private const string BerserkerStance = "Berserker Stance";

        // Backing fields set by packet handlers
        private Position _corpsePosition = new(0, 0, 0);
        private int _comboPoints;
        private bool _isAutoAttacking;
        private bool _canRiposte;
        private bool _mainhandIsEnchanted;
        private bool _tastyCorpsesNearby;
        private DateTime _corpseRecoveryReadyAtUtc = DateTime.MinValue;

        // Set from SMSG_CORPSE_QUERY response
        public Position CorpsePosition
        {
            get => _corpsePosition;
            set => _corpsePosition = value;
        }

        // Descriptor-backed ghost detection is authoritative for parity with FG snapshots.
        // Keep buff check only as a final fallback for incomplete descriptor updates.
        public bool InGhostForm
        {
            get
            {
                const uint playerFlagGhost = 0x10; // PLAYER_FLAGS_GHOST
                const uint standStateMask = 0xFF;
                const uint standStateDead = 7; // UNIT_STAND_STATE_DEAD

                var hasGhostFlag = (((uint)PlayerFlags) & playerFlagGhost) != 0;
                if (hasGhostFlag)
                    return true;

                var standState = Bytes1[0] & standStateMask;
                if (Health == 0 || standState == standStateDead)
                    return false;

                return HasBuff("Ghost");
            }
        }

        // Debuff type checks: scan debuffs by name pattern until GetDebuffs() provides EffectType
        // These work if debuff Spell objects are populated with spell names from the spell DB.
        // Returns false if debuffs list is empty (safe default).
        public bool IsCursed => Debuffs.Any(d => d.Name.StartsWith("Curse", StringComparison.OrdinalIgnoreCase));

        public bool IsPoisoned => Debuffs.Any(d =>
            d.Name.Contains("Poison", StringComparison.OrdinalIgnoreCase) ||
            d.Name.Contains("Venom", StringComparison.OrdinalIgnoreCase));

        public bool IsDiseased => Debuffs.Any(d =>
            d.Name.Contains("Disease", StringComparison.OrdinalIgnoreCase) ||
            d.Name.Contains("Plague", StringComparison.OrdinalIgnoreCase));

        public bool HasMagicDebuff => Debuffs.Any(d =>
            d.Name.Contains("Polymorph", StringComparison.OrdinalIgnoreCase) ||
            d.Name.Contains("Slow", StringComparison.OrdinalIgnoreCase) ||
            d.Name.Contains("Frost Nova", StringComparison.OrdinalIgnoreCase));

        // Set from SMSG_UPDATE_COMBO_POINTS or player descriptor bytes
        public int ComboPoints
        {
            get => _comboPoints;
            set => _comboPoints = value;
        }

        // Warrior stances: check buffs list for stance names
        public string CurrentStance
        {
            get
            {
                if (HasBuff(BattleStance)) return BattleStance;
                if (HasBuff(DefensiveStance)) return DefensiveStance;
                if (HasBuff(BerserkerStance)) return BerserkerStance;
                return "None";
            }
        }

        // Set by external code that has access to the object manager's unit list
        public bool TastyCorpsesNearby
        {
            get => _tastyCorpsesNearby;
            set => _tastyCorpsesNearby = value;
        }

        // Set from spell state tracking (Riposte usability)
        public bool CanRiposte
        {
            get => _canRiposte;
            set => _canRiposte = value;
        }

        // Set from item enchant data or GetWeaponEnchantInfo equivalent
        public bool MainhandIsEnchanted
        {
            get => _mainhandIsEnchanted;
            set => _mainhandIsEnchanted = value;
        }

        // Money: directly available from WoWPlayer.Coinage descriptor field
        public uint Copper => Coinage;

        // Set from combat state tracking (SMSG_ATTACKSTART / SMSG_ATTACKSTOP)
        public bool IsAutoAttacking
        {
            get => _isAutoAttacking;
            set => _isAutoAttacking = value;
        }

        // Can resurrect while ghosted when corpse position is known and reclaim delay has elapsed.
        public bool CanResurrect =>
            InGhostForm &&
            _corpsePosition.X != 0 &&
            _corpsePosition.Y != 0 &&
            _corpsePosition.Z != 0 &&
            CorpseRecoveryDelaySeconds == 0;

        // Remaining delay before CMSG_RECLAIM_CORPSE is accepted by the server.
        public int CorpseRecoveryDelaySeconds
        {
            get
            {
                const uint playerFlagGhost = 0x10; // PLAYER_FLAGS_GHOST
                const uint standStateMask = 0xFF;
                const uint standStateDead = 7; // UNIT_STAND_STATE_DEAD

                var standState = Bytes1[0] & standStateMask;
                var deadOrGhost = Health == 0
                                  || (((uint)PlayerFlags & playerFlagGhost) != 0)
                                  || standState == standStateDead;
                if (!deadOrGhost)
                    return 0;

                if (_corpseRecoveryReadyAtUtc <= DateTime.UtcNow)
                    return 0;
                return (int)Math.Ceiling((_corpseRecoveryReadyAtUtc - DateTime.UtcNow).TotalSeconds);
            }
            set
            {
                if (value <= 0)
                {
                    _corpseRecoveryReadyAtUtc = DateTime.MinValue;
                    return;
                }

                _corpseRecoveryReadyAtUtc = DateTime.UtcNow.AddSeconds(value);
            }
        }

        // Check if current map is a battleground
        public bool InBattleground => BattlegroundMapIds.Contains(MapId);

        // Check quest log for any active quests with objectives
        public bool HasQuestTargets => QuestLog.Any(q => q.QuestId != 0);

        public override WoWLocalPlayer Clone()
        {
            var clone = new WoWLocalPlayer(HighGuid, ObjectType);
            clone.CopyFrom(this);
            return clone;
        }

        public override void CopyFrom(WoWObject sourceBase)
        {
            base.CopyFrom(sourceBase);

            if (sourceBase is not WoWLocalPlayer source) return;

            _corpsePosition = new Position(source._corpsePosition.X, source._corpsePosition.Y, source._corpsePosition.Z);
            _comboPoints = source._comboPoints;
            _isAutoAttacking = source._isAutoAttacking;
            _canRiposte = source._canRiposte;
            _mainhandIsEnchanted = source._mainhandIsEnchanted;
            _tastyCorpsesNearby = source._tastyCorpsesNearby;
            CorpseRecoveryDelaySeconds = source.CorpseRecoveryDelaySeconds;
        }
    }
}
