using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Pathfinding;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Movement;

public static class PathfindingOverlayBuilder
{
    public const float DefaultNearbyObjectRadius = 40f;
    public const int MaxNearbyObjectCount = 64;

    private static readonly GameObjectType[] CollidableTypes =
    [
        GameObjectType.Door,
        GameObjectType.Button,
        GameObjectType.Chest,
        GameObjectType.Generic,
        GameObjectType.Goober,
        GameObjectType.Transport,
        GameObjectType.MapObject,
        GameObjectType.MapObjectTransport,
        GameObjectType.Mailbox,
        GameObjectType.AuctionHouse,
        GameObjectType.SpellCaster,
        GameObjectType.MeetingStone,
        GameObjectType.FlagStand,
        GameObjectType.FlagDrop,
        GameObjectType.CapturePoint,
        GameObjectType.DestructibleBuilding,
        GameObjectType.GuildBank,
        GameObjectType.TrapDoor
    ];

    public static DynamicObjectProto[] BuildNearbyObjects(
        IObjectManager objectManager,
        Position start,
        Position end,
        float maxDistance = DefaultNearbyObjectRadius,
        int maxObjectCount = MaxNearbyObjectCount)
    {
        ArgumentNullException.ThrowIfNull(objectManager);
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(end);

        var gameObjects = objectManager.GameObjects ?? Enumerable.Empty<IWoWGameObject>();
        var nearbyObjects = new List<(float distance, DynamicObjectProto proto)>();

        foreach (var gameObject in gameObjects)
        {
            if (!TryBuildNearbyObject(gameObject, start, end, maxDistance, out var proto, out var distance))
                continue;

            nearbyObjects.Add((distance, proto));
        }

        return nearbyObjects
            .OrderBy(candidate => candidate.distance)
            .ThenBy(candidate => candidate.proto.Guid)
            .Take(maxObjectCount)
            .Select(candidate => candidate.proto)
            .ToArray();
    }

    private static bool TryBuildNearbyObject(
        IWoWGameObject? gameObject,
        Position start,
        Position end,
        float maxDistance,
        out DynamicObjectProto proto,
        out float distance)
    {
        proto = new DynamicObjectProto();
        distance = float.MaxValue;

        if (gameObject == null)
            return false;

        try
        {
            var position = gameObject.Position;
            if (!IsFinitePosition(position))
                return false;

            if (gameObject.DisplayId == 0 || !IsCollidableType(gameObject.TypeId))
                return false;

            distance = MathF.Min(position!.DistanceTo(start), position.DistanceTo(end));
            if (distance > maxDistance)
                return false;

            proto = new DynamicObjectProto
            {
                Guid = gameObject.Guid,
                DisplayId = gameObject.DisplayId,
                X = position.X,
                Y = position.Y,
                Z = position.Z,
                Orientation = float.IsFinite(gameObject.Facing) ? gameObject.Facing : 0f,
                Scale = IsFinitePositive(gameObject.ScaleX) ? gameObject.ScaleX : 1f,
                GoState = (uint)gameObject.GoState,
            };

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsCollidableType(uint typeId)
        => Enum.IsDefined(typeof(GameObjectType), (int)typeId)
            && Array.IndexOf(CollidableTypes, (GameObjectType)typeId) >= 0;

    private static bool IsFinitePosition(Position? position)
        => position != null
            && float.IsFinite(position.X)
            && float.IsFinite(position.Y)
            && float.IsFinite(position.Z);

    private static bool IsFinitePositive(float value)
        => float.IsFinite(value) && value > 0f;
}
