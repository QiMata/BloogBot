using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Communication;
using Game;
using Microsoft.Extensions.Logging.Abstractions;
using WoWStateManager.Coordination;
using WoWStateManager.Settings;
using Xunit;

namespace BotRunner.Tests;

/// <summary>
/// P3.4 + P3.5 coverage for <see cref="BattlegroundCoordinator"/>:
/// - ApplyingLoadouts dispatches ApplyLoadout exactly once per bot with a spec,
///   auto-readies no-spec accounts, and gates on snapshot.LoadoutStatus.
/// - WaitingForRaidFormation trivially passes when no faction grouping is
///   required; otherwise waits on PartyLeaderGuid parity.
/// - LoadoutFailed bots are exposed via <see cref="BattlegroundCoordinator.ExcludedAccounts"/>.
/// </summary>
public class BattlegroundCoordinatorLoadoutTests
{
    private const uint WsgBgType = 2;
    private const uint WsgMapId = 489;
    private const uint AvBgType = 1;
    private const uint AvMapId = 30;
    private const int StagingMapId = 1;

    [Fact]
    public void ApplyingLoadouts_DispatchesApplyLoadout_OncePerAccountWithSpec()
    {
        var loadouts = new Dictionary<string, LoadoutSpecSettings>(StringComparer.OrdinalIgnoreCase)
        {
            ["BGLEADER"] = new() { TargetLevel = 60 },
            ["BGMEMBER1"] = new() { TargetLevel = 60 },
            ["BGMEMBER2"] = new() { TargetLevel = 60 },
        };
        var coordinator = BuildCoordinator(AvBgType, AvMapId, loadouts: loadouts);
        var snapshots = ReadySnapshots("BGLEADER", "BGMEMBER1", "BGMEMBER2", StagingMapId);

        // First poll: WaitingForBots → ApplyingLoadouts; returns ApplyLoadout for leader.
        var leaderAction = coordinator.GetAction("BGLEADER", snapshots);
        Assert.Equal(BattlegroundCoordinator.CoordState.ApplyingLoadouts, coordinator.State);
        Assert.Equal(ActionType.ApplyLoadout, leaderAction?.ActionType);
        Assert.NotNull(leaderAction!.LoadoutSpec);

        // Second poll for leader: already sent, stays in state (waits for Ready).
        Assert.Null(coordinator.GetAction("BGLEADER", snapshots));

        // Members each get their own ApplyLoadout.
        Assert.Equal(ActionType.ApplyLoadout, coordinator.GetAction("BGMEMBER1", snapshots)?.ActionType);
        Assert.Equal(ActionType.ApplyLoadout, coordinator.GetAction("BGMEMBER2", snapshots)?.ActionType);
    }

    [Fact]
    public void ApplyingLoadouts_AutoReadiesAccountsWithoutSpec_FastForwardsToQueue()
    {
        // AV (no faction grouping) + no loadout specs → chain-advance through
        // ApplyingLoadouts + WaitingForRaidFormation on the first poll.
        var coordinator = BuildCoordinator(AvBgType, AvMapId, loadouts: null);
        var snapshots = ReadySnapshots("BGLEADER", "BGMEMBER1", "BGMEMBER2", StagingMapId);

        var leaderAction = coordinator.GetAction("BGLEADER", snapshots);

        Assert.Equal(BattlegroundCoordinator.CoordState.QueueForBattleground, coordinator.State);
        Assert.Null(leaderAction);
    }

