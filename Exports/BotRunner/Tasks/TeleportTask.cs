using BotRunner.Interfaces;
using GameData.Core.Models;
using Serilog;
using System;

namespace BotRunner.Tasks;

/// <summary>
/// Task that teleports the player to a named location using GM .tele command.
/// Sends the command via chat, waits for position change confirmation, then pops.
/// </summary>
public class TeleportTask(IBotContext botContext, string destination) : BotTask(botContext), IBotTask
{
    private const uint PlayerFlagGhost = 0x10; // PLAYER_FLAGS_GHOST
    private const uint StandStateMask = 0xFF;
    private const uint StandStateDead = 7; // UNIT_STAND_STATE_DEAD

    private readonly string _destination = destination;
    private Position? _startPosition;
    private bool _commandSent;
    private DateTime _commandSentTime;
    private const int TELEPORT_TIMEOUT_MS = 10000;

    private static bool IsDeadOrGhost(GameData.Core.Interfaces.IWoWLocalPlayer player)
    {
        var hasGhostFlag = (((uint)player.PlayerFlags) & PlayerFlagGhost) != 0;
        var standDead = player.Bytes1 != null && player.Bytes1.Length > 0 && (player.Bytes1[0] & StandStateMask) == StandStateDead;
        return player.Health == 0 || hasGhostFlag || standDead || player.InGhostForm;
    }

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player?.Position == null)
            return;

        // Never attempt chat-driven GM setup commands while dead/ghost.
        // Server rejects chat when dead and it pollutes corpse-run tests with UI errors.
        if (IsDeadOrGhost(player))
        {
            Log.Information("[TELEPORT] Skipping teleport while player is dead/ghost.");
            PopTask("DeadOrGhost");
            return;
        }

        if (!_commandSent)
        {
            _startPosition = player.Position;
            var characterName = player.Name;

            if (string.IsNullOrEmpty(characterName))
            {
                Log.Warning("[TELEPORT] Player name not available yet, waiting...");
                return;
            }

            var command = $".tele name {characterName} {_destination}";
            Log.Information($"[TELEPORT] Sending: {command}");
            ObjectManager.SendChatMessage(command);

            _commandSent = true;
            _commandSentTime = DateTime.Now;
            return;
        }

        // Check if position changed (teleport completed)
        if (_startPosition != null && player.Position.DistanceTo(_startPosition) > 10.0f)
        {
            Log.Information($"[TELEPORT] Teleport to '{_destination}' completed. New position: ({player.Position.X:F1}, {player.Position.Y:F1}, {player.Position.Z:F1})");
            PopTask("TeleportCompleted");
            return;
        }

        // Timeout check
        if ((DateTime.Now - _commandSentTime).TotalMilliseconds > TELEPORT_TIMEOUT_MS)
        {
            Log.Warning($"[TELEPORT] Teleport to '{_destination}' timed out after {TELEPORT_TIMEOUT_MS}ms. Proceeding anyway.");
            PopTask("TeleportTimeout");
        }
    }
}
