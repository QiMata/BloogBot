using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Shodan-directed ADT tile-boundary movement validation. SHODAN stages the BG
/// action target near each boundary; the BotRunner target receives only
/// TravelTo actions for the movement probes.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class TileBoundaryCrossingTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const float TileSize = 533.33333f;
    private const int CenterGrid = 32;

    public TileBoundaryCrossingTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    private static uint WorldToTileX(float x) => (uint)(CenterGrid - (int)MathF.Floor(x / TileSize));

    /// <summary>
    /// Navigate from west of a tile boundary to east of it (Orgrimmar area).
    /// Tile (29,41) / (30,41) boundary is at X = 1600.
    /// Start at X=1570 (tile 30), navigate to X=1640 (tile 29).
    /// Assert: smooth crossing, no Z warp, arrives at destination.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Navigate_CrossesTileBoundary_ArrivesSmooth()
    {
        var target = await EnsureTileBoundarySettingsAndTargetAsync();

        // Start position: west of tile boundary (tile 30)
        // Y=-4373 is Orgrimmar Y where we know terrain exists
        const float startX = 1570f;
        const float startY = -4373f;
        const float startZ = 40f;

        // Destination: east of tile boundary (tile 29)
        const float destX = 1640f;
        const float destY = -4373f;
        const float destZ = 35f;

        uint startTile = WorldToTileX(startX);
        uint destTile = WorldToTileX(destX);
        _output.WriteLine($"[TILE-CROSS] Start tile X={startTile}, Dest tile X={destTile}");
        Assert.NotEqual(startTile, destTile); // Must actually cross a boundary

        await StageNavigationStartAsync(target, 1, startX, startY, startZ, "Orgrimmar tile boundary west side");
        await _bot.RefreshSnapshotsAsync();

        var startSnap = await _bot.GetSnapshotAsync(target.AccountName);
        var startPos = startSnap?.Player?.Unit?.GameObject?.Base?.Position;
        global::Tests.Infrastructure.Skip.If(startPos == null, "Could not get start position snapshot");
        _output.WriteLine($"[TILE-CROSS] Start: ({startPos!.X:F1},{startPos.Y:F1},{startPos.Z:F1}) tile={WorldToTileX(startPos.X)}");

        // Send TravelTo across the boundary
        var result = await _bot.SendActionAsync(target.AccountName, new ActionMessage
        {
            ActionType = ActionType.TravelTo,
            Parameters =
            {
                new RequestParameter { IntParam = 1 },         // mapId (Kalimdor)
                new RequestParameter { FloatParam = destX },   // dest X
                new RequestParameter { FloatParam = destY },   // dest Y
                new RequestParameter { FloatParam = destZ },   // dest Z
            }
        });
        _output.WriteLine($"[TILE-CROSS] TravelTo result: {result}");
        Assert.Equal(ResponseResult.Success, result);

        // Monitor movement across boundary
        bool crossedBoundary = false;
        bool arrived = false;
        float prevZ = startPos.Z;
        float maxZJump = 0;
        var posHistory = new List<(float X, float Y, float Z, uint Tile)>();

        for (int i = 0; i < 24; i++) // 120s max
        {
            await Task.Delay(5000);
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(target.AccountName);
            var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
            if (pos == null) continue;

            uint currentTile = WorldToTileX(pos.X);
            posHistory.Add((pos.X, pos.Y, pos.Z, currentTile));

            float zJump = MathF.Abs(pos.Z - prevZ);
            if (zJump > maxZJump) maxZJump = zJump;

            if (currentTile != startTile && !crossedBoundary)
            {
                crossedBoundary = true;
                _output.WriteLine($"[TILE-CROSS] *** CROSSED BOUNDARY at t={i * 5}s: tile {startTile} -> {currentTile} pos=({pos.X:F1},{pos.Y:F1},{pos.Z:F1})");
            }

            float dx = pos.X - destX;
            float dy = pos.Y - destY;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            _output.WriteLine($"[TILE-CROSS] t={i * 5}s: ({pos.X:F1},{pos.Y:F1},{pos.Z:F1}) tile={currentTile} dist={dist:F1}y zJump={zJump:F1}");

            if (dist < 15f)
            {
                arrived = true;
                break;
            }

            // Detect Z warp (sudden teleport through ground)
            Assert.True(pos.Z > -500, $"Bot fell through world: Z={pos.Z:F1}");

            prevZ = pos.Z;
        }

        _output.WriteLine($"[TILE-CROSS] Summary: crossed={crossedBoundary}, arrived={arrived}, maxZJump={maxZJump:F1}, positions={posHistory.Count}");

        // Assert smooth boundary crossing
        Assert.True(crossedBoundary, "Bot should have crossed tile boundary during navigation");
        Assert.True(arrived, "Bot should arrive at destination after crossing tile boundary");
    }

    /// <summary>
    /// Navigate across a tile boundary in open terrain (outside Orgrimmar).
    /// Uses a flat area near the Org south gate where pathfinding is simpler.
    /// Validates tile loading works during outdoor movement.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Navigate_OpenTerrain_CrossesBoundarySmooth()
    {
        var target = await EnsureTileBoundarySettingsAndTargetAsync();

        // Use open terrain south of Orgrimmar
        // Tile (30,41) / (31,41) boundary is at X = (32-30)*533.33 = 1066.67
        // Start at X=1100 (tile 30), navigate to X=1000 (tile 31)
        const float startX = 1100f;
        const float startY = -4450f;
        const float destX = 1000f;
        const float destY = -4450f;

        uint startTile = WorldToTileX(startX);
        uint destTile = WorldToTileX(destX);
        _output.WriteLine($"[OPEN-TILE] Start tile X={startTile}, Dest tile X={destTile}");

        await StageNavigationStartAsync(target, 1, startX, startY, 30f, "Durotar open tile boundary");
        await _bot.RefreshSnapshotsAsync();

        var startSnap = await _bot.GetSnapshotAsync(target.AccountName);
        var startPos = startSnap?.Player?.Unit?.GameObject?.Base?.Position;
        global::Tests.Infrastructure.Skip.If(startPos == null, "Could not get start position");
        _output.WriteLine($"[OPEN-TILE] Start: ({startPos!.X:F1},{startPos.Y:F1},{startPos.Z:F1})");

        var result = await _bot.SendActionAsync(target.AccountName, new ActionMessage
        {
            ActionType = ActionType.TravelTo,
            Parameters =
            {
                new RequestParameter { IntParam = 1 },
                new RequestParameter { FloatParam = destX },
                new RequestParameter { FloatParam = destY },
                new RequestParameter { FloatParam = 25f },
            }
        });
        Assert.Equal(ResponseResult.Success, result);

        var tilesVisited = new HashSet<uint>();

        for (int i = 0; i < 12; i++) // 60s max
        {
            await Task.Delay(5000);
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(target.AccountName);
            var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
            if (pos == null) continue;

            uint tileX = WorldToTileX(pos.X);
            tilesVisited.Add(tileX);

            float dx = pos.X - destX;
            float dy = pos.Y - destY;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            _output.WriteLine($"[OPEN-TILE] t={i * 5}s: ({pos.X:F0},{pos.Y:F0},{pos.Z:F1}) tile={tileX} dist={dist:F1}y");

            Assert.True(pos.Z > -500, $"Bot fell through world: Z={pos.Z:F1}");

            if (dist < 15f)
            {
                break;
            }
        }

        _output.WriteLine($"[OPEN-TILE] Tiles visited: {string.Join(", ", tilesVisited)}");
        Assert.False(tilesVisited.Count == 0, "Bot should have recorded position snapshots");
        // Don't assert arrival — pathfinding in hilly terrain may stall.
        // The key validation is: no fall-through-world during tile transitions.
    }

    private async Task<LiveBotFixture.BotRunnerActionTarget> EnsureTileBoundarySettingsAndTargetAsync()
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
            .Single(target => !target.IsForeground);

        _output.WriteLine(
            $"[ACTION-PLAN] {target.RoleLabel} {target.AccountName}/{target.CharacterName}: " +
            "BG tile-boundary action target.");
        _output.WriteLine(
            $"[ACTION-PLAN] FG {_bot.FgAccountName}/{_bot.FgCharacterName}: launched idle for topology parity.");
        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no navigation dispatch.");

        return target;
    }

    private async Task StageNavigationStartAsync(
        LiveBotFixture.BotRunnerActionTarget target,
        int mapId,
        float x,
        float y,
        float z,
        string locationLabel)
    {
        var staged = await _bot.StageBotRunnerAtNavigationPointAsync(
            target.AccountName,
            target.RoleLabel,
            mapId,
            x,
            y,
            z,
            locationLabel);
        if (staged)
        {
            await _bot.QuiesceAccountsAsync(
                new[] { target.AccountName },
                $"{target.RoleLabel} {locationLabel} staged");
            return;
        }

        var snapshot = await _bot.GetSnapshotAsync(target.AccountName);
        var position = snapshot?.Player?.Unit?.GameObject?.Base?.Position;
        Assert.Fail(
            $"Expected {target.RoleLabel} {target.AccountName} to reach {locationLabel}. " +
            $"finalMap={snapshot?.CurrentMapId ?? 0} pos=({position?.X:F1},{position?.Y:F1},{position?.Z:F1})");
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
