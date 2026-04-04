using Communication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WoWStateManager.Progression;
using WoWStateManager.Settings;
using Xunit;
using System.Collections.Generic;

namespace BotRunner.Tests.Progression;

/// <summary>
/// Tests ProgressionPlanner priority ordering.
/// </summary>
public class ProgressionPlannerTests
{
    private readonly ProgressionPlanner _planner = new(new NullLogger<ProgressionPlanner>());

    private static WoWActivitySnapshot MakeSnapshot(uint level = 60, uint coinage = 0)
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
                        Level = level
                    }
                },
                Coinage = coinage,
            }
        };
        return snapshot;
    }

    [Fact]
    public void NullConfig_ReturnsNull()
    {
        var result = _planner.GetNextAction(MakeSnapshot(), null);
        Assert.Null(result);
    }

    [Fact]
    public void EmptyConfig_ReturnsNull()
    {
        var result = _planner.GetNextAction(MakeSnapshot(), new CharacterBuildConfig());
        Assert.Null(result);
    }

    [Fact]
    public void BotNotInWorld_ReturnsNull()
    {
        var snapshot = MakeSnapshot();
        snapshot.ConnectionState = BotConnectionState.BotDisconnected;
        var result = _planner.GetNextAction(snapshot, new CharacterBuildConfig { GoldTargetCopper = 100 });
        Assert.Null(result);
    }

    [Fact]
    public void GoldBelowTarget_LogsButDoesNotOverride()
    {
        // Gold tracking should not override — bot grinds naturally
        var config = new CharacterBuildConfig { GoldTargetCopper = 1000000 };
        var result = _planner.GetNextAction(MakeSnapshot(coinage: 500), config);
        Assert.Null(result); // No override for gold — bot self-directs
    }

    [Fact]
    public void GoldAboveTarget_NoAction()
    {
        var config = new CharacterBuildConfig { GoldTargetCopper = 1000000 };
        var result = _planner.GetNextAction(MakeSnapshot(coinage: 2000000), config);
        Assert.Null(result);
    }

    [Fact]
    public void SkillPriorities_ParsedCorrectly()
    {
        var config = new CharacterBuildConfig
        {
            SkillPriorities = ["Mining:300", "Engineering:300"]
        };
        // Should not crash — priorities are evaluated but no action returned yet
        var result = _planner.GetNextAction(MakeSnapshot(), config);
        Assert.Null(result);
    }

    [Fact]
    public void QuestChains_EvaluatedWithoutCrash()
    {
        var config = new CharacterBuildConfig
        {
            QuestChains = ["MoltenCoreAttunement", "NonExistentChain"]
        };
        var result = _planner.GetNextAction(MakeSnapshot(), config);
        Assert.Null(result);
    }
}