    [Fact]
    public void ApplyingLoadouts_TransitionsWhenAllBotsReportLoadoutReady()
    {
        var loadouts = new Dictionary<string, LoadoutSpecSettings>(StringComparer.OrdinalIgnoreCase)
        {
            ["BGLEADER"] = new() { TargetLevel = 60 },
            ["BGMEMBER1"] = new() { TargetLevel = 60 },
        };
        var coordinator = BuildCoordinator(AvBgType, AvMapId, loadouts: loadouts, accounts: new[] { "BGLEADER", "BGMEMBER1" });
        var snapshots = ReadySnapshots("BGLEADER", "BGMEMBER1", mapId: StagingMapId);

        // Drain the ApplyLoadout dispatch for each bot.
        Assert.Equal(ActionType.ApplyLoadout, coordinator.GetAction("BGLEADER", snapshots)?.ActionType);
        Assert.Equal(ActionType.ApplyLoadout, coordinator.GetAction("BGMEMBER1", snapshots)?.ActionType);

        // Not yet ready → stays in ApplyingLoadouts.
        SetLoadoutStatus(snapshots, "BGLEADER", LoadoutStatus.LoadoutInProgress);
        SetLoadoutStatus(snapshots, "BGMEMBER1", LoadoutStatus.LoadoutInProgress);
        Assert.Null(coordinator.GetAction("BGLEADER", snapshots));
        Assert.Equal(BattlegroundCoordinator.CoordState.ApplyingLoadouts, coordinator.State);

        // Both report Ready → chain advances past WaitingForRaidFormation (AV) to QueueForBattleground.
        SetLoadoutStatus(snapshots, "BGLEADER", LoadoutStatus.LoadoutReady);
        SetLoadoutStatus(snapshots, "BGMEMBER1", LoadoutStatus.LoadoutReady);
        Assert.Null(coordinator.GetAction("BGLEADER", snapshots));
        Assert.Equal(BattlegroundCoordinator.CoordState.QueueForBattleground, coordinator.State);
    }

    [Fact]
    public void ApplyingLoadouts_LoadoutFailedBotsLandOnExclusionList_StillAdvancesToNextState()
    {
        var loadouts = new Dictionary<string, LoadoutSpecSettings>(StringComparer.OrdinalIgnoreCase)
        {
            ["BGLEADER"] = new() { TargetLevel = 60 },
            ["BGMEMBER1"] = new() { TargetLevel = 60 },
        };
        var coordinator = BuildCoordinator(AvBgType, AvMapId, loadouts: loadouts, accounts: new[] { "BGLEADER", "BGMEMBER1" });
        var snapshots = ReadySnapshots("BGLEADER", "BGMEMBER1", mapId: StagingMapId);

        coordinator.GetAction("BGLEADER", snapshots);
        coordinator.GetAction("BGMEMBER1", snapshots);

        SetLoadoutStatus(snapshots, "BGLEADER", LoadoutStatus.LoadoutReady);
        SetLoadoutStatus(snapshots, "BGMEMBER1", LoadoutStatus.LoadoutFailed, failureReason: "out of bag space");

        Assert.Null(coordinator.GetAction("BGLEADER", snapshots));
        Assert.Equal(BattlegroundCoordinator.CoordState.QueueForBattleground, coordinator.State);
        Assert.Contains("BGMEMBER1", coordinator.ExcludedAccounts);
        Assert.DoesNotContain("BGLEADER", coordinator.ExcludedAccounts);
    }

    [Fact]
    public void ApplyingLoadouts_StampsCorrelationId_OnDispatchedApplyLoadout()
    {
        // P5.1: coordinator must pre-stamp an ApplyLoadout CorrelationId so the
        // downstream ACK flow can be correlated even if the listener's sequence
        // stamper doesn't run (e.g. unit-test paths). The stamp also anchors the
        // coordinator's own ACK bookkeeping.
        var loadouts = new Dictionary<string, LoadoutSpecSettings>(StringComparer.OrdinalIgnoreCase)
        {
            ["BGLEADER"] = new() { TargetLevel = 60 },
        };
        var coordinator = BuildCoordinator(AvBgType, AvMapId, loadouts: loadouts, accounts: new[] { "BGLEADER", "BGMEMBER1" });
        var snapshots = ReadySnapshots("BGLEADER", "BGMEMBER1", mapId: StagingMapId);

        var action = coordinator.GetAction("BGLEADER", snapshots);

        Assert.NotNull(action);
        Assert.Equal(ActionType.ApplyLoadout, action!.ActionType);
        Assert.False(string.IsNullOrWhiteSpace(action.CorrelationId), "ApplyLoadout action must carry a coordinator-stamped CorrelationId");
        Assert.StartsWith("bg-coord:loadout:BGLEADER:", action.CorrelationId);
    }

