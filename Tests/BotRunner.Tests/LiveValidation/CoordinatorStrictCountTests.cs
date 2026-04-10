using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Communication;
using Game;
using Microsoft.Extensions.Logging.Abstractions;
using WoWStateManager.Coordination;
using WoWStateManager.Settings;

namespace BotRunner.Tests.LiveValidation;

public class CoordinatorStrictCountTests
{
    [Fact]
    public void BattlegroundCoordinator_DoesNotAdvanceToInBattleground_UntilEveryBotEntered()
    {
        var settings = CreateSettings("BGLEADER", "BGMEMBER1", "BGMEMBER2");
        var coordinator = new BattlegroundCoordinator(
            leaderAccount: "BGLEADER",
            allAccounts: settings.Select(setting => setting.AccountName),
            bgTypeId: 2,
            bgMapId: 489,
            logger: NullLogger.Instance);

        var readySnapshots = CreateSnapshots(
            CreateSnapshot("BGLEADER", level: 10, mapId: 1),
            CreateSnapshot("BGMEMBER1", level: 10, mapId: 1),
            CreateSnapshot("BGMEMBER2", level: 10, mapId: 1));

        Assert.Null(coordinator.GetAction("BGLEADER", readySnapshots));
        Assert.Equal(BattlegroundCoordinator.CoordState.QueueForBattleground, coordinator.State);

        Assert.Equal(ActionType.JoinBattleground, coordinator.GetAction("BGLEADER", readySnapshots)?.ActionType);
        Assert.Equal(ActionType.JoinBattleground, coordinator.GetAction("BGMEMBER1", readySnapshots)?.ActionType);
        Assert.Equal(ActionType.JoinBattleground, coordinator.GetAction("BGMEMBER2", readySnapshots)?.ActionType);
        Assert.Null(coordinator.GetAction("BGLEADER", readySnapshots));
        Assert.Equal(BattlegroundCoordinator.CoordState.WaitForInvite, coordinator.State);

        var partialEntrySnapshots = CreateSnapshots(
            CreateSnapshot("BGLEADER", level: 10, mapId: 489),
            CreateSnapshot("BGMEMBER1", level: 10, mapId: 1),
            CreateSnapshot("BGMEMBER2", level: 10, mapId: 1));

        Assert.Null(coordinator.GetAction("BGLEADER", partialEntrySnapshots));
        Assert.Equal(BattlegroundCoordinator.CoordState.WaitForInvite, coordinator.State);

        var fullEntrySnapshots = CreateSnapshots(
            CreateSnapshot("BGLEADER", level: 10, mapId: 489),
            CreateSnapshot("BGMEMBER1", level: 10, mapId: 489),
            CreateSnapshot("BGMEMBER2", level: 10, mapId: 489));

        Assert.Null(coordinator.GetAction("BGLEADER", fullEntrySnapshots));
        Assert.Equal(BattlegroundCoordinator.CoordState.InBattleground, coordinator.State);
    }

