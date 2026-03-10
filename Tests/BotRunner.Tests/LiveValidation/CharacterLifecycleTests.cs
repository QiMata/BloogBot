using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Character lifecycle integration tests.
///
/// Snapshot-driven goals:
/// 1) Character identity data must be present in activity snapshots.
/// 2) AddItem commands must produce bag-state changes in snapshots.
/// 3) Death test must show strict alive -> dead/ghost -> strict alive transitions.
/// </summary>
[RequiresMangosStack]
[Collection(LiveValidationCollection.Name)]
public class CharacterLifecycleTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const uint MinorHealingPotion = 118;

    private const uint PlayerFlagGhost = 0x10; // PLAYER_FLAGS_GHOST
    private const uint StandStateMask = 0xFF;
    private const uint StandStateDead = 7; // UNIT_STAND_STATE_DEAD

    public CharacterLifecycleTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    public async Task Equipment_AddItemToInventory()
    {
        var bgAccount = _bot.BgAccountName!;
        Assert.NotNull(bgAccount);
        _output.WriteLine($"=== BG Bot: {_bot.BgCharacterName} ({bgAccount}) ===");

        bool bgPassed, fgPassed = false;
        var hasFg = _bot.IsFgActionable;
        if (hasFg)
        {
            var fgAccount = _bot.FgAccountName!;
            Assert.NotNull(fgAccount);
            _output.WriteLine($"=== FG Bot: {_bot.FgCharacterName} ({fgAccount}) ===");
            _output.WriteLine("[PARITY] Running BG and FG add-item scenarios in parallel.");

            var bgTask = RunAddItemScenarioAsync(bgAccount, "BG", LiveBotFixture.TestItems.LinenCloth, 1, "Linen Cloth");
            var fgTask = RunAddItemScenarioAsync(fgAccount, "FG", LiveBotFixture.TestItems.LinenCloth, 1, "Linen Cloth");
            await Task.WhenAll(bgTask, fgTask);
            bgPassed = await bgTask;
            fgPassed = await fgTask;
        }
        else
        {
            bgPassed = await RunAddItemScenarioAsync(bgAccount, "BG", LiveBotFixture.TestItems.LinenCloth, 1, "Linen Cloth");
            _output.WriteLine("\nFG Bot: NOT AVAILABLE");
        }

        Assert.True(bgPassed, "BG bot: expected Linen Cloth to appear in bag snapshot after .additem.");
        if (hasFg)
            Assert.True(fgPassed, "FG bot: expected Linen Cloth to appear in bag snapshot after .additem. " +
                "If FG ObjectManager Item enumeration is broken, fix it — don't hide failures.");
    }

    [SkippableFact]
    public async Task Consumable_AddPotionToInventory()
    {
        var bgAccount = _bot.BgAccountName!;
        Assert.NotNull(bgAccount);
        _output.WriteLine($"=== BG Bot: {_bot.BgCharacterName} ({bgAccount}) ===");

        bool bgPassed, fgPassed = false;
        var hasFg = _bot.IsFgActionable;
        if (hasFg)
        {
            var fgAccount = _bot.FgAccountName!;
            Assert.NotNull(fgAccount);
            _output.WriteLine($"=== FG Bot: {_bot.FgCharacterName} ({fgAccount}) ===");
            _output.WriteLine("[PARITY] Running BG and FG add-item scenarios in parallel.");

            var bgTask = RunAddItemScenarioAsync(bgAccount, "BG", MinorHealingPotion, 5, "Minor Healing Potion");
            var fgTask = RunAddItemScenarioAsync(fgAccount, "FG", MinorHealingPotion, 5, "Minor Healing Potion");
            await Task.WhenAll(bgTask, fgTask);
            bgPassed = await bgTask;
            fgPassed = await fgTask;
        }
        else
        {
            bgPassed = await RunAddItemScenarioAsync(bgAccount, "BG", MinorHealingPotion, 5, "Minor Healing Potion");
            _output.WriteLine("\nFG Bot: NOT AVAILABLE");
        }

        Assert.True(bgPassed, "BG bot: expected Minor Healing Potion to appear in bag snapshot after .additem.");
        if (hasFg)
            Assert.True(fgPassed, "FG bot: expected Minor Healing Potion to appear in bag snapshot after .additem. " +
                "If FG ObjectManager Item enumeration is broken, fix it — don't hide failures.");
    }

    [SkippableFact]
    public async Task Death_KillAndRevive()
    {
        var bgAccount = _bot.BgAccountName!;
        Assert.NotNull(bgAccount);
        var bgCharacter = _bot.BgCharacterName;
        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(bgCharacter), "BG character name unavailable.");

        _output.WriteLine($"=== BG Bot: {bgCharacter} ({bgAccount}) ===");

        bool bgPassed, fgPassed = false;
        var hasFg = _bot.IsFgActionable;
        if (hasFg)
        {
            var fgAccount = _bot.FgAccountName!;
            Assert.NotNull(fgAccount);
            var fgCharacter = _bot.FgCharacterName;
            global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(fgCharacter), "FG character name unavailable.");

            _output.WriteLine($"=== FG Bot: {fgCharacter} ({fgAccount}) ===");
            _output.WriteLine("[PARITY] Running BG and FG death scenarios in parallel.");

            var bgTask = RunDeathScenarioAsync(bgAccount, bgCharacter!, "BG");
            var fgTask = RunDeathScenarioAsync(fgAccount, fgCharacter!, "FG");
            await Task.WhenAll(bgTask, fgTask);
            bgPassed = await bgTask;
            fgPassed = await fgTask;
        }
        else
        {
            bgPassed = await RunDeathScenarioAsync(bgAccount, bgCharacter!, "BG");
            _output.WriteLine("\nFG Bot: NOT AVAILABLE");
        }

        Assert.True(bgPassed, "BG bot: expected strict alive -> dead/ghost -> strict alive transition.");
        if (hasFg)
            Assert.True(fgPassed, "FG bot: expected strict alive -> dead/ghost -> strict alive transition.");
    }

    [SkippableFact]
    public async Task CharacterCreation_InfoAvailable()
    {
        await _bot.RefreshSnapshotsAsync();

        var bgSnap = _bot.BackgroundBot;
        Assert.NotNull(bgSnap);
        Assert.Equal("InWorld", bgSnap.ScreenState);
        Assert.False(string.IsNullOrWhiteSpace(bgSnap.CharacterName));
        Assert.False(string.IsNullOrWhiteSpace(bgSnap.AccountName));
        Assert.NotNull(bgSnap.Player?.Unit?.GameObject?.Base?.Position);
        Assert.NotEqual(0UL, bgSnap.Player?.Unit?.GameObject?.Base?.Guid ?? 0UL);

        var bgPos = bgSnap.Player!.Unit!.GameObject!.Base!.Position!;
        _output.WriteLine($"[BG] Character={bgSnap.CharacterName}, Account={bgSnap.AccountName}, GUID=0x{bgSnap.Player.Unit.GameObject.Base.Guid:X}");
        _output.WriteLine($"[BG] Position=({bgPos.X:F1}, {bgPos.Y:F1}, {bgPos.Z:F1}), Level={bgSnap.Player.Unit.GameObject.Level}");

        if (_bot.IsFgActionable)
        {
            var fgSnap = _bot.ForegroundBot;
            Assert.Equal("InWorld", fgSnap.ScreenState);
            Assert.False(string.IsNullOrWhiteSpace(fgSnap.CharacterName));
            Assert.False(string.IsNullOrWhiteSpace(fgSnap.AccountName));
            Assert.NotNull(fgSnap.Player?.Unit?.GameObject?.Base?.Position);
            Assert.NotEqual(0UL, fgSnap.Player?.Unit?.GameObject?.Base?.Guid ?? 0UL);

            var fgPos = fgSnap.Player!.Unit!.GameObject!.Base!.Position!;
            _output.WriteLine($"[FG] Character={fgSnap.CharacterName}, Account={fgSnap.AccountName}, GUID=0x{fgSnap.Player.Unit.GameObject.Base.Guid:X}");
            _output.WriteLine($"[FG] Position=({fgPos.X:F1}, {fgPos.Y:F1}, {fgPos.Z:F1}), Level={fgSnap.Player.Unit.GameObject.Level}");
        }
        else
        {
            _output.WriteLine("FG Bot: NOT AVAILABLE");
        }
    }

    private async Task<bool> RunAddItemScenarioAsync(
        string account,
        string label,
        uint itemId,
        int count,
        string itemName)
    {
        await EnsureStrictAliveAsync(account, label);
        await _bot.RefreshSnapshotsAsync();

        var baseline = await _bot.GetSnapshotAsync(account);
        if (baseline?.Player == null)
            return false;

        var beforeSlotsForItem = CountBagSlotsForItem(baseline.Player, itemId);
        var totalBagSlots = baseline.Player.BagContents.Count;
        _output.WriteLine($"  [{label}] {itemName} slots before setup: {beforeSlotsForItem}, bag entries: {totalBagSlots}");

        if (beforeSlotsForItem > 0 || totalBagSlots >= 15)
        {
            _output.WriteLine($"  [{label}] Clearing backpack for deterministic {itemName} add verification.");
            await _bot.BotClearInventoryAsync(account, includeExtraBags: false);
            await Task.Delay(1000);
            await _bot.RefreshSnapshotsAsync();
            baseline = await _bot.GetSnapshotAsync(account);
            if (baseline?.Player == null)
                return false;
            beforeSlotsForItem = CountBagSlotsForItem(baseline.Player, itemId);
            Assert.Equal(0, beforeSlotsForItem);
            _output.WriteLine($"  [{label}] {itemName} slots after cleanup: {beforeSlotsForItem}");
        }

        var trace = await _bot.SendGmChatCommandTrackedAsync(
            account,
            $".additem {itemId} {count}",
            captureResponse: true,
            delayMs: 1200);
        AssertCommandSucceeded(trace, label, $".additem {itemId} {count}");

        var appeared = await WaitForBagItemPresenceAsync(account, itemId, TimeSpan.FromSeconds(10));
        await _bot.RefreshSnapshotsAsync();
        var after = await _bot.GetSnapshotAsync(account);
        var afterSlotsForItem = CountBagSlotsForItem(after?.Player, itemId);

        _output.WriteLine($"  [{label}] {itemName} slots before/after: {beforeSlotsForItem} -> {afterSlotsForItem}");
        return appeared && afterSlotsForItem > 0;
    }

    private async Task<bool> RunDeathScenarioAsync(string account, string characterName, string label)
    {
        await EnsureStrictAliveAsync(account, label);
        await _bot.RefreshSnapshotsAsync();

        var baseline = await _bot.GetSnapshotAsync(account);
        if (!LiveBotFixture.IsStrictAlive(baseline))
        {
            _output.WriteLine($"  [{label}] FAIL: Baseline not strict-alive after setup.");
            return false;
        }

        _output.WriteLine($"  [{label}] Baseline alive — health={baseline!.Player!.Unit!.Health}, flags=0x{baseline.Player.PlayerFlags:X}");

        var deathResult = await _bot.InduceDeathForTestAsync(account, characterName, timeoutMs: 15000, requireCorpseTransition: false);
        _output.WriteLine($"  [{label}] Death setup: success={deathResult.Succeeded}, command={deathResult.Command}, details={deathResult.Details}");
        if (!deathResult.Succeeded)
            return false;

        var deadStateObserved = await WaitForDeadOrGhostStateAsync(account, TimeSpan.FromSeconds(10));
        _output.WriteLine($"  [{label}] Dead/ghost state observed: {deadStateObserved}");
        if (!deadStateObserved)
        {
            var debugSnap = await _bot.GetSnapshotAsync(account);
            _output.WriteLine($"  [{label}] FAIL: Dead state not detected. health={debugSnap?.Player?.Unit?.Health}, flags=0x{debugSnap?.Player?.PlayerFlags:X}, bytes1=0x{debugSnap?.Player?.Unit?.Bytes1:X}");
            return false;
        }

        // Use SOAP revive only as a deterministic cleanup fallback; reclaim timer can be long.
        var reviveResult = await _bot.RevivePlayerAsync(characterName);
        _output.WriteLine($"  [{label}] SOAP revive result: {reviveResult}");

        // FG descriptor memory may lag behind server state after SOAP revive;
        // allow up to 20s for the WoW client to process the update packet.
        var aliveAgain = await _bot.WaitForSnapshotConditionAsync(account, LiveBotFixture.IsStrictAlive, TimeSpan.FromSeconds(20),
            progressLabel: $"{label} revive-alive");
        if (!aliveAgain)
        {
            var debugSnap = await _bot.GetSnapshotAsync(account);
            _output.WriteLine($"  [{label}] FAIL: Not strict-alive after revive. health={debugSnap?.Player?.Unit?.Health}, flags=0x{debugSnap?.Player?.PlayerFlags:X}, bytes1=0x{debugSnap?.Player?.Unit?.Bytes1:X}");
            return false;
        }

        var afterRevive = await _bot.GetSnapshotAsync(account);
        var health = afterRevive?.Player?.Unit?.Health ?? 0;
        _output.WriteLine($"  [{label}] After revive: health={health}, flags=0x{afterRevive?.Player?.PlayerFlags:X}");
        return health > 0 && LiveBotFixture.IsStrictAlive(afterRevive);
    }

    private Task EnsureStrictAliveAsync(string account, string label)
        => _bot.EnsureStrictAliveAsync(account, label);

    private async Task<bool> WaitForDeadOrGhostStateAsync(string account, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            if (IsDeadOrGhost(snap, out _))
                return true;

            await Task.Delay(300);
        }

        return false;
    }

    private async Task<bool> WaitForBagItemPresenceAsync(string account, uint itemId, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            var hasItem = snap?.Player?.BagContents?.Values.Any(v => v == itemId) == true;
            if (hasItem)
                return true;

            await Task.Delay(400);
        }

        return false;
    }

    private static int CountBagSlotsForItem(Game.WoWPlayer? player, uint itemId)
        => player?.BagContents?.Values.Count(v => v == itemId) ?? 0;

    private static bool IsDeadOrGhost(WoWActivitySnapshot? snap, out string reason)
    {
        reason = string.Empty;

        var player = snap?.Player;
        var unit = player?.Unit;
        if (player == null || unit == null)
            return false;

        var hasGhostFlag = (player.PlayerFlags & PlayerFlagGhost) != 0;
        var standState = unit.Bytes1 & StandStateMask;

        if (hasGhostFlag)
        {
            reason = "ghost-flag";
            return true;
        }

        if (unit.Health == 0 || standState == StandStateDead)
        {
            reason = $"corpse-like health={unit.Health} standState={standState}";
            return true;
        }

        return false;
    }

    private static void AssertCommandSucceeded(LiveBotFixture.GmChatCommandTrace trace, string label, string command)
    {
        Assert.Equal(ResponseResult.Success, trace.DispatchResult);

        var rejected = trace.ChatMessages.Concat(trace.ErrorMessages).Any(LiveBotFixture.ContainsCommandRejection);
        Assert.False(rejected, $"[{label}] {command} was rejected by command table or permissions.");
    }
}