    [Fact]
    public void ApplyingLoadouts_Consumes_Success_AckFromSnapshot()
    {
        // P5.1: ACK Success short-circuits the snapshot.LoadoutStatus gate. Drive the
        // dispatch, then feed back a Success ACK carrying the stamped correlation id
        // without flipping snapshot.LoadoutStatus — the coordinator should still
        // mark the bot ready and advance.
        var loadouts = new Dictionary<string, LoadoutSpecSettings>(StringComparer.OrdinalIgnoreCase)
        {
            ["BGLEADER"] = new() { TargetLevel = 60 },
        };
        var coordinator = BuildCoordinator(AvBgType, AvMapId, loadouts: loadouts, accounts: new[] { "BGLEADER", "BGMEMBER1" });
        var snapshots = ReadySnapshots("BGLEADER", "BGMEMBER1", mapId: StagingMapId);

        var leaderAction = coordinator.GetAction("BGLEADER", snapshots);
        Assert.NotNull(leaderAction);
        var correlationId = leaderAction!.CorrelationId;
        Assert.False(string.IsNullOrWhiteSpace(correlationId));

        // No LoadoutStatus flip — only an ACK Success for BGLEADER's correlation id.
        AddAck(snapshots, "BGLEADER", correlationId, CommandAckEvent.Types.AckStatus.Success);

        // Advance: next poll should consume the ACK and stay OR advance (AV auto-
        // advances once all accounts resolved). BGMEMBER1 has no spec → auto-ready.
        Assert.Null(coordinator.GetAction("BGLEADER", snapshots));
        Assert.Equal(BattlegroundCoordinator.CoordState.QueueForBattleground, coordinator.State);
        Assert.DoesNotContain("BGLEADER", coordinator.ExcludedAccounts);
    }

    [Fact]
    public void ApplyingLoadouts_Consumes_Failed_AckFromSnapshot_AndExcludesAccount()
    {
        // P5.1: ACK Failed must mark the account as failed even when
        // snapshot.LoadoutStatus never flips to LoadoutFailed (e.g. pre-task
        // "loadout_task_already_active" or "unsupported_action" rejections).
        var loadouts = new Dictionary<string, LoadoutSpecSettings>(StringComparer.OrdinalIgnoreCase)
        {
            ["BGLEADER"] = new() { TargetLevel = 60 },
            ["BGMEMBER1"] = new() { TargetLevel = 60 },
        };
        var coordinator = BuildCoordinator(AvBgType, AvMapId, loadouts: loadouts, accounts: new[] { "BGLEADER", "BGMEMBER1" });
        var snapshots = ReadySnapshots("BGLEADER", "BGMEMBER1", mapId: StagingMapId);

        var leaderAction = coordinator.GetAction("BGLEADER", snapshots);
        var memberAction = coordinator.GetAction("BGMEMBER1", snapshots);
        Assert.NotNull(leaderAction);
        Assert.NotNull(memberAction);

        AddAck(snapshots, "BGLEADER", leaderAction!.CorrelationId, CommandAckEvent.Types.AckStatus.Success);
        AddAck(snapshots, "BGMEMBER1", memberAction!.CorrelationId, CommandAckEvent.Types.AckStatus.Failed, "loadout_task_already_active");

        // Snapshot.LoadoutStatus intentionally left as LoadoutNotStarted — the
        // coordinator must rely on the ACK to resolve BGMEMBER1.
        Assert.Null(coordinator.GetAction("BGLEADER", snapshots));
        Assert.Equal(BattlegroundCoordinator.CoordState.QueueForBattleground, coordinator.State);
        Assert.Contains("BGMEMBER1", coordinator.ExcludedAccounts);
        Assert.DoesNotContain("BGLEADER", coordinator.ExcludedAccounts);
    }

