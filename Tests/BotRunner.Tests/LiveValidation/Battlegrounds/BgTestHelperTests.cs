using System;
using System.Collections.Generic;
using Communication;
using Game;

namespace BotRunner.Tests.LiveValidation.Battlegrounds;

public class BgTestHelperTests
{
    [Fact]
    public void CountBotsOnMap_MatchesEitherCurrentOrNestedMapId()
    {
        var snapshots = new List<WoWActivitySnapshot>
        {
            CreateSnapshot(currentMapId: 489, nestedMapId: 1),
            CreateSnapshot(currentMapId: 1, nestedMapId: 489),
            CreateSnapshot(currentMapId: 1, nestedMapId: 1),
        };

        var count = BgTestHelper.CountBotsOnMap(snapshots, 489);

        Assert.Equal(2, count);
    }

    [Fact]
    public void CountMountedAccounts_CountsOnlyTrackedMountedBots()
    {
        var snapshots = new List<WoWActivitySnapshot>
        {
            CreateSnapshot(accountName: "A", currentMapId: 30, nestedMapId: 30, mountDisplayId: 14337),
            CreateSnapshot(accountName: "B", currentMapId: 30, nestedMapId: 30),
            CreateSnapshot(accountName: "C", currentMapId: 30, nestedMapId: 30, mountDisplayId: 14337),
        };

        var count = BgTestHelper.CountMountedAccounts(snapshots, new[] { "A", "B" });

        Assert.Equal(1, count);
    }

    [Fact]
    public void CountTrackedAccountsOnMap_FiltersToTrackedRoster()
    {
        var snapshots = new List<WoWActivitySnapshot>
        {
            CreateSnapshot(accountName: "A", currentMapId: 529, nestedMapId: 1),
            CreateSnapshot(accountName: "B", currentMapId: 1, nestedMapId: 529),
            CreateSnapshot(accountName: "C", currentMapId: 529, nestedMapId: 529),
        };

        var count = BgTestHelper.CountTrackedAccountsOnMap(snapshots, new[] { "A", "B" }, 529);

        Assert.Equal(2, count);
    }

    [Fact]
    public void DescribeTrackedAccountsOffMap_ReportsOffMapAndMissingAccounts()
    {
        var snapshots = new List<WoWActivitySnapshot>
        {
            CreateSnapshot(accountName: "A", currentMapId: 529, nestedMapId: 1),
            CreateSnapshot(accountName: "B", currentMapId: 0, nestedMapId: 0),
        };

        var offMap = BgTestHelper.DescribeTrackedAccountsOffMap(snapshots, new[] { "C", "A", "B" }, 529);

        Assert.Equal(
            [
                "B(screen=, map=0, current=0, objMgr=False)",
                "C(missing)"
            ],
            offMap);
    }

    [Fact]
    public void FindTrackedChatMatches_ReturnsOnlyTrackedMatchingMessages()
    {
        var snapshots = new List<WoWActivitySnapshot>
        {
            CreateSnapshot(accountName: "A", chatMessages: ["The Horde wins!"]),
            CreateSnapshot(accountName: "B", chatMessages: ["Random system text", "Victory is ours"]),
            CreateSnapshot(accountName: "C", chatMessages: ["The Horde wins!"]),
        };

        var matches = BgTestHelper.FindTrackedChatMatches(
            snapshots,
            new[] { "A", "B" },
            message => message.Contains("wins", System.StringComparison.OrdinalIgnoreCase)
                || message.Contains("victory", System.StringComparison.OrdinalIgnoreCase));

        Assert.Equal(
            ["A: The Horde wins!", "B: Victory is ours"],
            matches);
    }

