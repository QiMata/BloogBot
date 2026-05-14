using System.Collections.Generic;
using Communication;
using Game;
using Tests.Infrastructure;
using Xunit;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Unit-level coverage for <see cref="LavaHazardGuard"/>. Confirms the guard fires
/// only after consecutive non-combat health drops, respects map exemptions, and
/// resets on a stable / rising health signal.
/// </summary>
public class LavaHazardGuardTests
{
    private const int LavaMapId = 409;       // Molten Core (exempt)
    private const int OutdoorMapId = 0;      // Eastern Kingdoms (not exempt)
    private const uint UnitFlagInCombat = 0x00080000u;

    private static WoWActivitySnapshot MakeSnapshot(uint health, uint mapId, bool inCombat = false)
    {
        return new WoWActivitySnapshot
        {
            AccountName = "TESTBOT1",
            CurrentMapId = mapId,
            Player = new WoWPlayer
            {
                Unit = new WoWUnit
                {
                    Health = health,
                    UnitFlags = inCombat ? UnitFlagInCombat : 0u,
                    GameObject = new WoWGameObject
                    {
                        Base = new WoWObject { Position = new Position { X = 0, Y = 0, Z = 0 } },
                    },
                },
            },
        };
    }

    [Fact]
    public void FailsAfterThreeConsecutiveNonCombatHealthDrops()
    {
        var guard = new LavaHazardGuard("test", consecutiveDropsThreshold: 3);
        var failures = new List<string>();
        void Fail(string msg, WoWActivitySnapshot? _) => failures.Add(msg);

        guard.FailIfBurning(MakeSnapshot(2000, OutdoorMapId), Fail);
        guard.FailIfBurning(MakeSnapshot(1800, OutdoorMapId), Fail); // drop 1
        guard.FailIfBurning(MakeSnapshot(1600, OutdoorMapId), Fail); // drop 2
        Assert.Empty(failures);
        guard.FailIfBurning(MakeSnapshot(1400, OutdoorMapId), Fail); // drop 3 → fail

        Assert.Single(failures);
        Assert.Contains("environmental damage", failures[0]);
    }

    [Fact]
    public void DoesNotFailWhenInCombat()
    {
        var guard = new LavaHazardGuard("test", consecutiveDropsThreshold: 3);
        var failures = new List<string>();
        void Fail(string msg, WoWActivitySnapshot? _) => failures.Add(msg);

        guard.FailIfBurning(MakeSnapshot(2000, OutdoorMapId, inCombat: true), Fail);
        guard.FailIfBurning(MakeSnapshot(1800, OutdoorMapId, inCombat: true), Fail);
        guard.FailIfBurning(MakeSnapshot(1600, OutdoorMapId, inCombat: true), Fail);
        guard.FailIfBurning(MakeSnapshot(1400, OutdoorMapId, inCombat: true), Fail);

        Assert.Empty(failures);
    }

    [Fact]
    public void DoesNotFailOnExemptMap()
    {
        var guard = new LavaHazardGuard("test", consecutiveDropsThreshold: 3, exemptMapIds: LavaMapId);
        var failures = new List<string>();
        void Fail(string msg, WoWActivitySnapshot? _) => failures.Add(msg);

        guard.FailIfBurning(MakeSnapshot(2000, LavaMapId), Fail);
        guard.FailIfBurning(MakeSnapshot(1800, LavaMapId), Fail);
        guard.FailIfBurning(MakeSnapshot(1600, LavaMapId), Fail);
        guard.FailIfBurning(MakeSnapshot(1400, LavaMapId), Fail);

        Assert.Empty(failures);
    }

    [Fact]
    public void RisingHealthResetsTheCounter()
    {
        var guard = new LavaHazardGuard("test", consecutiveDropsThreshold: 3);
        var failures = new List<string>();
        void Fail(string msg, WoWActivitySnapshot? _) => failures.Add(msg);

        guard.FailIfBurning(MakeSnapshot(2000, OutdoorMapId), Fail);
        guard.FailIfBurning(MakeSnapshot(1800, OutdoorMapId), Fail); // drop 1
        guard.FailIfBurning(MakeSnapshot(1900, OutdoorMapId), Fail); // recovered → reset
        guard.FailIfBurning(MakeSnapshot(1850, OutdoorMapId), Fail); // drop 1 (after reset)
        guard.FailIfBurning(MakeSnapshot(1800, OutdoorMapId), Fail); // drop 2

        Assert.Empty(failures);
    }

    [Fact]
    public void DeadBotProducesNoSignal()
    {
        var guard = new LavaHazardGuard("test", consecutiveDropsThreshold: 1);
        var failures = new List<string>();
        void Fail(string msg, WoWActivitySnapshot? _) => failures.Add(msg);

        guard.FailIfBurning(MakeSnapshot(1500, OutdoorMapId), Fail);
        guard.FailIfBurning(MakeSnapshot(0, OutdoorMapId), Fail); // dead — skipped

        Assert.Empty(failures);
    }
}
