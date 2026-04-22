using System.Collections.Concurrent;
using System.Collections.Generic;
using Communication;
using WoWStateManager.Coordination;
using Xunit;

namespace BotRunner.Tests;

/// <summary>
/// P4.5.1 / P4.5.4: <see cref="BattlegroundCoordinator.LastAckStatus"/> surfaces the latest
/// <see cref="CommandAckEvent"/> seen across all bot snapshots for a given correlation id.
/// Tests feed scripted <see cref="WoWActivitySnapshot.RecentCommandAcks"/> rings through the
/// helper and assert the terminal status wins over Pending, per the documented contract in
/// <c>docs/TASKS.md</c>.
/// </summary>
public class BattlegroundCoordinatorAckTests
{
    [Fact]
    public void LastAckStatus_ReturnsNull_WhenNoMatchingAckPresent()
    {
        var snapshots = Snapshots(
            SnapshotWithAcks("BGLEADER",
                Ack("other:1", CommandAckEvent.Types.AckStatus.Success)));

        Assert.Null(BattlegroundCoordinator.LastAckStatus("BGLEADER:1", snapshots));
    }

    [Fact]
    public void LastAckStatus_ReturnsPending_WhenOnlyPendingRecorded()
    {
        var snapshots = Snapshots(
            SnapshotWithAcks("BGLEADER",
                Ack("BGLEADER:1", CommandAckEvent.Types.AckStatus.Pending)));

        Assert.Equal(
            CommandAckEvent.Types.AckStatus.Pending,
            BattlegroundCoordinator.LastAckStatus("BGLEADER:1", snapshots));
    }

    [Fact]
    public void LastAckStatus_PrefersTerminalOverPending_WithinSameSnapshot()
    {
        var snapshots = Snapshots(
            SnapshotWithAcks("BGLEADER",
                Ack("BGLEADER:7", CommandAckEvent.Types.AckStatus.Pending),
                Ack("BGLEADER:7", CommandAckEvent.Types.AckStatus.Success)));

        Assert.Equal(
            CommandAckEvent.Types.AckStatus.Success,
            BattlegroundCoordinator.LastAckStatus("BGLEADER:7", snapshots));
    }

    [Fact]
    public void LastAckStatus_ReturnsFailed_WithReasonPresent()
    {
        var snapshots = Snapshots(
            SnapshotWithAcks("BGMEMBER1",
                Ack("BGMEMBER1:9", CommandAckEvent.Types.AckStatus.Pending),
                Ack("BGMEMBER1:9", CommandAckEvent.Types.AckStatus.Failed, "loadout_task_already_active")));

        Assert.Equal(
            CommandAckEvent.Types.AckStatus.Failed,
            BattlegroundCoordinator.LastAckStatus("BGMEMBER1:9", snapshots));
    }

    [Fact]
    public void LastAckStatus_ScansAcrossSnapshots_WhenCorrelationIdCrossesAccounts()
    {
        // StateManager stamps correlation ids as "<account>:<seq>", but the
        // helper should not assume a fixed owner — it must scan every snapshot.
        var snapshots = Snapshots(
            SnapshotWithAcks("BGLEADER"),
            SnapshotWithAcks("BGMEMBER1",
                Ack("BGLEADER:3", CommandAckEvent.Types.AckStatus.TimedOut)));

        Assert.Equal(
            CommandAckEvent.Types.AckStatus.TimedOut,
            BattlegroundCoordinator.LastAckStatus("BGLEADER:3", snapshots));
    }

    [Fact]
    public void LastAckStatus_ReturnsNull_ForEmptyOrMissingInputs()
    {
        Assert.Null(BattlegroundCoordinator.LastAckStatus(
            string.Empty,
            new ConcurrentDictionary<string, WoWActivitySnapshot>()));

        Assert.Null(BattlegroundCoordinator.LastAckStatus(
            "BGLEADER:1",
            new ConcurrentDictionary<string, WoWActivitySnapshot>()));
    }

    // ---------- helpers ----------

    private static ConcurrentDictionary<string, WoWActivitySnapshot> Snapshots(
        params WoWActivitySnapshot[] snapshots)
    {
        var dict = new ConcurrentDictionary<string, WoWActivitySnapshot>(
            System.StringComparer.OrdinalIgnoreCase);
        foreach (var snapshot in snapshots)
            dict[snapshot.AccountName] = snapshot;
        return dict;
    }

    private static WoWActivitySnapshot SnapshotWithAcks(
        string accountName,
        params CommandAckEvent[] acks)
    {
        var snapshot = new WoWActivitySnapshot
        {
            AccountName = accountName,
            CharacterName = accountName,
        };
        foreach (var ack in acks)
            snapshot.RecentCommandAcks.Add(ack);
        return snapshot;
    }

    private static CommandAckEvent Ack(
        string correlationId,
        CommandAckEvent.Types.AckStatus status,
        string failureReason = "")
    {
        return new CommandAckEvent
        {
            CorrelationId = correlationId,
            Status = status,
            FailureReason = failureReason,
        };
    }
}
