using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WoWSharpClient.Models
{
    public class WoWUnit(HighGuid highGuid, WoWObjectType objectType = WoWObjectType.Unit) : WoWGameObject(highGuid, objectType), IWoWUnit
    {
        public HighGuid TargetHighGuid { get; private set; } = new(new byte[4], new byte[4]);
        public HighGuid Charm { get; private set; } = new(new byte[4], new byte[4]);
        public HighGuid Summon { get; private set; } = new(new byte[4], new byte[4]);
        public HighGuid CharmedBy { get; private set; } = new(new byte[4], new byte[4]);
        public HighGuid SummonedBy { get; private set; } = new(new byte[4], new byte[4]);
        public HighGuid Persuaded { get; private set; } = new(new byte[4], new byte[4]);
        public HighGuid ChannelObject { get; private set; } = new(new byte[4], new byte[4]);

        public ulong TargetGuid { get; set; }
        public uint Health { get; set; }
        public uint MaxHealth { get; set; }

        public Dictionary<Powers, uint> Powers { get; set; } = [];
        public Dictionary<Powers, uint> MaxPowers { get; set; } = [];

        public uint MountDisplayId { get; set; }
        public ulong SummonedByGuid { get; set; }
        public UnitReaction UnitReaction { get; set; }
        public UnitFlags UnitFlags { get; set; }
        public NPCFlags NpcFlags { get; set; }
        public uint NpcEmoteState { get; set; }
        public MovementFlags MovementFlags { get; set; }
        public uint MovementFlags2 { get; set; }
        public CreatureType CreatureType { get; set; }

        public uint FallTime { get; set; }
        public float WalkSpeed { get; set; }
        public float RunSpeed { get; set; }
        public float RunBackSpeed { get; set; }
        public float SwimSpeed { get; set; }
        public float SwimBackSpeed { get; set; }
        public float TurnRate { get; set; }

        public ulong TransportGuid { get; set; }
        public IWoWGameObject Transport { get; set; } = new WoWGameObject(new HighGuid(new byte[4], new byte[4]));
        public Position TransportOffset { get; set; } = new(0, 0, 0);
        public float TransportOrientation { get; set; }
        public float SwimPitch { get; set; }

        public float JumpVerticalSpeed { get; set; }
        public float JumpSinAngle { get; set; }
        public float JumpCosAngle { get; set; }
        public float JumpHorizontalSpeed { get; set; }

        public float SplineElevation { get; set; }
        public uint TransportLastUpdated { get; set; }

        // Remote unit extrapolation state — WoW.exe 0x616DE0.
        // Records the last known position + movement state for predicting
        // where this unit is between server heartbeat updates.
        internal uint ExtrapolationTimeMs { get; set; }
        internal MovementFlags ExtrapolationFlags { get; set; }
        internal float ExtrapolationFacing { get; set; }
        internal Position? ExtrapolationBasePosition { get; set; }
        private const float ExtrapolationDirectionEpsilon = 0.0001f;
        // WoW.exe movement constant VA 0x0081DA54: sin(45°) = 0.70710677.
        private const float DiagonalDamping = 0.70710677f;

        /// <summary>
        /// Predicts this unit's current position by extrapolating from the last known
        /// movement state. Matches WoW.exe remote unit prediction (0x616DE0).
        /// Speed thresholds: >60y/s = teleport (return raw), <3y/s = jitter (return raw).
        /// </summary>
        public Position GetExtrapolatedPosition(uint currentTimeMs)
        {
            if (ExtrapolationBasePosition == null || ExtrapolationTimeMs == 0)
                return Position;

            uint elapsed = currentTimeMs - ExtrapolationTimeMs;
            if (elapsed > 1500) return Position; // Stale — don't extrapolate

            // Only extrapolate if unit has directional movement flags.
            var dirFlags = ExtrapolationFlags & (MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_BACKWARD
                | MovementFlags.MOVEFLAG_STRAFE_LEFT | MovementFlags.MOVEFLAG_STRAFE_RIGHT);
            if (dirFlags == MovementFlags.MOVEFLAG_NONE) return Position;

            bool moveForward = (dirFlags & MovementFlags.MOVEFLAG_FORWARD) != 0;
            bool moveBackward = (dirFlags & MovementFlags.MOVEFLAG_BACKWARD) != 0;
            bool strafeLeft = (dirFlags & MovementFlags.MOVEFLAG_STRAFE_LEFT) != 0;
            bool strafeRight = (dirFlags & MovementFlags.MOVEFLAG_STRAFE_RIGHT) != 0;

            float speed = moveBackward && !moveForward ? RunBackSpeed : RunSpeed;
            if (speed * speed < 9f) return Position;   // Jitter filter: <3 y/s
            if (speed * speed > 3600f) return Position; // Teleport filter: >60 y/s

            float facing = ExtrapolationFacing;
            float cos = MathF.Cos(facing);
            float sin = MathF.Sin(facing);
            float dirX = 0f;
            float dirY = 0f;

            if (moveForward)
            {
                dirX += cos;
                dirY += sin;
            }

            if (moveBackward)
            {
                dirX -= cos;
                dirY -= sin;
            }

            if (strafeLeft)
            {
                dirX -= sin;
                dirY += cos;
            }

            if (strafeRight)
            {
                dirX += sin;
                dirY -= cos;
            }

            if ((moveForward || moveBackward) && (strafeLeft || strafeRight))
            {
                dirX *= DiagonalDamping;
                dirY *= DiagonalDamping;
            }

            float magnitudeSquared = dirX * dirX + dirY * dirY;
            if (magnitudeSquared < ExtrapolationDirectionEpsilon * ExtrapolationDirectionEpsilon)
                return Position;

            float dt = elapsed * 0.001f;
            float dx = dirX * speed * dt;
            float dy = dirY * speed * dt;

            return new Position(
                ExtrapolationBasePosition.X + dx,
                ExtrapolationBasePosition.Y + dy,
                ExtrapolationBasePosition.Z
            );
        }

        public SplineFlags SplineFlags { get; set; }
        public Position SplineFinalPoint { get; set; } = new(0, 0, 0);
        public ulong SplineTargetGuid { get; set; }
        public float SplineFinalOrientation { get; set; }
        public int SplineTimePassed { get; set; }
        public int SplineDuration { get; set; }
        public uint SplineId { get; set; }
        public List<Position> SplineNodes { get; set; } = [];

        public Position SplineFinalDestination { get; set; } = new(0, 0, 0);

        public uint[] Bytes0 { get; } = new uint[4];
        public uint[] VirtualItemSlotDisplay { get; } = new uint[3];
        public uint[] VirtualItemInfo { get; } = new uint[6];
        public uint[] AuraFields { get; } = new uint[48];
        public uint[] AuraFlags { get; } = new uint[6];
        public uint[] AuraLevels { get; } = new uint[12];
        public uint[] AuraApplications { get; } = new uint[12];
        public uint AuraState { get; set; }

        public float BaseAttackTime { get; set; }
        public float BaseAttackTime1 { get; set; }
        public float OffhandAttackTime { get; set; }
        public float OffhandAttackTime1 { get; set; }

        public float BoundingRadius { get; set; }
        public float CombatReach { get; set; }

        public uint NativeDisplayId { get; set; }
        public uint MinDamage { get; set; }
        public uint MaxDamage { get; set; }
        public uint MinOffhandDamage { get; set; }
        public uint MaxOffhandDamage { get; set; }

        public uint[] Bytes1 { get; } = new uint[4];

        public uint PetNumber { get; set; }
        public uint PetNameTimestamp { get; set; }
        public uint PetExperience { get; set; }
        public uint PetNextLevelExperience { get; set; }

        public uint ChannelingId { get; set; }
        public uint SpellcastId { get; set; }
        public bool IsCasting => SpellcastId > 0;
        public bool IsChanneling => ChannelingId > 0;
        public bool IsInCombat => UnitFlags.HasFlag(UnitFlags.UNIT_FLAG_IN_COMBAT);
        public float ModCastSpeed { get; set; }

        public uint CreatedBySpell { get; set; }
        public uint NPCEmoteState { get; set; }
        public uint TrainingPoints { get; set; }

        public uint Strength { get; set; }
        public uint Agility { get; set; }
        public uint Stamina { get; set; }
        public uint Intellect { get; set; }
        public uint Spirit { get; set; }

        public uint[] Resistances { get; } = new uint[7];

        public uint BaseMana { get; set; }
        public uint BaseHealth { get; set; }

        public uint[] Bytes2 { get; } = new uint[4];

        public uint AttackPower { get; set; }
        public uint AttackPowerMods { get; set; }
        public uint AttackPowerMultipler { get; set; }
        public uint RangedAttackPower { get; set; }
        public uint RangedAttackPowerMods { get; set; }
        public uint RangedAttackPowerMultipler { get; set; }

        public uint MinRangedDamage { get; set; }
        public uint MaxRangedDamage { get; set; }

        public uint[] PowerCostModifers { get; } = new uint[7];
        public uint[] PowerCostMultipliers { get; } = new uint[7];

        public List<Spell> Buffs { get; } = [];
        public List<Spell> Debuffs { get; } = [];
        public float FacingAngle { get; internal set; }
        public SplineType SplineType { get; internal set; }
        public Position FacingSpot { get; internal set; } = new Position(0, 0, 0);
        public uint SplineTimestamp { get; internal set; }
        public List<Position> SplinePoints { get; internal set; } = [];

        public override WoWUnit Clone()
        {
            var clone = new WoWUnit(HighGuid, ObjectType);
            clone.CopyFrom(this);
            return clone;
        }

        public override void CopyFrom(WoWObject sourceBase)
        {
            base.CopyFrom(sourceBase);

            if (sourceBase is not WoWUnit source) return;

            TargetHighGuid = source.TargetHighGuid;
            Charm = source.Charm;
            Summon = source.Summon;
            CharmedBy = source.CharmedBy;
            SummonedBy = source.SummonedBy;
            Persuaded = source.Persuaded;
            ChannelObject = source.ChannelObject;
            TargetGuid = source.TargetGuid;
            Health = source.Health;
            MaxHealth = source.MaxHealth;
            Powers = new Dictionary<Powers, uint>(source.Powers);
            MaxPowers = new Dictionary<Powers, uint>(source.MaxPowers);
            MountDisplayId = source.MountDisplayId;
            SummonedByGuid = source.SummonedByGuid;
            UnitReaction = source.UnitReaction;
            UnitFlags = source.UnitFlags;
            NpcFlags = source.NpcFlags;
            NpcEmoteState = source.NpcEmoteState;
            MovementFlags = source.MovementFlags;
            MovementFlags2 = source.MovementFlags2;
            CreatureType = source.CreatureType;
            FallTime = source.FallTime;
            WalkSpeed = source.WalkSpeed;
            RunSpeed = source.RunSpeed;
            RunBackSpeed = source.RunBackSpeed;
            SwimSpeed = source.SwimSpeed;
            SwimBackSpeed = source.SwimBackSpeed;
            TurnRate = source.TurnRate;
            TransportGuid = source.TransportGuid;
            //Transport = source.Transport.Clone();
            TransportOrientation = source.TransportOrientation;
            TransportOffset = source.TransportOffset;
            SwimPitch = source.SwimPitch;
            JumpVerticalSpeed = source.JumpVerticalSpeed;
            JumpSinAngle = source.JumpSinAngle;
            JumpCosAngle = source.JumpCosAngle;
            JumpHorizontalSpeed = source.JumpHorizontalSpeed;
            SplineElevation = source.SplineElevation;
            TransportLastUpdated = source.TransportLastUpdated;
            SplineFlags = source.SplineFlags;
            SplineFinalPoint = source.SplineFinalPoint;
            SplineTargetGuid = source.SplineTargetGuid;
            SplineFinalOrientation = source.SplineFinalOrientation;
            SplineTimePassed = source.SplineTimePassed;
            SplineDuration = source.SplineDuration;
            SplineId = source.SplineId;
            SplineFinalDestination = source.SplineFinalDestination;
            SplineNodes = [.. source.SplineNodes];
            Array.Copy(source.Bytes0, Bytes0, Bytes0.Length);
            Array.Copy(source.VirtualItemSlotDisplay, VirtualItemSlotDisplay, VirtualItemSlotDisplay.Length);
            Array.Copy(source.VirtualItemInfo, VirtualItemInfo, VirtualItemInfo.Length);
            Array.Copy(source.AuraFields, AuraFields, AuraFields.Length);
            Array.Copy(source.AuraFlags, AuraFlags, AuraFlags.Length);
            Array.Copy(source.AuraLevels, AuraLevels, AuraLevels.Length);
            Array.Copy(source.AuraApplications, AuraApplications, AuraApplications.Length);
            Array.Copy(source.Bytes1, Bytes1, Bytes1.Length);
            Array.Copy(source.Resistances, Resistances, Resistances.Length);
            Array.Copy(source.Bytes2, Bytes2, Bytes2.Length);
            Array.Copy(source.PowerCostModifers, PowerCostModifers, PowerCostModifers.Length);
            Array.Copy(source.PowerCostMultipliers, PowerCostMultipliers, PowerCostMultipliers.Length);
            AuraState = source.AuraState;
            BaseAttackTime = source.BaseAttackTime;
            BaseAttackTime1 = source.BaseAttackTime1;
            OffhandAttackTime = source.OffhandAttackTime;
            OffhandAttackTime1 = source.OffhandAttackTime1;
            BoundingRadius = source.BoundingRadius;
            CombatReach = source.CombatReach;
            NativeDisplayId = source.NativeDisplayId;
            MinDamage = source.MinDamage;
            MaxDamage = source.MaxDamage;
            MinOffhandDamage = source.MinOffhandDamage;
            MaxOffhandDamage = source.MaxOffhandDamage;
            PetNumber = source.PetNumber;
            PetNameTimestamp = source.PetNameTimestamp;
            PetExperience = source.PetExperience;
            PetNextLevelExperience = source.PetNextLevelExperience;
            ChannelingId = source.ChannelingId;
            SpellcastId = source.SpellcastId;
            ModCastSpeed = source.ModCastSpeed;
            CreatedBySpell = source.CreatedBySpell;
            NPCEmoteState = source.NPCEmoteState;
            TrainingPoints = source.TrainingPoints;
            Strength = source.Strength;
            Agility = source.Agility;
            Stamina = source.Stamina;
            Intellect = source.Intellect;
            Spirit = source.Spirit;
            BaseMana = source.BaseMana;
            BaseHealth = source.BaseHealth;
            AttackPower = source.AttackPower;
            AttackPowerMods = source.AttackPowerMods;
            AttackPowerMultipler = source.AttackPowerMultipler;
            RangedAttackPower = source.RangedAttackPower;
            RangedAttackPowerMods = source.RangedAttackPowerMods;
            RangedAttackPowerMultipler = source.RangedAttackPowerMultipler;
            MinRangedDamage = source.MinRangedDamage;
            MaxRangedDamage = source.MaxRangedDamage;
            FacingAngle = source.FacingAngle;
            SplineType = source.SplineType;
            FacingSpot = source.FacingSpot;
            SplineTimestamp = source.SplineTimestamp;
            SplinePoints = [.. source.SplinePoints];
            Buffs.Clear();
            Buffs.AddRange(source.Buffs.Select(b => b.Clone()));
            Debuffs.Clear();
            Debuffs.AddRange(source.Debuffs.Select(d => d.Clone()));
        }

        public bool HasBuff(string name) => Buffs.Any(a => a.Name == name);
        public bool HasDebuff(string name) => Debuffs.Any(a => a.Name == name);

        /// <summary>
        /// Rebuilds Buffs/Debuffs lists from raw AuraFields + AuraFlags (object update data).
        /// Called after SMSG_UPDATE_OBJECT processes aura field changes for BG bot.
        /// In Vanilla 1.12.1: AuraFlags has 6 uint32s, each covering 8 slots (4 bits per slot).
        /// Bit 0 (0x1) = active, bit 2 (0x4) = harmful (debuff).
        /// </summary>
        public void RebuildBuffsFromAuraFields()
        {
            Buffs.Clear();
            Debuffs.Clear();
            for (int slot = 0; slot < AuraFields.Length; slot++)
            {
                uint spellId = AuraFields[slot];
                if (spellId == 0) continue;

                // Read 4-bit flags for this slot from AuraFlags
                int flagsIndex = slot / 8;
                int bitOffset = (slot % 8) * 4;
                uint slotFlags = (flagsIndex < AuraFlags.Length)
                    ? (AuraFlags[flagsIndex] >> bitOffset) & 0xF
                    : 0;

                // Bit 0 = active; skip inactive slots
                if ((slotFlags & 0x1) == 0) continue;

                string name = GameData.Core.Constants.SpellData.GetSpellName(spellId) ?? $"Spell#{spellId}";
                var spell = new Spell(spellId, 0, name, "", "");

                bool isHarmful = (slotFlags & 0x4) != 0;
                if (isHarmful)
                    Debuffs.Add(spell);
                else
                    Buffs.Add(spell);
            }
        }
        public bool DismissBuff(string buffName)
        {
            var buff = Buffs.FirstOrDefault(b => b.Name == buffName);
            if (buff == null) return false;
            ObjectManager?.CancelAura(buff.Id);
            return true;
        }

        public IEnumerable<ISpellEffect> GetDebuffs() =>
            Debuffs.Select(d => (ISpellEffect)new SpellEffect("", d.Id, EffectType.None));

        public IEnumerable<ISpellEffect> GetBuffs() =>
            Buffs.Select(b => (ISpellEffect)new SpellEffect("", b.Id, EffectType.None));

        public Position GetPointBehindUnit(float distance)
        {
            // "Behind" = opposite of facing direction (facing + π)
            float behindAngle = FacingAngle + (float)Math.PI;
            float x = Position.X + (float)Math.Cos(behindAngle) * distance;
            float y = Position.Y + (float)Math.Sin(behindAngle) * distance;
            return new Position(x, y, Position.Z);
        }

        public void Interact()
        {
            // BG bot interaction is handled via network client components
        }
    }
}
