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
/// Pool-centric cast-position finder. Fishing pools sit in the water *below* the
/// pier, so the task is: find a spot on the pier to stand, and cast downward into
/// the water where the pool is. We ring-sweep ground-Z probes at the bobber landing
/// distance around the pool, keep only points that are on the same pier layer as
/// the bot, and LOS-test each one from the player's eye-height *down* to the water
/// surface over the pool. The closest LOS-clear candidate wins.
/// </summary>
public static class FishingCastPositionFinder
{
    private const float BobberLandingDistance = 18f;
    private const int AngularSteps = 16;

    // Candidate must be on the same pier surface the bot is standing on.
    private const float PierLayerZTolerance = 3f;

    // Probing parameters.
    private const float ProbeStartHeightAboveBot = 4f;
    private const float ProbeSearchRange = 15f;

    // LOS endpoints: player's eye height above their feet, and a thin margin above
    // the water surface at the pool (where the bobber actually lands).
    private const float EyeHeightAboveFeet = 1.8f;
    private const float WaterAimHeightAbovePoolZ = 0.5f;

    // MaNGOS often reports water-pool spawn Z as 0, even when the water surface sits
    // higher than 0. If the descriptor's Z isn't meaningfully below the bot, use
    // the bot's Z minus a fixed waterline drop as the LOS aim height instead — that
    // way the LOS ray still points downward toward the water like the bobber's arc.
    private const float FallbackWaterlineBelowPier = 4f;

    // Margin around the candidate that must also be on the pier layer. Prevents the
    // resolver from picking a pixel-perfect "on-pier" spot that sits a hair from the
    // pier edge — the bot's body occupies ~1y and WoW physics slides it off if the
    // standoff has water or a drop-off on the adjacent side.
    private const float PierMarginProbeOffset = 1.5f;
    private const int PierMarginProbeDirections = 4;

    public static FishingCastPosition? FindForPool(
        uint mapId,
        Position botPosition,
        Position poolPosition,
        Func<Position, bool>? isReachableFromBot = null)
    {
        var pierZ = botPosition.Z;
        var probeStartZ = pierZ + ProbeStartHeightAboveBot;
        var waterAimZ = poolPosition.Z < (pierZ - 1f)
            ? poolPosition.Z + WaterAimHeightAbovePoolZ
            : pierZ - FallbackWaterlineBelowPier;

        FishingCastPosition? best = null;
        var bestDistanceToPlayer = float.MaxValue;

        for (var a = 0; a < AngularSteps; a++)
        {
            var angle = a * (MathF.PI * 2f / AngularSteps);
            var cos = MathF.Cos(angle);
            var sin = MathF.Sin(angle);
            var candX = poolPosition.X + (cos * BobberLandingDistance);
            var candY = poolPosition.Y + (sin * BobberLandingDistance);

            var (groundZ, found) = NativeLocalPhysics.GetGroundZ(mapId, candX, candY, probeStartZ, ProbeSearchRange);
            if (!found)
                continue;

            // Same-pier check: surface Z must be close to where the bot is standing.
            if (MathF.Abs(groundZ - pierZ) > PierLayerZTolerance)
                continue;

            // Pier-margin check: the bot's body occupies ~1y; if any of the immediate
            // neighbors is off-pier (water, pier drop-off) the bot slides off when it
            // arrives. Reject candidates right at the pier edge — we want the spot
            // where the bot has walking room on every side.
            if (!HasPierMarginAllAround(mapId, candX, candY, probeStartZ, pierZ))
                continue;

            var standoff = new Position(candX, candY, groundZ);

            // Eye-level LOS from the stand point down to water-over-pool. If a pier
            // pillar/mast sits between the bot and the pool it blocks this ray.
            var losClear = NativeLocalPhysics.LineOfSight(
                mapId,
                standoff.X, standoff.Y, standoff.Z + EyeHeightAboveFeet,
                poolPosition.X, poolPosition.Y, waterAimZ);
            if (!losClear)
                continue;

            // Optional caller-supplied reachability validator (navmesh path check).
            if (isReachableFromBot != null && !isReachableFromBot(standoff))
                continue;

            var distToPlayer = standoff.DistanceTo2D(botPosition);
            if (distToPlayer >= bestDistanceToPlayer)
                continue;

            var facing = NormalizeFacing(MathF.Atan2(
                poolPosition.Y - standoff.Y,
                poolPosition.X - standoff.X));
            best = new FishingCastPosition(standoff, facing, BobberLandingDistance, HasLineOfSight: true);
            bestDistanceToPlayer = distToPlayer;
        }

        return best;
    }

    private static bool HasPierMarginAllAround(uint mapId, float cx, float cy, float probeStartZ, float pierZ)
    {
        // Sample ground Z at N cardinal offsets of PierMarginProbeOffset yards. If any
        // sample is missing or off-pier, the candidate is at an edge and the bot will
        // slide off when physics engages.
        for (var i = 0; i < PierMarginProbeDirections; i++)
        {
            var angle = i * (MathF.PI * 2f / PierMarginProbeDirections);
            var nx = cx + (MathF.Cos(angle) * PierMarginProbeOffset);
            var ny = cy + (MathF.Sin(angle) * PierMarginProbeOffset);
            var (neighborZ, neighborFound) = NativeLocalPhysics.GetGroundZ(mapId, nx, ny, probeStartZ, ProbeSearchRange);
            if (!neighborFound)
                return false;
            if (MathF.Abs(neighborZ - pierZ) > PierLayerZTolerance)
                return false;
        }
        return true;
    }

    private static float NormalizeFacing(float radians)
        => radians >= 0f ? radians : radians + (MathF.PI * 2f);
}
