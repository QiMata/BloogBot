using GameData.Core.Interfaces;
using GameData.Core.Models;
using System;

namespace BotRunner.Combat;

/// <summary>
/// Positional awareness for raid encounters.
/// Melee DPS: behind boss. Ranged/healers: max range. Tanks: face boss.
/// Avoids front cleave and tail swipe zones.
/// </summary>
public static class EncounterPositioning
{
    /// <summary>Get the ideal position for melee DPS (behind the boss).</summary>
    public static Position GetMeleePosition(IWoWUnit boss, float combatReach = 5f)
    {
        var behindAngle = boss.Facing + MathF.PI; // 180° behind facing
        if (behindAngle > MathF.PI * 2) behindAngle -= MathF.PI * 2;

        var dist = boss.BoundingRadius + combatReach * 0.8f;
        return new Position(
            boss.Position.X + MathF.Cos(behindAngle) * dist,
            boss.Position.Y + MathF.Sin(behindAngle) * dist,
            boss.Position.Z);
    }

    /// <summary>Get the ideal position for ranged DPS/healers (max range).</summary>
    public static Position GetRangedPosition(IWoWUnit boss, float maxRange = 30f, float angleOffset = 0f)
    {
        var angle = boss.Facing + MathF.PI + angleOffset; // Behind + offset
        if (angle > MathF.PI * 2) angle -= MathF.PI * 2;
        if (angle < 0) angle += MathF.PI * 2;

        return new Position(
            boss.Position.X + MathF.Cos(angle) * maxRange,
            boss.Position.Y + MathF.Sin(angle) * maxRange,
            boss.Position.Z);
    }

    /// <summary>Get tank position (in front of boss, facing it).</summary>
    public static Position GetTankPosition(IWoWUnit boss, float combatReach = 5f)
    {
        var frontAngle = boss.Facing;
        var dist = boss.BoundingRadius + combatReach * 0.5f;
        return new Position(
            boss.Position.X + MathF.Cos(frontAngle) * dist,
            boss.Position.Y + MathF.Sin(frontAngle) * dist,
            boss.Position.Z);
    }

    /// <summary>Check if position is in the boss's front cleave zone (60° cone).</summary>
    public static bool IsInFrontCleaveZone(Position pos, IWoWUnit boss, float coneHalfAngle = 0.52f)
    {
        var angleToPos = MathF.Atan2(pos.Y - boss.Position.Y, pos.X - boss.Position.X);
        if (angleToPos < 0) angleToPos += MathF.PI * 2;
        var angleDiff = MathF.Abs(angleToPos - boss.Facing);
        if (angleDiff > MathF.PI) angleDiff = MathF.PI * 2 - angleDiff;
        return angleDiff <= coneHalfAngle;
    }

    /// <summary>Check if position is in the boss's tail swipe zone (behind 60° cone).</summary>
    public static bool IsInTailSwipeZone(Position pos, IWoWUnit boss, float coneHalfAngle = 0.52f)
    {
        var behindFacing = boss.Facing + MathF.PI;
        if (behindFacing > MathF.PI * 2) behindFacing -= MathF.PI * 2;
        var angleToPos = MathF.Atan2(pos.Y - boss.Position.Y, pos.X - boss.Position.X);
        if (angleToPos < 0) angleToPos += MathF.PI * 2;
        var angleDiff = MathF.Abs(angleToPos - behindFacing);
        if (angleDiff > MathF.PI) angleDiff = MathF.PI * 2 - angleDiff;
        return angleDiff <= coneHalfAngle;
    }
}
