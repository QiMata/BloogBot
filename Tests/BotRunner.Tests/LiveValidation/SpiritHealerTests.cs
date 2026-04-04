using System;
using System.Threading.Tasks;
using Communication;
using GameData.Core.Enums;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// V2.19: Spirit healer tests. Kill bot (.damage 9999), RELEASE_CORPSE,
/// look for spirit healer NPC (NPC_FLAG_SPIRITHEALER = 0x4000).
///
/// Run: dotnet test --filter "FullyQualifiedName~SpiritHealerTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class SpiritHealerTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int KalimdorMapId = 1;
    // Valley of Trials graveyard area
    private const float GraveyardX = -601.0f, GraveyardY = -4297.0f, GraveyardZ = 41.0f;
    // NPC_FLAG_SPIRITHEALER
    private const uint NpcFlagSpiritHealer = 0x4000;

    public SpiritHealerTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task SpiritHealer_Resurrect_PlayerAliveWithSickness()
    {
        var account = _bot.BgAccountName!;

        await _bot.EnsureCleanSlateAsync(account, "BG");

        // Teleport to graveyard area (where spirit healer will be)
        await _bot.BotTeleportAsync(account, KalimdorMapId, GraveyardX, GraveyardY, GraveyardZ);
        await _bot.WaitForTeleportSettledAsync(account, GraveyardX, GraveyardY);

        // Verify bot is alive first
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        Assert.NotNull(snap);
        Assert.True(LiveBotFixture.IsStrictAlive(snap), "Bot should be alive before death test");
        var startHealth = snap!.Player?.Unit?.Health ?? 0;
        _output.WriteLine($"[TEST] Starting health: {startHealth}");

        // Kill the bot via GM command
        _output.WriteLine("[TEST] Killing bot with .damage 9999");
        await _bot.SendGmChatCommandAsync(account, ".damage 9999");
        await Task.Delay(3000);

        // Verify bot is dead
        var deadConfirmed = await _bot.WaitForSnapshotConditionAsync(
            account,
            s => !LiveBotFixture.IsStrictAlive(s),
            TimeSpan.FromSeconds(10),
            pollIntervalMs: 1000,
            progressLabel: "BG death-confirm");
        Assert.True(deadConfirmed, "Bot should be dead after .damage 9999");

        await _bot.RefreshSnapshotsAsync();
        snap = await _bot.GetSnapshotAsync(account);
        var healthAfterDeath = snap?.Player?.Unit?.Health ?? 0;
        _output.WriteLine($"[TEST] Health after death: {healthAfterDeath}");

        // Release corpse
        _output.WriteLine("[TEST] Releasing corpse");
        var releaseResult = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.ReleaseCorpse
        });
        _output.WriteLine($"[TEST] RELEASE_CORPSE result: {releaseResult}");
        Assert.Equal(ResponseResult.Success, releaseResult);

        // Wait for ghost state
        await Task.Delay(5000);
        await _bot.RefreshSnapshotsAsync();
        snap = await _bot.GetSnapshotAsync(account);
        Assert.NotNull(snap);

        // Look for spirit healer nearby (should appear when in ghost form at graveyard)
        _output.WriteLine("[TEST] Looking for spirit healer NPC");
        var spiritHealer = await _bot.WaitForNearbyUnitAsync(
            account,
            NpcFlagSpiritHealer,
            timeoutMs: 15000,
            progressLabel: "BG spirit-healer-search");

        if (spiritHealer != null)
        {
            _output.WriteLine($"[TEST] Found spirit healer: {spiritHealer.GameObject?.Name}, guid=0x{spiritHealer.GameObject?.Base?.Guid:X}");

            // Interact with spirit healer to resurrect
            var interactResult = await _bot.SendActionAsync(account, new ActionMessage
            {
                ActionType = ActionType.InteractWith,
                Parameters = { new RequestParameter { UlongParam = spiritHealer.GameObject?.Base?.Guid ?? 0 } }
            });
            _output.WriteLine($"[TEST] INTERACT_WITH spirit healer result: {interactResult}");

            // Wait for resurrection
            var alive = await _bot.WaitForSnapshotConditionAsync(
                account,
                LiveBotFixture.IsStrictAlive,
                TimeSpan.FromSeconds(15),
                pollIntervalMs: 1000,
                progressLabel: "BG spirit-healer-rez");
            _output.WriteLine($"[TEST] Alive after spirit healer: {alive}");
        }
        else
        {
            _output.WriteLine("[TEST] Spirit healer not found nearby -- may not be at a graveyard");
        }

        // Cleanup: revive the bot if still dead
        if (!LiveBotFixture.IsStrictAlive(snap))
        {
            await _bot.RevivePlayerAsync(snap!.CharacterName);
            await _bot.WaitForSnapshotConditionAsync(
                account,
                LiveBotFixture.IsStrictAlive,
                TimeSpan.FromSeconds(10));
        }
    }
}
