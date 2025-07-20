using Communication;
using Game;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Google.Protobuf.Collections;

namespace GameData.Core;

public static class ProtoConverters
{
    public static Position ToProto(this Position pos)
        => new() { X = pos.X, Y = pos.Y, Z = pos.Z };

    public static WoWObject ToProto(this IWoWObject obj, uint mapId = 0, uint zoneId = 0)
        => new()
        {
            Guid = obj.Guid,
            MapId = mapId,
            ZoneId = zoneId,
            ObjectType = (uint)obj.ObjectType,
            ScaleX = obj.ScaleX,
            Facing = obj.Facing,
            Position = obj.Position?.ToProto()
        };

    public static WoWGameObject ToProto(this IWoWGameObject obj, uint mapId = 0, uint zoneId = 0)
        => new()
        {
            Base = obj.ToProto(mapId, zoneId),
            GoState = (uint)obj.GoState,
            Level = obj.Level,
            FactionTemplate = obj.FactionTemplate
        };

    public static WoWUnit ToProto(this IWoWUnit unit, uint mapId = 0, uint zoneId = 0)
    {
        var proto = new WoWUnit
        {
            GameObject = unit.ToProto(mapId, zoneId),
            TargetGuid = unit.TargetGuid,
            Health = unit.Health,
            MaxHealth = unit.MaxHealth,
            MountDisplayId = unit.MountDisplayId,
            UnitFlags = (uint)unit.UnitFlags,
            MovementFlags = (uint)unit.MovementFlags,
            NpcFlags = (uint)unit.NpcFlags
        };

        foreach (var kv in unit.Powers)
            proto.Power.Add((uint)kv.Key, kv.Value);
        foreach (var kv in unit.MaxPowers)
            proto.MaxPower.Add((uint)kv.Key, kv.Value);
        return proto;
    }

    public static WoWPlayer ToProto(this IWoWPlayer player, uint mapId = 0, uint zoneId = 0)
        => new()
        {
            Unit = player.ToProto(mapId, zoneId),
            PlayerFlags = (uint)player.PlayerFlags,
            PlayerXP = player.XP,
            TrackCreatures = player.TrackCreatures,
            TrackResources = player.TrackResources,
            BlockPercent = player.BlockPercentage,
            DodgePercent = player.DodgePercentage,
            ParryPercent = player.ParryPercentage,
            CritPercent = player.CritPercentage,
            RangedCritPercent = player.RangedCritPercentage,
            RestStateExperience = player.RestStateExperience,
            Coinage = player.Coinage,
            AmmoId = player.AmmoId,
            SelfResSpell = player.SelfResSpell,
            PvpMedals = player.PvpMedals,
            SessionKills = player.SessionKills,
            WatchedFactionIndex = player.WatchedFactionIndex
        };
}