    [Fact]
    public void BattlegroundCoordinator_WaitForInvite_RetriesJoinThenAcceptForOffMapAccountsAfterDelay()
    {
        var settings = CreateSettings("BGLEADER", "BGMEMBER1", "BGMEMBER2");
        var coordinator = new BattlegroundCoordinator(
            leaderAccount: "BGLEADER",
            allAccounts: settings.Select(setting => setting.AccountName),
            bgTypeId: 2,
            bgMapId: 489,
            logger: NullLogger.Instance);

        var readySnapshots = CreateSnapshots(
            CreateSnapshot("BGLEADER", level: 10, mapId: 1),
            CreateSnapshot("BGMEMBER1", level: 10, mapId: 1),
            CreateSnapshot("BGMEMBER2", level: 10, mapId: 1));

        Assert.Null(coordinator.GetAction("BGLEADER", readySnapshots));
        Assert.Equal(BattlegroundCoordinator.CoordState.QueueForBattleground, coordinator.State);
        Assert.Equal(ActionType.JoinBattleground, coordinator.GetAction("BGLEADER", readySnapshots)?.ActionType);
        Assert.Equal(ActionType.JoinBattleground, coordinator.GetAction("BGMEMBER1", readySnapshots)?.ActionType);
        Assert.Equal(ActionType.JoinBattleground, coordinator.GetAction("BGMEMBER2", readySnapshots)?.ActionType);
        Assert.Null(coordinator.GetAction("BGLEADER", readySnapshots));
        Assert.Equal(BattlegroundCoordinator.CoordState.WaitForInvite, coordinator.State);

        SetPrivateField(coordinator, "_stateEnteredAt", DateTime.UtcNow - TimeSpan.FromSeconds(25));
        var partialEntrySnapshots = CreateSnapshots(
            CreateSnapshot("BGLEADER", level: 10, mapId: 489),
            CreateSnapshot("BGMEMBER1", level: 10, mapId: 1),
            CreateSnapshot("BGMEMBER2", level: 10, mapId: 1));

        var joinRetryAction = coordinator.GetAction("BGMEMBER1", partialEntrySnapshots);
        Assert.Equal(ActionType.JoinBattleground, joinRetryAction?.ActionType);
        Assert.Equal(2, joinRetryAction?.Parameters.Count ?? 0);
        Assert.Equal(2, joinRetryAction!.Parameters[0].IntParam);
        Assert.Equal(489, joinRetryAction.Parameters[1].IntParam);

        var acceptRetryAction = coordinator.GetAction("BGMEMBER1", partialEntrySnapshots);
        Assert.Equal(ActionType.AcceptBattleground, acceptRetryAction?.ActionType);

        // Retry is throttled per-account and should not fire again immediately.
        Assert.Null(coordinator.GetAction("BGMEMBER1", partialEntrySnapshots));

        var joinRetrySentAt = GetPrivateField<ConcurrentDictionary<string, DateTime>>(coordinator, "_joinRetrySentAt");
        var acceptRetrySentAt = GetPrivateField<ConcurrentDictionary<string, DateTime>>(coordinator, "_acceptRetrySentAt");
        var now = DateTime.UtcNow;
        joinRetrySentAt["BGMEMBER1"] = now - TimeSpan.FromSeconds(16);
        acceptRetrySentAt["BGMEMBER1"] = now - TimeSpan.FromSeconds(6);

        var secondJoinRetryAction = coordinator.GetAction("BGMEMBER1", partialEntrySnapshots);
        Assert.Equal(ActionType.JoinBattleground, secondJoinRetryAction?.ActionType);
    }

    [Fact]
    public void BattlegroundCoordinator_WaitsForEveryBotToReachQueueStagingArea()
    {
        var settings = CreateSettings("BGLEADER", "BGMEMBER1", "BGMEMBER2");
        var stagingTargets = new Dictionary<string, BattlegroundCoordinator.StagingTarget>(StringComparer.OrdinalIgnoreCase)
        {
            ["BGLEADER"] = new BattlegroundCoordinator.StagingTarget(1, 100, 100, 0),
            ["BGMEMBER1"] = new BattlegroundCoordinator.StagingTarget(1, 100, 100, 0),
            ["BGMEMBER2"] = new BattlegroundCoordinator.StagingTarget(0, 200, 200, 0),
        };

        var coordinator = new BattlegroundCoordinator(
            leaderAccount: "BGLEADER",
            allAccounts: settings.Select(setting => setting.AccountName),
            bgTypeId: 2,
            bgMapId: 489,
            logger: NullLogger.Instance,
            stagingTargets: stagingTargets);

        var unstagedSnapshots = CreateSnapshots(
            CreateSnapshot("BGLEADER", level: 10, mapId: 1, x: 100, y: 100),
            CreateSnapshot("BGMEMBER1", level: 10, mapId: 1, x: 100, y: 100),
            CreateSnapshot("BGMEMBER2", level: 10, mapId: 0, x: -6240, y: 331));

        Assert.Null(coordinator.GetAction("BGLEADER", unstagedSnapshots));
        Assert.Equal(BattlegroundCoordinator.CoordState.WaitingForBots, coordinator.State);

        var stagedSnapshots = CreateSnapshots(
            CreateSnapshot("BGLEADER", level: 10, mapId: 1, x: 100, y: 100),
            CreateSnapshot("BGMEMBER1", level: 10, mapId: 1, x: 100, y: 100),
            CreateSnapshot("BGMEMBER2", level: 10, mapId: 0, x: 200, y: 200));

        Assert.Null(coordinator.GetAction("BGLEADER", stagedSnapshots));
        Assert.Equal(BattlegroundCoordinator.CoordState.QueueForBattleground, coordinator.State);
    }

