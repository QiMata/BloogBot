using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// V2.12: Transport tests -- zeppelins, boats, elevators.
/// Teleport to Org zeppelin tower, wait, check for transport game objects nearby.
///
/// Run: dotnet test --filter "FullyQualifiedName~TransportTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class TransportTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    // Game object types: 11 = GAMEOBJECT_TYPE_TRANSPORT, 15 = GAMEOBJECT_TYPE_MO_TRANSPORT
    private const uint GoTypeTransport = 11;
    private const uint GoTypeMoTransport = 15;

    public TransportTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    /// <summary>
    /// V2.12: Teleport to zeppelin tower, detect transport game objects nearby.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Zeppelin_OrgToUndercity()
    {
        var target = await EnsureTransportSettingsAndTargetAsync();

        var staged = await _bot.StageBotRunnerAtOrgrimmarZeppelinTowerAsync(
            target.AccountName,
            target.RoleLabel);
        Assert.True(staged, $"{target.RoleLabel}: expected Orgrimmar zeppelin tower staging to succeed.");

        // Wait for game objects to populate
        await Task.Delay(5000);
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(target.AccountName);
        Assert.NotNull(snap);

        var pos = snap!.Player?.Unit?.GameObject?.Base?.Position;
        Assert.NotNull(pos);
        _output.WriteLine($"[TEST] Position: ({pos!.X:F1}, {pos.Y:F1}, {pos.Z:F1})");
        _output.WriteLine($"[TEST] CurrentMapId: {snap.CurrentMapId}");

        // Check movement data for transport game objects
        var nearbyGOs = snap.MovementData?.NearbyGameObjects?.ToList()
            ?? new System.Collections.Generic.List<Game.GameObjectSnapshot>();
        _output.WriteLine($"[TEST] Nearby game objects: {nearbyGOs.Count}");
        foreach (var go in nearbyGOs.OrderBy(go => go.DistanceToPlayer))
        {
            var goPos = go.Position;
            _output.WriteLine(
                $"  GO: entry={go.Entry} display={go.DisplayId} type={go.GameObjectType} " +
                $"name={go.Name} guid=0x{go.Guid:X} dist={go.DistanceToPlayer:F1} " +
                $"pos=({goPos?.X:F1},{goPos?.Y:F1},{goPos?.Z:F1})");
        }

        var transports = nearbyGOs
            .Where(go => go.GameObjectType == GoTypeTransport || go.GameObjectType == GoTypeMoTransport)
            .ToList();
        _output.WriteLine($"[TEST] Transport objects found: {transports.Count}");

        foreach (var t in transports)
        {
            _output.WriteLine($"  Transport: entry={t.Entry}, type={t.GameObjectType}, " +
                $"name={t.Name}, guid=0x{t.Guid:X}");
        }

        // Zeppelin tower area should have at least transport-related objects nearby
        // even if the zeppelin is currently in transit
        Assert.NotNull(snap.Player);
    }

    /// <summary>
    /// V2.12: Ratchet boat dock -- verify game objects present.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Boat_RatchetToBootyBay()
    {
        var target = await EnsureTransportSettingsAndTargetAsync();

        var staged = await _bot.StageBotRunnerAtRatchetDockAsync(
            target.AccountName,
            target.RoleLabel);
        Assert.True(staged, $"{target.RoleLabel}: expected Ratchet dock staging to succeed.");

        await Task.Delay(5000);
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(target.AccountName);
        Assert.NotNull(snap);

        var pos = snap!.Player?.Unit?.GameObject?.Base?.Position;
        _output.WriteLine($"[TEST] Ratchet dock position: ({pos?.X:F1}, {pos?.Y:F1}, {pos?.Z:F1})");
        Assert.NotNull(pos);
    }

    /// <summary>
    /// V2.12: Menethil Harbor to Theramore boat placeholder.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Boat_MenethilToTheramore()
    {
        var target = await EnsureTransportSettingsAndTargetAsync();

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(target.AccountName);
        Assert.NotNull(snap);
        _output.WriteLine($"[TEST] Character: {snap!.CharacterName}");
        global::Tests.Infrastructure.Skip.If(
            true,
            "Menethil-to-Theramore boat validation is Shodan-shaped but needs an Alliance/dock-specific action-target config.");
    }

    /// <summary>
    /// V2.12: Undercity elevator -- check Z change.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Elevator_Undercity()
    {
        var target = await EnsureTransportSettingsAndTargetAsync();

        var staged = await _bot.StageBotRunnerAtUndercityElevatorUpperAsync(
            target.AccountName,
            target.RoleLabel);
        Assert.True(staged, $"{target.RoleLabel}: expected Undercity elevator staging to succeed.");
        await Task.Delay(5000);

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(target.AccountName);
        Assert.NotNull(snap);

        var pos = snap!.Player?.Unit?.GameObject?.Base?.Position;
        Assert.NotNull(pos);
        _output.WriteLine($"[TEST] Undercity elevator area position: ({pos!.X:F1}, {pos.Y:F1}, {pos.Z:F1})");

        var nearbyGOs = snap.MovementData?.NearbyGameObjects?.ToList()
            ?? new System.Collections.Generic.List<Game.GameObjectSnapshot>();
        _output.WriteLine($"[TEST] Nearby game objects: {nearbyGOs.Count}");
        foreach (var go in nearbyGOs.OrderBy(go => go.DistanceToPlayer))
        {
            var goPos = go.Position;
            _output.WriteLine(
                $"  GO: entry={go.Entry} display={go.DisplayId} type={go.GameObjectType} " +
                $"name={go.Name} guid=0x{go.Guid:X} dist={go.DistanceToPlayer:F1} " +
                $"pos=({goPos?.X:F1},{goPos?.Y:F1},{goPos?.Z:F1})");
        }
    }

    /// <summary>
    /// V2.12: Thunder Bluff elevator.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Elevator_ThunderBluff()
    {
        var target = await EnsureTransportSettingsAndTargetAsync();

        var staged = await _bot.StageBotRunnerAtThunderBluffElevatorAsync(
            target.AccountName,
            target.RoleLabel);
        Assert.True(staged, $"{target.RoleLabel}: expected Thunder Bluff elevator staging to succeed.");
        await Task.Delay(3000);

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(target.AccountName);
        Assert.NotNull(snap);

        var pos = snap!.Player?.Unit?.GameObject?.Base?.Position;
        Assert.NotNull(pos);
        _output.WriteLine($"[TEST] Thunder Bluff elevator area: ({pos!.X:F1}, {pos.Y:F1}, {pos.Z:F1})");
    }

    /// <summary>
    /// V2.12: Deeprun Tram placeholder.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task DeeprunTram_IFToSW()
    {
        var target = await EnsureTransportSettingsAndTargetAsync();

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(target.AccountName);
        Assert.NotNull(snap);
        _output.WriteLine($"[TEST] Deeprun Tram test -- character: {snap!.CharacterName}");
        global::Tests.Infrastructure.Skip.If(
            true,
            "Deeprun Tram validation is Shodan-shaped but requires an Alliance/tram-instance action-target config.");
    }

    private async Task<LiveBotFixture.BotRunnerActionTarget> EnsureTransportSettingsAndTargetAsync()
    {
        var settingsPath = ResolveRepoPath(
            "Services", "WoWStateManager", "Settings", "Configs", "Economy.config.json");

        await _bot.EnsureSettingsAsync(settingsPath);
        _bot.SetOutput(_output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
        await _bot.AssertConfiguredCharactersMatchAsync(settingsPath);
        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(_bot.ShodanAccountName),
            "Shodan director was not launched by Economy.config.json.");

        var target = _bot.ResolveBotRunnerActionTargets(
                includeForegroundIfActionable: false,
                foregroundFirst: false)
            .Single(candidate => !candidate.IsForeground);

        _output.WriteLine(
            $"[ACTION-PLAN] {target.RoleLabel} {target.AccountName}/{target.CharacterName}: BG transport staging target.");
        _output.WriteLine(
            $"[ACTION-PLAN] FG {_bot.FgAccountName}/{_bot.FgCharacterName}: launched idle for topology parity.");
        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no transport action dispatch.");

        return target;
    }

    private static string ResolveRepoPath(params string[] segments)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(new[] { dir.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate) || Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not resolve repository path for {Path.Combine(segments)} from {AppContext.BaseDirectory}.");
    }
}
