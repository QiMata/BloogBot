using BotRunner.Travel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Mage city-teleport live validation.
///
/// Migrated to the Shodan test-director shape:
///   1) launch TRMAF5 + TRMAB5 + SHODAN with MageTeleport.config.json,
///   2) stage each BotRunner target through StageBotRunnerLoadoutAsync
///      (teach city-teleport spell + add Rune of Teleportation reagents)
///      and StageBotRunnerAtRazorHillAsync,
///   3) dispatch only ActionType.CastSpell from the test body,
///   4) assert on snapshot position arrival.
///
/// SHODAN remains director-only.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class MageTeleportTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int KalimdorMapId = 1;

    // Orgrimmar arrival landing (matches MageTeleportData[3567]).
    private const float OrgTeleportX = 1676.0f;
    private const float OrgTeleportY = -4315.0f;
    private const float OrgArrivalRadius = 50.0f;

    private const uint TeleportOrgrimmar = 3567;
    private const uint TeleportStormwind = 3561;
    private const uint RuneOfTeleportation = (uint)MageTeleportData.RuneOfTeleportation;

    private static readonly uint[] AllCityTeleportSpells =
    {
        3567, // Orgrimmar
        3563, // Undercity
        3566, // Thunder Bluff
        3561, // Stormwind
        3562, // Ironforge
        3565, // Darnassus
    };

    private static readonly string[] AllCityTeleportNames =
    {
        "Orgrimmar",
        "Undercity",
        "Thunder Bluff",
        "Stormwind",
        "Ironforge",
        "Darnassus",
    };

    public MageTeleportTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    /// <summary>
    /// Horde mage at Razor Hill casts Teleport: Orgrimmar (3567).
    ///
    /// BG-only by design: <c>ActionType.CastSpell</c> dispatches via
    /// <c>_objectManager.CastSpell(int spellId)</c>, which is a documented
    /// no-op on the Foreground runner (only the <c>CastSpellByName(string)</c>
    /// Lua overload casts there). The launch roster still includes the FG
    /// mage so the Shodan/FG/BG topology stays intact, but only BG receives
    /// the action dispatch.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task MageTeleport_Horde_OrgrimmarArrival()
    {
        await EnsureMageTeleportSettingsAsync();
        var targets = _bot.ResolveBotRunnerActionTargets(foregroundFirst: false);
        var bgTarget = targets.FirstOrDefault(t => !t.IsForeground);
        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(bgTarget.AccountName),
            "Horde Orgrimmar teleport requires a BG bot (FG ActionType.CastSpell-by-id is a no-op).");

        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no CastSpell dispatch.");
        foreach (var target in targets)
        {
            var willDispatch = target.IsForeground ? "idle (FG ActionType.CastSpell-by-id is a no-op)" : "dispatch CastSpell";
            _output.WriteLine(
                $"[ACTION-PLAN] {target.RoleLabel} {target.AccountName}/{target.CharacterName}: " +
                $"stage Razor Hill + Teleport: Orgrimmar ({TeleportOrgrimmar}), {willDispatch}.");
        }

        var passed = await RunOrgrimmarTeleportScenario(bgTarget.AccountName, bgTarget.RoleLabel);

        Assert.True(
            passed,
            $"{bgTarget.RoleLabel} bot ({bgTarget.AccountName}/{bgTarget.CharacterName}): " +
            "Mage should arrive in Orgrimmar within 15s of casting Teleport: Orgrimmar.");
    }

    /// <summary>
    /// Alliance mage casts Teleport: Stormwind (3561). Skips when the
    /// configured roster is Horde-only (the default for the live fixture).
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task MageTeleport_Alliance_StormwindArrival()
    {
        await EnsureMageTeleportSettingsAsync();
        var targets = _bot.ResolveBotRunnerActionTargets(foregroundFirst: false);
        var bgTarget = targets.FirstOrDefault(t => !t.IsForeground);
        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(bgTarget.AccountName),
            "Alliance Stormwind teleport requires a BG bot.");

        await _bot.RefreshSnapshotsAsync();
        var raceSnapshot = await _bot.GetSnapshotAsync(bgTarget.AccountName);
        global::Tests.Infrastructure.Skip.IfNot(
            IsAllianceRace(raceSnapshot),
            "MageTeleport.config.json roster is Horde-only; Stormwind teleport requires an Alliance character.");

        await _bot.StageBotRunnerLoadoutAsync(
            bgTarget.AccountName,
            bgTarget.RoleLabel,
            spellsToLearn: new[] { TeleportStormwind },
            itemsToAdd: new[] { new LiveBotFixture.ItemDirective(RuneOfTeleportation, 5) });

        var stormwindKnown = await _bot.WaitForSnapshotConditionAsync(
            bgTarget.AccountName,
            s => s.Player?.SpellList?.Contains(TeleportStormwind) == true,
            TimeSpan.FromSeconds(5),
            pollIntervalMs: 300,
            progressLabel: $"{bgTarget.RoleLabel} teleport-sw-learn");
        Assert.True(stormwindKnown, "Teleport: Stormwind should be present in SpellList before cast.");

        await _bot.RefreshSnapshotsAsync();
        var startSnap = await _bot.GetSnapshotAsync(bgTarget.AccountName);
        Assert.NotNull(startSnap);
        var startPos = startSnap!.Player?.Unit?.GameObject?.Base?.Position;
        Assert.NotNull(startPos);
        _output.WriteLine($"[TEST] Start position: ({startPos!.X:F1}, {startPos.Y:F1}, {startPos.Z:F1})");

        var castResult = await _bot.SendActionAsync(bgTarget.AccountName, new ActionMessage
        {
            ActionType = ActionType.CastSpell,
            Parameters = { new RequestParameter { IntParam = (int)TeleportStormwind } }
        });
        _output.WriteLine($"[TEST] CAST_SPELL result: {castResult}");
        Assert.Equal(ResponseResult.Success, castResult);

        var moved = await _bot.WaitForPositionChangeAsync(
            bgTarget.AccountName,
            startPos.X,
            startPos.Y,
            startPos.Z,
            timeoutMs: 15000,
            progressLabel: $"{bgTarget.RoleLabel} mage-teleport-sw");
        Assert.True(moved, "Mage should teleport to Stormwind within 15s.");
    }

    /// <summary>
    /// Portal: Orgrimmar (11417) placeholder — exercises the staging path so
    /// the spell appears in the BG snapshot's SpellList. A future migration
    /// can extend this to a multi-bot party portal flow.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task MagePortal_PartyTeleported()
    {
        await EnsureMageTeleportSettingsAsync();
        var targets = _bot.ResolveBotRunnerActionTargets(foregroundFirst: false);
        var bgTarget = targets.FirstOrDefault(t => !t.IsForeground);
        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(bgTarget.AccountName),
            "MagePortal placeholder requires a BG bot.");

        const uint portalOrgrimmar = 11417;
        await _bot.StageBotRunnerLoadoutAsync(
            bgTarget.AccountName,
            bgTarget.RoleLabel,
            spellsToLearn: new[] { portalOrgrimmar });

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(bgTarget.AccountName);
        Assert.NotNull(snap);
        var hasSpell = snap!.Player?.SpellList?.Contains(portalOrgrimmar) == true;
        _output.WriteLine($"[TEST] Has Portal: Orgrimmar spell: {hasSpell}");
        Assert.True(hasSpell, "Portal: Orgrimmar should be present in SpellList after staging.");
    }

    /// <summary>
    /// Validate all 6 city teleport spells can be staged through the Shodan
    /// director and observed in the BG snapshot's SpellList.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task MageAllCityTeleports()
    {
        await EnsureMageTeleportSettingsAsync();
        var targets = _bot.ResolveBotRunnerActionTargets(foregroundFirst: false);
        var bgTarget = targets.FirstOrDefault(t => !t.IsForeground);
        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(bgTarget.AccountName),
            "All-city teleport sweep requires a BG bot.");

        await _bot.StageBotRunnerLoadoutAsync(
            bgTarget.AccountName,
            bgTarget.RoleLabel,
            spellsToLearn: AllCityTeleportSpells);

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(bgTarget.AccountName);
        Assert.NotNull(snap);
        var spellList = snap!.Player?.SpellList;
        Assert.NotNull(spellList);

        var missing = new List<string>();
        for (var i = 0; i < AllCityTeleportSpells.Length; i++)
        {
            var has = spellList!.Contains(AllCityTeleportSpells[i]);
            _output.WriteLine($"[TEST] Spell {AllCityTeleportSpells[i]} ({AllCityTeleportNames[i]}): {(has ? "LEARNED" : "MISSING")}");
            if (!has)
                missing.Add($"{AllCityTeleportSpells[i]} ({AllCityTeleportNames[i]})");
        }

        Assert.True(
            missing.Count == 0,
            "All 6 city teleport spells should appear in BG SpellList after staging. Missing: " +
            string.Join(", ", missing));
    }

    private async Task<bool> RunOrgrimmarTeleportScenario(string account, string label)
    {
        // Teleport: Orgrimmar requires level 20 / ~620 mana. Bump level so the
        // mage has the mana pool for a self-teleport cast.
        const int MinTeleportLevel = 20;

        await _bot.StageBotRunnerLoadoutAsync(
            account,
            label,
            spellsToLearn: new[] { TeleportOrgrimmar },
            itemsToAdd: new[] { new LiveBotFixture.ItemDirective(RuneOfTeleportation, 5) },
            levelTo: MinTeleportLevel);

        var spellKnown = await _bot.WaitForSnapshotConditionAsync(
            account,
            s => s.Player?.SpellList?.Contains(TeleportOrgrimmar) == true,
            TimeSpan.FromSeconds(8),
            pollIntervalMs: 300,
            progressLabel: $"{label} teleport-org-learn");
        if (!spellKnown)
        {
            _output.WriteLine($"  [{label}] Teleport: Orgrimmar never appeared in SpellList; aborting.");
            return false;
        }

        var reagentKnown = await _bot.WaitForSnapshotConditionAsync(
            account,
            s => s.Player?.BagContents?.Values.Any(value => value == RuneOfTeleportation) == true,
            TimeSpan.FromSeconds(8),
            pollIntervalMs: 300,
            progressLabel: $"{label} teleport-reagent");
        if (!reagentKnown)
        {
            _output.WriteLine($"  [{label}] Rune of Teleportation never observed in bags; aborting.");
            return false;
        }

        var staged = await _bot.StageBotRunnerAtRazorHillAsync(account, label);
        if (!staged)
        {
            _output.WriteLine($"  [{label}] Razor Hill stage did not settle.");
            var stageSnap = await _bot.GetSnapshotAsync(account);
            _bot.DumpSnapshotDiagnostics(stageSnap, label);
            return false;
        }

        await _bot.RefreshSnapshotsAsync();
        var startSnap = await _bot.GetSnapshotAsync(account);
        var startPos = startSnap?.Player?.Unit?.GameObject?.Base?.Position;
        if (startPos == null)
        {
            _output.WriteLine($"  [{label}] No start position after Razor Hill stage; aborting.");
            return false;
        }
        _output.WriteLine($"  [{label}] Start position: ({startPos.X:F1}, {startPos.Y:F1}, {startPos.Z:F1})");

        var castResult = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.CastSpell,
            Parameters = { new RequestParameter { IntParam = (int)TeleportOrgrimmar } }
        });
        _output.WriteLine($"  [{label}] CAST_SPELL result: {castResult}");
        Assert.Equal(ResponseResult.Success, castResult);

        // Self-teleport spells have a 10s cast time, so allow 20s for cast + arrival.
        var arrived = await _bot.WaitForSnapshotConditionAsync(
            account,
            snap =>
            {
                var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
                if (pos == null) return false;
                var mapId = snap?.Player?.Unit?.GameObject?.Base?.MapId;
                if (mapId == null || mapId.Value != KalimdorMapId)
                    return false;
                var dist = LiveBotFixture.Distance2D(pos.X, pos.Y, OrgTeleportX, OrgTeleportY);
                return dist <= OrgArrivalRadius;
            },
            TimeSpan.FromSeconds(20),
            pollIntervalMs: 1000,
            progressLabel: $"{label} mage-teleport-org");

        await _bot.RefreshSnapshotsAsync();
        var endSnap = await _bot.GetSnapshotAsync(account);
        var endPos = endSnap?.Player?.Unit?.GameObject?.Base?.Position;
        _output.WriteLine($"  [{label}] End position: ({endPos?.X:F1}, {endPos?.Y:F1}, {endPos?.Z:F1})");

        if (!arrived)
            _bot.DumpSnapshotDiagnostics(endSnap, label);

        return arrived;
    }

    private async Task<string> EnsureMageTeleportSettingsAsync()
    {
        var settingsPath = ResolveRepoPath(
            "Services", "WoWStateManager", "Settings", "Configs", "MageTeleport.config.json");

        await _bot.EnsureSettingsAsync(settingsPath);
        _bot.SetOutput(_output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
        await _bot.AssertConfiguredCharactersMatchAsync(settingsPath);
        return settingsPath;
    }

    private static bool IsAllianceRace(WoWActivitySnapshot? snapshot)
    {
        var raceId = (snapshot?.Player?.PlayerBytes0 ?? 0) & 0xFF;
        return raceId is 1 or 3 or 4 or 7;
    }

    private static string ResolveRepoPath(params string[] segments)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine([dir.FullName, .. segments]);
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not locate repo path: {Path.Combine(segments)}");
    }
}
