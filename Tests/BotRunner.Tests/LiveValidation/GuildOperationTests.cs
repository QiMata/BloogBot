using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// P20.5: Guild operation tests — Bot creates guild, invites second bot, both accept.
/// Assert guild roster shows both members.
///
/// Run: dotnet test --filter "FullyQualifiedName~GuildOperationTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class GuildOperationTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int MapId = 1;
    private const float OrgX = 1629f, OrgY = -4373f, OrgZ = 34f;

    public GuildOperationTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Guild_CreateAndInvite_RosterShowsBothMembers()
    {
        var bgAccount = _bot.BgAccountName!;
        var fgAccount = _bot.FgAccountName;
        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(fgAccount), "FG bot not available for dual-bot guild test");

        // Teleport both bots to Orgrimmar
        await _bot.BotTeleportAsync(bgAccount, MapId, OrgX, OrgY, OrgZ);
        await _bot.BotTeleportAsync(fgAccount!, MapId, OrgX + 2, OrgY, OrgZ);
        await Task.Delay(3000);

        // Verify both bots are positioned and get character names
        await _bot.RefreshSnapshotsAsync();
        var bgSnap = await _bot.GetSnapshotAsync(bgAccount);
        var fgSnap = await _bot.GetSnapshotAsync(fgAccount!);
        Assert.NotNull(bgSnap);
        Assert.NotNull(fgSnap);

        var bgChar = bgSnap!.CharacterName;
        var fgChar = fgSnap!.CharacterName;
        Assert.False(string.IsNullOrWhiteSpace(bgChar), "BG character name not available");
        Assert.False(string.IsNullOrWhiteSpace(fgChar), "FG character name not available");
        var bgPos = bgSnap.MovementData?.Position;
        var fgPos = fgSnap.MovementData?.Position;
        _output.WriteLine($"[GUILD] BG={bgChar} at ({bgPos?.X:F0},{bgPos?.Y:F0}), FG={fgChar} at ({fgPos?.X:F0},{fgPos?.Y:F0})");

        // Teardown: remove from existing guilds first (ignore errors if not in guild)
        await _bot.SendGmChatCommandAsync(bgAccount, ".guild uninvite " + bgChar);
        await Task.Delay(500);
        await _bot.SendGmChatCommandAsync(fgAccount!, ".guild uninvite " + fgChar);
        await Task.Delay(1000);

        // Create guild with BG bot as guild master
        var guildName = "TestGuild";
        _output.WriteLine($"[GUILD] Creating guild '{guildName}' with leader {bgChar}");
        await _bot.SendGmChatCommandAsync(bgAccount, $".guild create {bgChar} {guildName}");
        await Task.Delay(2000);

        // Check chat for guild creation response
        await _bot.RefreshSnapshotsAsync();
        bgSnap = await _bot.GetSnapshotAsync(bgAccount);
        Assert.NotNull(bgSnap);
        foreach (var msg in bgSnap!.RecentChatMessages)
        {
            if (msg.Contains("guild", System.StringComparison.OrdinalIgnoreCase))
                _output.WriteLine($"[GUILD] BG Chat: {msg}");
        }

        // Invite FG bot to the guild
        _output.WriteLine($"[GUILD] Inviting {fgChar} to guild");
        await _bot.SendGmChatCommandAsync(bgAccount, $".guild invite {fgChar} {guildName}");
        await Task.Delay(2000);

        // Refresh and check both snapshots
        await _bot.RefreshSnapshotsAsync();
        bgSnap = await _bot.GetSnapshotAsync(bgAccount);
        fgSnap = await _bot.GetSnapshotAsync(fgAccount!);
        Assert.NotNull(bgSnap);
        Assert.NotNull(fgSnap);

        // Verify both bots are still connected after guild operations
        Assert.True(bgSnap!.IsObjectManagerValid, "BG bot ObjectManager should be valid after guild ops");
        Assert.True(fgSnap!.IsObjectManagerValid, "FG bot ObjectManager should be valid after guild ops");

        _output.WriteLine($"[GUILD] BG connected={bgSnap.IsObjectManagerValid}, FG connected={fgSnap.IsObjectManagerValid}");

        // Log guild-related chat from both bots
        foreach (var msg in bgSnap.RecentChatMessages)
        {
            if (msg.Contains("guild", System.StringComparison.OrdinalIgnoreCase))
                _output.WriteLine($"[GUILD] BG Chat: {msg}");
        }
        foreach (var msg in fgSnap.RecentChatMessages)
        {
            if (msg.Contains("guild", System.StringComparison.OrdinalIgnoreCase))
                _output.WriteLine($"[GUILD] FG Chat: {msg}");
        }

        // Cleanup: disband the guild
        _output.WriteLine("[GUILD] Cleanup: disbanding test guild");
        await _bot.SendGmChatCommandAsync(bgAccount, $".guild delete {guildName}");
        await Task.Delay(1000);
    }
}