    [Fact]
    public void BattlegroundCoordinator_QueuePhase_RestagesUnstagedMembersBeforeJoin()
    {
        var settings = CreateSettings("BGLEADER", "BGMEMBER1", "BGMEMBER2");
        var stagingTargets = new Dictionary<string, BattlegroundCoordinator.StagingTarget>(StringComparer.OrdinalIgnoreCase)
        {
            ["BGLEADER"] = new BattlegroundCoordinator.StagingTarget(1, 100, 100, 0),
            ["BGMEMBER1"] = new BattlegroundCoordinator.StagingTarget(1, 100, 100, 0),
            ["BGMEMBER2"] = new BattlegroundCoordinator.StagingTarget(1, 100, 100, 0),
        };

        var coordinator = new BattlegroundCoordinator(
            leaderAccount: "BGLEADER",
            allAccounts: settings.Select(setting => setting.AccountName),
            bgTypeId: 2,
            bgMapId: 489,
            logger: NullLogger.Instance,
            stagingTargets: stagingTargets);

        var unstagedSnapshots = CreateSnapshots(
            CreateSnapshot("BGLEADER", level: 10, mapId: 1, x: 100, y: 100),
            CreateSnapshot("BGMEMBER1", level: 10, mapId: 1, x: 100, y: 100),
            CreateSnapshot("BGMEMBER2", level: 10, mapId: 1, x: 420, y: -220));

        SetPrivateField(coordinator, "_state", BattlegroundCoordinator.CoordState.QueueForBattleground);
        SetPrivateField(coordinator, "_stateEnteredAt", DateTime.UtcNow - TimeSpan.FromSeconds(5));

        Assert.Equal(ActionType.JoinBattleground, coordinator.GetAction("BGLEADER", unstagedSnapshots)?.ActionType);
        Assert.Equal(ActionType.JoinBattleground, coordinator.GetAction("BGMEMBER1", unstagedSnapshots)?.ActionType);

        var restageAction = coordinator.GetAction("BGMEMBER2", unstagedSnapshots);
        Assert.Equal(ActionType.Goto, restageAction?.ActionType);
        Assert.Equal(4, restageAction?.Parameters.Count ?? 0);
        Assert.Equal(100f, restageAction!.Parameters[0].FloatParam);
        Assert.Equal(100f, restageAction.Parameters[1].FloatParam);
        Assert.Equal(0f, restageAction.Parameters[2].FloatParam);

        var stagedSnapshots = CreateSnapshots(
            CreateSnapshot("BGLEADER", level: 10, mapId: 1, x: 100, y: 100),
            CreateSnapshot("BGMEMBER1", level: 10, mapId: 1, x: 100, y: 100),
            CreateSnapshot("BGMEMBER2", level: 10, mapId: 1, x: 100, y: 100));

        var joinAction = coordinator.GetAction("BGMEMBER2", stagedSnapshots);
        Assert.Equal(ActionType.JoinBattleground, joinAction?.ActionType);
    }

    [Fact]
    public void BattlegroundCoordinator_UsesBattlegroundSpecificMinimumLevelBeforeQueueing()
    {
        var settings = CreateSettings("BGLEADER", "BGMEMBER1", "BGMEMBER2");
        var coordinator = new BattlegroundCoordinator(
            leaderAccount: "BGLEADER",
            allAccounts: settings.Select(setting => setting.AccountName),
            bgTypeId: (uint)BotRunner.Travel.BattlemasterData.BattlegroundType.ArathiBasin,
            bgMapId: 529,
            logger: NullLogger.Instance);

        var lowLevelSnapshots = CreateSnapshots(
            CreateSnapshot("BGLEADER", level: 19, mapId: 1),
            CreateSnapshot("BGMEMBER1", level: 19, mapId: 1),
            CreateSnapshot("BGMEMBER2", level: 19, mapId: 0));

        Assert.Null(coordinator.GetAction("BGLEADER", lowLevelSnapshots));
        Assert.Equal(BattlegroundCoordinator.CoordState.WaitingForBots, coordinator.State);

        var readySnapshots = CreateSnapshots(
            CreateSnapshot("BGLEADER", level: 20, mapId: 1),
            CreateSnapshot("BGMEMBER1", level: 20, mapId: 1),
            CreateSnapshot("BGMEMBER2", level: 20, mapId: 0));

        Assert.Null(coordinator.GetAction("BGLEADER", readySnapshots));
        Assert.Equal(BattlegroundCoordinator.CoordState.QueueForBattleground, coordinator.State);
    }

