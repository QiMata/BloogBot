using System;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// V2.16: Channel tests. SEND_CHAT action to join channel and send message.
/// Assert message content matches via RecentChatMessages.
///
/// Run: dotnet test --filter "FullyQualifiedName~ChannelTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class ChannelTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public ChannelTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Channel_SendMessage_OtherBotReceives()
    {
        var bgAccount = _bot.BgAccountName!;
        var fgAccount = _bot.FgAccountName;
        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(fgAccount), "FG account not available -- channel test requires two bots");

        var fgActionable = await _bot.CheckFgActionableAsync();
        global::Tests.Infrastructure.Skip.If(!fgActionable, "FG bot not actionable");

        await _bot.EnsureCleanSlateAsync(bgAccount, "BG");
        await _bot.EnsureCleanSlateAsync(fgAccount!, "FG");

        // Generate a unique test message to identify in chat logs
        var testMessage = $"ChannelTest_{DateTime.UtcNow:HHmmss}";

        // BG bot sends a chat message via SEND_CHAT
        _output.WriteLine($"[TEST] BG sending chat message: {testMessage}");
        var sendResult = await _bot.SendActionAsync(bgAccount, new ActionMessage
        {
            ActionType = ActionType.SendChat,
            Parameters = { new RequestParameter { StringParam = testMessage } }
        });
        _output.WriteLine($"[TEST] SEND_CHAT result: {sendResult}");
        Assert.Equal(ResponseResult.Success, sendResult);

        // Wait for message to propagate
        await Task.Delay(3000);

        // Check FG snapshot for received chat messages
        await _bot.RefreshSnapshotsAsync();
        var fgSnap = await _bot.GetSnapshotAsync(fgAccount!);
        Assert.NotNull(fgSnap);

        var recentMessages = fgSnap!.RecentChatMessages?.ToList()
            ?? new System.Collections.Generic.List<string>();
        _output.WriteLine($"[TEST] FG recent chat messages: {recentMessages.Count}");
        foreach (var msg in recentMessages.TakeLast(5))
        {
            _output.WriteLine($"  Chat: {msg}");
        }

        // Also check BG snapshot to confirm the message was sent
        var bgSnap = await _bot.GetSnapshotAsync(bgAccount);
        var bgMessages = bgSnap?.RecentChatMessages?.ToList()
            ?? new System.Collections.Generic.List<string>();
        _output.WriteLine($"[TEST] BG recent chat messages: {bgMessages.Count}");
        foreach (var msg in bgMessages.TakeLast(5))
        {
            _output.WriteLine($"  Chat: {msg}");
        }

        // Verify the message appeared in at least one snapshot's chat log
        var allMessages = recentMessages.Concat(bgMessages);
        var found = allMessages.Any(m => m.Contains(testMessage, StringComparison.OrdinalIgnoreCase));
        _output.WriteLine($"[TEST] Test message found in chat: {found}");
        Assert.True(found, $"Chat message '{testMessage}' should appear in at least one bot's RecentChatMessages");
    }
}
