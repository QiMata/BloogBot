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

    private static WoWActivitySnapshot CreateSnapshot(
        string accountName = "BOT",
        uint currentMapId = 1,
        uint nestedMapId = 1,
        float x = 0,
        float y = 0,
        float z = 0,
        ulong guid = 1,
        ulong partyLeaderGuid = 0,
        uint mountDisplayId = 0)
    {
        return new WoWActivitySnapshot
        {
            AccountName = accountName,
            CurrentMapId = currentMapId,
            PartyLeaderGuid = partyLeaderGuid,
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
    }
}