    [Fact]
    public void DungeoneeringCoordinator_WaitsForEveryBotBeforeStartingGroupFormation()
    {
        var settings = CreateSettings("RFCLEADER", "RFCMEMBER1", "RFCMEMBER2");
        var coordinator = new DungeoneeringCoordinator(
            leaderAccount: "RFCLEADER",
            allAccounts: settings.Select(setting => setting.AccountName),
            allSettings: settings,
            soapClient: null,
            logger: NullLogger.Instance);

        var partialSnapshots = CreateSnapshots(
            CreateSnapshot("RFCLEADER", level: 8, mapId: 0),
            CreateSnapshot("RFCMEMBER1", level: 8, mapId: 0),
            CreateSnapshot("RFCMEMBER2", level: 8, mapId: 0, isObjectManagerValid: false));

        Assert.Null(coordinator.GetAction("RFCLEADER", partialSnapshots));
        Assert.Equal(DungeoneeringCoordinator.CoordState.WaitingForBots, coordinator.State);

        var readySnapshots = CreateSnapshots(
            CreateSnapshot("RFCLEADER", level: 8, mapId: 1),
            CreateSnapshot("RFCMEMBER1", level: 8, mapId: 1),
            CreateSnapshot("RFCMEMBER2", level: 8, mapId: 0));

        Assert.Null(coordinator.GetAction("RFCLEADER", readySnapshots));
        Assert.Equal(DungeoneeringCoordinator.CoordState.FormGroup_Inviting, coordinator.State);
    }

    [Fact]
    public void DungeoneeringCoordinator_GroupFormationAndTeleportFlow_DoesNotEmitPrepCommands()
    {
        var settings = CreateSettings("RFCLEADER", "RFCMEMBER1", "RFCMEMBER2");
        var coordinator = new DungeoneeringCoordinator(
            leaderAccount: "RFCLEADER",
            allAccounts: settings.Select(setting => setting.AccountName),
            allSettings: settings,
            soapClient: null,
            logger: NullLogger.Instance);

        var leaderGuid = (ulong)"RFCLEADER".GetHashCode();
        var readySnapshots = CreateSnapshots(
            CreateSnapshot("RFCLEADER", level: 8, mapId: 1, partyLeaderGuid: 0, selfGuid: leaderGuid),
            CreateSnapshot("RFCMEMBER1", level: 8, mapId: 1),
            CreateSnapshot("RFCMEMBER2", level: 8, mapId: 1));

        Assert.Null(coordinator.GetAction("RFCLEADER", readySnapshots));
        Assert.Equal(DungeoneeringCoordinator.CoordState.FormGroup_Inviting, coordinator.State);

        var firstInvite = coordinator.GetAction("RFCLEADER", readySnapshots);
        Assert.Equal(ActionType.SendGroupInvite, firstInvite?.ActionType);
        AssertActionDoesNotUsePrepChat(firstInvite);

        Thread.Sleep(TimeSpan.FromMilliseconds(900));
        var firstAccept = coordinator.GetAction("RFCMEMBER1", readySnapshots);
        Assert.Equal(ActionType.AcceptGroupInvite, firstAccept?.ActionType);
        AssertActionDoesNotUsePrepChat(firstAccept);

        var firstGroupedSnapshots = CreateSnapshots(
            CreateSnapshot("RFCLEADER", level: 8, mapId: 1, partyLeaderGuid: leaderGuid, selfGuid: leaderGuid),
            CreateSnapshot("RFCMEMBER1", level: 8, mapId: 1, partyLeaderGuid: leaderGuid),
            CreateSnapshot("RFCMEMBER2", level: 8, mapId: 1));

        Assert.Null(coordinator.GetAction("RFCLEADER", firstGroupedSnapshots));
        var secondInvite = coordinator.GetAction("RFCLEADER", firstGroupedSnapshots);
        Assert.Equal(ActionType.SendGroupInvite, secondInvite?.ActionType);
        AssertActionDoesNotUsePrepChat(secondInvite);

        Thread.Sleep(TimeSpan.FromMilliseconds(900));
        var secondAccept = coordinator.GetAction("RFCMEMBER2", readySnapshots);
        Assert.Equal(ActionType.AcceptGroupInvite, secondAccept?.ActionType);
        AssertActionDoesNotUsePrepChat(secondAccept);

        var fullyGroupedSnapshots = CreateSnapshots(
            CreateSnapshot("RFCLEADER", level: 8, mapId: 1, partyLeaderGuid: leaderGuid, selfGuid: leaderGuid),
            CreateSnapshot("RFCMEMBER1", level: 8, mapId: 1, partyLeaderGuid: leaderGuid),
            CreateSnapshot("RFCMEMBER2", level: 8, mapId: 1, partyLeaderGuid: leaderGuid));

        Assert.Null(coordinator.GetAction("RFCLEADER", fullyGroupedSnapshots));
        Assert.Null(coordinator.GetAction("RFCLEADER", fullyGroupedSnapshots));
        Assert.Equal(DungeoneeringCoordinator.CoordState.FormGroup_Verify, coordinator.State);

        Assert.Null(coordinator.GetAction("RFCLEADER", fullyGroupedSnapshots));
        Assert.Null(coordinator.GetAction("RFCLEADER", fullyGroupedSnapshots));
        Assert.Null(coordinator.GetAction("RFCLEADER", fullyGroupedSnapshots));
        Assert.Equal(DungeoneeringCoordinator.CoordState.TeleportToRFC, coordinator.State);

        var teleportAction = coordinator.GetAction("RFCLEADER", fullyGroupedSnapshots);
        Assert.Equal(ActionType.SendChat, teleportAction?.ActionType);
        AssertActionDoesNotUsePrepChat(teleportAction);
        Assert.StartsWith(".go xyz 3", teleportAction!.Parameters[0].StringParam, StringComparison.Ordinal);
    }

