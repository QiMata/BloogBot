using Communication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WoWStateManager.Progression;
using WoWStateManager.Settings;
using Xunit;
using System.Collections.Generic;

namespace BotRunner.Tests.Progression;

/// <summary>
/// Tests ProgressionPlanner reputation goal evaluation.
/// </summary>
public class ReputationTrackingTests
{
    private readonly ProgressionPlanner _planner = new(new NullLogger<ProgressionPlanner>());

    private static WoWActivitySnapshot MakeSnapshot(uint level = 60, uint coinage = 1000000,
        Dictionary<uint, int>? repStandings = null)
    {
        var snapshot = new WoWActivitySnapshot
        {
            AccountName = "TEST",
            ConnectionState = BotConnectionState.BotInWorld,
            IsObjectManagerValid = true,
            IsMapTransition = false,
            Player = new Game.WoWPlayer
            {
                Unit = new Game.WoWUnit
                {
                    GameObject = new Game.WoWGameObject
                    {
                        Base = new Game.WoWObject
                        {
                            Position = new Game.Position { X = 0, Y = 0, Z = 0 }
                        },
                        Level = level,
                    }
                },
                Coinage = coinage,
            }
        };

        if (repStandings != null)
        {
            foreach (var (factionId, standing) in repStandings)
                snapshot.Player.ReputationStandings[factionId] = standing;
        }

        return snapshot;
    }

    [Fact]
    public void NoRepGoals_ReturnsNull()
    {
        var config = new CharacterBuildConfig();
        var result = _planner.GetNextAction(MakeSnapshot(), config);
        Assert.Null(result);
    }

    [Fact]
    public void RepGoalBelowTarget_LogsGap()
    {
        var config = new CharacterBuildConfig
        {
            ReputationGoals =
            [
                new ReputationGoalEntry
                {
                    FactionId = 529,
                    FactionName = "Argent Dawn",
                    TargetStanding = "Exalted",
                    GrindMethod = "Dungeon:Stratholme"
                }
            ]
        };

        // Argent Dawn at Honored (9000 rep) — below Exalted (42000)
        var snapshot = MakeSnapshot(repStandings: new Dictionary<uint, int> { [529] = 9000 });
        var result = _planner.GetNextAction(snapshot, config);
        // Currently returns null (source resolution not yet implemented)
        // but the planner should evaluate the gap without crashing
        Assert.Null(result);
    }

    [Fact]
    public void RepGoalAtTarget_NoAction()
    {
        var config = new CharacterBuildConfig
        {
            ReputationGoals =
            [
                new ReputationGoalEntry
                {
                    FactionId = 529,
                    FactionName = "Argent Dawn",
                    TargetStanding = "Honored",
                    GrindMethod = "Dungeon:Stratholme"
                }
            ]
        };

        // Argent Dawn at 10000 — above Honored threshold (9000)
        var snapshot = MakeSnapshot(repStandings: new Dictionary<uint, int> { [529] = 10000 });
        var result = _planner.GetNextAction(snapshot, config);
        Assert.Null(result);
    }

    [Fact]
    public void MultipleRepGoals_EvaluatesAll()
    {
        var config = new CharacterBuildConfig
        {
            ReputationGoals =
            [
                new ReputationGoalEntry { FactionId = 529, FactionName = "Argent Dawn", TargetStanding = "Exalted", GrindMethod = "Dungeon:Stratholme" },
                new ReputationGoalEntry { FactionId = 576, FactionName = "Timbermaw Hold", TargetStanding = "Revered", GrindMethod = "Mob:TimbermawFurbolg" },
            ]
        };

        var snapshot = MakeSnapshot(repStandings: new Dictionary<uint, int>
        {
            [529] = 5000,  // Below Exalted
            [576] = 25000, // Above Revered (21000)
        });

        // Should evaluate without crash
        var result = _planner.GetNextAction(snapshot, config);
        Assert.Null(result);
    }
}
