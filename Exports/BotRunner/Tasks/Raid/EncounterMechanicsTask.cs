using BotRunner.Combat;
using BotRunner.Interfaces;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Serilog; // TODO: migrate to ILogger when DI is available
using System;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Tasks.Raid;

/// <summary>
/// Data-driven boss encounter mechanic responses.
/// Mechanics: spread, stack, interrupt, dispel, taunt swap.
/// Loaded from encounter definitions — each boss has a list of mechanics
/// with trigger conditions and required responses.
/// </summary>
public class EncounterMechanicsTask : BotTask, IBotTask
{
    private readonly EncounterDefinition _encounter;
    private readonly string _role; // "tank", "healer", "melee_dps", "ranged_dps"
    private readonly ulong _playerGuid;

    public EncounterMechanicsTask(IBotContext context, EncounterDefinition encounter, string role, ulong playerGuid)
        : base(context)
    {
        _encounter = encounter;
        _role = role;
        _playerGuid = playerGuid;
    }

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player == null) return;

        var boss = ObjectManager.Units
            .Where(u => u.Entry == _encounter.BossEntry && u.Health > 0)
            .FirstOrDefault();

        if (boss == null)
        {
            // Boss dead — encounter complete
            BotContext.BotTasks.Pop();
            return;
        }

        // Check each mechanic trigger
        foreach (var mechanic in _encounter.Mechanics)
        {
            if (!ShouldRespond(mechanic, boss, player))
                continue;

            ExecuteMechanic(mechanic, boss, player);
            return; // One mechanic response per tick
        }
    }

    private bool ShouldRespond(MechanicDefinition mechanic, IWoWUnit boss, IWoWLocalPlayer player)
    {
        if (!mechanic.AffectsRole(_role))
            return false;

        return mechanic.Type switch
        {
            MechanicType.Spread => HasNearbyAllies(player, mechanic.TriggerDistanceYd),
            MechanicType.Stack => player.Position.DistanceTo(mechanic.StackPoint) > mechanic.TriggerDistanceYd,
            MechanicType.Interrupt => boss.IsCasting || boss.ChannelingId > 0,
            MechanicType.Dispel => HasDispellableDebuff(player, mechanic.DebuffNames),
            MechanicType.TauntSwap => _role == "tank" && ShouldTauntSwap(boss, mechanic.TauntAtStacks),
            _ => false
        };
    }

    private void ExecuteMechanic(MechanicDefinition mechanic, IWoWUnit boss, IWoWLocalPlayer player)
    {
        switch (mechanic.Type)
        {
            case MechanicType.Spread:
                // Move away from nearest ally
                var nearestAlly = ObjectManager.Players
                    .Where(p => p.Guid != _playerGuid && p.Health > 0)
                    .OrderBy(p => p.Position.DistanceTo(player.Position))
                    .FirstOrDefault();

                if (nearestAlly != null)
                {
                    var awayAngle = MathF.Atan2(
                        player.Position.Y - nearestAlly.Position.Y,
                        player.Position.X - nearestAlly.Position.X);
                    var spreadPos = new Position(
                        player.Position.X + MathF.Cos(awayAngle) * mechanic.TriggerDistanceYd,
                        player.Position.Y + MathF.Sin(awayAngle) * mechanic.TriggerDistanceYd,
                        player.Position.Z);
                    ObjectManager.MoveToward(spreadPos);
                    Log.Debug("[ENCOUNTER] Spreading from ally");
                }
                break;

            case MechanicType.Stack:
                ObjectManager.MoveToward(mechanic.StackPoint);
                Log.Debug("[ENCOUNTER] Stacking at ({X:F0},{Y:F0})",
                    mechanic.StackPoint.X, mechanic.StackPoint.Y);
                break;

            case MechanicType.Interrupt:
                // Cast interrupt — handled by combat rotation priority
                Log.Information("[ENCOUNTER] Interrupt needed on boss cast (channeling={ChannelId})", boss.ChannelingId);
                break;

            case MechanicType.Dispel:
                Log.Information("[ENCOUNTER] Dispel needed");
                break;

            case MechanicType.TauntSwap:
                Log.Information("[ENCOUNTER] Taunt swap needed");
                break;
        }
    }

    private bool HasNearbyAllies(IWoWLocalPlayer player, float minDistance)
    {
        return ObjectManager.Players
            .Any(p => p.Guid != _playerGuid
                && p.Health > 0
                && p.Position.DistanceTo(player.Position) < minDistance);
    }

    private static bool HasDispellableDebuff(IWoWLocalPlayer player, IReadOnlyList<string> debuffNames)
    {
        return debuffNames.Any(name => player.HasDebuff(name));
    }

    private bool ShouldTauntSwap(IWoWUnit boss, int tauntAtStacks)
    {
        // Simplified: taunt swap when tank has N+ debuff stacks
        var stacks = ObjectManager.Player?.GetDebuffs()
            .Sum(d => (int)d.StackCount) ?? 0;
        return stacks >= tauntAtStacks;
    }
}

/// <summary>
/// Defines a boss encounter with its mechanics.
/// </summary>
public record EncounterDefinition(
    string BossName,
    uint BossEntry,
    IReadOnlyList<MechanicDefinition> Mechanics);

/// <summary>
/// Defines a single encounter mechanic and its trigger/response.
/// </summary>
public record MechanicDefinition
{
    public MechanicType Type { get; init; }
    public float TriggerDistanceYd { get; init; } = 10f;
    public Position StackPoint { get; init; } = new(0, 0, 0);
    public IReadOnlyList<uint> InterruptSpellEntries { get; init; } = [];
    public IReadOnlyList<string> DebuffNames { get; init; } = [];
    public int TauntAtStacks { get; init; } = 3;
    public IReadOnlyList<string> AffectedRoles { get; init; } = ["tank", "healer", "melee_dps", "ranged_dps"];

    public bool AffectsRole(string role) => AffectedRoles.Contains(role);
}

public enum MechanicType
{
    Spread,
    Stack,
    Interrupt,
    Dispel,
    TauntSwap,
}
