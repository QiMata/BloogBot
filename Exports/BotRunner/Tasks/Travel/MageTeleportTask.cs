using BotRunner.Interfaces;
using BotRunner.Travel;
using GameData.Core.Enums;
using GameData.Core.Models;
using Serilog;
using System;
using System.Linq;

namespace BotRunner.Tasks.Travel;

/// <summary>
/// Casts a mage Teleport spell to instantly travel to a capital city.
/// Steps:
/// 1. Check if spell is known and off cooldown.
/// 2. Stop movement.
/// 3. Cast the teleport spell.
/// 4. Detect teleport (mapId change or position delta >100y).
/// 5. Pop task.
/// </summary>
public class MageTeleportTask : BotTask, IBotTask
{
    private enum TeleState { Check, StopAndCast, WaitForCast, DetectTeleport, Complete }

    private TeleState _state = TeleState.Check;
    private readonly uint _spellId;
    private readonly string _spellName;
    private long _castStartMs;
    private Position? _startPosition;
    private uint _startMapId;
    private const int CastDurationMs = 10_000;
    private const int TeleportDetectTimeoutMs = 15_000;
    private const float TeleportDistanceThreshold = 100f;

    public MageTeleportTask(IBotContext context, uint spellId, string spellName)
        : base(context)
    {
        _spellId = spellId;
        _spellName = spellName;
    }

    /// <summary>
    /// Create a task for a specific destination using MageTeleportData.
    /// Returns null if the spell data isn't found.
    /// </summary>
    public static MageTeleportTask? ForDestination(IBotContext context, string destinationName)
    {
        var data = MageTeleportData.GetAllSpells()
            .FirstOrDefault(t => t.DestinationName.Equals(destinationName, StringComparison.OrdinalIgnoreCase));
        if (data == null) return null;
        return new MageTeleportTask(context, (uint)data.SpellId, data.DestinationName);
    }

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player == null) return;

        if (player.IsInCombat)
        {
            Log.Warning("[MageTeleport] Cancelled — in combat.");
            BotContext.BotTasks.Pop();
            return;
        }

        switch (_state)
        {
            case TeleState.Check:
                if (player.Class != Class.Mage)
                {
                    Log.Warning("[MageTeleport] Character is not a Mage.");
                    BotContext.BotTasks.Pop();
                    return;
                }

                if (!ObjectManager.IsSpellReady(_spellName))
                {
                    Log.Warning("[MageTeleport] Spell '{Name}' (id={SpellId}) not ready or not known.", _spellName, _spellId);
                    BotContext.BotTasks.Pop();
                    return;
                }

                _startPosition = new Position(player.Position.X, player.Position.Y, player.Position.Z);
                _startMapId = player.MapId;
                _state = TeleState.StopAndCast;
                break;

            case TeleState.StopAndCast:
                ObjectManager.StopMovement(ControlBits.Front | ControlBits.Back | ControlBits.StrafeLeft | ControlBits.StrafeRight);
                ObjectManager.CastSpell(_spellName);
                _castStartMs = Environment.TickCount64;
                _state = TeleState.WaitForCast;
                Log.Information("[MageTeleport] Casting {SpellName}...", _spellName);
                break;

            case TeleState.WaitForCast:
                if (Environment.TickCount64 - _castStartMs > CastDurationMs)
                    _state = TeleState.DetectTeleport;

                if (!player.IsChanneling && !player.IsCasting
                    && Environment.TickCount64 - _castStartMs > 2000)
                {
                    Log.Warning("[MageTeleport] Cast interrupted.");
                    BotContext.BotTasks.Pop();
                    return;
                }
                break;

            case TeleState.DetectTeleport:
                if (_startPosition == null) { _state = TeleState.Complete; return; }

                var dist = player.Position.DistanceTo(_startPosition);
                if (dist > TeleportDistanceThreshold || player.MapId != _startMapId)
                {
                    Log.Information("[MageTeleport] Teleported! Distance={Dist:F0}y, mapChanged={Changed}",
                        dist, player.MapId != _startMapId);
                    _state = TeleState.Complete;
                    return;
                }

                if (Environment.TickCount64 - _castStartMs > TeleportDetectTimeoutMs)
                {
                    Log.Warning("[MageTeleport] Teleport detection timeout.");
                    _state = TeleState.Complete;
                }
                break;

            case TeleState.Complete:
                BotContext.BotTasks.Pop();
                break;
        }
    }
}
