using BotRunner.Clients;
using GameData.Core.Enums;
using GameData.Core.Frames;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using WoWSharpClient.Client;
using WoWSharpClient.Models;
using WoWSharpClient.Movement;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Parsers;
using WoWSharpClient.Screens;
using WoWSharpClient.Utils;
using static GameData.Core.Enums.UpdateFields;
using Enum = System.Enum;
using Timer = System.Timers.Timer;

namespace WoWSharpClient
{
    public partial class WoWSharpObjectManager
    {

        private WoWObject CreateObjectFromFields(
            WoWObjectType objectType,
            ulong guid,
            Dictionary<uint, object?> fields
        )
        {
            WoWObject obj = objectType switch
            {
                WoWObjectType.Item => new WoWItem(new HighGuid(guid)),
                WoWObjectType.Container => new WoWContainer(new HighGuid(guid)),
                WoWObjectType.Unit => new WoWUnit(new HighGuid(guid)),
                WoWObjectType.Player => guid == PlayerGuid.FullGuid
                    ? (WoWLocalPlayer)Player
                    : new WoWPlayer(new HighGuid(guid)),
                WoWObjectType.GameObj => new WoWGameObject(new HighGuid(guid)),
                WoWObjectType.DynamicObj => new WoWDynamicObject(new HighGuid(guid)),
                WoWObjectType.Corpse => new WoWCorpse(new HighGuid(guid)),
                _ => CreateFallbackObject(objectType, guid),
            };
            ApplyFieldDiffs(obj, fields);
            return obj;
        }

        /// <summary>
        /// Fallback object creation when ObjectType byte is None/unknown.
        /// MaNGOS sometimes sends CREATE_OBJECT with ObjectType=0 for dynamically
        /// spawned game objects (fishing bobbers, traps, etc.). We detect these by
        /// checking the GUID's high type bits: 0xF110/0xF130 = game object range.
        /// Without this, the bobber is created as a generic WoWObject, fails the
        /// OfType&lt;IWoWGameObject&gt;() filter, and never appears in NearbyObjects
        /// or triggers auto-catch in HandleGameObjectCustomAnim.
        /// </summary>


        /// <summary>
        /// Fallback object creation when ObjectType byte is None/unknown.
        /// MaNGOS sometimes sends CREATE_OBJECT with ObjectType=0 for dynamically
        /// spawned game objects (fishing bobbers, traps, etc.). We detect these by
        /// checking the GUID's high type bits: 0xF110/0xF130 = game object range.
        /// Without this, the bobber is created as a generic WoWObject, fails the
        /// OfType&lt;IWoWGameObject&gt;() filter, and never appears in NearbyObjects
        /// or triggers auto-catch in HandleGameObjectCustomAnim.
        /// </summary>
        private static WoWObject CreateFallbackObject(WoWObjectType objectType, ulong guid)
        {
            ushort highType = (ushort)(guid >> 48);
            if (highType is 0xF110 or 0xF130)
            {
                Log.Information("[CreateFallback] Guid=0x{Guid:X} has GO GUID range (0x{High:X4}) but objectType={Type} — creating WoWGameObject",
                    guid, highType, objectType);
                return new WoWGameObject(new HighGuid(guid));
            }

            Log.Debug("[CreateFallback] Guid=0x{Guid:X} objectType={Type} highType=0x{High:X4} — creating generic WoWObject",
                guid, objectType, highType);
            return new WoWObject(new HighGuid(guid));
        }


        private static readonly List<WoWObject> _objects = [];

        private static readonly object _objectsLock = new();


        /// <summary>
        /// Returns an object by its full GUID, or null if not found.
        /// Checks the local player first, then the objects list.
        /// </summary>
        public WoWObject GetObjectByGuid(ulong guid)
        {
            if (guid == PlayerGuid.FullGuid)
                return Player as WoWObject;
            lock (_objectsLock) return _objects.FirstOrDefault(o => o.Guid == guid);
        }

        /// <summary>
        /// Returns a snapshot of the objects list. Safe to enumerate from any thread
        /// while ProcessUpdatesAsync modifies the underlying list.
        /// </summary>
        public IEnumerable<IWoWObject> Objects
        {
            get { lock (_objectsLock) return _objects.ToArray(); }
        }

        /// <summary>Look up a unit by GUID (includes Player). Returns null if not found.</summary>
        internal WoWUnit? GetUnitByGuid(ulong guid)
        {
            if (Player != null && Player.Guid == guid)
                return (WoWUnit)Player;
            lock (_objectsLock)
                return _objects.OfType<WoWUnit>().FirstOrDefault(u => u.Guid == guid);
        }

        internal void SyncTransportPassengerWorldPositions()
        {
            if (Player is WoWUnit playerUnit)
                SyncTransportPassengerWorldPosition(playerUnit);

            foreach (var unit in Objects.OfType<WoWUnit>().Where(u => Player == null || u.Guid != Player.Guid))
                SyncTransportPassengerWorldPosition(unit);
        }

        internal void SyncTransportPassengerWorldPosition(WoWUnit unit)
        {
            if (unit.TransportGuid == 0)
                return;

            if (GetObjectByGuid(unit.TransportGuid) is not WoWGameObject transport)
                return;

            unit.Transport = transport;
            unit.Position = TransportCoordinateHelper.LocalToWorld(
                unit.TransportOffset,
                transport.Position,
                transport.Facing);
            unit.Facing = TransportCoordinateHelper.LocalToWorldFacing(
                unit.TransportOrientation,
                transport.Facing);
        }

        /// <summary>
        /// Units that are alive and have a hostile faction reaction (Hated/Hostile/Unfriendly/Neutral).
        /// Matches ForegroundBotRunner ObjectManager.Combat.cs logic.
        /// UnitReaction is computed from FactionData when UNIT_FIELD_FACTIONTEMPLATE is received.
        /// </summary>
        public IEnumerable<IWoWUnit> Hostiles
        {
            get
            {
                var playerGuid = PlayerGuid.FullGuid;
                if (playerGuid == 0) return [];
                return Objects.OfType<IWoWUnit>()
                    .Where(u => u.Health > 0 && u.Guid != playerGuid)
                    .Where(u =>
                        u.UnitReaction == UnitReaction.Hated ||
                        u.UnitReaction == UnitReaction.Hostile ||
                        u.UnitReaction == UnitReaction.Unfriendly ||
                        u.UnitReaction == UnitReaction.Neutral);
            }
        }

        /// <summary>
        /// Units actively in combat that are targeting the player or party.
        /// </summary>


        /// <summary>
        /// Units actively in combat that are targeting the player or party.
        /// </summary>
        public IEnumerable<IWoWUnit> Aggressors =>
            Hostiles.Where(u => u.IsInCombat || u.IsFleeing);

        /// <summary>Aggressors that have mana (likely casters).</summary>


        /// <summary>Aggressors that have mana (likely casters).</summary>
        public IEnumerable<IWoWUnit> CasterAggressors =>
            Aggressors.Where(u => u.ManaPercent > 0);

        /// <summary>Aggressors that have no mana (melee).</summary>


        /// <summary>Aggressors that have no mana (melee).</summary>
        public IEnumerable<IWoWUnit> MeleeAggressors =>
            Aggressors.Where(u => u.ManaPercent <= 0);

        /// <summary>
        /// Drains all pending system messages (CHAT_MSG_SYSTEM) received since last call.
        /// </summary>


