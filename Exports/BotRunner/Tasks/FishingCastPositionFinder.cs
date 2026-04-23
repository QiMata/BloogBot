using GameData.Core.Models;
using System;
using WoWSharpClient.Movement;

namespace BotRunner.Tasks;

public readonly record struct FishingCastPosition(
    Position Position,
    float FacingRadians,
    float EdgeDistance,
    bool HasLineOfSight);

/// <summary>
/// Sphere-sweep cast-position finder for elevated fishing surfaces (docks,
/// piers, shoreline cliffs). Walks a ray from the bot toward the target pool,
/// samples ground Z every <see cref="EdgeProbeStep"/> yards, and picks the
/// first standoff behind a sharp drop-off. All native queries go through
/// <see cref="NativeLocalPhysics"/> directly — no PathfindingClient indirection.
/// </summary>
public static class FishingCastPositionFinder
{
    private const float EdgeProbeStep = 1.5f;
    private const float MaxProbeDistance = 32f;
    private const float EdgeDropThreshold = 3.0f;
    private const float GroundProbeHeightOffset = 4f;
    private const float GroundProbeMaxSearchDistance = 40f;
    private const float BobberLandingDistance = 18f;

    public static FishingCastPosition? FindForPool(
        uint mapId,
        Position botPosition,
        Position poolPosition)
    {
        var deltaX = poolPosition.X - botPosition.X;
        var deltaY = poolPosition.Y - botPosition.Y;
        var distanceToPool2D = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        if (distanceToPool2D < 0.1f)
            return null;

        var dirX = deltaX / distanceToPool2D;
        var dirY = deltaY / distanceToPool2D;
        var probeZ = botPosition.Z + GroundProbeHeightOffset;

        if (!TryGetGroundZ(mapId, botPosition.X, botPosition.Y, probeZ, out var previousGroundZ))
            return null;

        for (var edgeDistance = EdgeProbeStep; edgeDistance <= MaxProbeDistance; edgeDistance += EdgeProbeStep)
        {
            var candidateX = botPosition.X + (dirX * edgeDistance);
            var candidateY = botPosition.Y + (dirY * edgeDistance);
            if (!TryGetGroundZ(mapId, candidateX, candidateY, probeZ, out var currentGroundZ))
                continue;

            if ((previousGroundZ - currentGroundZ) < EdgeDropThreshold)
            {
                previousGroundZ = currentGroundZ;
                continue;
            }

            FishingCastPosition? bestCandidate = null;
            foreach (var standOffDistance in new[] { 3.0f, 4.5f, 6.0f })
            {
                var standDistance = edgeDistance - standOffDistance;
                if (standDistance < 0.1f)
                    continue;

                var standX = botPosition.X + (dirX * standDistance);
                var standY = botPosition.Y + (dirY * standDistance);
                if (!TryGetGroundZ(mapId, standX, standY, probeZ, out var standGroundZ))
                    continue;

                var standPosition = new Position(standX, standY, standGroundZ + 1.0f);
                var bobberLandingX = standX + (dirX * BobberLandingDistance);
                var bobberLandingY = standY + (dirY * BobberLandingDistance);
                if (!TryGetGroundZ(mapId, bobberLandingX, bobberLandingY, standPosition.Z + GroundProbeHeightOffset, out var bobberLandingGroundZ))
                    continue;

                var hasLineOfSight = NativeLocalPhysics.LineOfSight(
                    mapId,
                    standPosition.X, standPosition.Y, standPosition.Z + 1.5f,
                    bobberLandingX, bobberLandingY, bobberLandingGroundZ + 0.5f);
                var facing = NormalizeFacing(MathF.Atan2(poolPosition.Y - standPosition.Y, poolPosition.X - standPosition.X));
                var result = new FishingCastPosition(
                    standPosition,
                    facing,
                    standDistance,
                    hasLineOfSight);

                bestCandidate ??= result;
                if (hasLineOfSight)
                    return result;
            }

            return bestCandidate;
        }

        return null;
    }

    private static bool TryGetGroundZ(
        uint mapId,
        float x,
        float y,
        float z,
        out float groundZ)
    {
        var (probedZ, found) = NativeLocalPhysics.GetGroundZ(
            mapId, x, y, z, GroundProbeMaxSearchDistance);
        groundZ = probedZ;
        return found;
    }

    private static float NormalizeFacing(float radians)
        => radians >= 0f ? radians : radians + (MathF.PI * 2f);
}
