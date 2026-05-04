using System;
using System.Linq;
using Communication;

namespace BotRunner.Tests.LiveValidation;

internal sealed class LongPathingRouteBlockerGuard(TimeSpan dwellTimeout, float movementThresholdYards)
{
    private string? _zoneName;
    private uint _anchorMapId;
    private float _anchorX;
    private float _anchorY;
    private float _anchorZ;
    private DateTime _anchorUtc;

    public void Reset()
    {
        _zoneName = null;
        _anchorUtc = DateTime.MinValue;
    }

    public void FailIfBlocked(WoWActivitySnapshot? snapshot, Action<string, WoWActivitySnapshot?> fail)
    {
        if (LongPathingRouteBlockers.TryDescribeImmediateBlocker(snapshot, out var immediateReason))
        {
            Reset();
            fail(immediateReason, snapshot);
            return;
        }

        if (!LongPathingRouteBlockers.TryFindKnownBlockerZone(snapshot, out var zone))
        {
            Reset();
            return;
        }

        var position = LongPathingRouteBlockers.GetPosition(snapshot);
        if (position == null || snapshot == null)
        {
            Reset();
            return;
        }

        var now = DateTime.UtcNow;
        var moved = _zoneName == zone.Name && snapshot.CurrentMapId == _anchorMapId
            ? Distance2D(position.X, position.Y, _anchorX, _anchorY)
            : float.MaxValue;

        if (_zoneName != zone.Name || snapshot.CurrentMapId != _anchorMapId || moved > movementThresholdYards)
        {
            _zoneName = zone.Name;
            _anchorMapId = snapshot.CurrentMapId;
            _anchorX = position.X;
            _anchorY = position.Y;
            _anchorZ = position.Z;
            _anchorUtc = now;
            return;
        }

        if (now - _anchorUtc < dwellTimeout)
            return;

        fail(
            $"Known Crossroads -> Undercity pathing blocker: {zone.Name}. " +
            $"map={snapshot.CurrentMapId} anchor=({_anchorX:F1},{_anchorY:F1},{_anchorZ:F1}) " +
            $"current=({position.X:F1},{position.Y:F1},{position.Z:F1}) moved={moved:F1} " +
            $"flags=0x{snapshot.MovementData?.MovementFlags ?? 0:X} " +
            $"transport=0x{snapshot.MovementData?.TransportGuid ?? 0:X}.",
            snapshot);
    }

    private static float Distance2D(float ax, float ay, float bx, float by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}

internal static class LongPathingRouteBlockers
{
    private const uint OrgrimmarMapId = 1;
    private const string OrgrimmarZeppelinWalkTarget = "target=(1320.1,-4653.2,53.9)";

    private static readonly KnownBlockerZone[] KnownZones =
    [
        new(
            "Orgrimmar bonfire/object-collision choke after taxi landing",
            OrgrimmarMapId,
            1673.0f,
            -4334.0f,
            53.0f,
            RadiusYards: 9f,
            ZToleranceYards: 14f),
        new(
            "Orgrimmar palm-tree collision on the Valley of Strength descent",
            OrgrimmarMapId,
            1605.0f,
            -4425.2f,
            10.2f,
            RadiusYards: 10f,
            ZToleranceYards: 8f),
        new(
            "Orgrimmar steep-incline route outside the zeppelin approach",
            OrgrimmarMapId,
            1383.0f,
            -4385.0f,
            28.0f,
            RadiusYards: 14f,
            ZToleranceYards: 8f),
        new(
            "Orgrimmar tower support/flagpole object collision",
            OrgrimmarMapId,
            1371.0f,
            -4439.4f,
            30.5f,
            RadiusYards: 9f,
            ZToleranceYards: 8f),
        new(
            "Orgrimmar zeppelin tower base/deck mismatch",
            OrgrimmarMapId,
            1342.7f,
            -4641.4f,
            24.6f,
            RadiusYards: 12f,
            ZToleranceYards: 9f),
    ];

    public static bool TryDescribeImmediateBlocker(WoWActivitySnapshot? snapshot, out string reason)
    {
        reason = string.Empty;
        var diagnostic = snapshot?.RecentChatMessages
            .LastOrDefault(IsOrgrimmarZeppelinWalkDiagnostic);

        if (diagnostic == null)
            return false;

        if (diagnostic.Contains("afford=SteepClimb", StringComparison.Ordinal))
        {
            reason = "Known Crossroads -> Undercity pathing blocker: Orgrimmar walk route selected " +
                $"a steep climb that the live client cannot run up. diagnostic={Truncate(diagnostic)}";
            return true;
        }

        if (diagnostic.Contains("nav=False", StringComparison.Ordinal)
            && diagnostic.Contains("resolution=no_route", StringComparison.Ordinal)
            && diagnostic.Contains("active=none", StringComparison.Ordinal))
        {
            reason = "Known Crossroads -> Undercity pathing blocker: Orgrimmar zeppelin tower " +
                $"route resolved to the ground/base instead of the deck. diagnostic={Truncate(diagnostic)}";
            return true;
        }

        return false;
    }

    public static bool TryFindKnownBlockerZone(WoWActivitySnapshot? snapshot, out KnownBlockerZone zone)
    {
        zone = default;
        var position = GetPosition(snapshot);
        if (snapshot == null || position == null)
            return false;

        foreach (var candidate in KnownZones)
        {
            if (snapshot.CurrentMapId != candidate.MapId)
                continue;

            var dx = position.X - candidate.X;
            var dy = position.Y - candidate.Y;
            var distance = MathF.Sqrt(dx * dx + dy * dy);
            var zDelta = MathF.Abs(position.Z - candidate.Z);
            if (distance <= candidate.RadiusYards && zDelta <= candidate.ZToleranceYards)
            {
                zone = candidate;
                return true;
            }
        }

        return false;
    }

    public static Game.Position? GetPosition(WoWActivitySnapshot? snapshot)
        => snapshot?.Player?.Unit?.GameObject?.Base?.Position
            ?? snapshot?.MovementData?.Position;

    private static bool IsOrgrimmarZeppelinWalkDiagnostic(string message)
        => message.Contains("[TRAVEL_WALK_NAV]", StringComparison.Ordinal)
            && message.Contains("leg=1", StringComparison.Ordinal)
            && message.Contains(OrgrimmarZeppelinWalkTarget, StringComparison.Ordinal);

    private static string Truncate(string message)
        => message.Length <= 360 ? message : message[..360] + "...";
}

internal readonly record struct KnownBlockerZone(
    string Name,
    uint MapId,
    float X,
    float Y,
    float Z,
    float RadiusYards,
    float ZToleranceYards);
