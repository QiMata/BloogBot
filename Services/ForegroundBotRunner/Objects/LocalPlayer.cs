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
        internal static readonly HashSet<uint> BattlegroundMapIds = [30u, 489u, 529u, 566u];

        internal static bool IsBattlegroundMapId(uint mapId) => BattlegroundMapIds.Contains(mapId);

        internal LocalPlayer(nint pointer, HighGuid guid, WoWObjectType objectType)
            : base(pointer, guid, objectType) { }

        public readonly IDictionary<string, int[]> PlayerSpells = new Dictionary<string, int[]>();
        /// <summary>
        /// Raw spell IDs from the spell book array, populated by RefreshSpells() via atomic replacement.
        /// Includes all spell IDs regardless of whether a name can be resolved via the spell DB.
        /// Talent spells (e.g. 16462 Deflection) may not have a DB entry in the client and would
        /// otherwise be silently dropped by the name-lookup path in PlayerSpells.
        /// Assignment is a single reference swap (thread-safe: readers always see a consistent snapshot).
        /// </summary>
        public IReadOnlyCollection<uint> RawSpellBookIds = Array.Empty<uint>();
        public readonly List<int> PlayerSkills = [];
        public new ulong TargetGuid => MemoryManager.ReadUlong(Offsets.Player.TargetGuid, true);

        public static bool TargetInMeleeRange =>
            Functions.LuaCallWithResult("{0} = CheckInteractDistance(\"target\", 3)")[0] == "1";

        public new Class Class => base.Class;
        public new Race Race => base.Race;
        public new Gender Gender => base.Gender;

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

        internal static bool EvaluateGhostFormState(
            uint health,
            uint playerFlags,
            uint[]? bytes1,
            int memoryGhostFlag,
            string? luaGhostValue,
            Position corpsePos)
        {
            const uint playerFlagGhost = 0x10; // PLAYER_FLAGS_GHOST
            const uint standStateMask = 0xFF;
            const uint standStateDead = 7; // UNIT_STAND_STATE_DEAD

            var hasGhostFlag = (playerFlags & playerFlagGhost) != 0;
            var standDead = bytes1 is { Length: > 0 } && (bytes1[0] & standStateMask) == standStateDead;

            // Descriptor-backed state is authoritative for snapshot parity.
            if (hasGhostFlag)
                return true;

            // Corpse state should not be treated as ghost.
            if (health == 0 || standDead)
                return false;

            // Memory ghost flag can flicker in transition states; only trust it when health is non-zero.
            if (memoryGhostFlag != 0 && health > 0)
                return true;

            if (!string.IsNullOrWhiteSpace(luaGhostValue))
                return luaGhostValue == "1";

            // Final fallback: ghosts typically have near-1 HP and a known corpse location.
            // Exclude health==0 so corpse state is not misclassified as ghost.
            return health > 0 && health <= 1 && (corpsePos.X != 0 || corpsePos.Y != 0 || corpsePos.Z != 0);
        }

        public bool InGhostForm
        {
            get
            {
                try
                {
                    var health = Health;
                    string? luaGhostValue = null;

                    try
                    {
                        var result = Functions.LuaCallWithResult("{0} = UnitIsGhost('player')");
                        if (result.Length > 0)
                            luaGhostValue = result[0];
                    }
                    catch
                    {
                        // Fall through to structural heuristic.
                    }

                    return EvaluateGhostFormState(
                        health,
                        (uint)PlayerFlags,
                        Bytes1,
                        MemoryManager.ReadInt(Offsets.Player.IsGhost),
                        luaGhostValue,
                        CorpsePosition);
                }
                catch
                {
                    // During world transfers the local-player object can be temporarily unstable.
                    return false;
                }
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

        public uint Copper
        {
            get
            {
                try
                {
                    return ResolveCopper(Coinage, Functions.LuaCallWithResult);
                }
                catch
                {
                    return Coinage;
                }
            }
        }

        internal static uint ResolveCopper(uint descriptorCoinage, Func<string, string[]> luaCallWithResult)
        {
            var result = luaCallWithResult("{0} = GetMoney() or ''");
            if (result.Length > 0
                && long.TryParse(result[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var luaCopper)
                && luaCopper >= 0)
            {
                return luaCopper > uint.MaxValue ? uint.MaxValue : (uint)luaCopper;
            }

            return descriptorCoinage;
        }

        // Track auto-attack state to avoid spamming CastSpellByName('Attack') every tick.
        // Set true when StartMeleeAttack() fires the Lua, cleared on StopAllMovement/target death.
        // The behavior tree checks !IsAutoAttacking before calling StartMeleeAttack().
        internal bool _isAutoAttacking;
        public bool IsAutoAttacking => _isAutoAttacking;

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

        public bool InBattleground => IsBattlegroundMapId(MapId);

        public bool HasQuestTargets => QuestLog.Any(slot => slot.QuestId != 0);
    }
}