        private static void ApplyMovementData(WoWUnit unit, MovementInfoUpdate data, bool allowPositionWrite, bool allowMovementFlagWrite = true)
        {
            if (allowMovementFlagWrite)
                unit.MovementFlags = data.MovementFlags;
            unit.LastUpdated = data.LastUpdated;

            if (allowPositionWrite)
            {
                // Diagnostic: detect when server overwrites local player position
                if (unit is WoWLocalPlayer)
                {
                    float dx = data.X - unit.Position.X;
                    float dy = data.Y - unit.Position.Y;
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    if (dist > 0.1f)
                    {
                        Log.Warning("[POS_OVERWRITE] Server writing local player pos: " +
                            "cur=({CurX:F3},{CurY:F3},{CurZ:F3}) new=({NewX:F3},{NewY:F3},{NewZ:F3}) delta={Dist:F3}",
                            unit.Position.X, unit.Position.Y, unit.Position.Z,
                            data.X, data.Y, data.Z, dist);
                    }
                }
                unit.Position.X = data.X;
                unit.Position.Y = data.Y;
                unit.Position.Z = data.Z;
                unit.Facing = data.Facing;

                // Record extrapolation state for remote unit position prediction.
                // WoW.exe 0x616DE0 predicts other-unit positions between heartbeats.
                if (unit is not WoWLocalPlayer)
                {
                    unit.ExtrapolationBasePosition = new Position(data.X, data.Y, data.Z);
                    unit.ExtrapolationFlags = data.MovementFlags;
                    unit.ExtrapolationFacing = data.Facing;
                    unit.ExtrapolationTimeMs = data.LastUpdated;
                }
            }
            unit.TransportGuid = data.TransportGuid ?? 0;
            if (unit.TransportGuid == 0)
            {
                unit.TransportOffset = new Position(0, 0, 0);
                unit.TransportOrientation = 0f;
                unit.TransportLastUpdated = 0;
            }
            else
            {
                unit.TransportOffset = data.TransportOffset ?? unit.TransportOffset;
                unit.TransportOrientation = data.TransportOrientation ?? unit.TransportOrientation;
                unit.TransportLastUpdated = data.TransportLastUpdated ?? unit.TransportLastUpdated;
                Instance.SyncTransportPassengerWorldPosition(unit);
            }
            unit.SwimPitch = data.SwimPitch ?? 0f;
            unit.FallTime = data.FallTime;
            unit.JumpVerticalSpeed = data.JumpVerticalSpeed ?? 0f;
            unit.JumpSinAngle = data.JumpSinAngle ?? 0f;
            unit.JumpCosAngle = data.JumpCosAngle ?? 0f;
            unit.JumpHorizontalSpeed = data.JumpHorizontalSpeed ?? 0f;
            unit.SplineElevation = data.SplineElevation ?? 0f;

            if (data.MovementBlockUpdate != null)
            {
                unit.WalkSpeed = data.MovementBlockUpdate.WalkSpeed;
                unit.RunSpeed = data.MovementBlockUpdate.RunSpeed;
                unit.RunBackSpeed = data.MovementBlockUpdate.RunBackSpeed;
                unit.SwimSpeed = data.MovementBlockUpdate.SwimSpeed;
                unit.SwimBackSpeed = data.MovementBlockUpdate.SwimBackSpeed;
                unit.TurnRate = data.MovementBlockUpdate.TurnRate;
                unit.SplineFlags = data.MovementBlockUpdate.SplineFlags ?? SplineFlags.None;
                unit.SplineFinalPoint = data.MovementBlockUpdate.SplineFinalPoint ?? unit.SplineFinalPoint;
                unit.SplineTargetGuid = data.MovementBlockUpdate.SplineTargetGuid ?? 0;
                unit.SplineFinalOrientation = data.MovementBlockUpdate.SplineFinalOrientation ?? 0f;
                unit.SplineTimePassed = data.MovementBlockUpdate.SplineTimePassed ?? 0;
                unit.SplineDuration = data.MovementBlockUpdate.SplineDuration ?? 0;
                unit.SplineId = data.MovementBlockUpdate.SplineId ?? 0;
                unit.SplineNodes = data.MovementBlockUpdate.SplineNodes ?? [];
                unit.SplineFinalDestination = data.MovementBlockUpdate.SplineFinalDestination ?? unit.SplineFinalDestination;
                unit.SplineType = data.MovementBlockUpdate.SplineType;
                unit.SplineTargetGuid = data.MovementBlockUpdate.FacingTargetGuid;
                unit.FacingAngle = data.MovementBlockUpdate.FacingAngle;
                unit.FacingSpot = data.MovementBlockUpdate.FacingSpot;
                unit.SplineTimestamp = data.MovementBlockUpdate.SplineTimestamp;
                unit.SplinePoints = data.MovementBlockUpdate.SplinePoints;
            }
        }


        private static void ApplyContainerFieldDiffs(
            WoWContainer container,
            uint key,
            object? value
        )
        {
            var field = (EContainerFields)key;
            switch (field)
            {
                case EContainerFields.CONTAINER_FIELD_NUM_SLOTS:
                    container.NumOfSlots = (int)value;
                    break;
                case EContainerFields.CONTAINER_ALIGN_PAD:
                    break;
                case >= EContainerFields.CONTAINER_FIELD_SLOT_1
                and <= EContainerFields.CONTAINER_FIELD_SLOT_LAST:
                    {
                        // Store both low and high GUID parts at their natural offsets
                        // Slots[slot*2] = low part, Slots[slot*2+1] = high part
                        var slotFieldOffset =
                            (uint)field - (uint)EContainerFields.CONTAINER_FIELD_SLOT_1;

                        if (slotFieldOffset < container.Slots.Length)
                        {
                            container.Slots[slotFieldOffset] = (uint)value;
                        }
                    }
                    break;
                case EContainerFields.CONTAINER_END:
                    break;
            }
        }


