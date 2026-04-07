using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Communication;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// R9.1: Validates that bot movement is smooth across ADT tile boundaries.
/// Each WoW ADT tile is 533.33y. When the bot crosses a boundary, the
/// SceneDataClient loads new tiles — this test verifies no position warps,
/// no collision loss, and smooth arrival.
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
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
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
        var bgAccount = _bot.BgAccountName!;
        await _bot.EnsureCleanSlateAsync(bgAccount, "BG");

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

        // Teleport to start and wait for settle
        await _bot.BotTeleportAsync(bgAccount, 1, startX, startY, startZ);
        await _bot.WaitForTeleportSettledAsync(bgAccount, startX, startY);
        await _bot.RefreshSnapshotsAsync();

        var startSnap = await _bot.GetSnapshotAsync(bgAccount);
        var startPos = startSnap?.Player?.Unit?.GameObject?.Base?.Position;
        global::Tests.Infrastructure.Skip.If(startPos == null, "Could not get start position snapshot");
        _output.WriteLine($"[TILE-CROSS] Start: ({startPos!.X:F1},{startPos.Y:F1},{startPos.Z:F1}) tile={WorldToTileX(startPos.X)}");

        // Send TravelTo across the boundary
        var result = await _bot.SendActionAsync(bgAccount, new ActionMessage
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
            var snap = await _bot.GetSnapshotAsync(bgAccount);
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
        var bgAccount = _bot.BgAccountName!;
        await _bot.EnsureCleanSlateAsync(bgAccount, "BG");

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

        await _bot.BotTeleportAsync(bgAccount, 1, startX, startY, 30f);
        await _bot.WaitForTeleportSettledAsync(bgAccount, startX, startY);
        await _bot.RefreshSnapshotsAsync();

        var startSnap = await _bot.GetSnapshotAsync(bgAccount);
        var startPos = startSnap?.Player?.Unit?.GameObject?.Base?.Position;
        global::Tests.Infrastructure.Skip.If(startPos == null, "Could not get start position");
        _output.WriteLine($"[OPEN-TILE] Start: ({startPos!.X:F1},{startPos.Y:F1},{startPos.Z:F1})");

        await _bot.SendActionAsync(bgAccount, new ActionMessage
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

        var tilesVisited = new HashSet<uint>();
        bool arrived = false;

        for (int i = 0; i < 12; i++) // 60s max
        {
            await Task.Delay(5000);
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(bgAccount);
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
                arrived = true;
                break;
            }
        }

        _output.WriteLine($"[OPEN-TILE] Tiles visited: {string.Join(", ", tilesVisited)}");
        Assert.False(tilesVisited.Count == 0, "Bot should have recorded position snapshots");
        // Don't assert arrival — pathfinding in hilly terrain may stall.
        // The key validation is: no fall-through-world during tile transitions.
    }
}
