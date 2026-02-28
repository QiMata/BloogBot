using ForegroundBotRunner.Mem;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Functions = ForegroundBotRunner.Mem.Functions;

namespace ForegroundBotRunner.Objects
{
    public class WoWUnit(
        nint pointer,
        HighGuid guid,
        WoWObjectType objectType) : WoWObject(pointer, guid, objectType), IWoWUnit
    {
        private static readonly string[] ImmobilizedSpellText = ["Immobilized"];

        public int CreatureId => int.Parse(Guid.ToString("X").Substring(10, 6), System.Globalization.NumberStyles.HexNumber);

        public ulong TargetGuid => MemoryManager.ReadUlong(GetDescriptorPtr() + MemoryAddresses.WoWUnit_TargetGuidOffset);

        public uint Health => MemoryManager.ReadUint(GetDescriptorPtr() + MemoryAddresses.WoWUnit_HealthOffset);

        public uint MaxHealth => MemoryManager.ReadUint(GetDescriptorPtr() + MemoryAddresses.WoWUnit_MaxHealthOffset);

        public uint Mana => MemoryManager.ReadUint(GetDescriptorPtr() + MemoryAddresses.WoWUnit_ManaOffset);

        public uint MaxMana => MemoryManager.ReadUint(GetDescriptorPtr() + MemoryAddresses.WoWUnit_MaxManaOffset);

        public uint Rage => MemoryManager.ReadUint(GetDescriptorPtr() + MemoryAddresses.WoWUnit_RageOffset) / 10;

        public uint Energy => MemoryManager.ReadUint(GetDescriptorPtr() + MemoryAddresses.WoWUnit_EnergyOffset);

        public float BoundingRadius => MemoryManager.ReadFloat(nint.Add(GetDescriptorPtr(), MemoryAddresses.WoWUnit_BoundingRadiusOffset));

        public float CombatReach => MemoryManager.ReadFloat(nint.Add(GetDescriptorPtr(), MemoryAddresses.WoWUnit_CombatReachOffset));

        public uint ChannelingId => MemoryManager.ReadUint(GetDescriptorPtr() + MemoryAddresses.WoWUnit_CurrentChannelingOffset);

        public bool IsChanneling => ChannelingId > 0;

        public uint SpellcastId => MemoryManager.ReadUint(Pointer + MemoryAddresses.WoWUnit_CurrentSpellcastOffset);

        public bool IsCasting => SpellcastId > 0;

        public uint Level => MemoryManager.ReadUint(GetDescriptorPtr() + MemoryAddresses.WoWUnit_LevelOffset);

        public DynamicFlags DynamicFlags => (DynamicFlags)MemoryManager.ReadInt(GetDescriptorPtr() + MemoryAddresses.WoWUnit_DynamicFlagsOffset);

        public bool CanBeLooted => Health == 0 && DynamicFlags.HasFlag(DynamicFlags.CanBeLooted);

        public bool TappedByOther => DynamicFlags.HasFlag(DynamicFlags.Tapped) && !DynamicFlags.HasFlag(DynamicFlags.TappedByMe);

        public UnitFlags UnitFlags => (UnitFlags)MemoryManager.ReadInt(GetDescriptorPtr() + MemoryAddresses.WoWUnit_UnitFlagsOffset);

        public bool IsInCombat => UnitFlags.HasFlag(UnitFlags.UNIT_FLAG_IN_COMBAT);

        public bool IsStunned => UnitFlags.HasFlag(UnitFlags.UNIT_FLAG_STUNNED);

        public bool IsConfused => UnitFlags.HasFlag(UnitFlags.UNIT_FLAG_CONFUSED);

        public bool IsFleeing => UnitFlags.HasFlag(UnitFlags.UNIT_FLAG_FLEEING);

        public ulong SummonedByGuid => MemoryManager.ReadUlong(GetDescriptorPtr() + MemoryAddresses.WoWUnit_SummonedByGuidOffset);

        public int FactionId => MemoryManager.ReadInt(GetDescriptorPtr() + MemoryAddresses.WoWUnit_FactionIdOffset);

        public bool NotAttackable => UnitFlags.HasFlag(UnitFlags.UNIT_FLAG_NON_ATTACKABLE);

        // in radians
        public float GetFacingForPosition(Position position)
        {
            var f = (float)Math.Atan2(position.Y - Position.Y, position.X - Position.X);
            if (f < 0.0f)
                f += (float)Math.PI * 2.0f;
            else
            {
                if (f > (float)Math.PI * 2)
                    f -= (float)Math.PI * 2.0f;
            }
            return f;
        }

        public bool IsBehind(WoWUnit target)
        {
            if (target == null) return false;

            float facing = GetFacingForPosition(target.Position);

            var halfPi = Math.PI / 2;
            var twoPi = Math.PI * 2;
            var leftThreshold = target.Facing - halfPi;
            var rightThreshold = target.Facing + halfPi;

            bool condition;
            if (leftThreshold < 0)
                condition = facing < rightThreshold || facing > twoPi + leftThreshold;
            else if (rightThreshold > twoPi)
                condition = facing > leftThreshold || facing < rightThreshold - twoPi;
            else
                condition = facing > leftThreshold && facing < rightThreshold;

            return condition;
        }

        public bool IsBehind(Position position, float targetFacing)
        {
            if (position == null) return false;

            float facing = GetFacingForPosition(position);

            var halfPi = Math.PI / 2;
            var twoPi = Math.PI * 2;
            var leftThreshold = targetFacing - halfPi;
            var rightThreshold = targetFacing + halfPi;

            bool condition;
            if (leftThreshold < 0)
                condition = facing < rightThreshold || facing > twoPi + leftThreshold;
            else if (rightThreshold > twoPi)
                condition = facing > leftThreshold || facing < rightThreshold - twoPi;
            else
                condition = facing > leftThreshold && facing < rightThreshold;

            return condition;
        }

        public MovementFlags MovementFlags => (MovementFlags)MemoryManager.ReadInt(nint.Add(Pointer, MemoryAddresses.WoWUnit_MovementFlagsOffset));

        public bool IsMoving => MovementFlags.HasFlag(MovementFlags.MOVEFLAG_FORWARD);

        public bool IsSwimming => MovementFlags.HasFlag(MovementFlags.MOVEFLAG_SWIMMING);

        public bool IsFalling => MovementFlags.HasFlag(MovementFlags.MOVEFLAG_JUMPING);
        public uint MountDisplayId => MemoryManager.ReadUint(GetDescriptorPtr() + MemoryAddresses.WoWUnit_MountDisplayIdOffset);

        public bool IsMounted => MountDisplayId > 0;

        public bool IsPet => SummonedByGuid > 0;

        public CreatureType CreatureType => Functions.GetCreatureType(Pointer);

        public UnitReaction UnitReaction => Functions.GetUnitReaction(Pointer, Pointer);

        public virtual CreatureRank CreatureRank => (CreatureRank)Functions.GetCreatureRank(Pointer);

        public static ISpell GetSpellById(int spellId)
        {
            var spellsBasePtr = MemoryManager.ReadIntPtr(0x00C0D788);
            var spellPtr = MemoryManager.ReadIntPtr(spellsBasePtr + spellId * 4);

            var spellCost = MemoryManager.ReadInt(spellPtr + 0x0080);

            var spellNamePtr = MemoryManager.ReadIntPtr(spellPtr + 0x1E0);
            var spellName = MemoryManager.ReadString(spellNamePtr);

            var spellDescriptionPtr = MemoryManager.ReadIntPtr(spellPtr + 0x228);
            var spellDescription = MemoryManager.ReadString(spellDescriptionPtr);

            var spellTooltipPtr = MemoryManager.ReadIntPtr(spellPtr + 0x24C);
            var spellTooltip = MemoryManager.ReadString(spellTooltipPtr);

            return new Spell((uint)spellId, (uint)spellCost, spellName, spellDescription, spellTooltip);
        }

        public IEnumerable<ISpell> Buffs
        {
            get
            {
                var buffs = new List<ISpell>();
                var currentBuffOffset = MemoryAddresses.WoWUnit_BuffsBaseOffset;
                for (var i = 0; i < 32; i++)
                {
                    var buffId = MemoryManager.ReadInt(GetDescriptorPtr() + currentBuffOffset);
                    if (buffId != 0)
                        buffs.Add(GetSpellById(buffId));
                    currentBuffOffset += 4;
                }
                return buffs;
            }
        }

        public IEnumerable<ISpell> Debuffs
        {
            get
            {
                var debuffs = new List<ISpell>();
                var currentDebuffOffset = MemoryAddresses.WoWUnit_DebuffsBaseOffset;
                for (var i = 0; i < 16; i++)
                {
                    var debuffId = MemoryManager.ReadInt(GetDescriptorPtr() + currentDebuffOffset);
                    if (debuffId != 0)
                        debuffs.Add(GetSpellById(debuffId));
                    currentDebuffOffset += 4;
                }
                return debuffs;
            }
        }

        public static IEnumerable<ISpellEffect> GetDebuffs(LuaTarget target)
        {
            var debuffs = new List<SpellEffect>();

            for (var i = 1; i <= 16; i++)
            {
                var result = Functions.LuaCallWithResult("{0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10} = UnitDebuff('" + target.ToString().ToLower() + "', " + i + ")");
                var icon = result[0];
                var stackCount = result[1];
                var debuffTypeString = result[2];

                if (string.IsNullOrEmpty(icon))
                    break;

                var success = Enum.TryParse(debuffTypeString, out EffectType type);
                if (!success)
                    type = EffectType.None;

                debuffs.Add(new SpellEffect(icon, Convert.ToUInt32(stackCount), type));
            }

            return debuffs;
        }

        public bool HasBuff(string name) => Buffs.Any(a => a.Name == name);

        public bool HasDebuff(string name) => Debuffs.Any(a => a.Name == name);

        public IEnumerable<ISpellEffect> GetDebuffs()
        {
            return GetDebuffs(LuaTarget.Target);
        }

        public Position GetPointBehindUnit(float distance)
        {
            float behindAngle = Facing + (float)Math.PI;
            float x = Position.X + (float)Math.Cos(behindAngle) * distance;
            float y = Position.Y + (float)Math.Sin(behindAngle) * distance;
            return new Position(x, y, Position.Z);
        }

        public bool DismissBuff(string buffName)
        {
            return false;
        }

        public IEnumerable<ISpellEffect> GetBuffs()
        {
            return Buffs.Select(b => (ISpellEffect)new SpellEffect("", 1, EffectType.None));
        }

        public bool IsImmobilized
        {
            get
            {
                return Debuffs.Any(d => ImmobilizedSpellText.Any(s => d.Description.Contains(s) || d.Tooltip.Contains(s)));
            }
        }

        public IWoWUnit Target => null;

        public Dictionary<Powers, uint> Powers => new Dictionary<Powers, uint>();

        public Dictionary<Powers, uint> MaxPowers => new Dictionary<Powers, uint>();

        public uint DisplayId => 0;

        public GOState GoState => default;

        public uint ArtKit => 0;

        public uint AnimProgress => 0;

        public uint FactionTemplate => 0;

        public uint TypeId => 0; // Units are not GameObjects; TypeId only meaningful for WoWGameObject

        public NPCFlags NPCFlags => (NPCFlags)MemoryManager.ReadInt(GetDescriptorPtr() + MemoryAddresses.WoWUnit_NPCFlagsOffset);

        public uint[] Bytes0 => ReadPackedByteField(UpdateFields.EUnitFields.UNIT_FIELD_BYTES_0);

        public uint[] VirtualItemInfo => Array.Empty<uint>();

        public uint[] VirtualItemSlotDisplay => Array.Empty<uint>();

        public uint[] AuraFields
        {
            get
            {
                const int count = 48; // UNIT_FIELD_AURA: 48 spell ID slots
                var result = new uint[count];
                var descriptorPtr = GetDescriptorPtr();
                int baseOffset = (int)UpdateFields.EUnitFields.UNIT_FIELD_AURA * 4;
                for (int i = 0; i < count; i++)
                    result[i] = MemoryManager.ReadUint(descriptorPtr + baseOffset + i * 4);
                return result;
            }
        }

        public uint[] AuraFlags
        {
            get
            {
                const int count = 6; // UNIT_FIELD_AURAFLAGS: 6 uint32 values
                var result = new uint[count];
                var descriptorPtr = GetDescriptorPtr();
                int baseOffset = (int)UpdateFields.EUnitFields.UNIT_FIELD_AURAFLAGS * 4;
                for (int i = 0; i < count; i++)
                    result[i] = MemoryManager.ReadUint(descriptorPtr + baseOffset + i * 4);
                return result;
            }
        }

        public uint[] AuraLevels
        {
            get
            {
                const int count = 12; // UNIT_FIELD_AURALEVELS: 12 uint32 values
                var result = new uint[count];
                var descriptorPtr = GetDescriptorPtr();
                int baseOffset = (int)UpdateFields.EUnitFields.UNIT_FIELD_AURALEVELS * 4;
                for (int i = 0; i < count; i++)
                    result[i] = MemoryManager.ReadUint(descriptorPtr + baseOffset + i * 4);
                return result;
            }
        }

        public uint[] AuraApplications
        {
            get
            {
                const int count = 12; // UNIT_FIELD_AURAAPPLICATIONS: 12 uint32 values
                var result = new uint[count];
                var descriptorPtr = GetDescriptorPtr();
                int baseOffset = (int)UpdateFields.EUnitFields.UNIT_FIELD_AURAAPPLICATIONS * 4;
                for (int i = 0; i < count; i++)
                    result[i] = MemoryManager.ReadUint(descriptorPtr + baseOffset + i * 4);
                return result;
            }
        }

        public uint AuraState
        {
            get
            {
                var descriptorPtr = GetDescriptorPtr();
                int offset = (int)UpdateFields.EUnitFields.UNIT_FIELD_AURASTATE * 4;
                return MemoryManager.ReadUint(descriptorPtr + offset);
            }
        }

        public float BaseAttackTime => 0f;

        public float OffhandAttackTime => 0f;

        public uint NativeDisplayId => 0;

        public uint MinDamage => 0;

        public uint MaxDamage => 0;

        public uint MinOffhandDamage => 0;

        public uint MaxOffhandDamage => 0;

        public uint[] Bytes1 => ReadPackedByteField(UpdateFields.EUnitFields.UNIT_FIELD_BYTES_1);

        public uint PetNumber => 0;

        public uint PetNameTimestamp => 0;

        public uint PetExperience => 0;

        public uint PetNextLevelExperience => 0;

        public float ModCastSpeed => 0f;

        public uint CreatedBySpell => 0;

        public NPCFlags NpcFlags => (NPCFlags)MemoryManager.ReadInt(GetDescriptorPtr() + MemoryAddresses.WoWUnit_NPCFlagsOffset);

        public uint NpcEmoteState => 0;

        public uint TrainingPoints => 0;

        public uint Strength => 0;

        public uint Agility => 0;

        public uint Stamina => 0;

        public uint Intellect => 0;

        public uint Spirit => 0;

        public uint[] Resistances => Array.Empty<uint>();

        public uint BaseMana => 0;

        public uint BaseHealth => 0;

        public uint[] Bytes2 => ReadPackedByteField(UpdateFields.EUnitFields.UNIT_FIELD_BYTES_2);

        public uint AttackPower => 0;

        public uint AttackPowerMods => 0;

        public uint AttackPowerMultipler => 0;

        public uint RangedAttackPower => 0;

        public uint RangedAttackPowerMods => 0;

        public uint RangedAttackPowerMultipler => 0;

        public uint MinRangedDamage => 0;

        public uint MaxRangedDamage => 0;

        public uint[] PowerCostModifers => Array.Empty<uint>();

        public uint[] PowerCostMultipliers => Array.Empty<uint>();

        private uint[] ReadPackedByteField(UpdateFields.EUnitFields field)
        {
            var descriptorPtr = GetDescriptorPtr();
            var packed = MemoryManager.ReadUint(descriptorPtr + (int)field * 4);
            return
            [
                packed & 0xFF,
                (packed >> 8) & 0xFF,
                (packed >> 16) & 0xFF,
                (packed >> 24) & 0xFF
            ];
        }

        /// <summary>
        /// Tick count when the fall began. Use with current tick to calculate fall duration.
        /// </summary>
        public uint FallStartTime => MemoryManager.ReadUint(nint.Add(Pointer, MemoryAddresses.WoWUnit_FallStartTimeOffset));

        /// <summary>
        /// Duration of current fall in milliseconds. Returns 0 if not falling.
        /// Uses Environment.TickCount (GetTickCount clock) which matches the clock
        /// the WoW client uses for FallStartTime at base+0x78.
        /// Previous bug: used LastHardwareAction (last mouse/keyboard input time) which
        /// only updates on HW events, producing garbage durations.
        /// </summary>
        public uint FallTime
        {
            get
            {
                if (!IsFalling) return 0;
                uint currentTick = (uint)Environment.TickCount;
                uint fallStart = FallStartTime;
                return fallStart > 0 && currentTick > fallStart ? currentTick - fallStart : 0;
            }
        }

        /// <summary>
        /// Z position at start of fall. Used to calculate fall damage.
        /// </summary>
        public float FallStartHeight => MemoryManager.ReadFloat(nint.Add(Pointer, MemoryAddresses.WoWUnit_FallStartHeightOffset));

        /// <summary>
        /// Active movement speed in yards/second.
        /// </summary>
        public float CurrentSpeed => MemoryManager.ReadFloat(nint.Add(Pointer, MemoryAddresses.WoWUnit_CurrentSpeedOffset));

        /// <summary>
        /// Walk speed in yards/second (default 2.5).
        /// </summary>
        public float WalkSpeed => MemoryManager.ReadFloat(nint.Add(Pointer, MemoryAddresses.WoWUnit_WalkSpeedOffset));

        /// <summary>
        /// Forward run speed in yards/second (default 7.0).
        /// </summary>
        public float RunSpeed => MemoryManager.ReadFloat(nint.Add(Pointer, MemoryAddresses.WoWUnit_RunSpeedOffset));

        /// <summary>
        /// Backward run speed in yards/second (default 4.5).
        /// </summary>
        public float RunBackSpeed => MemoryManager.ReadFloat(nint.Add(Pointer, MemoryAddresses.WoWUnit_RunBackSpeedOffset));

        /// <summary>
        /// Forward swim speed in yards/second (default 4.722).
        /// </summary>
        public float SwimSpeed => MemoryManager.ReadFloat(nint.Add(Pointer, MemoryAddresses.WoWUnit_SwimSpeedOffset));

        /// <summary>
        /// Backward swim speed in yards/second (default 2.5).
        /// </summary>
        public float SwimBackSpeed => MemoryManager.ReadFloat(nint.Add(Pointer, MemoryAddresses.WoWUnit_SwimBackSpeedOffset));

        /// <summary>
        /// Turn rate in radians/second (default π).
        /// </summary>
        public float TurnRate => MemoryManager.ReadFloat(nint.Add(Pointer, MemoryAddresses.WoWUnit_TurnRateOffset));

        public HighGuid Charm => new HighGuid(new byte[4], new byte[4]);

        public HighGuid Summon => new HighGuid(new byte[4], new byte[4]);

        public HighGuid CharmedBy => new HighGuid(new byte[4], new byte[4]);

        public HighGuid SummonedBy => new HighGuid(new byte[4], new byte[4]);

        public HighGuid Persuaded => new HighGuid(new byte[4], new byte[4]);

        public HighGuid ChannelObject => new HighGuid(new byte[4], new byte[4]);

        public HighGuid CreatedBy => new HighGuid(new byte[4], new byte[4]);

        public uint Flags => 0;

        public float[] Rotation => Array.Empty<float>();

        /// <summary>
        /// Transport GUID — CONFIRMED with zeppelin recording. Non-zero when on transport.
        /// When set, the main Position fields (0x9B8-0x9C0) auto-switch to transport-local coordinates.
        /// </summary>
        public ulong TransportGuid => MemoryManager.ReadUlong(nint.Add(Pointer, MemoryAddresses.WoWUnit_TransportGuidOffset));

        /// <summary>
        /// WARNING: These offsets (base+0x28..0x30) do NOT contain transport-local position in vanilla 1.12.1.
        /// They read sin/cos garbage. When TransportGuid != 0, use the main Position property instead.
        /// </summary>
        public Position TransportOffset => new(
            MemoryManager.ReadFloat(nint.Add(Pointer, MemoryAddresses.WoWUnit_Unknown0x9D0)),
            MemoryManager.ReadFloat(nint.Add(Pointer, MemoryAddresses.WoWUnit_Unknown0x9D4)),
            MemoryManager.ReadFloat(nint.Add(Pointer, MemoryAddresses.WoWUnit_Unknown0x9D8))
        );

        /// <summary>
        /// WARNING: Offset base+0x34 does NOT contain transport orientation in vanilla 1.12.1.
        /// </summary>
        public float TransportOrientation => MemoryManager.ReadFloat(nint.Add(Pointer, MemoryAddresses.WoWUnit_Unknown0x9DC));

        public float SwimPitch => MemoryManager.ReadFloat(nint.Add(Pointer, MemoryAddresses.WoWUnit_SwimPitchOffset));

        public float JumpVerticalSpeed => MemoryManager.ReadFloat(nint.Add(Pointer, MemoryAddresses.WoWUnit_JumpVelocityOffset));

        public float JumpSinAngle => MemoryManager.ReadFloat(nint.Add(Pointer, MemoryAddresses.WoWUnit_JumpSinAngleOffset));

        public float JumpCosAngle => MemoryManager.ReadFloat(nint.Add(Pointer, MemoryAddresses.WoWUnit_JumpCosAngleOffset));

        public float JumpHorizontalSpeed => 0f; // Vanilla 1.12.1 does not store horizontal jump speed in CMovementInfo — computed from facing + speed at jump start

        /// <summary>
        /// Current vertical fall velocity from static address 0x0087D894.
        /// Available even when per-unit JumpVelocity offset reads stale data.
        /// </summary>
        public float CurrentFallingSpeed => MemoryManager.ReadFloat(MemoryAddresses.FallingSpeedPtr);

        public float SplineElevation => 0f; // Server-side spline data, not in CMovementInfo

        public uint MovementFlags2 => 0;

        public IWoWGameObject Transport { get; set; }

        // ── MoveSpline data (read from unit memory via pointer at 0xD8) ──
        // UNVERIFIED offset: the MoveSpline pointer location needs empirical testing.
        // All reads are wrapped in try/catch to handle wrong offsets gracefully.

        /// <summary>
        /// Reads the MoveSpline pointer from the unit. Returns nint.Zero if not available.
        /// Does NOT gate on MOVEFLAG_SPLINE_ENABLED since that flag may not be set
        /// in client memory even when NPCs are on active server splines.
        /// </summary>
        private nint GetMoveSplinePtr()
        {
            try
            {
                var ptr = MemoryManager.ReadIntPtr(nint.Add(Pointer, MemoryAddresses.WoWUnit_MoveSplinePtrOffset));
                // Basic validity: pointer should be non-zero and look like a heap address
                if (ptr == nint.Zero || (long)ptr < 0x10000)
                    return nint.Zero;
                return ptr;
            }
            catch { return nint.Zero; }
        }

        /// <summary>
        /// Whether this unit has a non-zero MoveSpline pointer OR MOVEFLAG_SPLINE_ENABLED set.
        /// </summary>
        public bool HasMoveSpline
        {
            get
            {
                try
                {
                    if (MovementFlags.HasFlag(GameData.Core.Enums.MovementFlags.MOVEFLAG_SPLINE_ENABLED))
                        return true;
                    return GetMoveSplinePtr() != nint.Zero;
                }
                catch { return false; }
            }
        }

        /// <summary>
        /// Raw MoveSpline pointer value for diagnostic purposes.
        /// </summary>
        public nint RawMoveSplinePtr
        {
            get
            {
                try { return MemoryManager.ReadIntPtr(nint.Add(Pointer, MemoryAddresses.WoWUnit_MoveSplinePtrOffset)); }
                catch { return nint.Zero; }
            }
        }

        public new GameData.Core.Enums.SplineFlags SplineFlags
        {
            get
            {
                try
                {
                    var ptr = GetMoveSplinePtr();
                    if (ptr == nint.Zero) return GameData.Core.Enums.SplineFlags.None;
                    return (GameData.Core.Enums.SplineFlags)MemoryManager.ReadUint(nint.Add(ptr, MemoryAddresses.MoveSpline_FlagsOffset));
                }
                catch { return GameData.Core.Enums.SplineFlags.None; }
            }
            set { }
        }

        public new Position SplineFinalPoint
        {
            get
            {
                try
                {
                    var ptr = GetMoveSplinePtr();
                    if (ptr == nint.Zero) return new Position(0, 0, 0);
                    // Destination Vector3 at +0x58 (confirmed: endpoint of flight path)
                    var baseAddr = nint.Add(ptr, MemoryAddresses.MoveSpline_DestinationOffset);
                    return new Position(
                        MemoryManager.ReadFloat(baseAddr),
                        MemoryManager.ReadFloat(nint.Add(baseAddr, 4)),
                        MemoryManager.ReadFloat(nint.Add(baseAddr, 8)));
                }
                catch { return new Position(0, 0, 0); }
            }
            set { }
        }

        public new int SplineTimePassed
        {
            get
            {
                try
                {
                    var ptr = GetMoveSplinePtr();
                    if (ptr == nint.Zero) return 0;
                    // +0x20: ms elapsed since spline start (confirmed: 16→4938 over ~5s of flight)
                    return MemoryManager.ReadInt(nint.Add(ptr, MemoryAddresses.MoveSpline_TimePassedOffset));
                }
                catch { return 0; }
            }
            set { }
        }

        /// <summary>
        /// Total spline duration in milliseconds.
        /// Confirmed at MoveSpline+0x24: flight ends when time_passed ≈ this value.
        /// (134139ms for XR→OG, 699388ms for OG→XR)
        /// </summary>
        public new int SplineDuration
        {
            get
            {
                try
                {
                    var ptr = GetMoveSplinePtr();
                    if (ptr == nint.Zero) return 0;
                    return MemoryManager.ReadInt(nint.Add(ptr, MemoryAddresses.MoveSpline_DurationOffset));
                }
                catch { return 0; }
            }
            set { }
        }

        public new uint SplineId
        {
            get
            {
                try
                {
                    var ptr = GetMoveSplinePtr();
                    if (ptr == nint.Zero) return 0;
                    // +0x28 is constant during a flight — serves as a unique spline identifier
                    return MemoryManager.ReadUint(nint.Add(ptr, MemoryAddresses.MoveSpline_Unknown28Offset));
                }
                catch { return 0; }
            }
            set { }
        }

        /// <summary>
        /// Reads spline path nodes from the node array pointer at MoveSpline+0x3C.
        /// Node count is at MoveSpline+0x00. Confirmed with Orgrimmar↔Crossroads flight (68 nodes).
        /// </summary>
        public new List<Position> SplineNodes
        {
            get
            {
                try
                {
                    var ptr = GetMoveSplinePtr();
                    if (ptr == nint.Zero) return [];
                    var count = MemoryManager.ReadInt(nint.Add(ptr, MemoryAddresses.MoveSpline_NodeCountOffset));
                    if (count <= 0 || count > 200) return [];
                    var dataPtr = MemoryManager.ReadIntPtr(nint.Add(ptr, MemoryAddresses.MoveSpline_PointsDataPtrOffset));
                    if (dataPtr == nint.Zero || (long)dataPtr < 0x10000) return [];
                    var nodes = new List<Position>(count);
                    for (int i = 0; i < count; i++)
                    {
                        var nodeAddr = nint.Add(dataPtr, i * 12);
                        nodes.Add(new Position(
                            MemoryManager.ReadFloat(nodeAddr),
                            MemoryManager.ReadFloat(nint.Add(nodeAddr, 4)),
                            MemoryManager.ReadFloat(nint.Add(nodeAddr, 8))));
                    }
                    return nodes;
                }
                catch { return []; }
            }
            set { }
        }

        public new Position SplineFinalDestination
        {
            get
            {
                try
                {
                    var nodes = SplineNodes;
                    if (nodes.Count == 0) return new Position(0, 0, 0);
                    // In MaNGOS, FinalDestination = spline.getPoint(spline.last())
                    // spline.last() = index_hi, but the points include padding entries
                    // The last usable point is at index = pointCount - 1
                    return nodes[^1];
                }
                catch { return new Position(0, 0, 0); }
            }
            set { }
        }

        public new ulong SplineTargetGuid { get => 0; set { } } // Not stored in MoveSpline struct
        public new float SplineFinalOrientation { get => 0f; set { } } // Not stored in MoveSpline struct
    }
}
