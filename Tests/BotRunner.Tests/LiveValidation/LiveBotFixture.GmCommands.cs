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

    private int TrackSoapCommand(string command)
    {
        lock (_commandTrackingLock)
        {
            _soapCommandCounts.TryGetValue(command, out var count);
            count++;
            _soapCommandCounts[command] = count;
            return count;
        }
    }


    private void LogDuplicateCommand(string channel, string command, int count, string? accountName = null)
    {
        if (count <= 1)
            return;

        var message = accountName == null
            ? $"[CMD-TRACK] Duplicate {channel} command (x{count}): {command}"
            : $"[CMD-TRACK] Duplicate {channel} command for {accountName} (x{count}): {command}";

        _logger.LogWarning("{Message}", message);
        _testOutput?.WriteLine(message);
    }


    // ---- GM Command Helpers (via SOAP — independent of bots) ----

    /// <summary>Execute any GM command via SOAP.</summary>
    public async Task<string> ExecuteGMCommandAsync(string command)
    {
        var attemptCount = TrackSoapCommand(command);
        LogDuplicateCommand("SOAP", command, attemptCount);
        _logger.LogInformation("[GM] {Command}", command);

        var xmlPayload = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
        <soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
          <soap:Body>
            <ns1:executeCommand xmlns:ns1=""urn:MaNGOS"">
              <command>{command}</command>
            </ns1:executeCommand>
          </soap:Body>
        </soap:Envelope>";

        using var client = new HttpClient();
        var byteArray = Encoding.ASCII.GetBytes("ADMINISTRATOR:PASSWORD");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

        var content = new StringContent(xmlPayload, Encoding.UTF8, "text/xml");

        try
        {
            var response = await client.PostAsync($"http://127.0.0.1:{Config.SoapPort}", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[GM] Command failed: {Status}", response.StatusCode);
                return string.Empty;
            }

            var xml = XDocument.Parse(responseContent);

            // Check for SOAP fault
            var faultString = xml.Descendants("faultstring").FirstOrDefault()?.Value;
            if (!string.IsNullOrEmpty(faultString))
            {
                _logger.LogWarning("[GM] SOAP fault for '{Command}': {Fault}", command, faultString);

                // "There is no such command." means the command doesn't exist in MaNGOS command table — always a bug.
                if (faultString.Contains("no such command", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"[GM] Command not found in MaNGOS command table: '{command}'. " +
                        $"SOAP fault: {faultString}. Fix the command or remove it.");
                }

                return $"FAULT: {faultString}";
            }

            // Try namespace-qualified result first, then unqualified
            var result = xml.Descendants(XName.Get("result", "urn:MaNGOS")).FirstOrDefault()?.Value
                      ?? xml.Descendants("result").FirstOrDefault()?.Value;
            _logger.LogInformation("[GM] Result: {Result}", result);
            return result ?? "OK (no output)";
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[GM] Error: {Error}", ex.Message);
            return string.Empty;
        }
    }

    /// <summary>
    /// Teleport a character to coordinates. Uses bot chat (.go xyz) since SOAP
    /// .teleport name with coordinates does NOT exist on this MaNGOS server.
    /// Resolves character name to account name automatically.
    /// </summary>


    /// <summary>
    /// Teleport a character to coordinates. Uses bot chat (.go xyz) since SOAP
    /// .teleport name with coordinates does NOT exist on this MaNGOS server.
    /// Resolves character name to account name automatically.
    /// </summary>
    public async Task<string> TeleportAsync(string? characterName, int mapId, float x, float y, float z)
    {
        var account = ResolveAccountName(characterName);
        if (account == null)
        {
            _logger.LogWarning("[TELEPORT] Cannot resolve account for character '{Name}' — falling back to BG", characterName);
            account = BgAccountName!;
        }
        await BotTeleportAsync(account, mapId, x, y, z);
        return "OK";
    }

    /// <summary>Teleport the BG bot to a position (backward-compatible overload).</summary>


    /// <summary>Teleport the BG bot to a position (backward-compatible overload).</summary>
    public async Task<string> TeleportAsync(int mapId, float x, float y, float z)
    {
        await BotTeleportAsync(BgAccountName!, mapId, x, y, z);
        return "OK";
    }

    /// <summary>Resolve a character name to its account name.</summary>


    /// <summary>Resolve a character name to its account name.</summary>
    private string? ResolveAccountName(string? characterName)
    {
        if (characterName == null) return BgAccountName;
        if (characterName == BgCharacterName) return BgAccountName;
        if (characterName == FgCharacterName) return FgAccountName;
        var match = AllBots.FirstOrDefault(b => b.CharacterName == characterName);
        return match?.AccountName ?? BgAccountName;
    }

    /// <summary>Teleport a character to a named tele location via SOAP (.tele name). Works for offline characters.</summary>


    /// <summary>Teleport a character to a named tele location via SOAP (.tele name). Works for offline characters.</summary>
    public Task<string> TeleportToNamedAsync(string? characterName, string locationName)
        => ExecuteGMCommandWithRetryAsync($".tele name {characterName ?? BgCharacterName} {locationName}");

    /// <summary>Teleport the BG bot to a named tele location via SOAP.</summary>


    /// <summary>Teleport the BG bot to a named tele location via SOAP.</summary>
    public Task<string> TeleportToNamedAsync(string locationName)
        => TeleportToNamedAsync(BgCharacterName, locationName);

    /// <summary>Set a character's level.</summary>


    /// <summary>Set a character's level.</summary>
    public Task<string> SetLevelAsync(string? characterName, int level)
        => ExecuteGMCommandWithRetryAsync($".character level {characterName ?? BgCharacterName} {level}");


    public Task<string> SetLevelAsync(int level) => SetLevelAsync(BgCharacterName, level);

    /// <summary>Add an item to a character's bags.</summary>


    /// <summary>Add an item to a character's bags.</summary>
    public Task<string> AddItemAsync(string? characterName, uint itemId, int count = 1)
        => ExecuteGMCommandWithRetryAsync($".send items {characterName ?? BgCharacterName} \"Test\" \"item\" {itemId}:{count}");


    public Task<string> AddItemAsync(uint itemId, int count = 1) => AddItemAsync(BgCharacterName, itemId, count);

    public Task<string> KillPlayerAsync(string? characterName = null)
        => ExecuteGMCommandAsync($".die");


    public Task<string> RevivePlayerAsync(string? characterName = null)
        => ExecuteGMCommandWithRetryAsync($".revive {characterName ?? BgCharacterName}");


    public Task<string> SetFullHealthManaAsync() => ExecuteGMCommandAsync(".modify hp 9999");


    public Task<string> AddMoneyAsync(uint copper)
        => ExecuteGMCommandAsync($".modify money {copper}");


    public Task<string> LearnSpellAsync(uint spellId)
        => ExecuteGMCommandAsync($".learn {spellId}");

    /// <summary>
    /// Reset spells via bot chat (.reset spells). VMaNGOS requires GetSelectedPlayer() — SOAP has no selection context.
    /// Routes through bot chat where the player auto-selects itself.
    /// </summary>


    /// <summary>
    /// Reset spells via bot chat (.reset spells). VMaNGOS requires GetSelectedPlayer() — SOAP has no selection context.
    /// Routes through bot chat where the player auto-selects itself.
    /// </summary>
    public async Task ResetSpellsAsync(string? accountName = null)
        => await SendGmChatCommandAsync(accountName ?? BgAccountName!, ".reset spells");


    public Task<string> UnlearnSpellAsync(uint spellId)
        => ExecuteGMCommandAsync($".unlearn {spellId}");

    /// <summary>
    /// Reset a character's items via SOAP GM command.
    /// Strips ALL equipment + inventory. Character must be online.
    /// </summary>


    /// <summary>
    /// Reset a character's items via SOAP GM command.
    /// Strips ALL equipment + inventory. Character must be online.
    /// </summary>
    public Task<string> ResetItemsAsync(string characterName)
        => ExecuteGMCommandWithRetryAsync($".reset items {characterName}");

    /// <summary>
    /// Clear a bot's backpack by sending DestroyItem actions for all 16 slots.
    /// Alternative to `.reset items` (SOAP) — useful when you want to preserve equipped gear.
    /// `.reset items` strips ALL gear + inventory; this method only clears bag contents.
    /// Also destroys items in extra bag containers (bags 1-4, up to 16 slots each).
    /// Equipment slots (0-18) are NOT touched.
    /// </summary>


    // ---- Dual-bot helpers ----

    /// <summary>Execute a GM command for a specific character, with retry.</summary>
    public async Task<string> ExecuteGMCommandWithRetryAsync(string command, int maxRetries = 3)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            var result = await ExecuteGMCommandAsync(command);
            if (!string.IsNullOrEmpty(result) || i == maxRetries - 1)
                return result;
            _logger.LogWarning("[GM] Retry {Attempt}/{Max} for: {Command}", i + 1, maxRetries, command);
            await Task.Delay(1000);
        }
        return string.Empty;
    }

    /// <summary>Teleport a character and verify the position changed.</summary>


    /// <summary>Teleport a character and verify the position changed.</summary>
    public async Task<bool> TeleportAndVerifyAsync(string charName, string accountName, int mapId, float x, float y, float z, int timeoutMs = 10000)
    {
        async Task<bool> WaitForPositionAsync(int waitTimeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < waitTimeoutMs)
            {
                var snap = await GetSnapshotAsync(accountName);
                var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
                if (pos != null)
                {
                    var dist = Math.Sqrt(Math.Pow(pos.X - x, 2) + Math.Pow(pos.Y - y, 2));
                    var zDiff = Math.Abs(pos.Z - z);
                    if (dist < 50 && zDiff < 40) // avoid false positives when player keeps falling after teleport
                    {
                        _logger.LogInformation("[VERIFY] {Name} teleported to ({X:F0},{Y:F0},{Z:F0}), actual ({AX:F0},{AY:F0},{AZ:F0})",
                            charName, x, y, z, pos.X, pos.Y, pos.Z);
                        return true;
                    }
                }

                await Task.Delay(1000);
            }

            return false;
        }

        // Use bot chat .go xyz - SOAP .teleport name with coordinates does not exist on MaNGOS.
        await BotTeleportAsync(accountName, mapId, x, y, z);
        await WaitForTeleportSettledAsync(accountName, x, y);

        var firstWindowMs = Math.Max(3000, timeoutMs / 2);
        if (await WaitForPositionAsync(firstWindowMs))
            return true;

        // Retry with explicit map-less syntax when first attempt misses target.
        var xText = x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        var yText = y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        var zText = z.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        var fallbackCommand = $".go xyz {xText} {yText} {zText}";
        _logger.LogWarning("[VERIFY] {Name} teleport not near target yet; retrying with '{Command}'", charName, fallbackCommand);
        await SendGmChatCommandTrackedAsync(
            accountName,
            fallbackCommand,
            captureResponse: true,
            delayMs: 1000,
            allowWhenDead: false);
        await WaitForTeleportSettledAsync(accountName, x, y);

        var secondWindowMs = Math.Max(2000, timeoutMs - firstWindowMs);
        if (await WaitForPositionAsync(secondWindowMs))
            return true;

        _logger.LogWarning("[VERIFY] {Name} teleport failed - position not near target", charName);
        return false;
    }

    /// <summary>Send an action to a bot and wait for a brief processing delay.</summary>
}