        private static void ApplyUnitFieldDiffs(WoWUnit unit, uint key, object? value)
        {
            var field = (EUnitFields)key;
            switch (field)
            {
                case EUnitFields.UNIT_FIELD_CHARM:
                    unit.Charm.LowGuidValue = (byte[])value;
                    break;
                case EUnitFields.UNIT_FIELD_CHARM + 1:
                    unit.Charm.HighGuidValue = (byte[])value;
                    break;
                case EUnitFields.UNIT_FIELD_SUMMON:
                    unit.Summon.LowGuidValue = (byte[])value;
                    break;
                case EUnitFields.UNIT_FIELD_SUMMON + 1:
                    unit.Summon.HighGuidValue = (byte[])value;
                    break;
                case EUnitFields.UNIT_FIELD_CHARMEDBY:
                    unit.CharmedBy.LowGuidValue = (byte[])value;
                    break;
                case EUnitFields.UNIT_FIELD_CHARMEDBY + 1:
                    unit.CharmedBy.HighGuidValue = (byte[])value;
                    break;
                case EUnitFields.UNIT_FIELD_SUMMONEDBY:
                    unit.SummonedBy.LowGuidValue = (byte[])value;
                    break;
                case EUnitFields.UNIT_FIELD_SUMMONEDBY + 1:
                    unit.SummonedBy.HighGuidValue = (byte[])value;
                    break;
                case EUnitFields.UNIT_FIELD_CREATEDBY:
                    unit.CreatedBy.LowGuidValue = (byte[])value;
                    break;
                case EUnitFields.UNIT_FIELD_CREATEDBY + 1:
                    unit.CreatedBy.HighGuidValue = (byte[])value;
                    break;
                case EUnitFields.UNIT_FIELD_TARGET:
                    unit.TargetHighGuid.LowGuidValue = (byte[])value;
                    unit.TargetGuid = unit.TargetHighGuid.FullGuid;
                    break;
                case EUnitFields.UNIT_FIELD_TARGET + 1:
                    unit.TargetHighGuid.HighGuidValue = (byte[])value;
                    unit.TargetGuid = unit.TargetHighGuid.FullGuid;
                    break;
                case EUnitFields.UNIT_FIELD_PERSUADED:
                    unit.Persuaded.LowGuidValue = (byte[])value;
                    break;
                case EUnitFields.UNIT_FIELD_PERSUADED + 1:
                    unit.Persuaded.HighGuidValue = (byte[])value;
                    break;
                case EUnitFields.UNIT_FIELD_CHANNEL_OBJECT:
                    unit.ChannelObject.LowGuidValue = (byte[])value;
                    break;
                case EUnitFields.UNIT_FIELD_CHANNEL_OBJECT + 1:
                    unit.ChannelObject.HighGuidValue = (byte[])value;
                    break;
                case EUnitFields.UNIT_FIELD_HEALTH:
                    unit.Health = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_POWER1:
                    unit.Powers[Powers.MANA] = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_POWER2:
                    unit.Powers[Powers.RAGE] = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_POWER3:
                    unit.Powers[Powers.FOCUS] = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_POWER4:
                    unit.Powers[Powers.ENERGY] = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_POWER5:
                    unit.Powers[Powers.HAPPINESS] = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_MAXHEALTH:
                    unit.MaxHealth = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_MAXPOWER1:
                    unit.MaxPowers[Powers.MANA] = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_MAXPOWER2:
                    unit.MaxPowers[Powers.RAGE] = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_MAXPOWER3:
                    unit.MaxPowers[Powers.FOCUS] = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_MAXPOWER4:
                    unit.MaxPowers[Powers.ENERGY] = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_MAXPOWER5:
                    unit.MaxPowers[Powers.HAPPINESS] = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_LEVEL:
                    unit.Level = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_FACTIONTEMPLATE:
                    unit.FactionTemplate = (uint)value;
                    // Compute UnitReaction from faction template masks
                    var playerFt = Instance?.Player?.FactionTemplate ?? 0;
                    if (playerFt != 0 && unit.FactionTemplate != 0)
                        unit.UnitReaction = GameData.Core.Constants.FactionData.GetReaction(playerFt, unit.FactionTemplate);
                    break;
                case EUnitFields.UNIT_FIELD_BYTES_0:
                    byte[] value1 = (byte[])value;

                    unit.Bytes0[0] = value1[0];
                    unit.Bytes0[1] = value1[1];
                    unit.Bytes0[2] = value1[2];
                    unit.Bytes0[3] = value1[3];

                    // Unpack Race/Class/Gender for player objects
                    if (unit is WoWPlayer player0)
                    {
                        player0.Race = (Race)value1[0];
                        player0.Class = (Class)value1[1];
                        player0.Gender = (Gender)value1[2];
                    }
                    break;
                case >= EUnitFields.UNIT_VIRTUAL_ITEM_SLOT_DISPLAY
                and <= EUnitFields.UNIT_VIRTUAL_ITEM_SLOT_DISPLAY_02:
                    unit.VirtualItemSlotDisplay[
                        field - EUnitFields.UNIT_VIRTUAL_ITEM_SLOT_DISPLAY
                    ] = (uint)value;
                    break;
                case >= EUnitFields.UNIT_VIRTUAL_ITEM_INFO
                and <= EUnitFields.UNIT_VIRTUAL_ITEM_INFO_05:
                    unit.VirtualItemInfo[field - EUnitFields.UNIT_VIRTUAL_ITEM_INFO] = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_FLAGS:
                    unit.UnitFlags = (UnitFlags)value;
                    break;
                case >= EUnitFields.UNIT_FIELD_AURA
                and <= EUnitFields.UNIT_FIELD_AURA_LAST:
                    unit.AuraFields[field - EUnitFields.UNIT_FIELD_AURA] = (uint)value;
                    break;
                case >= EUnitFields.UNIT_FIELD_AURAFLAGS
                and <= EUnitFields.UNIT_FIELD_AURAFLAGS_05:
                    unit.AuraFlags[field - EUnitFields.UNIT_FIELD_AURAFLAGS] = (uint)value;
                    break;
                case >= EUnitFields.UNIT_FIELD_AURALEVELS
                and <= EUnitFields.UNIT_FIELD_AURALEVELS_LAST:
                    unit.AuraLevels[field - EUnitFields.UNIT_FIELD_AURALEVELS] = (uint)value;
                    break;
                case >= EUnitFields.UNIT_FIELD_AURAAPPLICATIONS
                and <= EUnitFields.UNIT_FIELD_AURAAPPLICATIONS_LAST:
                    unit.AuraApplications[field - EUnitFields.UNIT_FIELD_AURAAPPLICATIONS] =
                        (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_AURASTATE:
                    unit.AuraState = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_BASEATTACKTIME:
                    unit.BaseAttackTime = (float)value;
                    break;
                case EUnitFields.UNIT_FIELD_OFFHANDATTACKTIME:
                    unit.OffhandAttackTime = (float)value;
                    break;
                case EUnitFields.UNIT_FIELD_RANGEDATTACKTIME:
                    unit.OffhandAttackTime1 = (float)value;
                    break;
                case EUnitFields.UNIT_FIELD_BOUNDINGRADIUS:
                    unit.BoundingRadius = (float)value;
                    break;
                case EUnitFields.UNIT_FIELD_COMBATREACH:
                    unit.CombatReach = (float)value;
                    break;
                case EUnitFields.UNIT_FIELD_DISPLAYID:
                    unit.DisplayId = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_NATIVEDISPLAYID:
                    unit.NativeDisplayId = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_MOUNTDISPLAYID:
                    unit.MountDisplayId = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_MINDAMAGE:
                    unit.MinDamage = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_MAXDAMAGE:
                    unit.MaxDamage = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_MINOFFHANDDAMAGE:
                    unit.MinOffhandDamage = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_MAXOFFHANDDAMAGE:
                    unit.MaxOffhandDamage = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_BYTES_1:
                    byte[] value2 = (byte[])value;

                    unit.Bytes1[0] = value2[0];
                    unit.Bytes1[1] = value2[1];
                    unit.Bytes1[2] = value2[2];
                    unit.Bytes1[3] = value2[3];
                    break;
                case EUnitFields.UNIT_FIELD_PETNUMBER:
                    unit.PetNumber = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_PET_NAME_TIMESTAMP:
                    unit.PetNameTimestamp = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_PETEXPERIENCE:
                    unit.PetExperience = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_PETNEXTLEVELEXP:
                    unit.PetNextLevelExperience = (uint)value;
                    break;
                case EUnitFields.UNIT_DYNAMIC_FLAGS:
                    unit.DynamicFlags = (DynamicFlags)value;
                    break;
                case EUnitFields.UNIT_CHANNEL_SPELL:
                    unit.ChannelingId = (uint)value;
                    break;
                case EUnitFields.UNIT_MOD_CAST_SPEED:
                    unit.ModCastSpeed = (float)value;
                    break;
                case EUnitFields.UNIT_CREATED_BY_SPELL:
                    unit.CreatedBySpell = (uint)value;
                    break;
                case EUnitFields.UNIT_NPC_FLAGS:
                    unit.NpcFlags = (NPCFlags)value;
                    break;
                case EUnitFields.UNIT_NPC_EMOTESTATE:
                    unit.NpcEmoteState = (uint)value;
                    break;
                case EUnitFields.UNIT_TRAINING_POINTS:
                    unit.TrainingPoints = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_STAT0:
                    unit.Strength = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_STAT1:
                    unit.Agility = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_STAT2:
                    unit.Stamina = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_STAT3:
                    unit.Intellect = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_STAT4:
                    unit.Spirit = (uint)value;
                    break;
                case >= EUnitFields.UNIT_FIELD_RESISTANCES
                and <= EUnitFields.UNIT_FIELD_RESISTANCES_06:
                    unit.Resistances[field - EUnitFields.UNIT_FIELD_RESISTANCES] = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_BASE_MANA:
                    unit.BaseMana = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_BASE_HEALTH:
                    unit.BaseHealth = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_BYTES_2:
                    byte[] value3 = (byte[])value;

                    unit.Bytes2[0] = value3[0];
                    unit.Bytes2[1] = value3[1];
                    unit.Bytes2[2] = value3[2];
                    unit.Bytes2[3] = value3[3];
                    break;
                case EUnitFields.UNIT_FIELD_ATTACK_POWER:
                    unit.AttackPower = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_ATTACK_POWER_MODS:
                    unit.AttackPowerMods = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_ATTACK_POWER_MULTIPLIER:
                    unit.AttackPowerMultipler = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_RANGED_ATTACK_POWER:
                    unit.RangedAttackPower = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_RANGED_ATTACK_POWER_MODS:
                    unit.RangedAttackPowerMods = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_RANGED_ATTACK_POWER_MULTIPLIER:
                    unit.RangedAttackPowerMultipler = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_MINRANGEDDAMAGE:
                    unit.MinRangedDamage = (uint)value;
                    break;
                case EUnitFields.UNIT_FIELD_MAXRANGEDDAMAGE:
                    unit.MaxRangedDamage = (uint)value;
                    break;
                case >= EUnitFields.UNIT_FIELD_POWER_COST_MODIFIER
                and <= EUnitFields.UNIT_FIELD_POWER_COST_MODIFIER_06:
                    unit.PowerCostModifers[field - EUnitFields.UNIT_FIELD_POWER_COST_MODIFIER] =
                        (uint)value;
                    break;
                case >= EUnitFields.UNIT_FIELD_POWER_COST_MULTIPLIER
                and <= EUnitFields.UNIT_FIELD_POWER_COST_MULTIPLIER_06:
                    unit.PowerCostMultipliers[
                        field - EUnitFields.UNIT_FIELD_POWER_COST_MULTIPLIER
                    ] = (uint)value;
                    break;
            }
        }


        private static void ApplyFieldDiffs(WoWObject obj, Dictionary<uint, object?> updatedFields)
        {
            bool auraFieldsModified = false;
            foreach (var (key, value) in updatedFields)
            {
                if (value == null)
                    continue;

                // Track if any aura fields were modified so we can rebuild Buffs/Debuffs after
                if (key >= (uint)EUnitFields.UNIT_FIELD_AURA && key <= (uint)EUnitFields.UNIT_FIELD_AURAFLAGS_05)
                    auraFieldsModified = true;

                bool fieldHandled = false;

                // Check object-specific fields first, in inheritance order (most specific to least specific)

                // WoWContainer (inherits from WoWItem)
                if (obj is WoWContainer container)
                {
                    if (Enum.IsDefined(typeof(EContainerFields), key))
                    {
                        ApplyContainerFieldDiffs(container, key, value);
                        fieldHandled = true;
                    }
                    else if (Enum.IsDefined(typeof(EItemFields), key))
                    {
                        ApplyItemFieldDiffs(container, key, value);
                        fieldHandled = true;
                    }
                }
                // WoWItem (but not container since container was handled above)
                else if (obj is WoWItem item)
                {
                    if (Enum.IsDefined(typeof(EItemFields), key))
                    {
                        ApplyItemFieldDiffs(item, key, value);
                        fieldHandled = true;
                    }
                }
                // WoWPlayer/WoWLocalPlayer (inherits from WoWUnit)
                else if (obj is WoWPlayer player)
                {
                    // Player fields use ranges (e.g., PACK_SLOT_1..PACK_SLOT_LAST) where only
                    // the first and last values are in the enum. Enum.IsDefined returns false for
                    // intermediate values, silently dropping inventory/bank slot fields.
                    // Use a range check instead.
                    if (key >= (uint)EPlayerFields.PLAYER_DUEL_ARBITER && key <= (uint)EPlayerFields.PLAYER_END)
                    {
                        ApplyPlayerFieldDiffs(player, key, value, _objects);
                        fieldHandled = true;
                    }
                    else if (key >= (uint)EUnitFields.UNIT_FIELD_CHARM && key < (uint)EUnitFields.UNIT_END)
                    {
                        ApplyUnitFieldDiffs(player, key, value);
                        fieldHandled = true;
                    }
                }
                // WoWUnit (but not player since player was handled above)
                else if (obj is WoWUnit unit)
                {
                    // Same range-based check as player fields — unit fields have arrays
                    // (auras, aura flags, etc.) where intermediate values aren't in the enum.
                    if (key >= (uint)EUnitFields.UNIT_FIELD_CHARM && key < (uint)EUnitFields.UNIT_END)
                    {
                        ApplyUnitFieldDiffs(unit, key, value);
                        fieldHandled = true;
                    }
                }
                // WoWGameObject
                else if (obj is WoWGameObject go)
                {
                    if (Enum.IsDefined(typeof(EGameObjectFields), key))
                    {
                        ApplyGameObjectFieldDiffs(go, key, value);
                        fieldHandled = true;
                    }
                }
                // WoWDynamicObject
                else if (obj is WoWDynamicObject dyn)
                {
                    if (Enum.IsDefined(typeof(EDynamicObjectFields), key))
                    {
                        ApplyDynamicObjectFieldDiffs(dyn, key, value);
                        fieldHandled = true;
                    }
                }
                // WoWCorpse
                else if (obj is WoWCorpse corpse)
                {
                    if (Enum.IsDefined(typeof(ECorpseFields), key))
                    {
                        ApplyCorpseFieldDiffs(corpse, key, value);
                        fieldHandled = true;
                    }
                }

                // Fall back to base object fields if no specific field type was handled
                if (!fieldHandled && Enum.IsDefined(typeof(EObjectFields), key))
                {
                    ApplyObjectFieldDiffs(obj, key, value);
                }
            }

            // Rebuild Buffs/Debuffs lists from raw aura field data after all fields are applied
            if (auraFieldsModified && obj is WoWUnit auraUnit)
                auraUnit.RebuildBuffsFromAuraFields();
        }


        private static void ApplyObjectFieldDiffs(WoWObject obj, uint key, object value)
        {
            var field = (EObjectFields)key;
            switch (field)
            {
                case EObjectFields.OBJECT_FIELD_GUID:
                    obj.HighGuid.LowGuidValue = (byte[])value; // COMMENTED OUT - should not modify object's own GUID
                    break;
                case EObjectFields.OBJECT_FIELD_GUID + 1:
                    obj.HighGuid.HighGuidValue = (byte[])value; // COMMENTED OUT - should not modify object's own GUID
                    break;
                case EObjectFields.OBJECT_FIELD_TYPE:
                    break;
                case EObjectFields.OBJECT_FIELD_ENTRY:
                    obj.Entry = (uint)value;
                    break;
                case EObjectFields.OBJECT_FIELD_SCALE_X:
                    obj.ScaleX = (float)value;
                    break;
            }
        }


        private static void ApplyItemFieldDiffs(WoWItem item, uint key, object value)
        {
            var field = (EItemFields)key;
            switch (field)
            {
                case EItemFields.ITEM_FIELD_OWNER:
                    item.Owner.LowGuidValue = (byte[])value;
                    break;
                case EItemFields.ITEM_FIELD_OWNER + 1:
                    item.Owner.HighGuidValue = (byte[])value;
                    break;
                case EItemFields.ITEM_FIELD_CONTAINED:
                    {
                        var bytes = (byte[])value;
                        item.Contained.LowGuidValue = bytes;
                        break;
                    }
                case EItemFields.ITEM_FIELD_CONTAINED + 1:
                    item.Contained.HighGuidValue = (byte[])value;
                    break;
                case EItemFields.ITEM_FIELD_CREATOR:
                    item.CreatedBy.LowGuidValue = (byte[])value;
                    break;
                case EItemFields.ITEM_FIELD_CREATOR + 1:
                    item.CreatedBy.HighGuidValue = (byte[])value;
                    break;
                case EItemFields.ITEM_FIELD_GIFTCREATOR:
                    item.GiftCreator.LowGuidValue = (byte[])value;
                    break;
                case EItemFields.ITEM_FIELD_GIFTCREATOR + 1:
                    item.GiftCreator.HighGuidValue = (byte[])value;
                    break;
                case EItemFields.ITEM_FIELD_STACK_COUNT:
                    item.StackCount = (uint)value;
                    break;
                case EItemFields.ITEM_FIELD_DURATION:
                    item.Duration = (uint)value;
                    break;
                case EItemFields.ITEM_FIELD_SPELL_CHARGES:
                    item.SpellCharges[0] = (uint)value;
                    break;
                case EItemFields.ITEM_FIELD_SPELL_CHARGES_01:
                    item.SpellCharges[1] = (uint)value;
                    break;
                case EItemFields.ITEM_FIELD_SPELL_CHARGES_02:
                    item.SpellCharges[2] = (uint)value;
                    break;
                case EItemFields.ITEM_FIELD_SPELL_CHARGES_03:
                    item.SpellCharges[3] = (uint)value;
                    break;
                case EItemFields.ITEM_FIELD_SPELL_CHARGES_04:
                    item.SpellCharges[4] = (uint)value;
                    break;
                case EItemFields.ITEM_FIELD_FLAGS:
                    item.ItemDynamicFlags = (ItemDynFlags)value;
                    break;
                case EItemFields.ITEM_FIELD_ENCHANTMENT:
                    item.Enchantments[0] = (uint)value;
                    break;
                case EItemFields.ITEM_FIELD_PROPERTY_SEED:
                    item.PropertySeed = (uint)value;
                    break;
                case EItemFields.ITEM_FIELD_RANDOM_PROPERTIES_ID:
                    item.PropertySeed = (uint)value;
                    break;
                case EItemFields.ITEM_FIELD_ITEM_TEXT_ID:
                    item.ItemTextId = (uint)value;
                    break;
                case EItemFields.ITEM_FIELD_DURABILITY:
                    item.Durability = (uint)value;
                    break;
                case EItemFields.ITEM_FIELD_MAXDURABILITY:
                    item.MaxDurability = (uint)value;
                    break;
                case EItemFields.ITEM_END:
                    break;
            }
        }


        private static void ApplyGameObjectFieldDiffs(WoWGameObject go, uint key, object value)
        {
            var field = (EGameObjectFields)key;
            switch (field)
            {
                case EGameObjectFields.OBJECT_FIELD_CREATED_BY:
                    go.CreatedBy.LowGuidValue = (byte[])value;
                    break;
                case EGameObjectFields.OBJECT_FIELD_CREATED_BY + 1:
                    go.CreatedBy.HighGuidValue = (byte[])value;
                    break;
                case EGameObjectFields.GAMEOBJECT_DISPLAYID:
                    go.DisplayId = ToUInt32(value);
                    break;
                case EGameObjectFields.GAMEOBJECT_FLAGS:
                    go.Flags = ToUInt32(value);
                    break;
                case >= EGameObjectFields.GAMEOBJECT_ROTATION
                and < EGameObjectFields.GAMEOBJECT_STATE:
                    go.Rotation[key - (uint)EGameObjectFields.GAMEOBJECT_ROTATION] = ToSingle(value);
                    break;
                case EGameObjectFields.GAMEOBJECT_STATE:
                    go.GoState = (GOState)ToUInt32(value);
                    break;
                case EGameObjectFields.GAMEOBJECT_POS_X:
                    go.Position.X = ToSingle(value);
                    break;
                case EGameObjectFields.GAMEOBJECT_POS_Y:
                    go.Position.Y = ToSingle(value);
                    break;
                case EGameObjectFields.GAMEOBJECT_POS_Z:
                    go.Position.Z = ToSingle(value);
                    break;
                case EGameObjectFields.GAMEOBJECT_FACING:
                    go.Facing = ToSingle(value);
                    break;
                case EGameObjectFields.GAMEOBJECT_DYN_FLAGS:
                    go.DynamicFlags = (DynamicFlags)ToUInt32(value);
                    break;
                case EGameObjectFields.GAMEOBJECT_FACTION:
                    go.FactionTemplate = ToUInt32(value);
                    break;
                case EGameObjectFields.GAMEOBJECT_TYPE_ID:
                    go.TypeId = ToUInt32(value);
                    break;
                case EGameObjectFields.GAMEOBJECT_LEVEL:
                    go.Level = ToUInt32(value);
                    break;
                case EGameObjectFields.GAMEOBJECT_ARTKIT:
                    go.ArtKit = ToUInt32(value);
                    break;
                case EGameObjectFields.GAMEOBJECT_ANIMPROGRESS:
                    go.AnimProgress = ToUInt32(value);
                    break;
            }
        }


        private static uint ToUInt32(object value) => value switch
        {
            uint u => u,
            int i => unchecked((uint)i),
            ushort us => us,
            short s => unchecked((uint)s),
            byte b => b,
            sbyte sb => unchecked((uint)sb),
            ulong ul => unchecked((uint)ul),
            long l => unchecked((uint)l),
            float f => unchecked((uint)MathF.Max(0f, f)),
            double d => unchecked((uint)Math.Max(0d, d)),
            Enum e => Convert.ToUInt32(e, CultureInfo.InvariantCulture),
            _ => Convert.ToUInt32(value, CultureInfo.InvariantCulture),
        };


        private static float ToSingle(object value) => value switch
        {
            float f => f,
            double d => (float)d,
            uint u => u,
            int i => i,
            long l => l,
            ulong ul => ul,
            Enum e => Convert.ToUInt32(e, CultureInfo.InvariantCulture),
            _ => Convert.ToSingle(value, CultureInfo.InvariantCulture),
        };


        private static void ApplyDynamicObjectFieldDiffs(
            WoWDynamicObject dyn,
            uint key,
            object value
        )
        {
            var field = (EDynamicObjectFields)key;
            switch (field)
            {
                case EDynamicObjectFields.DYNAMICOBJECT_CASTER:
                    dyn.Caster.LowGuidValue = (byte[])value;
                    break;
                case EDynamicObjectFields.DYNAMICOBJECT_CASTER + 1:
                    dyn.Caster.HighGuidValue = (byte[])value;
                    break;
                case EDynamicObjectFields.DYNAMICOBJECT_BYTES:
                    dyn.Bytes = (byte[])value;
                    break;
                case EDynamicObjectFields.DYNAMICOBJECT_SPELLID:
                    dyn.SpellId = (uint)value;
                    break;
                case EDynamicObjectFields.DYNAMICOBJECT_RADIUS:
                    dyn.Radius = (float)value;
                    break;
                case EDynamicObjectFields.DYNAMICOBJECT_POS_X:
                    dyn.Position.X = (float)value;
                    break;
                case EDynamicObjectFields.DYNAMICOBJECT_POS_Y:
                    dyn.Position.Y = (float)value;
                    break;
                case EDynamicObjectFields.DYNAMICOBJECT_POS_Z:
                    dyn.Position.Z = (float)value;
                    break;
                case EDynamicObjectFields.DYNAMICOBJECT_FACING:
                    dyn.Facing = (float)value;
                    break;
            }
        }


        private static void ApplyCorpseFieldDiffs(WoWCorpse corpse, uint key, object value)
        {
            var field = (ECorpseFields)key;
            switch (field)
            {
                case ECorpseFields.CORPSE_FIELD_OWNER:
                    corpse.OwnerGuid.LowGuidValue = (byte[])value;
                    break;
                case ECorpseFields.CORPSE_FIELD_OWNER + 1:
                    corpse.OwnerGuid.HighGuidValue = (byte[])value;
                    break;
                case ECorpseFields.CORPSE_FIELD_FACING:
                    corpse.Facing = (float)value;
                    break;
                case ECorpseFields.CORPSE_FIELD_POS_X:
                    corpse.Position.X = (float)value;
                    break;
                case ECorpseFields.CORPSE_FIELD_POS_Y:
                    corpse.Position.Y = (float)value;
                    break;
                case ECorpseFields.CORPSE_FIELD_POS_Z:
                    corpse.Position.Z = (float)value;
                    break;
                case ECorpseFields.CORPSE_FIELD_DISPLAY_ID:
                    corpse.DisplayId = (uint)value;
                    break;
                case >= ECorpseFields.CORPSE_FIELD_ITEM
                and < ECorpseFields.CORPSE_FIELD_BYTES_1:
                    corpse.Items[key - (uint)ECorpseFields.CORPSE_FIELD_ITEM] = (uint)value;
                    break;
                case ECorpseFields.CORPSE_FIELD_BYTES_1:
                    corpse.Bytes1 = (byte[])value;
                    break;
                case ECorpseFields.CORPSE_FIELD_BYTES_2:
                    corpse.Bytes2 = (byte[])value;
                    break;
                case ECorpseFields.CORPSE_FIELD_GUILD:
                    corpse.Guild = (uint)value;
                    break;
                case ECorpseFields.CORPSE_FIELD_FLAGS:
                    corpse.CorpseFlags = (CorpseFlags)value;
                    break;
                case ECorpseFields.CORPSE_FIELD_DYNAMIC_FLAGS:
                    corpse.DynamicFlags = (DynamicFlags)value;
                    break;
            }
        }


        private static void ApplyPlayerFieldDiffs(
            WoWPlayer player,
            uint key,
            object value,
            List<WoWObject> objects
        )
        {
            var field = (EPlayerFields)key;
            Log.Verbose("[ApplyPlayerFieldDiffs] Field={Field} (0x{Key:X})", field, (uint)field);
            switch (field)
            {
                case EPlayerFields.PLAYER_FIELD_THIS_WEEK_CONTRIBUTION:
                    player.ThisWeekContribution = (uint)value;
                    break;
                case EPlayerFields.PLAYER_DUEL_ARBITER:
                    player.DuelArbiter.LowGuidValue = (byte[])value;
                    break;
                case EPlayerFields.PLAYER_DUEL_ARBITER + 1:
                    player.DuelArbiter.HighGuidValue = (byte[])value;
                    break;
                case EPlayerFields.PLAYER_FLAGS:
                    player.PlayerFlags = (PlayerFlags)value;
                    break;
                case EPlayerFields.PLAYER_GUILDID:
                    player.GuildId = (uint)value;
                    break;
                case EPlayerFields.PLAYER_GUILDRANK:
                    player.GuildRank = (uint)value;
                    break;
                case EPlayerFields.PLAYER_BYTES:
                    player.PlayerBytes = (byte[])value;
                    break;
                case EPlayerFields.PLAYER_BYTES_2:
                    player.PlayerBytes2 = (byte[])value;
                    break;
                case EPlayerFields.PLAYER_BYTES_3:
                    player.PlayerBytes3 = (byte[])value;
                    break;
                case EPlayerFields.PLAYER_DUEL_TEAM:
                    player.GuildTimestamp = (uint)value;
                    break;
                case EPlayerFields.PLAYER_GUILD_TIMESTAMP:
                    player.GuildTimestamp = (uint)value;
                    break;
                case >= EPlayerFields.PLAYER_QUEST_LOG_1_1
                and <= EPlayerFields.PLAYER_QUEST_LOG_LAST_3:
                    {
                        uint questField = (field - EPlayerFields.PLAYER_QUEST_LOG_1_1) % 3;
                        int questIndex = (int)((field - EPlayerFields.PLAYER_QUEST_LOG_1_1) / 3);

                        if (questIndex >= 0 && questIndex < player.QuestLog.Length)
                        {
                            switch (questField)
                            {
                                case 0:
                                    var prevQuestId = player.QuestLog[questIndex].QuestId;
                                    player.QuestLog[questIndex].QuestId = (uint)value;
                                    if ((uint)value != prevQuestId)
                                        Log.Information("[QuestFieldDiff] QuestLog[{Index}].QuestId: {Prev} -> {New}",
                                            questIndex, prevQuestId, (uint)value);
                                    break;
                                case 1:
                                    player.QuestLog[questIndex].QuestCounters = (byte[])value;
                                    break;
                                case 2:
                                    var prevState = player.QuestLog[questIndex].QuestState;
                                    player.QuestLog[questIndex].QuestState = (uint)value;
                                    if ((uint)value != prevState)
                                        Log.Information("[QuestFieldDiff] QuestLog[{Index}].QuestState: {Prev} -> {New}",
                                            questIndex, prevState, (uint)value);
                                    break;
                            }
                        }
                        else
                        {
                            Log.Warning("[ApplyPlayerFieldDiffs] QuestLog index {Index} out of bounds (length {Length})",
                                questIndex, player.QuestLog.Length);
                        }
                    }
                    break;
                case >= EPlayerFields.PLAYER_VISIBLE_ITEM_1_CREATOR
                and <= EPlayerFields.PLAYER_VISIBLE_ITEM_19_PAD:
                    {
                        uint visibleItemField =
                            (field - EPlayerFields.PLAYER_VISIBLE_ITEM_1_CREATOR) % 12;
                        int itemIndex = (int)(
                            (field - EPlayerFields.PLAYER_VISIBLE_ITEM_1_CREATOR) / 12
                        );
                        var visibleItem = player.VisibleItems[itemIndex];
                        switch (visibleItemField)
                        {
                            case 0:
                                visibleItem.CreatedBy.LowGuidValue = (byte[])value;
                                break;
                            case 1:
                                visibleItem.CreatedBy.HighGuidValue = (byte[])value;
                                break;
                            case 2:
                                ((WoWItem)visibleItem).ItemId = (uint)value;
                                break;
                            case 3:
                                visibleItem.Owner.LowGuidValue = (byte[])value;
                                break;
                            case 4:
                                visibleItem.Owner.HighGuidValue = (byte[])value;
                                break;
                            case 5:
                                visibleItem.Contained.LowGuidValue = (byte[])value;
                                break;
                            case 6:
                                visibleItem.Contained.HighGuidValue = (byte[])value;
                                break;
                            case 7:
                                visibleItem.GiftCreator.LowGuidValue = (byte[])value;
                                break;
                            case 8:
                                visibleItem.GiftCreator.HighGuidValue = (byte[])value;
                                break;
                            case 9:
                                ((WoWItem)visibleItem).StackCount = (uint)value;
                                break;
                            case 10:
                                ((WoWItem)visibleItem).Durability = (uint)value;
                                break;
                            case 11:
                                ((WoWItem)visibleItem).PropertySeed = (uint)value;
                                break;
                        }
                    }
                    break;

                case >= EPlayerFields.PLAYER_FIELD_INV_SLOT_HEAD
                and < EPlayerFields.PLAYER_FIELD_PACK_SLOT_1:
                    {
                        var inventoryIndex = field - EPlayerFields.PLAYER_FIELD_INV_SLOT_HEAD;
                        if (inventoryIndex >= 0 && inventoryIndex < player.Inventory.Length)
                        {
                            player.Inventory[inventoryIndex] = (uint)value;

                            // If this is a 2-byte field pair representing a GUID, populate VisibleItems
                            var itemGuid = (ulong)(uint)value;
                            if (itemGuid != 0 && inventoryIndex < player.VisibleItems.Length)
                            {
                                WoWItem actualItem;
                                lock (_objectsLock) actualItem = objects.FirstOrDefault(o => o.Guid == itemGuid) as WoWItem;
                                if (actualItem != null)
                                {
                                    player.VisibleItems[inventoryIndex] = actualItem;
                                }
                                else
                                {
                                    Log.Verbose("[ApplyPlayerFieldDiffs] No item found for GUID {Guid:X} at inventory index {Index}",
                                        itemGuid, inventoryIndex);
                                }
                            }
                        }
                        else
                        {
                            Log.Warning("[ApplyPlayerFieldDiffs] inventoryIndex {Index} out of bounds (length {Length}), field {Field}",
                                inventoryIndex, player.Inventory.Length, field);
                        }
                    }
                    break;
                case >= EPlayerFields.PLAYER_FIELD_PACK_SLOT_1
                and <= EPlayerFields.PLAYER_FIELD_PACK_SLOT_LAST:
                    {
                        var packIndex = field - EPlayerFields.PLAYER_FIELD_PACK_SLOT_1;
                        if (packIndex >= 0 && packIndex < player.PackSlots.Length)
                        {
                            var oldVal = player.PackSlots[packIndex];
                            player.PackSlots[packIndex] = (uint)value;
                            if ((uint)value != 0 && oldVal == 0)
                                Log.Information("[PackSlots] index={Index} set to 0x{Value:X8} (slot {Slot})",
                                    packIndex, (uint)value, packIndex / 2);
                        }
                        else
                        {
                            Log.Warning("[ApplyPlayerFieldDiffs] packIndex {Index} out of bounds (length {Length}), field {Field}",
                                packIndex, player.PackSlots.Length, field);
                        }
                    }
                    break;
                case >= EPlayerFields.PLAYER_FIELD_BANK_SLOT_1
                and <= EPlayerFields.PLAYER_FIELD_BANK_SLOT_LAST:
                    {
                        var bankIndex = field - EPlayerFields.PLAYER_FIELD_BANK_SLOT_1;
                        if (bankIndex >= 0 && bankIndex < player.BankSlots.Length)
                        {
                            player.BankSlots[bankIndex] = (uint)value;
                        }
                        else
                        {
                            Log.Warning("[ApplyPlayerFieldDiffs] bankIndex {Index} out of bounds (length {Length}), field {Field}",
                                bankIndex, player.BankSlots.Length, field);
                        }
                    }
                    break;
                case >= EPlayerFields.PLAYER_FIELD_BANKBAG_SLOT_1
                and <= EPlayerFields.PLAYER_FIELD_BANKBAG_SLOT_LAST:
                    {
                        var bankBagIndex = field - EPlayerFields.PLAYER_FIELD_BANKBAG_SLOT_1;
                        if (bankBagIndex >= 0 && bankBagIndex < player.BankBagSlots.Length)
                        {
                            player.BankBagSlots[bankBagIndex] = (uint)value;
                        }
                        else
                        {
                            Log.Warning("[ApplyPlayerFieldDiffs] bankBagIndex {Index} out of bounds (length {Length}), field {Field}",
                                bankBagIndex, player.BankBagSlots.Length, field);
                        }
                    }
                    break;
                case >= EPlayerFields.PLAYER_FIELD_VENDORBUYBACK_SLOT_1
                and <= EPlayerFields.PLAYER_FIELD_VENDORBUYBACK_SLOT_LAST:
                    {
                        var vendorIndex = field - EPlayerFields.PLAYER_FIELD_VENDORBUYBACK_SLOT_1;
                        if (vendorIndex >= 0 && vendorIndex < player.VendorBuybackSlots.Length)
                        {
                            player.VendorBuybackSlots[vendorIndex] = (uint)value;
                        }
                        else
                        {
                            Log.Warning("[ApplyPlayerFieldDiffs] vendorIndex {Index} out of bounds (length {Length}), field {Field}",
                                vendorIndex, player.VendorBuybackSlots.Length, field);
                        }
                    }
                    break;
                case >= EPlayerFields.PLAYER_FIELD_KEYRING_SLOT_1
                and <= EPlayerFields.PLAYER_FIELD_KEYRING_SLOT_LAST:
                    {
                        var keyringIndex = field - EPlayerFields.PLAYER_FIELD_KEYRING_SLOT_1;
                        if (keyringIndex >= 0 && keyringIndex < player.KeyringSlots.Length)
                        {
                            player.KeyringSlots[keyringIndex] = (uint)value;
                        }
                        else
                        {
                            Log.Warning("[ApplyPlayerFieldDiffs] keyringIndex {Index} out of bounds (length {Length}), field {Field}",
                                keyringIndex, player.KeyringSlots.Length, field);
                        }
                    }
                    break;
                case EPlayerFields.PLAYER_FARSIGHT:
                    player.Farsight = (uint)value;
                    break;
                case EPlayerFields.PLAYER_FIELD_COMBO_TARGET:
                    player.ComboTarget.LowGuidValue = (byte[])value;
                    break;
                case EPlayerFields.PLAYER_FIELD_COMBO_TARGET + 1:
                    player.ComboTarget.HighGuidValue = (byte[])value;
                    break;
                case EPlayerFields.PLAYER_XP:
                    player.XP = (uint)value;
                    break;
                case EPlayerFields.PLAYER_NEXT_LEVEL_XP:
                    player.NextLevelXP = (uint)value;
                    break;
                case >= EPlayerFields.PLAYER_SKILL_INFO_1_1
                and <= EPlayerFields.PLAYER_SKILL_INFO_1_1 + 383:
                    {
                        // WoW 1.12.1 PLAYER_SKILL_INFO layout: INTERLEAVED, 3 fields per skill.
                        // Each skill slot occupies 3 consecutive uint32 fields:
                        //   offset + 0: SkillLine (low16) | Step (high16)   → SkillInt1
                        //   offset + 1: Current (low16)   | Max (high16)    → SkillInt2
                        //   offset + 2: TempBonus (low16) | PermBonus (high16) → SkillInt3
                        // Total: 128 skills × 3 fields = 384 fields.
                        int offset = (int)(field - EPlayerFields.PLAYER_SKILL_INFO_1_1);
                        int skillIndex = offset / 3;
                        int fieldType = offset % 3;
                        if (skillIndex < 128)
                        {
                            switch (fieldType)
                            {
                                case 0:
                                    player.SkillInfo[skillIndex].SkillInt1 = (uint)value;
                                    break;
                                case 1:
                                    player.SkillInfo[skillIndex].SkillInt2 = (uint)value;
                                    break;
                                case 2:
                                    player.SkillInfo[skillIndex].SkillInt3 = (uint)value;
                                    break;
                            }
                        }
                    }
                    break;
                case EPlayerFields.PLAYER_CHARACTER_POINTS1:
                    player.CharacterPoints1 = (uint)value;
                    break;
                case EPlayerFields.PLAYER_CHARACTER_POINTS2:
                    player.CharacterPoints2 = (uint)value;
                    break;
                case EPlayerFields.PLAYER_TRACK_CREATURES:
                    player.TrackCreatures = (uint)value;
                    break;
                case EPlayerFields.PLAYER_TRACK_RESOURCES:
                    player.TrackResources = (uint)value;
                    break;
                case EPlayerFields.PLAYER_BLOCK_PERCENTAGE:
                    player.BlockPercentage = (uint)value;
                    break;
                case EPlayerFields.PLAYER_DODGE_PERCENTAGE:
                    player.DodgePercentage = (uint)value;
                    break;
                case EPlayerFields.PLAYER_PARRY_PERCENTAGE:
                    player.ParryPercentage = (uint)value;
                    break;
                case EPlayerFields.PLAYER_CRIT_PERCENTAGE:
                    player.CritPercentage = (uint)value;
                    break;
                case EPlayerFields.PLAYER_RANGED_CRIT_PERCENTAGE:
                    player.RangedCritPercentage = (uint)value;
                    break;
                case >= EPlayerFields.PLAYER_EXPLORED_ZONES_1
                and < EPlayerFields.PLAYER_REST_STATE_EXPERIENCE:
                    player.ExploredZones[field - EPlayerFields.PLAYER_EXPLORED_ZONES_1] =
                        (uint)value;
                    break;
                case EPlayerFields.PLAYER_REST_STATE_EXPERIENCE:
                    player.RestStateExperience = (uint)value;
                    break;
                case EPlayerFields.PLAYER_FIELD_COINAGE:
                    player.Coinage = (uint)value;
                    break;
                case >= EPlayerFields.PLAYER_FIELD_POSSTAT0
                and <= EPlayerFields.PLAYER_FIELD_POSSTAT4:
                    player.StatBonusesPos[field - EPlayerFields.PLAYER_FIELD_POSSTAT0] =
                        (uint)value;
                    break;
                case >= EPlayerFields.PLAYER_FIELD_NEGSTAT0
                and <= EPlayerFields.PLAYER_FIELD_NEGSTAT4:
                    player.StatBonusesNeg[field - EPlayerFields.PLAYER_FIELD_NEGSTAT0] =
                        (uint)value;
                    break;
                case >= EPlayerFields.PLAYER_FIELD_RESISTANCEBUFFMODSPOSITIVE
                and <= EPlayerFields.PLAYER_FIELD_RESISTANCEBUFFMODSNEGATIVE + 6:
                    if (field <= EPlayerFields.PLAYER_FIELD_RESISTANCEBUFFMODSNEGATIVE)
                        player.ResistBonusesPos[
                            field - EPlayerFields.PLAYER_FIELD_RESISTANCEBUFFMODSPOSITIVE
                        ] = (uint)value;
                    else
                        player.ResistBonusesNeg[
                            field - EPlayerFields.PLAYER_FIELD_RESISTANCEBUFFMODSNEGATIVE
                        ] = (uint)value;
                    break;
                case >= EPlayerFields.PLAYER_FIELD_MOD_DAMAGE_DONE_POS
                and <= EPlayerFields.PLAYER_FIELD_MOD_DAMAGE_DONE_POS + 6:
                    player.ModDamageDonePos[
                        field - EPlayerFields.PLAYER_FIELD_MOD_DAMAGE_DONE_POS
                    ] = (uint)value;
                    break;
                case >= EPlayerFields.PLAYER_FIELD_MOD_DAMAGE_DONE_NEG
                and <= EPlayerFields.PLAYER_FIELD_MOD_DAMAGE_DONE_NEG + 6:
                    player.ModDamageDoneNeg[
                        field - EPlayerFields.PLAYER_FIELD_MOD_DAMAGE_DONE_NEG
                    ] = (uint)value;
                    break;
                case >= EPlayerFields.PLAYER_FIELD_MOD_DAMAGE_DONE_PCT
                and <= EPlayerFields.PLAYER_FIELD_MOD_DAMAGE_DONE_PCT + 6:
                    player.ModDamageDonePct[
                        field - EPlayerFields.PLAYER_FIELD_MOD_DAMAGE_DONE_PCT
                    ] = (float)value;
                    break;
                case EPlayerFields.PLAYER_AMMO_ID:
                    player.AmmoId = (uint)value;
                    break;
                case EPlayerFields.PLAYER_SELF_RES_SPELL:
                    player.SelfResSpell = (uint)value;
                    break;
                case EPlayerFields.PLAYER_FIELD_PVP_MEDALS:
                    player.PvpMedals = (uint)value;
                    break;
                case >= EPlayerFields.PLAYER_FIELD_BUYBACK_PRICE_1
                and <= EPlayerFields.PLAYER_FIELD_BUYBACK_PRICE_LAST:
                    player.BuybackPrices[field - EPlayerFields.PLAYER_FIELD_BUYBACK_PRICE_1] =
                        (uint)value;
                    break;
                case >= EPlayerFields.PLAYER_FIELD_BUYBACK_TIMESTAMP_1
                and <= EPlayerFields.PLAYER_FIELD_BUYBACK_TIMESTAMP_LAST:
                    player.BuybackTimestamps[
                        field - EPlayerFields.PLAYER_FIELD_BUYBACK_TIMESTAMP_1
                    ] = (uint)value;
                    break;
                case EPlayerFields.PLAYER_FIELD_KILLS:
                    player.SessionKills = (uint)value;
                    break;
                case EPlayerFields.PLAYER_FIELD_YESTERDAY_KILLS:
                    player.YesterdayKills = (uint)value;
                    break;
                case EPlayerFields.PLAYER_FIELD_LAST_WEEK_KILLS:
                    player.LastWeekKills = (uint)value;
                    break;
                case EPlayerFields.PLAYER_FIELD_LAST_WEEK_CONTRIBUTION:
                    player.LastWeekContribution = (uint)value;
                    break;
                case EPlayerFields.PLAYER_FIELD_LIFETIME_HONORABLE_KILLS:
                    player.LifetimeHonorableKills = (uint)value;
                    break;
                // Note: PLAYER_FIELD_LIFETIME_DISHONORABLE_KILLS (0x4E8) is a vanilla-computed
                // value that collides with the visible items range (0x4DC-0x998) in this TBC enum.
                // Dishonorable kills was removed in TBC; this field is handled by the visible items range.
                case EPlayerFields.PLAYER_FIELD_BYTES2:
                    player.FieldBytes2 = (byte[])value;
                    break;
                case EPlayerFields.PLAYER_FIELD_WATCHED_FACTION_INDEX:
                    player.WatchedFactionIndex = (uint)value;
                    break;
                case >= EPlayerFields.PLAYER_FIELD_COMBAT_RATING_1
                and <= EPlayerFields.PLAYER_FIELD_COMBAT_RATING_1 + 20:
                    player.CombatRating[field - EPlayerFields.PLAYER_FIELD_COMBAT_RATING_1] =
                        (uint)value;
                    break;
                case EPlayerFields.PLAYER_CHOSEN_TITLE:
                    player.ChosenTitle = (uint)value;
                    break;
                case EPlayerFields.PLAYER__FIELD_KNOWN_TITLES:
                    player.KnownTitles = (player.KnownTitles & 0xFFFFFFFF00000000UL) | (uint)value;
                    break;
                case EPlayerFields.PLAYER__FIELD_KNOWN_TITLES + 1:
                    player.KnownTitles = (player.KnownTitles & 0x00000000FFFFFFFFUL) | ((ulong)(uint)value << 32);
                    break;
                case EPlayerFields.PLAYER_FIELD_MOD_HEALING_DONE_POS:
                    player.ModHealingDonePos = (uint)value;
                    break;
                case EPlayerFields.PLAYER_FIELD_MOD_TARGET_RESISTANCE:
                    player.ModTargetResistance = (uint)value;
                    break;
                case EPlayerFields.PLAYER_FIELD_BYTES:
                    player.FieldBytes = (byte[])value;
                    break;
                case EPlayerFields.PLAYER_OFFHAND_CRIT_PERCENTAGE:
                    player.OffhandCritPercentage = ToSingle(value);
                    break;
                case >= EPlayerFields.PLAYER_SPELL_CRIT_PERCENTAGE1
                and <= EPlayerFields.PLAYER_SPELL_CRIT_PERCENTAGE1 + 6:
                    player.SpellCritPercentage[field - EPlayerFields.PLAYER_SPELL_CRIT_PERCENTAGE1] = ToSingle(value);
                    break;
                case >= EPlayerFields.PLAYER_FIELD_ARENA_TEAM_INFO_1_1
                and <= EPlayerFields.PLAYER_FIELD_ARENA_TEAM_INFO_1_1 + 17:
                    // TBC-only — ArenaTeamInfo, HonorCurrency, ArenaCurrency (no property needed for vanilla)
                    break;
                case EPlayerFields.PLAYER_FIELD_MOD_MANA_REGEN:
                    player.ModManaRegen = ToSingle(value);
                    break;
                case EPlayerFields.PLAYER_FIELD_MOD_MANA_REGEN_INTERRUPT:
                    player.ModManaRegenInterrupt = ToSingle(value);
                    break;
                case EPlayerFields.PLAYER_FIELD_MAX_LEVEL:
                    player.MaxLevel = (uint)value;
                    break;
                case >= EPlayerFields.PLAYER_FIELD_DAILY_QUESTS_1
                and <= EPlayerFields.PLAYER_FIELD_DAILY_QUESTS_1 + 9:
                    player.DailyQuests[field - EPlayerFields.PLAYER_FIELD_DAILY_QUESTS_1] = (uint)value;
                    break;
                case EPlayerFields.PLAYER_FIELD_PADDING:
                    // Padding field, usually ignored
                    break;
            }
        }


        private static void ApplyMovementData(WoWUnit unit, MovementInfoUpdate data)
        {
            // Diagnostic: detect when 2-arg overload writes local player position (always writes)
            if (unit is WoWLocalPlayer)
            {
                float dx = data.X - unit.Position.X;
                float dy = data.Y - unit.Position.Y;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                if (dist > 0.1f)
                {
                    Log.Warning("[POS_OVERWRITE_ADD] Server ADD writing local player pos: " +
                        "cur=({CurX:F3},{CurY:F3},{CurZ:F3}) new=({NewX:F3},{NewY:F3},{NewZ:F3}) delta={Dist:F3}",
                        unit.Position.X, unit.Position.Y, unit.Position.Z,
                        data.X, data.Y, data.Z, dist);
                }
            }
            unit.MovementFlags = data.MovementFlags;
            unit.LastUpdated = data.LastUpdated;
            unit.Position.X = data.X;
            unit.Position.Y = data.Y;
            unit.Position.Z = data.Z;
            unit.Facing = data.Facing;
            unit.TransportGuid = data.TransportGuid ?? 0;
            if (unit.TransportGuid == 0)
            {
                unit.TransportOffset = new Position(0, 0, 0);
                unit.TransportOrientation = 0f;
                unit.TransportLastUpdated = 0;
            }
            else
            {
                unit.TransportOffset = data.TransportOffset ?? unit.TransportOffset;
                unit.TransportOrientation = data.TransportOrientation ?? unit.TransportOrientation;
                unit.TransportLastUpdated = data.TransportLastUpdated ?? unit.TransportLastUpdated;
                Instance.SyncTransportPassengerWorldPosition(unit);
            }
            unit.SwimPitch = data.SwimPitch ?? 0f;
            unit.FallTime = data.FallTime;
            unit.JumpVerticalSpeed = data.JumpVerticalSpeed ?? 0f;
            unit.JumpSinAngle = data.JumpSinAngle ?? 0f;
            unit.JumpCosAngle = data.JumpCosAngle ?? 0f;
            unit.JumpHorizontalSpeed = data.JumpHorizontalSpeed ?? 0f;
            unit.SplineElevation = data.SplineElevation ?? 0f;

            if (data.MovementBlockUpdate != null)
            {
                unit.WalkSpeed = data.MovementBlockUpdate.WalkSpeed;
                unit.RunSpeed = data.MovementBlockUpdate.RunSpeed;
                unit.RunBackSpeed = data.MovementBlockUpdate.RunBackSpeed;
                unit.SwimSpeed = data.MovementBlockUpdate.SwimSpeed;
                unit.SwimBackSpeed = data.MovementBlockUpdate.SwimBackSpeed;
                unit.TurnRate = data.MovementBlockUpdate.TurnRate;
                unit.SplineFlags = data.MovementBlockUpdate.SplineFlags ?? SplineFlags.None;
                unit.SplineFinalPoint = data.MovementBlockUpdate.SplineFinalPoint ?? unit.SplineFinalPoint;
                unit.SplineTargetGuid = data.MovementBlockUpdate.SplineTargetGuid ?? 0;
                unit.SplineFinalOrientation = data.MovementBlockUpdate.SplineFinalOrientation ?? 0f;
                unit.SplineTimePassed = data.MovementBlockUpdate.SplineTimePassed ?? 0;
                unit.SplineDuration = data.MovementBlockUpdate.SplineDuration ?? 0;
                unit.SplineId = data.MovementBlockUpdate.SplineId ?? 0;
                unit.SplineNodes = data.MovementBlockUpdate.SplineNodes ?? [];
                unit.SplineFinalDestination = data.MovementBlockUpdate.SplineFinalDestination ?? unit.SplineFinalDestination;
                unit.SplineType = data.MovementBlockUpdate.SplineType;
                unit.SplineTargetGuid = data.MovementBlockUpdate.FacingTargetGuid;
                unit.FacingAngle = data.MovementBlockUpdate.FacingAngle;
                unit.FacingSpot = data.MovementBlockUpdate.FacingSpot;
                unit.SplineTimestamp = data.MovementBlockUpdate.SplineTimestamp;
                unit.SplinePoints = data.MovementBlockUpdate.SplinePoints;
            }
        }

    }
}
