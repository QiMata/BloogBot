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
    private const double InviteTimeoutSec = 300.0;

    public BattlegroundQueueTask(
        IBotContext botContext,
        BattlemasterData.BattlegroundType bgType,
        uint expectedBgMapId,
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

        var timeout = _state == BgState.WaitForInvite ? InviteTimeoutSec : StateTimeoutSec;
        if ((DateTime.UtcNow - _stateEnteredAt).TotalSeconds > timeout)
        {
            Log.Warning("[BG-QUEUE] Timed out in {State} after {Sec}s", _state, timeout);
            PopTask("timeout");
            return;
        }

        if (player.MapId == _expectedBgMapId)
        {
            Log.Information("[BG-QUEUE] Already on BG map {MapId} - done!", _expectedBgMapId);
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
                HandleInteractAndQueue();
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
        var faction = player.Race switch
        {
            Race.Orc or Race.Undead or Race.Tauren or Race.Troll => DungeonEntryData.DungeonFaction.Horde,
            _ => DungeonEntryData.DungeonFaction.Alliance
        };
        var bmData = BattlemasterData.FindBattlemaster(_bgType, faction);

        if (bmData != null)
        {
            const uint battlemasterFlag = (uint)NPCFlags.UNIT_NPC_FLAG_BATTLEMASTER;

            _bmNpc = ObjectManager.Units
                .Where(u => u.Health > 0
                    && u.Position != null
                    && u.Entry == bmData.NpcEntry
                    && ((uint)u.NpcFlags & battlemasterFlag) != 0)
                .OrderBy(u => u.Position!.DistanceTo(bmData.Position))
                .ThenBy(u => player.Position.DistanceTo(u.Position))
                .FirstOrDefault();

            if (_bmNpc == null)
            {
                // npc_flags can lag after teleports, but the creature entry is stable.
                _bmNpc = ObjectManager.Units
                    .Where(u => u.Health > 0
                        && u.Position != null
                        && u.Entry == bmData.NpcEntry
                        && u.Position.DistanceTo(bmData.Position) < 30f)
                    .OrderBy(u => u.Position!.DistanceTo(bmData.Position))
                    .ThenBy(u => player.Position.DistanceTo(u.Position))
                    .FirstOrDefault();
            }
        }
        else
        {
            Log.Warning("[BG-QUEUE] No battlemaster data for bgType={BgType}, faction={Faction}; falling back to nearest battlemaster flag",
                _bgType, faction);
        }

        if (_bmNpc == null && bmData == null)
        {
            _bmNpc = ObjectManager.Units
                .Where(u => u.Health > 0
                    && u.Position != null
                    && ((uint)u.NpcFlags & (uint)NPCFlags.UNIT_NPC_FLAG_BATTLEMASTER) != 0)
                .OrderBy(u => player.Position.DistanceTo(u.Position))
                .FirstOrDefault();
        }

        if (_bmNpc == null && bmData != null && player.Position.DistanceTo(bmData.Position) < 15f)
        {
            _bmGuid = bmData.PackedGuid;
            Log.Information("[BG-QUEUE] Using known packed GUID 0x{Guid:X} for {NpcName} ({NpcTitle}, entry={Entry})",
                _bmGuid, bmData.NpcName, bmData.NpcTitle, bmData.NpcEntry);
            SetState(BgState.InteractAndQueue);
            return;
        }

        if (_bmNpc != null)
        {
            _bmGuid = _bmNpc.Guid;
            var source = ((uint)_bmNpc.NpcFlags & (uint)NPCFlags.UNIT_NPC_FLAG_BATTLEMASTER) != 0 ? "npc_flags" : "entry";
            Log.Information("[BG-QUEUE] Found battlemaster: {Name} ({NpcTitle}, entry={Entry}, 0x{Guid:X}) at {Dist:F0}y via {Source} flags=0x{Flags:X}",
                _bmNpc.Name,
                bmData?.NpcTitle ?? "unknown title",
                _bmNpc.Entry,
                _bmGuid,
                player.Position.DistanceTo(_bmNpc.Position),
                source,
                (uint)_bmNpc.NpcFlags);
            SetState(BgState.MoveToBattlemaster);
            return;
        }

        if (bmData != null)
        {
            var distToKnown = player.Position.DistanceTo(bmData.Position);
            if (distToKnown < 30f)
            {
                if (!Wait.For("bm_wait_nearby", 2000, true))
                    return;
                Log.Debug("[BG-QUEUE] Waiting for {NpcName} ({NpcTitle}) near {City} ({Dist:F0}y away)",
                    bmData.NpcName, bmData.NpcTitle, bmData.City, distToKnown);
                return;
            }

            if (!Wait.For("bm_navigate", 3000, true))
                return;
            Log.Information("[BG-QUEUE] No battlemaster visible - navigating to {NpcName} ({NpcTitle}) in {City} ({X:F0},{Y:F0})",
                bmData.NpcName, bmData.NpcTitle, bmData.City, bmData.Position.X, bmData.Position.Y);
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

    private void HandleInteractAndQueue()
    {
        if (!Wait.For("bg_interact", 1500, true))
            return;

        if (ShouldWaitForLeaderGroupQueue())
        {
            Log.Information("[BG-QUEUE] Grouped member detected - waiting for leader queue instead of interacting.");
            SetState(BgState.WaitForInvite);
            return;
        }

        _actionAttempts++;
        if (_actionAttempts > 5)
        {
            Log.Warning("[BG-QUEUE] Too many queue attempts, aborting");
            PopTask("queue_failed");
            return;
        }

        var joinAsGroup = ShouldQueueAsGroup();
        ObjectManager.SetTarget(_bmGuid);
        ObjectManager.InteractWithNpcAsync(_bmGuid, CancellationToken.None)
            .GetAwaiter().GetResult();

        Thread.Sleep(500);

        var gossipFrame = ObjectManager.GossipFrame;
        if (gossipFrame?.IsOpen == true)
        {
            Log.Information("[BG-QUEUE] Selecting battlemaster gossip option on NPC 0x{Guid:X}", _bmGuid);
            gossipFrame.SelectFirstGossipOfType(DialogType.battlemaster);
            Thread.Sleep(500);
        }

        if (_bgClient != null)
        {
            Log.Information("[BG-QUEUE] Sending CMSG_BATTLEMASTER_JOIN mapId={MapId} via NPC 0x{Guid:X} asGroup={AsGroup}",
                _expectedBgMapId, _bmGuid, joinAsGroup);
            _bgClient.JoinQueueAsync(_expectedBgMapId, 0, joinAsGroup, CancellationToken.None, battleMasterGuid: _bmGuid)
                .GetAwaiter().GetResult();
            SetState(BgState.WaitForInvite);
        }
        else
        {
            Log.Information("[BG-QUEUE] No BattlegroundNetworkClient - using foreground battlemaster UI path (asGroup={AsGroup})",
                joinAsGroup);
            ObjectManager.JoinBattleGroundQueue();
            SetState(BgState.WaitForInvite);
        }
    }

    private void HandleWaitForInvite()
    {
        if (_bgClient != null)
        {
            var bgState = _bgClient.CurrentState;
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
        else if (Wait.For("bg_accept_fg", 1000, true))
        {
            ObjectManager.AcceptBattlegroundInvite();
        }

        if (ObjectManager.Player?.MapId == _expectedBgMapId)
            SetState(BgState.Done);
    }

    private void HandleAcceptInvite()
    {
        if (!Wait.For("bg_accept", 1000, true))
            return;

        if (_bgClient != null)
        {
            Log.Information("[BG-QUEUE] Accepting BG invite");
            _bgClient.AcceptInviteAsync(CancellationToken.None).GetAwaiter().GetResult();
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
            Log.Debug("[BG-QUEUE] {Old} -> {New}", _state, newState);
            _state = newState;
            _stateEnteredAt = DateTime.UtcNow;
            _actionAttempts = 0;
        }
    }

    private bool ShouldQueueAsGroup()
    {
        if (RequiresIndividualQueue())
            return false;

        var player = ObjectManager.Player;
        if (player == null)
            return false;

        if (ObjectManager.PartyLeaderGuid != player.Guid)
            return false;

        return ObjectManager.Party1Guid != 0
            || ObjectManager.Party2Guid != 0
            || ObjectManager.Party3Guid != 0
            || ObjectManager.Party4Guid != 0
            || ObjectManager.PartyMembers.Skip(1).Any();
    }

    private bool ShouldWaitForLeaderGroupQueue()
    {
        if (RequiresIndividualQueue())
            return false;

        var player = ObjectManager.Player;
        if (player == null)
            return false;

        var leaderGuid = ObjectManager.PartyLeaderGuid;
        return leaderGuid != 0 && leaderGuid != player.Guid;
    }

    private bool RequiresIndividualQueue()
    {
        // VMaNGOS AV queuing is not reliable via one leader-side group join for the full staged raid.
        // Each AV participant needs to talk to the battlemaster and send their own queue request.
        return _bgType == BattlemasterData.BattlegroundType.AlteracValley;
    }
}
