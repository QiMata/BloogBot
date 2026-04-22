using BotRunner.Native;
using GameData.Core.Models;
using System;
using System.Runtime.InteropServices;

namespace BotRunner.Tasks;

public readonly record struct FishingCastPosition(
    Position Position,
    float FacingRadians,
    float EdgeDistance,
    bool HasLineOfSight);

public static class FishingCastPositionFinder
{
    private const float InvalidGroundZ = -50000f;
    private const float EdgeProbeStep = 1.5f;
    private const float MaxProbeDistance = 32f;
    private const float EdgeDropThreshold = 3.0f;
    private const float GroundProbeHeightOffset = 4f;
    private const float GroundProbeMaxSearchDistance = 40f;
    private const float BobberLandingDistance = 18f;

    private static bool _nativeUnavailable;

    public static FishingCastPosition? FindForPool(
        uint mapId,
        Position botPosition,
        Position poolPosition)
    {
        if (_nativeUnavailable)
            return null;

        var deltaX = poolPosition.X - botPosition.X;
        var deltaY = poolPosition.Y - botPosition.Y;
        var distanceToPool2D = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        if (distanceToPool2D < 0.1f)
            return null;

        var dirX = deltaX / distanceToPool2D;
        var dirY = deltaY / distanceToPool2D;
        var probeZ = botPosition.Z + GroundProbeHeightOffset;

        if (!TryGetGroundZ(mapId, botPosition.X, botPosition.Y, probeZ, GroundProbeMaxSearchDistance, out var previousGroundZ))
            return null;

        for (var edgeDistance = EdgeProbeStep; edgeDistance <= MaxProbeDistance; edgeDistance += EdgeProbeStep)
        {
            var candidateX = botPosition.X + (dirX * edgeDistance);
            var candidateY = botPosition.Y + (dirY * edgeDistance);
            if (!TryGetGroundZ(mapId, candidateX, candidateY, probeZ, GroundProbeMaxSearchDistance, out var currentGroundZ))
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
                if (!TryGetGroundZ(mapId, standX, standY, probeZ, GroundProbeMaxSearchDistance, out var standGroundZ))
                    continue;

                var standPosition = new Position(standX, standY, standGroundZ + 1.0f);
                var bobberLandingX = standX + (dirX * BobberLandingDistance);
                var bobberLandingY = standY + (dirY * BobberLandingDistance);
                if (!TryGetGroundZ(mapId, bobberLandingX, bobberLandingY, standPosition.Z + GroundProbeHeightOffset, GroundProbeMaxSearchDistance, out var bobberLandingGroundZ))
                    continue;

                var hasLineOfSight = TryLineOfSight(
                    mapId,
                    standPosition.X,
                    standPosition.Y,
                    standPosition.Z + 1.5f,
                    bobberLandingX,
                    bobberLandingY,
                    bobberLandingGroundZ + 0.5f);
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
        float maxSearchDist,
        out float groundZ)
    {
        groundZ = default;
        if (_nativeUnavailable)
            return false;

        try
        {
            NavigationDllResolver.Register();
            groundZ = GetGroundZNative(mapId, x, y, z, maxSearchDist);
            return groundZ > InvalidGroundZ;
        }
        catch
        {
            _nativeUnavailable = true;
            groundZ = default;
            return false;
        }
    }

    private static bool TryLineOfSight(
        uint mapId,
        float fromX,
        float fromY,
        float fromZ,
        float toX,
        float toY,
        float toZ)
    {
        if (_nativeUnavailable)
            return false;

        try
        {
            NavigationDllResolver.Register();
            return LineOfSightNative(mapId, fromX, fromY, fromZ, toX, toY, toZ);
        }
        catch
        {
            _nativeUnavailable = true;
            return false;
        }
    }

    private static float NormalizeFacing(float radians)
        => radians >= 0f ? radians : radians + (MathF.PI * 2f);

    [DllImport("Navigation", CallingConvention = CallingConvention.Cdecl, EntryPoint = "GetGroundZ")]
    private static extern float GetGroundZNative(uint mapId, float x, float y, float z, float maxSearchDist);

    [DllImport("Navigation", CallingConvention = CallingConvention.Cdecl, EntryPoint = "LineOfSight")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool LineOfSightNative(uint mapId, float fx, float fy, float fz, float tx, float ty, float tz);
}
