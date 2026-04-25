using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Shodan-directed Alliance navigation staging checks. The BG action target is
/// an Alliance character from Navigation.config.json; SHODAN owns location
/// staging and the test body asserts snapshots only.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class AllianceNavigationTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int EasternKingdoms = 0;

    private static readonly AllianceNavigationPoint Goldshire = new(
        "Goldshire",
        EasternKingdoms,
        -9464.0f,
        62.0f,
        56.0f);

    private static readonly AllianceNavigationPoint StormwindTradeDistrict = new(
        "Stormwind Trade District",
        EasternKingdoms,
        -8833.0f,
        628.0f,
        94.0f);

    private static readonly AllianceNavigationPoint WestfallDeadminesApproach = new(
        "Westfall Deadmines approach",
        EasternKingdoms,
        -11208.0f,
        1670.0f,
        25.0f);

    private static readonly AllianceNavigationPoint StormwindStockadeEntrance = new(
        "Stormwind Stockade entrance",
        EasternKingdoms,
        -8776.0f,
        839.0f,
        91.0f);

    private static readonly AllianceNavigationPoint DunMoroghGnomereganApproach = new(
        "Dun Morogh Gnomeregan approach",
        EasternKingdoms,
        -5163.0f,
        925.0f,
        257.0f);

    public AllianceNavigationTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Alliance_GoldshireToStormwind()
    {
        await StageAndAssertSnapshotAsync(Goldshire, "P29.19 Alliance Goldshire to Stormwind navigation");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Alliance_VendorBuySell()
    {
        await StageAndAssertSnapshotAsync(StormwindTradeDistrict, "P29.20 Alliance vendor buy/sell");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Alliance_Deadmines_Entry()
    {
        await StageAndAssertSnapshotAsync(WestfallDeadminesApproach, "P29.21 Alliance Deadmines entry");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Alliance_Stockade_Entry()
    {
        await StageAndAssertSnapshotAsync(StormwindStockadeEntrance, "P29.22 Alliance Stockade entry");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Alliance_Gnomeregan_Entry()
    {
        await StageAndAssertSnapshotAsync(DunMoroghGnomereganApproach, "P29.23 Alliance Gnomeregan entry");
    }

    private async Task StageAndAssertSnapshotAsync(AllianceNavigationPoint point, string scenario)
    {
        var target = await EnsureAllianceNavigationSettingsAndTargetAsync();
        _output.WriteLine($"[SHODAN] Staging {target.RoleLabel} at {point.Label}: {scenario}");

        var staged = await _bot.StageBotRunnerAtNavigationPointAsync(
            target.AccountName,
            target.RoleLabel,
            point.MapId,
            point.X,
            point.Y,
            point.Z,
            point.Label);
        if (staged)
        {
            await _bot.QuiesceAccountsAsync(
                new[] { target.AccountName },
                $"{target.RoleLabel} alliance navigation staged");
        }
        else
        {
            var failed = await _bot.GetSnapshotAsync(target.AccountName);
            var failedPos = failed?.Player?.Unit?.GameObject?.Base?.Position;
            Assert.Fail(
                $"Expected {target.RoleLabel} {target.AccountName} to stage at {point.Label}. " +
                $"finalMap={failed?.CurrentMapId ?? 0} pos=({failedPos?.X:F1},{failedPos?.Y:F1},{failedPos?.Z:F1})");
        }

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(target.AccountName);
        Assert.NotNull(snap);

        var pos = snap!.Player?.Unit?.GameObject?.Base?.Position;
        Assert.NotNull(pos);
        Assert.Equal((uint)point.MapId, snap.CurrentMapId);

        var distance = LiveBotFixture.Distance2D(pos!.X, pos.Y, point.X, point.Y);
        Assert.True(distance <= 15f,
            $"Expected {target.AccountName} near {point.Label}; distance={distance:F1}y.");

        _output.WriteLine(
            $"[TEST] snapshot received - {scenario}; {target.AccountName} at " +
            $"map={snap.CurrentMapId} pos=({pos.X:F1},{pos.Y:F1},{pos.Z:F1})");
    }

    private async Task<LiveBotFixture.BotRunnerActionTarget> EnsureAllianceNavigationSettingsAndTargetAsync()
    {
        var settingsPath = ResolveRepoPath(
            "Services", "WoWStateManager", "Settings", "Configs", "Navigation.config.json");

        await _bot.EnsureSettingsAsync(settingsPath);
        _bot.SetOutput(_output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
        await _bot.AssertConfiguredCharactersMatchAsync(settingsPath);
        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(_bot.ShodanAccountName),
            "Shodan director was not launched by Navigation.config.json.");

        var target = _bot.ResolveBotRunnerActionTargets(
                includeForegroundIfActionable: false,
                foregroundFirst: false)
            .Single(target => !target.IsForeground);

        _output.WriteLine(
            $"[ACTION-PLAN] {target.RoleLabel} {target.AccountName}/{target.CharacterName}: " +
            "BG Alliance navigation staging target.");
        _output.WriteLine(
            $"[ACTION-PLAN] FG {_bot.FgAccountName}/{_bot.FgCharacterName}: launched idle for topology parity.");
        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no navigation dispatch.");

        return target;
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

    private readonly record struct AllianceNavigationPoint(
        string Label,
        int MapId,
        float X,
        float Y,
        float Z);
}