    private static List<CharacterSettings> CreateSettings(params string[] accounts)
    {
        return accounts
            .Select((account, index) => new CharacterSettings
            {
                AccountName = account,
                CharacterClass = "Warrior",
                CharacterRace = "Orc",
                CharacterGender = "Female",
                RunnerType = index == 0 ? WoWStateManager.Settings.BotRunnerType.Foreground : WoWStateManager.Settings.BotRunnerType.Background,
                ShouldRun = true,
                GmLevel = 6,
            })
            .ToList();
    }

    private static ConcurrentDictionary<string, WoWActivitySnapshot> CreateSnapshots(params WoWActivitySnapshot[] snapshots)
    {
        return new ConcurrentDictionary<string, WoWActivitySnapshot>(
            snapshots.ToDictionary(snapshot => snapshot.AccountName, StringComparer.OrdinalIgnoreCase));
    }

    private static WoWActivitySnapshot CreateSnapshot(
        string accountName,
        uint level,
        int mapId,
        ulong partyLeaderGuid = 0,
        ulong? selfGuid = null,
        float x = 0,
        float y = 0,
        bool isObjectManagerValid = true)
    {
        return new WoWActivitySnapshot
        {
            AccountName = accountName,
            CharacterName = accountName,
            ScreenState = "InWorld",
            IsObjectManagerValid = isObjectManagerValid,
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
                            Position = new Position { X = x, Y = y, Z = 0 }
                        }
                    }
                }
            }
        };
    }

    private static void AssertActionDoesNotUsePrepChat(ActionMessage? action)
    {
        if (action?.ActionType != ActionType.SendChat)
            return;

        var chat = action.Parameters.FirstOrDefault()?.StringParam ?? string.Empty;
        Assert.DoesNotContain(".learn", chat, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".character level", chat, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".reset", chat, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".additem", chat, StringComparison.OrdinalIgnoreCase);
    }

    private static void SetPrivateField(object instance, string fieldName, object value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(instance.GetType().FullName, fieldName);
        field.SetValue(instance, value);
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(instance.GetType().FullName, fieldName);
        return (T)field.GetValue(instance)!;
    }
}