    [Fact]
    public void CaptureTrackedBagItemCounts_ReturnsPerAccountCountsForTrackedRoster()
    {
        var snapshots = new List<WoWActivitySnapshot>
        {
            CreateSnapshot(accountName: "A", bagItemIds: [20558, 20558, 36]),
            CreateSnapshot(accountName: "B", bagItemIds: [20558]),
            CreateSnapshot(accountName: "C", bagItemIds: [20558, 20559]),
        };

        var counts = BgTestHelper.CaptureTrackedBagItemCounts(snapshots, new[] { "A", "B", "D" }, 20558);

        Assert.Equal(2, counts["A"]);
        Assert.Equal(1, counts["B"]);
        Assert.Equal(0, counts["D"]);
    }

    [Fact]
    public void FindAccountsWithBagItemIncrease_ReturnsOnlyAccountsAboveBaseline()
    {
        var baselineCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = 1,
            ["B"] = 0,
            ["C"] = 2,
        };
        var currentCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = 3,
            ["B"] = 0,
            ["C"] = 1,
        };

        var increased = BgTestHelper.FindAccountsWithBagItemIncrease(baselineCounts, currentCounts);

        Assert.Equal(["A"], increased);
    }

    [Fact]
    public void CaptureTrackedChatMatchCounts_ReturnsPerAccountMatchingChatTotals()
    {
        var snapshots = new List<WoWActivitySnapshot>
        {
            CreateSnapshot(accountName: "A", chatMessages: ["flags reset", "other", "flags reset"]),
            CreateSnapshot(accountName: "B", chatMessages: ["other"]),
            CreateSnapshot(accountName: "C", chatMessages: ["flags reset"]),
        };

        var counts = BgTestHelper.CaptureTrackedChatMatchCounts(
            snapshots,
            new[] { "A", "B", "D" },
            message => message.Contains("flags reset", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(2, counts["A"]);
        Assert.Equal(0, counts["B"]);
        Assert.Equal(0, counts["D"]);
    }

    [Fact]
    public void FindAccountsWithChatMatchIncrease_ReturnsOnlyAccountsAboveBaseline()
    {
        var baselineCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = 1,
            ["B"] = 0,
            ["C"] = 2,
        };
        var currentCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = 2,
            ["B"] = 0,
            ["C"] = 2,
        };

        var increased = BgTestHelper.FindAccountsWithChatMatchIncrease(baselineCounts, currentCounts);

        Assert.Equal(["A"], increased);
    }

    [Fact]
    public void SumTrackedBagItemCounts_AddsCountsAcrossRoster()
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = 2,
            ["B"] = 1,
            ["C"] = 0,
        };

        var total = BgTestHelper.SumTrackedBagItemCounts(counts);

        Assert.Equal(3, total);
    }

    [Fact]
    public void SumTrackedChatMatchCounts_AddsCountsAcrossRoster()
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = 2,
            ["B"] = 1,
            ["C"] = 0,
        };

        var total = BgTestHelper.SumTrackedChatMatchCounts(counts);

        Assert.Equal(3, total);
    }

    [Fact]
    public void FindNearestNearbyGameObject_ReturnsClosestMatch()
    {
        var snapshot = CreateSnapshot(
            nearbyGameObjects:
            [
                new GameObjectSnapshot { Entry = 180089, Guid = 1, Name = "Farm Banner", DistanceToPlayer = 18f },
                new GameObjectSnapshot { Entry = 180089, Guid = 2, Name = "Farm Banner", DistanceToPlayer = 6f },
                new GameObjectSnapshot { Entry = 179830, Guid = 3, Name = "Silverwing Flag", DistanceToPlayer = 4f },
            ]);

        var match = BgTestHelper.FindNearestNearbyGameObject(snapshot, gameObject => gameObject.Entry == 180089);

        Assert.NotNull(match);
        Assert.Equal((uint)2, match!.Guid);
        Assert.Equal((uint)180089, match.Entry);
    }

    [Fact]
    public void CountAccountsNearTargets_UsesAssignedObjectiveDistances()
    {
        var snapshots = new List<WoWActivitySnapshot>
        {
            CreateSnapshot(accountName: "A", currentMapId: 30, nestedMapId: 30, x: 100, y: 100),
            CreateSnapshot(accountName: "B", currentMapId: 30, nestedMapId: 30, x: 180, y: 180),
        };
        var targets = new Dictionary<string, AlteracValleyLoadoutPlan.ObjectiveTarget>
        {
            ["A"] = new(30, 110, 105, 0),
            ["B"] = new(30, 100, 100, 0),
        };

        var count = BgTestHelper.CountAccountsNearTargets(snapshots, new[] { "A", "B" }, targets, maxDistance: 20f);

        Assert.Equal(1, count);
    }

    [Fact]
    public void CountAccountsGroupedToLeader_UsesLeaderGuidParity()
    {
        var leaderGuid = 0xABCUL;
        var snapshots = new List<WoWActivitySnapshot>
        {
            CreateSnapshot(accountName: "LEADER", currentMapId: 30, nestedMapId: 30, guid: leaderGuid, partyLeaderGuid: leaderGuid),
            CreateSnapshot(accountName: "A", currentMapId: 30, nestedMapId: 30, guid: 1, partyLeaderGuid: leaderGuid),
            CreateSnapshot(accountName: "B", currentMapId: 30, nestedMapId: 30, guid: 2, partyLeaderGuid: 0),
        };

        var count = BgTestHelper.CountAccountsGroupedToLeader(snapshots, new[] { "LEADER", "A", "B" }, "LEADER");

        Assert.Equal(2, count);
    }

    [Fact]
    public void BuildIncrementalRedispatchTarget_ReturnsIntermediateHopOnLargeVerticalDelta()
    {
        var snapshots = new List<WoWActivitySnapshot>
        {
            CreateSnapshot(accountName: "A", currentMapId: 30, nestedMapId: 30, x: -831, y: -592, z: 154),
        };

        var objective = new AlteracValleyLoadoutPlan.ObjectiveTarget(30, -799, -552, 54);

        var redispatchTarget = BgTestHelper.BuildIncrementalRedispatchTarget(snapshots, "A", objective);

        Assert.Equal(30u, redispatchTarget.MapId);
        Assert.Equal(154f, redispatchTarget.Z);
        Assert.True(BgTestHelper.Distance2D(redispatchTarget.X, redispatchTarget.Y, -831, -592) <= 28.5f);
        Assert.True(BgTestHelper.Distance2D(redispatchTarget.X, redispatchTarget.Y, objective.X, objective.Y) < 51f);
    }

    [Fact]
    public void BuildIncrementalRedispatchTarget_ReturnsObjectiveWhenAlreadyNearTarget()
    {
        var snapshots = new List<WoWActivitySnapshot>
        {
            CreateSnapshot(accountName: "A", currentMapId: 30, nestedMapId: 30, x: 100, y: 100, z: 40),
        };

        var objective = new AlteracValleyLoadoutPlan.ObjectiveTarget(30, 108, 106, 40);

        var redispatchTarget = BgTestHelper.BuildIncrementalRedispatchTarget(snapshots, "A", objective);

        Assert.Equal(objective, redispatchTarget);
    }

    [Fact]
    public void BuildIncrementalRedispatchTarget_CapsFlatHopAtFiftyFourYards()
    {
        var snapshots = new List<WoWActivitySnapshot>
        {
            CreateSnapshot(accountName: "A", currentMapId: 489, nestedMapId: 489, x: 1540.4f, y: 1481.3f, z: 352.6f),
        };

        var objective = new AlteracValleyLoadoutPlan.ObjectiveTarget(489, 1235.5f, 1427.1f, 309.7f);

        var redispatchTarget = BgTestHelper.BuildIncrementalRedispatchTarget(snapshots, "A", objective);

        Assert.Equal(489u, redispatchTarget.MapId);
        Assert.Equal(352.6f, redispatchTarget.Z);
        Assert.InRange(
            BgTestHelper.Distance2D(redispatchTarget.X, redispatchTarget.Y, 1540.4f, 1481.3f),
            18f,
            54.5f);
        Assert.True(
            BgTestHelper.Distance2D(redispatchTarget.X, redispatchTarget.Y, objective.X, objective.Y)
            < BgTestHelper.Distance2D(1540.4f, 1481.3f, objective.X, objective.Y));
    }

    [Fact]
    public void BuildMediumRangeRedispatchTarget_CapsFlatHopAtTwentyYards()
    {
        var snapshots = new List<WoWActivitySnapshot>
        {
            CreateSnapshot(accountName: "A", currentMapId: 489, nestedMapId: 489, x: 1534.3f, y: 1481.5f, z: 352.0f),
        };

        var objective = new AlteracValleyLoadoutPlan.ObjectiveTarget(489, 1235.5f, 1427.1f, 309.7f);

        var redispatchTarget = BgTestHelper.BuildMediumRangeRedispatchTarget(snapshots, "A", objective);

        Assert.Equal(489u, redispatchTarget.MapId);
        Assert.Equal(352.0f, redispatchTarget.Z);
        Assert.InRange(
            BgTestHelper.Distance2D(redispatchTarget.X, redispatchTarget.Y, 1534.3f, 1481.5f),
            10f,
            20.5f);
    }

    [Fact]
    public void BuildCloseRangeRedispatchTarget_CapsFlatHopAtTwelveYards()
    {
        var snapshots = new List<WoWActivitySnapshot>
        {
            CreateSnapshot(accountName: "A", currentMapId: 489, nestedMapId: 489, x: 1540.4f, y: 1481.3f, z: 352.6f),
        };

        var objective = new AlteracValleyLoadoutPlan.ObjectiveTarget(489, 1235.5f, 1427.1f, 309.7f);

        var redispatchTarget = BgTestHelper.BuildCloseRangeRedispatchTarget(snapshots, "A", objective);

        Assert.Equal(489u, redispatchTarget.MapId);
        Assert.Equal(352.6f, redispatchTarget.Z);
        Assert.InRange(
            BgTestHelper.Distance2D(redispatchTarget.X, redispatchTarget.Y, 1540.4f, 1481.3f),
            6f,
            12.5f);
    }

    private static WoWActivitySnapshot CreateSnapshot(
        string accountName = "BOT",
        uint currentMapId = 1,
        uint nestedMapId = 1,
        float x = 0,
        float y = 0,
        float z = 0,
        ulong guid = 1,
        ulong partyLeaderGuid = 0,
        uint mountDisplayId = 0,
        IReadOnlyCollection<string>? chatMessages = null,
        IReadOnlyCollection<uint>? bagItemIds = null,
        IReadOnlyCollection<GameObjectSnapshot>? nearbyGameObjects = null)
    {
        var snapshot = new WoWActivitySnapshot
        {
            AccountName = accountName,
            CurrentMapId = currentMapId,
            PartyLeaderGuid = partyLeaderGuid,
            MovementData = new MovementData(),
            Player = new WoWPlayer
            {
                Unit = new WoWUnit
                {
                    MountDisplayId = mountDisplayId,
                    GameObject = new WoWGameObject
                    {
                        Base = new WoWObject
                        {
                            Guid = guid,
                            MapId = nestedMapId,
                            Position = new Position
                            {
                                X = x,
                                Y = y,
                                Z = z,
                            }
                        }
                    }
                }
            }
        };

        if (chatMessages != null)
            snapshot.RecentChatMessages.Add(chatMessages);

        if (bagItemIds != null)
        {
            uint slotIndex = 0;
            foreach (var itemId in bagItemIds)
                snapshot.Player.BagContents[slotIndex++] = itemId;
        }

        if (nearbyGameObjects != null)
            snapshot.MovementData.NearbyGameObjects.Add(nearbyGameObjects);

        return snapshot;
    }
}
