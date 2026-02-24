using ForegroundBotRunner.Mem;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using static GameData.Core.Constants.Spellbook;
using Functions = ForegroundBotRunner.Mem.Functions;

namespace ForegroundBotRunner.Objects
{
    public class LocalPlayer : WoWPlayer, IWoWLocalPlayer
    {
        internal LocalPlayer(nint pointer, HighGuid guid, WoWObjectType objectType)
            : base(pointer, guid, objectType) { }

        public readonly IDictionary<string, int[]> PlayerSpells = new Dictionary<string, int[]>();
        public readonly List<int> PlayerSkills = [];
        public new ulong TargetGuid => MemoryManager.ReadUlong(Offsets.Player.TargetGuid, true);

        public static bool TargetInMeleeRange =>
            Functions.LuaCallWithResult("{0} = CheckInteractDistance(\"target\", 3)")[0] == "1";

        public new Class Class => (Class)MemoryManager.ReadByte(MemoryAddresses.LocalPlayerClass);
        public new Race Race =>
            Enum.GetValues(typeof(Race))
                .Cast<Race>()
                .FirstOrDefault(v =>
                    v.GetDescription() == Functions.LuaCallWithResult("{0} = UnitRace('player')")[0]
                );

        public Position CorpsePosition =>
            new(
                MemoryManager.ReadFloat(MemoryAddresses.LocalPlayerCorpsePositionX),
                MemoryManager.ReadFloat(MemoryAddresses.LocalPlayerCorpsePositionY),
                MemoryManager.ReadFloat(MemoryAddresses.LocalPlayerCorpsePositionZ)
            );

        public string CurrentStance
        {
            get
            {
                if (Buffs.Any(b => b.Name == BattleStance))
                    return BattleStance;

                if (Buffs.Any(b => b.Name == DefensiveStance))
                    return DefensiveStance;

                if (Buffs.Any(b => b.Name == BerserkerStance))
                    return BerserkerStance;

                return "None";
            }
        }

        public bool InGhostForm
        {
            get
            {
                const uint playerFlagGhost = 0x10; // PLAYER_FLAGS_GHOST
                const uint standStateMask = 0xFF;
                const uint standStateDead = 7; // UNIT_STAND_STATE_DEAD

                var health = Health;
                var hasGhostFlag = (((uint)PlayerFlags) & playerFlagGhost) != 0;
                var standDead = Bytes1.Length > 0 && (Bytes1[0] & standStateMask) == standStateDead;

                // Descriptor-backed state is authoritative for snapshot parity.
                if (hasGhostFlag)
                    return true;

                // Corpse state should not be treated as ghost.
                if (health == 0 || standDead)
                    return false;

                // Memory ghost flag can flicker in transition states; only trust it when health is non-zero.
                if (MemoryManager.ReadInt(Offsets.Player.IsGhost) != 0 && health > 0)
                    return true;

                try
                {
                    var result = Functions.LuaCallWithResult("{0} = UnitIsGhost('player')");
                    if (result.Length > 0)
                        return result[0] == "1";
                }
                catch
                {
                    // Fall through to structural heuristic.
                }

                // Final fallback: ghosts typically have near-1 HP and a known corpse location.
                // Exclude health==0 so corpse state is not misclassified as ghost.
                var corpsePos = CorpsePosition;
                return health > 0 && health <= 1 && (corpsePos.X != 0 || corpsePos.Y != 0 || corpsePos.Z != 0);
            }
        }

        private ulong ComboPointGuid { get; set; }

        public int ComboPoints
        {
            get
            {
                var result = Functions.LuaCallWithResult("{0} = GetComboPoints('target')");

                if (result.Length > 0)
                    return Convert.ToByte(result[0]);
                else
                    return 0;
            }
        }

        public string CurrentShapeshiftForm
        {
            get
            {
                if (HasBuff(BearForm))
                    return BearForm;

                if (HasBuff(CatForm))
                    return CatForm;

                return "Human Form";
            }
        }

        public bool IsDiseased =>
            GetDebuffs(LuaTarget.Player).Any(t => t.Type == EffectType.Disease);

        public bool IsCursed => GetDebuffs(LuaTarget.Player).Any(t => t.Type == EffectType.Curse);

        public bool IsPoisoned =>
            GetDebuffs(LuaTarget.Player).Any(t => t.Type == EffectType.Poison);

        public bool HasMagicDebuff =>
            GetDebuffs(LuaTarget.Player).Any(t => t.Type == EffectType.Magic);

        public int GetSpellId(string spellName, int rank = -1)
        {
            int spellId;

            var maxRank = PlayerSpells[spellName].Length;
            if (rank < 1 || rank > maxRank)
                spellId = PlayerSpells[spellName][maxRank - 1];
            else
                spellId = PlayerSpells[spellName][rank - 1];

            return spellId;
        }

        public bool IsSpellReady(string spellName, int rank = -1)
        {
            if (!PlayerSpells.ContainsKey(spellName))
                return false;

            var spellId = GetSpellId(spellName, rank);

            return !Functions.IsSpellOnCooldown(spellId);
        }

        public int GetManaCost(string spellName, int rank = -1)
        {
            var parId = GetSpellId(spellName, rank);

            if (parId >= MemoryManager.ReadUint(0x00C0D780 + 0xC) || parId <= 0)
                return 0;

            var entryPtr = MemoryManager.ReadIntPtr(
                (nint)(uint)(MemoryManager.ReadUint(0x00C0D780 + 8) + parId * 4)
            );
            return MemoryManager.ReadInt(entryPtr + 0x0080);
        }

        public bool KnowsSpell(string name) => PlayerSpells.ContainsKey(name);

        public bool MainhandIsEnchanted =>
            Functions.LuaCallWithResult("{0} = GetWeaponEnchantInfo()")[0] == "1";

        public bool CanRiposte
        {
            get
            {
                if (PlayerSpells.ContainsKey("Riposte"))
                {
                    var results = Functions.LuaCallWithResult(
                        "{0}, {1} = IsUsableSpell('Riposte')"
                    );
                    if (results.Length > 0)
                        return results[0] == "1";
                    else
                        return false;
                }
                return false;
            }
        }

        public bool TastyCorpsesNearby => false;

        public uint Copper => 0;

        // FG bot auto-attack is started via StateManager action, not this property.
        // Safe to return false — BotRunnerService behavior tree only runs for BG bot.
        public bool IsAutoAttacking => false;

        public bool CanResurrect =>
            InGhostForm &&
            (CorpsePosition.X != 0 || CorpsePosition.Y != 0 || CorpsePosition.Z != 0) &&
            CorpseRecoveryDelaySeconds == 0;

        // Lua API returns milliseconds on some client builds and seconds on others.
        // Normalize to whole seconds for snapshot/decision logic.
        public int CorpseRecoveryDelaySeconds
        {
            get
            {
                try
                {
                    if (!InGhostForm && Health > 0)
                        return 0;

                    var result = Functions.LuaCallWithResult("{0} = GetCorpseRecoveryDelay()");
                    if (result.Length == 0 || string.IsNullOrWhiteSpace(result[0]))
                        return 0;

                    if (!float.TryParse(result[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var rawDelay))
                        return 0;

                    if (rawDelay <= 0)
                        return 0;

                    var seconds = rawDelay > 3600f ? rawDelay / 1000f : rawDelay;
                    return (int)Math.Ceiling(seconds);
                }
                catch
                {
                    return 0;
                }
            }
        }

        public bool InBattleground => false;
        public bool HasQuestTargets => false;
    }
}