    [Fact]
    public void ApplyingLoadouts_Consumes_TimedOut_AckFromSnapshot_AndExcludesAccount()
    {
        // P5.1: ACK TimedOut (step exceeded retry cap) must be treated as a
        // terminal failure — coordinator excludes the bot and continues.
        var loadouts = new Dictionary<string, LoadoutSpecSettings>(StringComparer.OrdinalIgnoreCase)
        {
            ["BGLEADER"] = new() { TargetLevel = 60 },
            ["BGMEMBER1"] = new() { TargetLevel = 60 },
        };
        var coordinator = BuildCoordinator(AvBgType, AvMapId, loadouts: loadouts, accounts: new[] { "BGLEADER", "BGMEMBER1" });
        var snapshots = ReadySnapshots("BGLEADER", "BGMEMBER1", mapId: StagingMapId);

        var leaderAction = coordinator.GetAction("BGLEADER", snapshots);
        var memberAction = coordinator.GetAction("BGMEMBER1", snapshots);

        AddAck(snapshots, "BGLEADER", leaderAction!.CorrelationId, CommandAckEvent.Types.AckStatus.Success);
        AddAck(snapshots, "BGMEMBER1", memberAction!.CorrelationId, CommandAckEvent.Types.AckStatus.TimedOut,
            "step 'learn 1234' exceeded retries");

        Assert.Null(coordinator.GetAction("BGLEADER", snapshots));
        Assert.Equal(BattlegroundCoordinator.CoordState.QueueForBattleground, coordinator.State);
        Assert.Contains("BGMEMBER1", coordinator.ExcludedAccounts);
    }

    [Fact]
    public void ApplyingLoadouts_PendingAckDoesNotAdvance_AndIsOverriddenByTerminal()
    {
        // P5.1: Pending ACKs must not be treated as terminal. The coordinator
        // stays in ApplyingLoadouts until a terminal ACK (or snapshot.LoadoutStatus)
        // is observed.
        var loadouts = new Dictionary<string, LoadoutSpecSettings>(StringComparer.OrdinalIgnoreCase)
        {
            ["BGLEADER"] = new() { TargetLevel = 60 },
        };
        var coordinator = BuildCoordinator(AvBgType, AvMapId, loadouts: loadouts, accounts: new[] { "BGLEADER", "BGMEMBER1" });
        var snapshots = ReadySnapshots("BGLEADER", "BGMEMBER1", mapId: StagingMapId);

        var leaderAction = coordinator.GetAction("BGLEADER", snapshots);
        Assert.NotNull(leaderAction);
        var corr = leaderAction!.CorrelationId;

        // Only Pending recorded — coordinator must hold.
        AddAck(snapshots, "BGLEADER", corr, CommandAckEvent.Types.AckStatus.Pending);
        Assert.Null(coordinator.GetAction("BGLEADER", snapshots));
        Assert.Equal(BattlegroundCoordinator.CoordState.ApplyingLoadouts, coordinator.State);

        // Terminal Success arrives later — coordinator advances.
        AddAck(snapshots, "BGLEADER", corr, CommandAckEvent.Types.AckStatus.Success);
        Assert.Null(coordinator.GetAction("BGLEADER", snapshots));
        Assert.Equal(BattlegroundCoordinator.CoordState.QueueForBattleground, coordinator.State);
    }

    [Fact]
    public void WaitingForRaidFormation_HoldsWhenFactionGroupNotFormed_ReleasesWhenLeaderGuidMatches()
    {
        // WSG (faction group required) with a desired-party leader mapping.
        var desiredParty = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BGLEADER"] = "BGLEADER",
            ["BGMEMBER1"] = "BGLEADER",
            ["BGMEMBER2"] = "BGLEADER",
        };
        var coordinator = BuildCoordinator(
            WsgBgType,
            WsgMapId,
            accounts: new[] { "BGLEADER", "BGMEMBER1", "BGMEMBER2" },
            desiredPartyLeaderAccounts: desiredParty);

        // Drive past WaitingForBots with in-world/staged snapshots — but members
        // not yet grouped under the leader.
        var leaderGuid = 0xABCDEFUL;
        var ungroupedSnapshots = CreateSnapshots(
            CreateSnapshot("BGLEADER", level: 60, mapId: StagingMapId, selfGuid: leaderGuid),
            CreateSnapshot("BGMEMBER1", level: 60, mapId: StagingMapId, partyLeaderGuid: 0),
            CreateSnapshot("BGMEMBER2", level: 60, mapId: StagingMapId, partyLeaderGuid: 0));

        // WSG with desiredParty requires faction group BEFORE leaving WaitingForBots,
        // so the coordinator stays in WaitingForBots on ungrouped snapshots.
        Assert.Null(coordinator.GetAction("BGLEADER", ungroupedSnapshots));
        Assert.Equal(BattlegroundCoordinator.CoordState.WaitingForBots, coordinator.State);

