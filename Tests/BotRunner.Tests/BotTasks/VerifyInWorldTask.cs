using System;
using Tests.Infrastructure.BotTasks;

namespace BotRunner.Tests.BotTasks;

/// <summary>
/// BotTask that verifies a character is logged in and in-world.
/// Checks: PlayerGuid != 0, ContinentId is a valid map, Player object exists.
/// This task requires a live WoW client with an injected bot.
/// </summary>
public class VerifyInWorldTask : TestBotTask
{
    private readonly Func<ulong> _getPlayerGuid;
    private readonly Func<uint> _getContinentId;
    private readonly Func<bool> _hasPlayer;

    public VerifyInWorldTask(
        Func<ulong> getPlayerGuid,
        Func<uint> getContinentId,
        Func<bool> hasPlayer)
        : base("VerifyInWorld")
    {
        _getPlayerGuid = getPlayerGuid;
        _getContinentId = getContinentId;
        _hasPlayer = hasPlayer;
        Timeout = TimeSpan.FromSeconds(15);
    }

    public override void Update()
    {
        ulong guid = _getPlayerGuid();
        if (guid == 0)
        {
            Fail("PlayerGuid is 0 — not logged in");
            return;
        }

        uint continentId = _getContinentId();
        if (continentId == 0xFFFFFFFF)
        {
            Fail("ContinentId is 0xFFFFFFFF — at character select, not in world");
            return;
        }

        if (continentId == 0xFF)
        {
            Fail("ContinentId is 0xFF — loading screen, not yet in world");
            return;
        }

        if (!_hasPlayer())
        {
            Fail("Player object is null — ObjectManager hasn't enumerated player yet");
            return;
        }

        Complete();
    }
}
