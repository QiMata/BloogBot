using BotRunner.Interfaces;
using BotRunner.Travel;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Serilog;
using System;
using System.Linq;
using System.Threading;
using WoWSharpClient.Networking.ClientComponents;

namespace BotRunner.Tasks.Battlegrounds;

/// <summary>
/// Task that navigates to a battlemaster NPC, interacts to queue for a battleground,
/// waits for the invite, and accepts to enter the BG.
///
/// Flow:
///   1. FindBattlemaster — locate the NPC by NpcFlags (UNIT_NPC_FLAG_BATTLEMASTER = 0x100000)
///   2. MoveToBattlemaster — pathfind within interact range
///   3. InteractAndQueue — right-click NPC, send CMSG_BATTLEMASTER_JOIN
///   4. WaitForInvite — poll for SMSG_BATTLEFIELD_STATUS with WaitJoin
///   5. AcceptInvite — send CMSG_BATTLEFIELD_PORT (accept)
///   6. WaitForEntry — wait for mapId to change to the BG map
///   7. Done — pop task
/// </summary>
public class BattlegroundQueueTask : BotTask, IBotTask
{
    private enum BgState
    {
        FindBattlemaster,
        MoveToBattlemaster,
        InteractAndQueue,
        WaitForInvite,
        AcceptInvite,
        WaitForEntry,
        Done,
    }

    private readonly BattlemasterData.BattlegroundType _bgType;
    private readonly uint _expectedBgMapId;
    private readonly BattlegroundNetworkClientComponent? _bgClient;
    private BgState _state = BgState.FindBattlemaster;
    private IWoWUnit? _bmNpc;
    private ulong _bmGuid;
    private DateTime _stateEnteredAt = DateTime.UtcNow;
    private int _actionAttempts;
    private const double StateTimeoutSec = 60.0;
    private const double InviteTimeoutSec = 300.0; // BG queue can take up to 5 min