        // Groups form → leaves WaitingForBots. No loadouts → ApplyingLoadouts auto-
        // ready. WaitingForRaidFormation re-checks: groups satisfied → QueueForBattleground.
        var groupedSnapshots = CreateSnapshots(
            CreateSnapshot("BGLEADER", level: 60, mapId: StagingMapId, selfGuid: leaderGuid),
            CreateSnapshot("BGMEMBER1", level: 60, mapId: StagingMapId, partyLeaderGuid: leaderGuid),
            CreateSnapshot("BGMEMBER2", level: 60, mapId: StagingMapId, partyLeaderGuid: leaderGuid));

        Assert.Null(coordinator.GetAction("BGLEADER", groupedSnapshots));
        Assert.Equal(BattlegroundCoordinator.CoordState.QueueForBattleground, coordinator.State);
    }

    // ---------- helpers ----------

    private static BattlegroundCoordinator BuildCoordinator(
        uint bgType,
        uint bgMapId,
        string[]? accounts = null,
        IReadOnlyDictionary<string, LoadoutSpecSettings>? loadouts = null,
        IReadOnlyDictionary<string, string>? desiredPartyLeaderAccounts = null)
    {
        accounts ??= new[] { "BGLEADER", "BGMEMBER1", "BGMEMBER2" };
        return new BattlegroundCoordinator(
            leaderAccount: accounts[0],
            allAccounts: accounts,
            bgTypeId: bgType,
            bgMapId: bgMapId,
            logger: NullLogger.Instance,
            stagingTargets: null,
            desiredPartyLeaderAccounts: desiredPartyLeaderAccounts,
            loadoutSpecs: loadouts);
    }

    private static ConcurrentDictionary<string, WoWActivitySnapshot> ReadySnapshots(
        string leader,
        string member1,
        string member2,
        int mapId)
    {
        return CreateSnapshots(
            CreateSnapshot(leader, level: 60, mapId: mapId),
            CreateSnapshot(member1, level: 60, mapId: mapId),
            CreateSnapshot(member2, level: 60, mapId: mapId));
    }

    private static ConcurrentDictionary<string, WoWActivitySnapshot> ReadySnapshots(
        string leader,
        string member1,
        int mapId)
    {
        return CreateSnapshots(
            CreateSnapshot(leader, level: 60, mapId: mapId),
            CreateSnapshot(member1, level: 60, mapId: mapId));
    }

    private static ConcurrentDictionary<string, WoWActivitySnapshot> CreateSnapshots(params WoWActivitySnapshot[] snapshots)
        => new(snapshots.ToDictionary(s => s.AccountName, StringComparer.OrdinalIgnoreCase));

    private static WoWActivitySnapshot CreateSnapshot(
        string accountName,
        uint level,
        int mapId,
        ulong partyLeaderGuid = 0,
        ulong? selfGuid = null)
    {
        return new WoWActivitySnapshot
        {
            AccountName = accountName,
            CharacterName = accountName,
            ScreenState = "InWorld",
            IsObjectManagerValid = true,
            PartyLeaderGuid = partyLeaderGuid,
            Player = new WoWPlayer
            {
                Unit = new WoWUnit
                {
                    Health = 100,
                    MaxHealth = 100,
                    GameObject = new WoWGameObject
                    {
                        Name = accountName,
                        Level = level,
                        Base = new WoWObject
                        {
                            Guid = selfGuid ?? (ulong)accountName.GetHashCode(),
                            MapId = (uint)mapId,
                            Position = new Position { X = 0, Y = 0, Z = 0 }
                        }
                    }
                }
            }
        };
    }

    private static void SetLoadoutStatus(
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots,
        string account,
        LoadoutStatus status,
        string failureReason = "")
    {
        var snapshot = snapshots[account];
        snapshot.LoadoutStatus = status;
        snapshot.LoadoutFailureReason = failureReason;
    }

    private static void AddAck(
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots,
        string account,
        string correlationId,
        CommandAckEvent.Types.AckStatus status,
        string failureReason = "")
    {
        snapshots[account].RecentCommandAcks.Add(new CommandAckEvent
        {
            CorrelationId = correlationId,
            ActionType = ActionType.ApplyLoadout,
            Status = status,
            FailureReason = failureReason,
        });
    }
}
