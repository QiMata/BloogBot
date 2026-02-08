using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;

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

        // Set from SMSG_CORPSE_QUERY response
        public Position CorpsePosition
        {
            get => _corpsePosition;
            set => _corpsePosition = value;
        }

        // Ghost form: check for Ghost buff (set when player is dead and released spirit)
        public bool InGhostForm => HasBuff("Ghost");

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

        // Can resurrect if dead (health == 0) and corpse position is known
        public bool CanResurrect =>
            Health == 0 &&
            _corpsePosition.X != 0 &&
            _corpsePosition.Y != 0 &&
            _corpsePosition.Z != 0;

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
        }
    }
}