    /// <summary>
    /// Create a BG queue task.
    /// </summary>
    /// <param name="botContext">Bot context.</param>
    /// <param name="bgType">Which battleground to queue for.</param>
    /// <param name="expectedBgMapId">The instance map ID (489=WSG, 529=AB, 30=AV).</param>
    /// <param name="bgClient">The BG network client for sending queue/accept packets. Nullable for FG bots.</param>
    public BattlegroundQueueTask(IBotContext botContext, BattlemasterData.BattlegroundType bgType, uint expectedBgMapId,
        BattlegroundNetworkClientComponent? bgClient = null)
        : base(botContext)
    {
        _bgType = bgType;
        _expectedBgMapId = expectedBgMapId;
        _bgClient = bgClient;
        Log.Information("[BG-QUEUE] Task started: bgType={BgType}, expectedMap={MapId}, hasClient={HasClient}",
            bgType, expectedBgMapId, bgClient != null);
    }

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player?.Position == null)
        {
            PopTask("no_player");
            return;
        }

        // Timeout per state (except WaitForInvite which has a longer timeout)
        var timeout = _state == BgState.WaitForInvite ? InviteTimeoutSec : StateTimeoutSec;
        if ((DateTime.UtcNow - _stateEnteredAt).TotalSeconds > timeout)
        {
            Log.Warning("[BG-QUEUE] Timed out in {State} after {Sec}s", _state, timeout);
            PopTask("timeout");
            return;
        }

        // Already in the BG map — done
        if (player.MapId == _expectedBgMapId)
        {
            Log.Information("[BG-QUEUE] Already on BG map {MapId} — done!", _expectedBgMapId);
            ObjectManager.StopAllMovement();
            PopTask("already_in_bg");
            return;
        }

        switch (_state)
        {
            case BgState.FindBattlemaster:
                HandleFindBattlemaster(player);
                break;
            case BgState.MoveToBattlemaster:
                HandleMoveToBattlemaster(player);
                break;
            case BgState.InteractAndQueue:
                HandleInteractAndQueue(player);
                break;
            case BgState.WaitForInvite:
                HandleWaitForInvite();
                break;
            case BgState.AcceptInvite:
                HandleAcceptInvite();
                break;
            case BgState.WaitForEntry:
                HandleWaitForEntry(player);
                break;
            case BgState.Done:
                PopTask("bg_entered");
                break;
        }
    }

    private void HandleFindBattlemaster(IWoWPlayer player)
    {
        // Find nearby NPC with UNIT_NPC_FLAG_BATTLEMASTER (0x800 = 2048)
        const uint BattlemasterFlag = 0x800;

        _bmNpc = ObjectManager.Units
            .Where(u => u.Health > 0
                && u.Position != null
                && ((uint)u.NpcFlags & BattlemasterFlag) != 0)
            .OrderBy(u => player.Position.DistanceTo(u.Position))
            .FirstOrDefault();

        if (_bmNpc != null)
        {
            _bmGuid = _bmNpc.Guid;
            Log.Information("[BG-QUEUE] Found battlemaster: {Name} (0x{Guid:X}) at {Dist:F0}y",
                _bmNpc.Name, _bmGuid, player.Position.DistanceTo(_bmNpc.Position));
            SetState(BgState.MoveToBattlemaster);
            return;
        }

        // No battlemaster visible — navigate toward known battlemaster position.
        // guid=0 queue is rejected by VMaNGOS PassiveAnticheat ("invalid BG type").
        // The bot must interact with the actual NPC to queue properly.
        var factionStr = player.Race switch
        {
            Race.Orc or Race.Undead or Race.Tauren or Race.Troll =>
                DungeonEntryData.DungeonFaction.Horde,
            _ => DungeonEntryData.DungeonFaction.Alliance
        };
        var bmData = BattlemasterData.FindBattlemaster(_bgType, factionStr);
        if (bmData != null)
        {
            Log.Information("[BG-QUEUE] No battlemaster visible — navigating to {City} ({X:F0},{Y:F0})",
                bmData.City, bmData.Position.X, bmData.Position.Y);
            TryNavigateToward(bmData.Position, allowDirectFallback: true);
        }
        else
        {
            Log.Warning("[BG-QUEUE] No battlemaster data for bgType={BgType}", _bgType);
        }
    }

    private void HandleMoveToBattlemaster(IWoWPlayer player)
    {
        if (_bmNpc?.Position == null)
        {
            SetState(BgState.FindBattlemaster);
            return;
        }

        var dist = player.Position.DistanceTo(_bmNpc.Position);
        if (dist <= Config.NpcInteractRange)
        {
            ObjectManager.StopAllMovement();
            ClearNavigation();
            SetState(BgState.InteractAndQueue);
            return;
        }

        NavigateToward(_bmNpc.Position);
    }

    private void HandleInteractAndQueue(IWoWPlayer player)
    {
        if (!Wait.For("bg_interact", 1500, true))
            return;

        _actionAttempts++;
        if (_actionAttempts > 5)
        {
            Log.Warning("[BG-QUEUE] Too many queue attempts, aborting");
            PopTask("queue_failed");
            return;
        }

        // Target and interact with the battlemaster — opens the BG queue dialog
        ObjectManager.SetTarget(_bmGuid);
        ObjectManager.InteractWithNpcAsync(_bmGuid, CancellationToken.None)
            .GetAwaiter().GetResult();

        // Wait for the gossip/BG dialog to open before sending queue packet.
        // VMaNGOS expects CMSG_BATTLEMASTER_JOIN to come AFTER gossip interaction.
        Thread.Sleep(1000);

        // Queue for the BG via the battlemaster NPC — pass NPC GUID
        var bgAgent = _bgClient;
        if (bgAgent != null)
        {
            // Send BG MAP ID (not type ID) — VMaNGOS reads this field as mapId
            // and converts internally via GetBattleGroundTypeIdByMapId
            Log.Information("[BG-QUEUE] Sending CMSG_BATTLEMASTER_JOIN mapId={MapId} via NPC 0x{Guid:X}",
                _expectedBgMapId, _bmGuid);
            bgAgent.JoinQueueAsync(_expectedBgMapId, 0, false, CancellationToken.None, battleMasterGuid: _bmGuid)
                .GetAwaiter().GetResult();
            SetState(BgState.WaitForInvite);
        }
        else
        {
            // Fallback: interact and hope the server queues via the gossip flow
            Log.Warning("[BG-QUEUE] No BattlegroundNetworkClient — using NPC interaction only");
            SetState(BgState.WaitForInvite);
        }
    }

    private void HandleWaitForInvite()
    {
        // Check if BG network client has received an invite
        var bgAgent = _bgClient;
        if (bgAgent != null)
        {
            var bgState = bgAgent.CurrentState;
            if (bgState == WoWSharpClient.Networking.ClientComponents.BattlegroundState.Invited)
            {
                Log.Information("[BG-QUEUE] BG invite received!");
                SetState(BgState.AcceptInvite);
                return;
            }
            if (bgState == WoWSharpClient.Networking.ClientComponents.BattlegroundState.InBattleground)
            {
                Log.Information("[BG-QUEUE] Already in BG!");
                SetState(BgState.Done);
                return;
            }
        }

        // Also check by position — if mapId changed, we're in
        if (ObjectManager.Player?.MapId == _expectedBgMapId)
        {
            SetState(BgState.Done);
        }
    }

    private void HandleAcceptInvite()
    {
        if (!Wait.For("bg_accept", 1000, true))
            return;

        var bgAgent = _bgClient;
        if (bgAgent != null)
        {
            Log.Information("[BG-QUEUE] Accepting BG invite");
            bgAgent.AcceptInviteAsync(CancellationToken.None).GetAwaiter().GetResult();
            SetState(BgState.WaitForEntry);
        }
        else
        {
            Log.Warning("[BG-QUEUE] No BattlegroundNetworkClient for accept");
            PopTask("no_bg_agent");
        }
    }

    private void HandleWaitForEntry(IWoWPlayer player)
    {
        if (player.MapId == _expectedBgMapId)
        {
            Log.Information("[BG-QUEUE] Entered BG map {MapId}!", _expectedBgMapId);
            SetState(BgState.Done);
        }
    }

    private void SetState(BgState newState)
    {
        if (_state != newState)
        {
            Log.Debug("[BG-QUEUE] {Old} → {New}", _state, newState);
            _state = newState;
            _stateEnteredAt = DateTime.UtcNow;
            _actionAttempts = 0;
        }
    }
}
