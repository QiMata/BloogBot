using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Communication;
using Microsoft.Extensions.Logging;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

public partial class LiveBotFixture
{
    private readonly object _snapshotMessageDeltaLock = new();

    private readonly SemaphoreSlim _refreshLock = new(1, 1);


    // ---- Snapshot Queries ----

    /// <summary>Refresh all bot snapshots from StateManager. Logs chat/error messages to test output.</summary>
    public async Task RefreshSnapshotsAsync()
    {
        // Fail fast if a client has crashed — prevents tests from timing out
        _serviceFixture.AssertClientAlive();

        if (_stateManagerClient == null) return;

        // Serialize concurrent callers (e.g. parallel corpse-run tasks) to prevent
        // race conditions on shared AllBots/BackgroundBot/ForegroundBot state.
        await _refreshLock.WaitAsync();
        try
        {
            // Determine how many bots we expect based on previously known account names.
            int expectedCount = (BgAccountName != null ? 1 : 0) + (FgAccountName != null ? 1 : 0);

            List<WoWActivitySnapshot> inWorld;
            // Brief retry: if InWorld count drops below expected (transient CharacterSelect flicker),
            // poll up to 3 times with 500ms intervals before accepting the reduced list.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                var snapshots = await _stateManagerClient.QuerySnapshotsAsync();
                inWorld = snapshots.Where(s => s.ScreenState == "InWorld" && !string.IsNullOrEmpty(s.CharacterName)).ToList();

                if (inWorld.Count >= expectedCount || expectedCount == 0)
                {
                    AllBots = inWorld;
                    IdentifyBots(inWorld);
                    await EnsureFgGmModeAsync();
                    LogSnapshotMessages();
                    return;
                }

                // Short wait before retry — transient flicker usually resolves in <1s
                if (attempt < 2)
                    await Task.Delay(500);
            }

            // Accept whatever we have after retries
            var finalSnapshots = await _stateManagerClient.QuerySnapshotsAsync();
            AllBots = finalSnapshots.Where(s => s.ScreenState == "InWorld" && !string.IsNullOrEmpty(s.CharacterName)).ToList();
            IdentifyBots(AllBots);
            await EnsureFgGmModeAsync();
            LogSnapshotMessages();
        }
        finally
        {
            _refreshLock.Release();
        }
    }


    private void LogSnapshotMessages()
    {
        // Surface chat/error messages from snapshots to test output (via ITestOutputHelper so they appear in xUnit output)
        foreach (var snap in AllBots)
        {
            var label = snap.AccountName == FgAccountName ? "FG" : "BG";
            var accountKey = string.IsNullOrWhiteSpace(snap.AccountName) ? label : snap.AccountName;
            WriteSnapshotMessageDelta(accountKey, label, snap.RecentChatMessages, _lastPrintedChatCountByAccount, "CHAT");
            WriteSnapshotMessageDelta(accountKey, label, snap.RecentErrors, _lastPrintedErrorCountByAccount, "ERROR");
        }
    }

    /// <summary>
    /// Send .gm on to FG bot if it just appeared InWorld and hasn't received it yet.
    /// FG enters world late (WoW.exe injection takes 60s+), so this catches the case
    /// where fixture init completed before FG was available.
    /// </summary>


    /// <summary>
    /// Send .gm on to FG bot if it just appeared InWorld and hasn't received it yet.
    /// FG enters world late (WoW.exe injection takes 60s+), so this catches the case
    /// where fixture init completed before FG was available.
    /// </summary>
    private async Task EnsureFgGmModeAsync()
    {
        if (_fgGmModeSent || FgAccountName == null || ForegroundBot == null) return;

        _logger.LogInformation("[FIXTURE] FG bot just entered world — sending deferred .gm on");
        await SendGmChatCommandAsync(FgAccountName, ".gm on");
        _fgGmModeSent = true;
    }


    private void WriteSnapshotMessageDelta(
        string accountKey,
        string label,
        IReadOnlyList<string> messages,
        Dictionary<string, int> lastPrintedCountByAccount,
        string channel)
    {
        lock (_snapshotMessageDeltaLock)
        {
            lastPrintedCountByAccount.TryGetValue(accountKey, out var lastPrintedCount);

            // Snapshot windows are rolling (MaxBufferedMessages). If they shrink or reset, print from 0.
            var startIndex = messages.Count >= lastPrintedCount ? lastPrintedCount : 0;
            for (var i = startIndex; i < messages.Count; i++)
                _testOutput?.WriteLine($"[{label}:{channel}] {messages[i]}");

            lastPrintedCountByAccount[accountKey] = messages.Count;
        }
    }

    /// <summary>Log key diagnostic fields from a snapshot. Useful for debugging FG issues.</summary>


    /// <summary>Log key diagnostic fields from a snapshot. Useful for debugging FG issues.</summary>
    public void DumpSnapshotDiagnostics(WoWActivitySnapshot? snap, string label)
    {
        if (snap == null) { _testOutput?.WriteLine($"[{label}] Snapshot is null"); return; }
        var p = snap.Player;
        _testOutput?.WriteLine($"[{label}] Screen={snap.ScreenState}, Char={snap.CharacterName}, " +
            $"Inventory={p?.Inventory?.Count ?? -1}, Bags={p?.BagContents?.Count ?? -1}, Skills={p?.SkillInfo?.Count ?? -1}");
        if (p?.Inventory != null)
            foreach (var kvp in p.Inventory)
                _testOutput?.WriteLine($"[{label}]   Equip[{kvp.Key}] = 0x{kvp.Value:X}");
        if (snap.RecentChatMessages.Count > 0)
            foreach (var msg in snap.RecentChatMessages)
                _testOutput?.WriteLine($"[{label}]   Chat: {msg}");
        if (snap.RecentErrors.Count > 0)
            foreach (var err in snap.RecentErrors)
                _testOutput?.WriteLine($"[{label}]   Error: {err}");
    }

    /// <summary>Get a fresh snapshot for a specific account.</summary>


    /// <summary>Get a fresh snapshot for a specific account.</summary>
    public async Task<WoWActivitySnapshot?> GetSnapshotAsync(string accountName)
    {
        if (_stateManagerClient == null) return null;
        var snapshots = await _stateManagerClient.QuerySnapshotsAsync(accountName);
        return snapshots.FirstOrDefault();
    }

    /// <summary>Forward an action to a specific bot.</summary>


    /// <summary>
    /// Generic snapshot polling helper. Refreshes snapshots in a loop until the predicate
    /// returns true or the timeout expires. Replaces ad-hoc Stopwatch + while + RefreshSnapshots
    /// polling loops scattered across test files.
    /// </summary>
    /// <param name="accountName">Account to poll snapshots for.</param>
    /// <param name="predicate">Condition to check on each snapshot. Null snapshots are treated as non-matching.</param>
    /// <param name="timeout">Maximum time to wait before returning false.</param>
    /// <param name="pollIntervalMs">Milliseconds between polls (default 400ms).</param>
    /// <returns>True if the predicate was satisfied within the timeout, false otherwise.</returns>
    public async Task<bool> WaitForSnapshotConditionAsync(
        string accountName,
        Func<WoWActivitySnapshot, bool> predicate,
        TimeSpan timeout,
        int pollIntervalMs = 400)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await RefreshSnapshotsAsync();
            var snap = await GetSnapshotAsync(accountName);
            if (snap != null && predicate(snap))
                return true;
            await Task.Delay(pollIntervalMs);
        }
        return false;
    }

    /// <summary>
    /// Ensure a bot is strict-alive before test setup. If dead or ghost, issues a SOAP revive
    /// and polls snapshots until the alive state is confirmed. Skips the test if alive state
    /// cannot be established (infrastructure issue, not a test failure).
    /// </summary>
    /// <param name="account">Account name of the bot to check.</param>
    /// <param name="label">Human-readable label for log messages (e.g. "BG", "FG").</param>
    /// <param name="timeoutSeconds">Seconds to wait for strict-alive after revive (default 15).</param>


    // ---- Snapshot-based helpers (replace old ObjectManager-direct queries) ----

    /// <summary>
    /// Wait for player position to change (via snapshot polling).
    /// Returns true if position changed by more than 1 unit on X or Y axis.
    /// </summary>
    public async Task<bool> WaitForPositionChangeAsync(string? accountName, float startX, float startY, float startZ, int timeoutMs = 10000)
    {
        var acct = accountName ?? BgAccountName;
        if (acct == null || _stateManagerClient == null) return false;

        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            var snap = await GetSnapshotAsync(acct);
            var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
            if (pos != null)
            {
                if (Math.Abs(pos.X - startX) > 1 || Math.Abs(pos.Y - startY) > 1)
                    return true;
            }
            await Task.Delay(1000);
        }
        return false;
    }


    public Task<bool> WaitForPositionChangeAsync(float startX, float startY, float startZ, int timeoutMs = 10000)
        => WaitForPositionChangeAsync(BgAccountName, startX, startY, startZ, timeoutMs);

    /// <summary>
    /// Wait for Z to stabilize (not falling through world).
    /// Polls snapshots and checks Z samples over the given window.
    /// </summary>


    /// <summary>
    /// Wait for a nearby unit with specified NPC flags to appear in the bot's snapshot.
    /// </summary>
    public async Task<Game.WoWUnit?> WaitForNearbyUnitAsync(string? accountName, uint npcFlags, int timeoutMs = 10000)
    {
        var acct = accountName ?? BgAccountName;
        if (acct == null || _stateManagerClient == null) return null;

        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            var snap = await GetSnapshotAsync(acct);
            if (snap != null)
            {
                var unit = snap.NearbyUnits.FirstOrDefault(u => (u.NpcFlags & npcFlags) != 0);
                if (unit != null) return unit;
            }
            await Task.Delay(1000);
        }
        return null;
    }


    public Task<Game.WoWUnit?> WaitForNearbyUnitAsync(uint npcFlags, int timeoutMs = 10000)
        => WaitForNearbyUnitAsync(BgAccountName, npcFlags, timeoutMs);

    // ---- Bot-chat GM command helpers (bots are GM accounts, send commands through their own chat) ----

    /// <summary>
    /// Send a GM command through a bot's in-game chat. The bot types the command in chat,
    /// and because the account is GM-level, the server processes it. This is used for
    /// commands like .learn, .additem that need to target the current character (self).
    /// </summary>


    /// <summary>
    /// Wait for a bot to settle at the expected position after a teleport.
    /// Polls snapshots until XY is within 50y of target and Z is stable (2 consecutive samples within 1y).
    /// </summary>
    public async Task<bool> WaitForTeleportSettledAsync(string accountName, float expectedX, float expectedY, int timeoutMs = 3000)
    {
        float? lastZ = null;
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            var snap = await GetSnapshotAsync(accountName);
            var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
            if (pos != null)
            {
                var dx = pos.X - expectedX;
                var dy = pos.Y - expectedY;
                var dist2D = MathF.Sqrt(dx * dx + dy * dy);
                if (dist2D < 50f && lastZ.HasValue && MathF.Abs(pos.Z - lastZ.Value) < 1f)
                    return true;
                lastZ = pos.Z;
            }

            await Task.Delay(500);
        }

        return false;
    }

    /// <summary>Teleport a bot to a named location by having it type .tele in chat (self-teleport).</summary>
}
