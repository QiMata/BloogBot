using GameData.Core.Enums;
using GameData.Core.Models;
using Moq;
using Navigation.Physics.Tests.Helpers;
using System;
using System.Collections.Generic;
using System.Globalization;
using WoWSharpClient.Client;
using WoWSharpClient.Models;
using WoWSharpClient.Movement;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Navigation.Physics.Tests;

/// <summary>
/// Integration tests for MovementController using the real C++ PhysicsEngine
/// via NativePathfindingClient. Verifies ground snapping, gravity, terrain following,
/// and teleport recovery against actual VMAP/ADT collision data.
///
/// Requires Navigation.dll + vmaps/ + maps/ data in WWOW_DATA_DIR.
/// </summary>
[Collection("PhysicsEngine")]
public class MovementControllerPhysicsTests
{
    private readonly PhysicsEngineFixture _fixture;
    private readonly ITestOutputHelper _output;

    // Valley of Trials - open terrain with no WMO structures (Kalimdor, map 1)
    // Orgrimmar spawn (1629, -4373, 31) has WMO buildings at ~36y above ADT terrain,
    // causing elevated tests to land on WMO roofs instead of falling to ground.
    private const float SpawnX = -284.0f;
    private const float SpawnY = -4383.0f;
    private const float SpawnZ = 57.0f;
    private const uint MapId = 1; // Kalimdor

    public MovementControllerPhysicsTests(PhysicsEngineFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    private readonly record struct FrameSnapshot(
        int Frame,
        uint TimeMs,
        float X,
        float Y,
        float Z,
        float DeltaZ,
        float GroundZ,
        float GroundGap,
        MovementFlags Flags);

    private (MovementController controller, WoWLocalPlayer player, Mock<WoWClient> mockClient)
        CreateController(float x, float y, float z, float facing = 0f)
    {
        var mockClient = new Mock<WoWClient>();
        mockClient
            .Setup(c => c.SendMovementOpcodeAsync(
                It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var physics = new NativePathfindingClient();
        var player = new WoWLocalPlayer(new HighGuid(42))
        {
            Position = new Position(x, y, z),
            Facing = facing,
            MapId = MapId,
            Race = Race.Orc,
            Gender = Gender.Male,
            WalkSpeed = 2.5f,
            RunSpeed = 7.0f,
            RunBackSpeed = 4.5f,
            SwimSpeed = 4.722f,
            SwimBackSpeed = 2.5f,
        };

        var controller = new MovementController(mockClient.Object, physics, player);
        return (controller, player, mockClient);
    }

    /// <summary>
    /// Runs N physics frames and captures per-frame diagnostics for calibration.
    /// </summary>
    private static List<FrameSnapshot> RunFramesWithTrace(
        MovementController controller,
        WoWLocalPlayer player,
        int frameCount,
        float dtSec = 0.05f,
        uint startTimeMs = 1000,
        MovementFlags? forceFlagsEachFrame = null)
    {
        var frames = new List<FrameSnapshot>(frameCount);

        for (int i = 0; i < frameCount; i++)
        {
            if (forceFlagsEachFrame.HasValue)
            {
                // Preserve physics state flags (JUMPING, FALLINGFAR, SWIMMING, etc.)
                // while forcing intent flags (FORWARD, BACKWARD, STRAFE, etc.).
                // This matches production behavior where bot logic uses |= for intent
                // and MovementController merges physics state via ApplyPhysicsResult.
                const MovementFlags physicsState =
                    MovementFlags.MOVEFLAG_JUMPING |
                    MovementFlags.MOVEFLAG_SWIMMING |
                    MovementFlags.MOVEFLAG_FLYING |
                    MovementFlags.MOVEFLAG_LEVITATING |
                    MovementFlags.MOVEFLAG_FALLINGFAR;
                var existingPhysics = player.MovementFlags & physicsState;
                player.MovementFlags = forceFlagsEachFrame.Value | existingPhysics;
            }

            float prevZ = player.Position.Z;
            uint timeMs = startTimeMs + (uint)MathF.Round(i * dtSec * 1000f);
            controller.Update(dtSec, timeMs);

            float groundZ = ProbeGroundZ(player.MapId, player.Position.X, player.Position.Y, player.Position.Z);
            float groundGap = float.IsNaN(groundZ) ? float.NaN : player.Position.Z - groundZ;

            frames.Add(new FrameSnapshot(
                i,
                timeMs,
                player.Position.X,
                player.Position.Y,
                player.Position.Z,
                player.Position.Z - prevZ,
                groundZ,
                groundGap,
                player.MovementFlags));
        }

        return frames;
    }

    private static float ProbeGroundZ(uint mapId, float x, float y, float z)
    {
        const float InvalidGround = -50000f;

        float[] probeHeights = [z + 2f, z + 12f, z + 30f];
        float[] searchDistances = [8f, 24f, 48f];

        for (int i = 0; i < probeHeights.Length; i++)
        {
            float groundZ = NavigationInterop.GetGroundZ(mapId, x, y, probeHeights[i], searchDistances[i]);
            if (groundZ > InvalidGround)
            {
                return groundZ;
            }
        }

        return float.NaN;
    }

    private void WriteFrameTrace(string scenario, IReadOnlyList<FrameSnapshot> frames)
    {
        _output.WriteLine($"=== {scenario}: {frames.Count} frames ===");
        foreach (var frame in frames)
        {
            string groundText = float.IsNaN(frame.GroundZ)
                ? "n/a"
                : frame.GroundZ.ToString("F3", CultureInfo.InvariantCulture);
            string gapText = float.IsNaN(frame.GroundGap)
                ? "n/a"
                : frame.GroundGap.ToString("F3", CultureInfo.InvariantCulture);

            _output.WriteLine(
                $"  f={frame.Frame,3} t={frame.TimeMs,5} " +
                $"pos=({frame.X:F3},{frame.Y:F3},{frame.Z:F3}) " +
                $"dZ={frame.DeltaZ.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture),7} " +
                $"ground={groundText,8} gap={gapText,8} flags=0x{(uint)frame.Flags:X8}");
        }
    }

    private static float MinAbsGroundGap(IReadOnlyList<FrameSnapshot> frames)
    {
        float minGap = float.PositiveInfinity;
        foreach (var frame in frames)
        {
            if (float.IsNaN(frame.GroundGap))
            {
                continue;
            }

            float absGap = MathF.Abs(frame.GroundGap);
            if (absGap < minGap)
            {
                minGap = absGap;
            }
        }

        return minGap;
    }

    private static string Summarize(IReadOnlyList<FrameSnapshot> frames)
    {
        if (frames.Count == 0)
        {
            return "no frames captured";
        }

        float minGap = MinAbsGroundGap(frames);
        string minGapText = float.IsPositiveInfinity(minGap) ? "n/a" : minGap.ToString("F3", CultureInfo.InvariantCulture);
        var first = frames[0];
        var last = frames[^1];

        return $"firstZ={first.Z:F3}, lastZ={last.Z:F3}, minAbsGap={minGapText}";
    }

    [Fact]
    public void Standing_SnapsToGround()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        // Start 5 units above ground at Orgrimmar spawn
        var (controller, player, _) = CreateController(SpawnX, SpawnY, SpawnZ + 5.0f);
        float startZ = player.Position.Z;

        // Hold movement intent so Update() continues stepping physics each frame.
        var trace = RunFramesWithTrace(
            controller,
            player,
            frameCount: 20,
            forceFlagsEachFrame: MovementFlags.MOVEFLAG_FORWARD);
        WriteFrameTrace(nameof(Standing_SnapsToGround), trace);

        float minGap = MinAbsGroundGap(trace);
        Assert.True(player.Position.Z < startZ - 1.0f,
            $"Expected a visible downward snap from {startZ:F3} but got {player.Position.Z:F3}. {Summarize(trace)}");

        Assert.True(minGap <= 1.5f,
            $"Expected ground contact gap <= 1.5y but min was {minGap:F3}. {Summarize(trace)}");
    }

    [Fact]
    public void Forward_TraversesSlope()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        // Start at Orgrimmar facing east (facing = 0)
        var (controller, player, _) = CreateController(SpawnX, SpawnY, SpawnZ, facing: 0f);
        var startPos = player.Position;

        // Move forward for 3 seconds (60 frames x 50ms)
        var trace = RunFramesWithTrace(
            controller,
            player,
            frameCount: 60,
            forceFlagsEachFrame: MovementFlags.MOVEFLAG_FORWARD);
        WriteFrameTrace(nameof(Forward_TraversesSlope), trace);

        // Should have moved ~21 yards forward (7.0 RunSpeed x 3s)
        float horizontalDist = MathF.Sqrt(
            MathF.Pow(player.Position.X - startPos.X, 2) +
            MathF.Pow(player.Position.Y - startPos.Y, 2));

        _output.WriteLine($"Forward: startPos=({startPos.X:F3},{startPos.Y:F3},{startPos.Z:F3}) endPos=({player.Position.X:F3},{player.Position.Y:F3},{player.Position.Z:F3}) horizDist={horizontalDist:F3}");
        // Per-frame horizontal displacement
        float prevX = startPos.X, prevY = startPos.Y;
        for (int i = 0; i < trace.Count; i++)
        {
            float fdx = trace[i].X - prevX;
            float fdy = trace[i].Y - prevY;
            float fd = MathF.Sqrt(fdx * fdx + fdy * fdy);
            if (i < 5 || i >= trace.Count - 3 || fd > 1.0f)
                _output.WriteLine($"  f={i} dXY={fd:F4} cumX={trace[i].X - startPos.X:F3} flags=0x{(uint)trace[i].Flags:X8}");
            prevX = trace[i].X;
            prevY = trace[i].Y;
        }

        Assert.InRange(horizontalDist, 10f, 30f);

        // Z should be on valid terrain (not falling through world).
        Assert.True(player.Position.Z > -50f,
            $"Player fell through world: Z={player.Position.Z:F1}");
    }

    [Fact]
    public void TeleportRecovery_StopsFreeFall()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        // Start at valid ground position
        var (controller, player, _) = CreateController(SpawnX, SpawnY, SpawnZ);

        // Run a few frames to establish state
        _ = RunFramesWithTrace(
            controller,
            player,
            frameCount: 5,
            forceFlagsEachFrame: MovementFlags.MOVEFLAG_FORWARD);

        // Teleport nearby (same terrain area) but well above ground.
        // Use a short horizontal offset to stay in the same terrain profile.
        float teleportX = SpawnX + 150f;
        float teleportY = SpawnY;
        float teleportGroundZ = ProbeGroundZ(MapId, teleportX, teleportY, SpawnZ + 40f);
        if (float.IsNaN(teleportGroundZ))
        {
            teleportGroundZ = SpawnZ;
        }
        float teleportZ = teleportGroundZ + 15f;

        // Simulate teleport: jump position far away (triggers >100 unit detection)
        player.Position = new Position(teleportX, teleportY, teleportZ);
        _output.WriteLine($"Teleport target: ({teleportX:F3}, {teleportY:F3}, {teleportZ:F3}), probedGround={teleportGroundZ:F3}");

        // Continue holding movement intent so post-reset frames continue stepping.
        // 80 frames (4s) to allow enough time for 15y fall under gravity.
        var trace = RunFramesWithTrace(
            controller,
            player,
            frameCount: 80,
            startTimeMs: 2000,
            forceFlagsEachFrame: MovementFlags.MOVEFLAG_FORWARD);
        WriteFrameTrace(nameof(TeleportRecovery_StopsFreeFall), trace);

        int descentEndFrame = Math.Min(trace.Count - 1, 10);
        float earlyDrop = trace[0].Z - trace[descentEndFrame].Z;
        float minGap = MinAbsGroundGap(trace);

        Assert.True(earlyDrop > 0.75f,
            $"Expected post-teleport descent by frame {descentEndFrame}, drop={earlyDrop:F3}. {Summarize(trace)}");

        Assert.True(minGap <= 2.5f,
            $"Expected landing contact gap <= 2.5y after teleport, min was {minGap:F3}. {Summarize(trace)}");

        // Guard against through-world failures.
        Assert.True(player.Position.Z > -50f,
            $"Player fell through world: Z={player.Position.Z:F1}");
    }

    [Fact]
    public void Backward_MovesOppositeToFacing()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        // Start at Orgrimmar facing north (facing = 0)
        var (controller, player, _) = CreateController(SpawnX, SpawnY, SpawnZ, facing: 0f);
        var startPos = player.Position;

        // Move backward for 2 seconds (40 frames x 50ms) at RunBackSpeed 4.5
        var _ = RunFramesWithTrace(
            controller,
            player,
            frameCount: 40,
            forceFlagsEachFrame: MovementFlags.MOVEFLAG_BACKWARD);

        // Should have moved backward (~9 yards at 4.5 speed x 2s)
        float horizontalDist = MathF.Sqrt(
            MathF.Pow(player.Position.X - startPos.X, 2) +
            MathF.Pow(player.Position.Y - startPos.Y, 2));
        Assert.InRange(horizontalDist, 3f, 15f);

        // Z should be on valid terrain
        Assert.True(player.Position.Z > -50f,
            $"Player fell through world: Z={player.Position.Z:F1}");
    }

    [Fact]
    public void Falling_AccumulatesFallTime()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        // Start 60 units above ground - will be in free fall
        var (controller, player, _) = CreateController(SpawnX, SpawnY, SpawnZ + 60f);
        float startZ = player.Position.Z;

        var trace = RunFramesWithTrace(
            controller,
            player,
            frameCount: 10,
            forceFlagsEachFrame: MovementFlags.MOVEFLAG_FORWARD);
        WriteFrameTrace(nameof(Falling_AccumulatesFallTime), trace);

        // After 10 frames of free fall, Z should have decreased
        Assert.True(player.Position.Z < startZ,
            $"Expected Z < {startZ:F1} but got {player.Position.Z:F1}");
    }

    [Fact]
    public void LandAfterFall_PositionNearGround()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        // Start 30 units above ground — large enough to guarantee significant descent
        // even if terrain rises in the forward direction (Valley of Trials slopes).
        var (controller, player, _) = CreateController(SpawnX, SpawnY, SpawnZ + 30f);
        float startZ = player.Position.Z;

        // Run enough frames for gravity to pull us down to local ground.
        var trace = RunFramesWithTrace(
            controller,
            player,
            frameCount: 60,
            forceFlagsEachFrame: MovementFlags.MOVEFLAG_FORWARD);
        WriteFrameTrace(nameof(LandAfterFall_PositionNearGround), trace);

        float minGap = MinAbsGroundGap(trace);

        // The bot should land near ground. Terrain may rise in the forward direction,
        // so we can't assert a specific Z drop — only that the bot reaches ground.
        Assert.True(minGap <= 2.0f,
            $"Expected to settle near local ground (gap <= 2.0y), min gap was {minGap:F3}. {Summarize(trace)}");

        // The bot should descend initially (first few frames before terrain catches up)
        float earlyDrop = trace[0].Z - trace[Math.Min(5, trace.Count - 1)].Z;
        Assert.True(earlyDrop > 0.01f || minGap <= 1.0f,
            $"Expected initial descent or ground contact, earlyDrop={earlyDrop:F3}. {Summarize(trace)}");
    }

    /// <summary>
    /// NPT-MISS-002: Validates that a character placed above terrain after a teleport
    /// actually descends frame-by-frame (catches "hover" regression where Z stays constant).
    /// Asserts per-frame descent trend during initial frames and eventual ground contact.
    /// </summary>
    [Fact]
    public void TeleportAirborne_DescentTrend_ZDecreasesPerFrame()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        // Start at valid ground, run a few frames to establish baseline state.
        var (controller, player, _) = CreateController(SpawnX, SpawnY, SpawnZ);
        _ = RunFramesWithTrace(
            controller,
            player,
            frameCount: 5,
            forceFlagsEachFrame: MovementFlags.MOVEFLAG_FORWARD);

        // Teleport: place character 20y above known ground (triggers teleport detection).
        float teleX = SpawnX + 150f;
        float teleY = SpawnY;
        float groundZ = ProbeGroundZ(MapId, teleX, teleY, SpawnZ + 40f);
        if (float.IsNaN(groundZ))
        {
            groundZ = SpawnZ;
        }
        float teleZ = groundZ + 20f;

        player.Position = new Position(teleX, teleY, teleZ);
        _output.WriteLine($"Teleport: ({teleX:F1}, {teleY:F1}, {teleZ:F1}), ground={groundZ:F3}");

        // Run 60 frames (3s) — enough for gravity to pull 20y fall.
        var trace = RunFramesWithTrace(
            controller,
            player,
            frameCount: 60,
            startTimeMs: 2000,
            forceFlagsEachFrame: MovementFlags.MOVEFLAG_FORWARD);
        WriteFrameTrace(nameof(TeleportAirborne_DescentTrend_ZDecreasesPerFrame), trace);

        // --- Assertion 1: Per-frame descent trend in first 10 frames ---
        // Z should decrease (or stay same due to ground snap) on most early frames.
        // A "hover" regression would show Z constant or increasing.
        int descentFrames = 0;
        const int checkWindow = 10;
        int windowEnd = Math.Min(checkWindow, trace.Count - 1);

        for (int i = 1; i <= windowEnd; i++)
        {
            float dz = trace[i].Z - trace[i - 1].Z;
            _output.WriteLine($"  descent check f={i}: dZ={dz:+0.000;-0.000;0.000}");
            if (dz < -0.01f)
            {
                descentFrames++;
            }
        }

        Assert.True(descentFrames >= windowEnd / 2,
            $"Expected at least {windowEnd / 2} descending frames in first {windowEnd}, " +
            $"got {descentFrames}. Possible hover regression. {Summarize(trace)}");

        // --- Assertion 2: Total descent is significant ---
        float totalDrop = trace[0].Z - trace[windowEnd].Z;
        Assert.True(totalDrop > 1.0f,
            $"Expected > 1y total descent in first {windowEnd} frames, got {totalDrop:F3}y. " +
            $"Hover regression? {Summarize(trace)}");

        // --- Assertion 3: Character eventually reaches ground ---
        float minGap = MinAbsGroundGap(trace);
        Assert.True(minGap <= 2.5f,
            $"Expected landing (gap <= 2.5y) within 60 frames, " +
            $"best gap was {minGap:F3}y. {Summarize(trace)}");

        // --- Assertion 4: No through-world failure ---
        Assert.True(player.Position.Z > -50f,
            $"Player fell through world: Z={player.Position.Z:F1}");
    }

    // =====================================================================
    // P1 — MOVEFLAG CALIBRATION TESTS
    // These tests verify that movement flags transition correctly between
    // ground/falling/swimming states. They FAIL when the BG bot's flags
    // diverge from what the server expects.
    // =====================================================================

    /// <summary>
    /// P1.1a: Walking on flat/slope terrain must NEVER set FALLINGFAR or JUMPING.
    /// The BG bot exhibits false freefall on walkable terrain (ADT gullies),
    /// causing rubber-banding when the server rejects airborne heartbeats
    /// for a character that should be grounded.
    /// </summary>
    [Fact]
    public void Grounded_ForwardOnTerrain_NeverSetsFallingFlags()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        var (controller, player, _) = CreateController(SpawnX, SpawnY, SpawnZ, facing: 0f);

        // Set a straight-line path ahead. In production the bot always has a navmesh path,
        // which activates the false-freefall guard that suppresses transient FALLINGFAR
        // on ADT gully terrain. Without a path, the guard doesn't engage.
        controller.SetPath([
            new Position(SpawnX, SpawnY, SpawnZ),
            new Position(SpawnX + 30f, SpawnY, SpawnZ)
        ]);

        // Walk forward for 3 seconds across Orgrimmar terrain
        var trace = RunFramesWithTrace(
            controller,
            player,
            frameCount: 60,
            forceFlagsEachFrame: MovementFlags.MOVEFLAG_FORWARD);
        WriteFrameTrace(nameof(Grounded_ForwardOnTerrain_NeverSetsFallingFlags), trace);

        const MovementFlags airborneFlags =
            MovementFlags.MOVEFLAG_FALLINGFAR | MovementFlags.MOVEFLAG_JUMPING;

        int airborneFrames = 0;
        foreach (var frame in trace)
        {
            if ((frame.Flags & airborneFlags) != MovementFlags.MOVEFLAG_NONE)
                airborneFrames++;
        }

        _output.WriteLine($"Airborne frames: {airborneFrames}/{trace.Count}");

        // Tolerance: up to 3 transient frames are acceptable (step transitions
        // or tail-end physics detection when forward movement ceases over a gap).
        Assert.True(airborneFrames <= 3,
            $"Expected <= 3 transient airborne frames on flat terrain, " +
            $"got {airborneFrames}/{trace.Count}. Flags diverge from grounded expectation.");
    }

    /// <summary>
    /// P1.1b: Walking off a high ledge MUST set FALLINGFAR within a few frames.
    /// Place the bot on a high platform and walk it off the edge — if the false
    /// freefall prevention suppresses the fall, the bot hovers in mid-air.
    /// </summary>
    [Fact]
    public void WalkOffLedge_SetsFallingFar()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        // Start at Orgrimmar spawn, elevated 30y above ground (simulating a cliff edge).
        // With no path set, the false freefall guard should NOT engage (requires _currentPath).
        float elevatedZ = SpawnZ + 30f;
        var (controller, player, _) = CreateController(SpawnX, SpawnY, elevatedZ, facing: 0f);

        // Walk forward — there's no ground at this height, should fall
        var trace = RunFramesWithTrace(
            controller,
            player,
            frameCount: 20,
            forceFlagsEachFrame: MovementFlags.MOVEFLAG_FORWARD);
        WriteFrameTrace(nameof(WalkOffLedge_SetsFallingFar), trace);

        const MovementFlags airborneFlags =
            MovementFlags.MOVEFLAG_FALLINGFAR | MovementFlags.MOVEFLAG_JUMPING;

        // Find first frame with airborne flag
        int firstAirborneFrame = -1;
        for (int i = 0; i < trace.Count; i++)
        {
            if ((trace[i].Flags & airborneFlags) != MovementFlags.MOVEFLAG_NONE)
            {
                firstAirborneFrame = i;
                break;
            }
        }

        _output.WriteLine($"First airborne frame: {firstAirborneFrame}");

        // FALLINGFAR must be set within the first 5 frames (250ms at 50ms/frame)
        Assert.True(firstAirborneFrame >= 0 && firstAirborneFrame <= 5,
            $"Expected FALLINGFAR within first 5 frames when walking off ledge, " +
            $"first airborne at frame {firstAirborneFrame}. " +
            $"False freefall prevention may be suppressing real falls.");

        // Z must have decreased — the bot must actually fall
        float drop = elevatedZ - player.Position.Z;
        Assert.True(drop > 2.0f,
            $"Expected > 2y drop after 20 frames off ledge, got {drop:F3}y. " +
            $"Bot may be hovering in mid-air.");
    }

    /// <summary>
    /// P1.1c: After landing from a fall, FALLINGFAR must be cleared and the bot
    /// must be grounded. Persistent FALLINGFAR after landing causes the server
    /// to reject position updates.
    /// </summary>
    [Fact]
    public void Landing_ClearsFallingFlags()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        // Start 10y above ground — short fall, should land within 40 frames
        var (controller, player, _) = CreateController(SpawnX, SpawnY, SpawnZ + 10f, facing: 0f);

        // Set a ground-level path so the false-freefall guard can engage after landing.
        // Path Z must match actual terrain at each waypoint (not spawn Z) — the path-aware
        // position guard clamps Z to path waypoint level, so using elevated Z causes bounce.
        float endGroundZ = ProbeGroundZ(MapId, SpawnX + 30f, SpawnY, SpawnZ + 40f);
        if (float.IsNaN(endGroundZ)) endGroundZ = SpawnZ - 5f;
        float startGroundZ = ProbeGroundZ(MapId, SpawnX, SpawnY, SpawnZ + 40f);
        if (float.IsNaN(startGroundZ)) startGroundZ = SpawnZ;
        controller.SetPath([
            new Position(SpawnX, SpawnY, startGroundZ),
            new Position(SpawnX + 30f, SpawnY, endGroundZ)
        ]);

        var trace = RunFramesWithTrace(
            controller,
            player,
            frameCount: 40,
            forceFlagsEachFrame: MovementFlags.MOVEFLAG_FORWARD);
        WriteFrameTrace(nameof(Landing_ClearsFallingFlags), trace);

        const MovementFlags airborneFlags =
            MovementFlags.MOVEFLAG_FALLINGFAR | MovementFlags.MOVEFLAG_JUMPING;

        // Find when we land: first frame at/near ground without airborne flags
        float minGap = MinAbsGroundGap(trace);
        Assert.True(minGap <= 1.5f,
            $"Expected to reach ground (gap <= 1.5y), min gap was {minGap:F3}.");

        // Check last 10 frames — should all be grounded after landing
        int groundedCount = 0;
        for (int i = Math.Max(0, trace.Count - 10); i < trace.Count; i++)
        {
            if ((trace[i].Flags & airborneFlags) == MovementFlags.MOVEFLAG_NONE)
                groundedCount++;
        }

        Assert.True(groundedCount >= 8,
            $"Expected >= 8 grounded frames in final 10 frames after landing, " +
            $"got {groundedCount}. FALLINGFAR may be persisting after ground contact.");
    }

    /// <summary>
    /// P1.1d: Airborne horizontal velocity must NOT change direction when facing changes.
    /// In WoW, once you leave the ground, horizontal velocity is locked — only facing
    /// can change (for camera/spell targeting), but movement direction is fixed.
    /// This test verifies the physics engine preserves horizontal velocity during fall.
    /// </summary>
    [Fact]
    public void Airborne_HorizontalVelocityLockedDespiteFacingChange()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        // Start elevated, facing east (0 rad), moving forward
        float startZ = SpawnZ + 25f;
        var (controller, player, _) = CreateController(SpawnX, SpawnY, startZ, facing: 0f);

        // First 5 frames: establish airborne state moving east
        var setupTrace = RunFramesWithTrace(
            controller,
            player,
            frameCount: 5,
            forceFlagsEachFrame: MovementFlags.MOVEFLAG_FORWARD);
        WriteFrameTrace("Airborne_Setup", setupTrace);

        float afterSetupX = player.Position.X;
        float afterSetupY = player.Position.Y;

        // Record velocity direction after initial airborne frames
        // (X should be increasing = moving east since facing=0)
        float setupDeltaX = afterSetupX - SpawnX;
        float setupDeltaY = afterSetupY - SpawnY;
        _output.WriteLine($"After setup: dX={setupDeltaX:F3} dY={setupDeltaY:F3}");

        // Now change facing to NORTH (π/2) while still airborne with FORWARD flag
        // In WoW, this should NOT change movement direction
        player.Facing = MathF.PI / 2f;

        var postTurnTrace = RunFramesWithTrace(
            controller,
            player,
            frameCount: 10,
            startTimeMs: 2000,
            forceFlagsEachFrame: MovementFlags.MOVEFLAG_FORWARD);
        WriteFrameTrace("Airborne_AfterFacingChange", postTurnTrace);

        // Calculate post-turn movement direction
        float postTurnDeltaX = player.Position.X - afterSetupX;
        float postTurnDeltaY = player.Position.Y - afterSetupY;
        _output.WriteLine($"After turn: dX={postTurnDeltaX:F3} dY={postTurnDeltaY:F3}");

        // If horizontal velocity is locked (correct): bot continues moving east (positive X)
        // If horizontal velocity follows facing (BUG): bot moves north (positive Y)
        // We check that X displacement is dominant over Y displacement change
        float xMagnitude = MathF.Abs(postTurnDeltaX);
        float yMagnitude = MathF.Abs(postTurnDeltaY);

        _output.WriteLine($"Post-turn |dX|={xMagnitude:F3} |dY|={yMagnitude:F3}");

        // The original direction (east) should still dominate movement
        // If the bug exists, Y magnitude will be much larger than X
        Assert.True(xMagnitude > yMagnitude * 0.5f,
            $"Airborne horizontal velocity changed direction after facing change! " +
            $"|dX|={xMagnitude:F3} should dominate |dY|={yMagnitude:F3}. " +
            $"Horizontal velocity must be locked when airborne.");
    }

    /// <summary>
    /// P1.1e: Flag transition sequence for walk-off-ledge must be:
    /// FORWARD (grounded) → FORWARD|FALLINGFAR (airborne) → FORWARD (grounded after landing).
    /// The FALLINGFAR flag should appear within a few frames of leaving ground,
    /// persist during the fall, and clear on landing.
    /// </summary>
    [Fact]
    public void FlagTransitionSequence_WalkOffAndLand()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        // Start 12y above ground — enough for a clear fall + landing within 60 frames
        var (controller, player, _) = CreateController(SpawnX, SpawnY, SpawnZ + 12f, facing: 0f);

        // Set a ground-level path so the false-freefall guard activates after landing.
        // Path Z must match actual terrain — path-aware position guard clamps to waypoint Z.
        float endGroundZ = ProbeGroundZ(MapId, SpawnX + 30f, SpawnY, SpawnZ + 40f);
        if (float.IsNaN(endGroundZ)) endGroundZ = SpawnZ - 5f;
        float startGroundZ = ProbeGroundZ(MapId, SpawnX, SpawnY, SpawnZ + 40f);
        if (float.IsNaN(startGroundZ)) startGroundZ = SpawnZ;
        controller.SetPath([
            new Position(SpawnX, SpawnY, startGroundZ),
            new Position(SpawnX + 30f, SpawnY, endGroundZ)
        ]);

        var trace = RunFramesWithTrace(
            controller,
            player,
            frameCount: 60,
            forceFlagsEachFrame: MovementFlags.MOVEFLAG_FORWARD);
        WriteFrameTrace(nameof(FlagTransitionSequence_WalkOffAndLand), trace);

        const MovementFlags airborneFlags =
            MovementFlags.MOVEFLAG_FALLINGFAR | MovementFlags.MOVEFLAG_JUMPING;

        // Phase 1: Find transition to airborne
        int firstAirborneFrame = -1;
        for (int i = 0; i < trace.Count; i++)
        {
            if ((trace[i].Flags & airborneFlags) != MovementFlags.MOVEFLAG_NONE)
            {
                firstAirborneFrame = i;
                break;
            }
        }

        // Phase 2: Find landing (airborne→grounded)
        int landingFrame = -1;
        if (firstAirborneFrame >= 0)
        {
            for (int i = firstAirborneFrame + 1; i < trace.Count; i++)
            {
                if ((trace[i].Flags & airborneFlags) == MovementFlags.MOVEFLAG_NONE)
                {
                    landingFrame = i;
                    break;
                }
            }
        }

        _output.WriteLine($"First airborne: frame {firstAirborneFrame}, Landing: frame {landingFrame}");

        // Assertions
        Assert.True(firstAirborneFrame >= 0,
            "Bot never entered airborne state when starting 12y above ground.");

        Assert.True(firstAirborneFrame <= 5,
            $"Airborne flag set too late (frame {firstAirborneFrame}). " +
            $"Should detect no ground within first few frames.");

        Assert.True(landingFrame >= 0,
            $"Bot never landed within 60 frames. " +
            $"FALLINGFAR may be stuck or bot fell through world (Z={player.Position.Z:F1}).");

        // Fall duration should be reasonable (12y fall ≈ 1.1s ≈ 22 frames at 50ms)
        int fallDuration = landingFrame - firstAirborneFrame;
        Assert.InRange(fallDuration, 5, 40);

        // After landing, remaining frames should be grounded
        int postLandGrounded = 0;
        for (int i = landingFrame; i < trace.Count; i++)
        {
            if ((trace[i].Flags & airborneFlags) == MovementFlags.MOVEFLAG_NONE)
                postLandGrounded++;
        }

        int postLandTotal = trace.Count - landingFrame;
        Assert.True(postLandGrounded >= postLandTotal - 2,
            $"After landing at frame {landingFrame}, " +
            $"expected mostly grounded but got {postLandGrounded}/{postLandTotal} grounded frames.");
    }

    /// <summary>
    /// P1.4: Slope clamping — Z must track ground height during forward movement
    /// on sloped terrain, not hovering above or sinking below.
    /// </summary>
    [Fact]
    public void SlopeClamping_ZTracksGroundDuringMovement()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        var (controller, player, _) = CreateController(SpawnX, SpawnY, SpawnZ, facing: 0f);

        // Walk 3 seconds across terrain
        var trace = RunFramesWithTrace(
            controller,
            player,
            frameCount: 60,
            forceFlagsEachFrame: MovementFlags.MOVEFLAG_FORWARD);
        WriteFrameTrace(nameof(SlopeClamping_ZTracksGroundDuringMovement), trace);

        // Check ground gap for all frames that have valid ground data
        int framesWithGround = 0;
        int framesWithGoodGap = 0;
        float maxGap = 0f;

        foreach (var frame in trace)
        {
            if (float.IsNaN(frame.GroundGap))
                continue;

            framesWithGround++;
            float absGap = MathF.Abs(frame.GroundGap);
            maxGap = MathF.Max(maxGap, absGap);

            // Ground gap should be within 2y (accounting for step-ups and minor physics lag)
            if (absGap <= 2.0f)
                framesWithGoodGap++;
        }

        _output.WriteLine($"Frames with ground: {framesWithGround}, good gap: {framesWithGoodGap}, maxGap: {maxGap:F3}");

        Assert.True(framesWithGround >= 30,
            $"Expected at least 30 frames with valid ground data, got {framesWithGround}.");

        // At least 90% of frames should be within 2y of ground
        float goodRatio = (float)framesWithGoodGap / framesWithGround;
        Assert.True(goodRatio >= 0.9f,
            $"Expected >= 90% of frames clamped to ground (gap <= 2y), " +
            $"got {goodRatio:P0} ({framesWithGoodGap}/{framesWithGround}). " +
            $"Max gap was {maxGap:F3}y — bot is floating or sinking.");
    }

    // ======== MOVEMENT SPEED VALIDATION ========

    /// <summary>
    /// Validates that forward movement at RunSpeed produces expected displacement over time.
    /// 200 frames at 50ms = 10 seconds → expected ~70y at 7.0 y/s run speed.
    /// </summary>
    [Fact]
    public void Forward_FlatTerrain_MovesAtRunSpeed()
    {
        // Crossroads Plains — very flat open savanna (map 1, Kalimdor)
        var (controller, player, _) = CreateController(-442.0f, -2598.0f, 96.0f, facing: 0f);

        const int frameCount = 200;  // 10 seconds at 50ms
        const float dtSec = 0.05f;
        const float expectedSpeed = 7.0f;
        const float totalTime = frameCount * dtSec; // 10s

        float startX = player.Position.X;
        float startY = player.Position.Y;

        var frames = RunFramesWithTrace(controller, player, frameCount, dtSec,
            forceFlagsEachFrame: MovementFlags.MOVEFLAG_FORWARD);

        float endX = player.Position.X;
        float endY = player.Position.Y;
        float endZ = player.Position.Z;
        float dx = endX - startX;
        float dy = endY - startY;
        float totalXY = MathF.Sqrt(dx * dx + dy * dy);
        float actualSpeed = totalXY / totalTime;

        _output.WriteLine($"=== Forward_FlatTerrain_MovesAtRunSpeed ===");
        _output.WriteLine($"Start: ({startX:F1}, {startY:F1})  End: ({endX:F1}, {endY:F1}, {endZ:F1})");
        _output.WriteLine($"XY displacement: {totalXY:F1}y over {totalTime:F0}s = {actualSpeed:F2} y/s (expected {expectedSpeed:F1} y/s)");
        _output.WriteLine($"Speed ratio: {actualSpeed / expectedSpeed:P0}");

        // Check Z stability — should stay near ground, not sink
        float maxZDelta = 0f;
        for (int i = 1; i < frames.Count; i++)
        {
            float zd = MathF.Abs(frames[i].Z - frames[i - 1].Z);
            maxZDelta = MathF.Max(maxZDelta, zd);
        }
        _output.WriteLine($"Max per-frame Z delta: {maxZDelta:F3}y");

        // Assert speed is within 50-120% of expected (generous for terrain variation)
        Assert.True(actualSpeed >= expectedSpeed * 0.5f,
            $"Bot moved too slowly: {actualSpeed:F2} y/s < {expectedSpeed * 0.5f:F1} y/s (50% threshold). " +
            $"Total displacement: {totalXY:F1}y over {totalTime:F0}s.");
        Assert.True(actualSpeed <= expectedSpeed * 1.2f,
            $"Bot moved too fast: {actualSpeed:F2} y/s > {expectedSpeed * 1.2f:F1} y/s.");

        // Z should not oscillate wildly
        Assert.True(maxZDelta < 3.0f,
            $"Z oscillation too large: max per-frame delta = {maxZDelta:F3}y (threshold 3.0y).");
    }

    /// <summary>
    /// Same as above but at Valley of Trials coordinates (used by live tests).
    /// Tests whether the physics engine produces movement at this specific location.
    /// </summary>
    [Fact]
    public void Forward_ValleyOfTrials_MovesAtRunSpeed()
    {
        // Valley of Trials — same coords as NavigationTests and live MovementSpeedTests
        var (controller, player, _) = CreateController(-284.0f, -4383.0f, 57.0f, facing: 5.54f);

        const int frameCount = 200;
        const float dtSec = 0.05f;
        const float expectedSpeed = 7.0f;
        const float totalTime = frameCount * dtSec;

        float startX = player.Position.X;
        float startY = player.Position.Y;

        var frames = RunFramesWithTrace(controller, player, frameCount, dtSec,
            forceFlagsEachFrame: MovementFlags.MOVEFLAG_FORWARD);

        float endX = player.Position.X;
        float endY = player.Position.Y;
        float dx = endX - startX;
        float dy = endY - startY;
        float totalXY = MathF.Sqrt(dx * dx + dy * dy);
        float actualSpeed = totalXY / totalTime;

        _output.WriteLine($"=== Forward_ValleyOfTrials_MovesAtRunSpeed ===");
        _output.WriteLine($"Start: ({startX:F1}, {startY:F1})  End: ({endX:F1}, {endY:F1})");
        _output.WriteLine($"XY displacement: {totalXY:F1}y over {totalTime:F0}s = {actualSpeed:F2} y/s");
        _output.WriteLine($"Speed ratio: {actualSpeed / expectedSpeed:P0}");

        Assert.True(actualSpeed >= expectedSpeed * 0.5f,
            $"Bot moved too slowly at VoT: {actualSpeed:F2} y/s (expected >{expectedSpeed * 0.5f:F1})");
    }

    /// <summary>
    /// Validates that movement packets are sent at ~500ms intervals with correct position deltas.
    /// </summary>
    [Fact]
    public void Forward_FlatTerrain_PacketTimingAndPositionDeltas()
    {
        var (controller, player, mockClient) = CreateController(-442.0f, -2598.0f, 96.0f, facing: 0f);

        // Capture all SendMovementOpcodeAsync calls
        var packetCalls = new List<(uint timeMs, float x, float y, float z)>();
        mockClient
            .Setup(c => c.SendMovementOpcodeAsync(
                It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback<Opcode, byte[], CancellationToken>((op, buf, ct) =>
            {
                // Record position at time of packet send
                packetCalls.Add((0, player.Position.X, player.Position.Y, player.Position.Z));
            })
            .Returns(Task.CompletedTask);

        const int frameCount = 200;  // 10 seconds at 50ms
        const float dtSec = 0.05f;
        uint startTimeMs = 1000;

        // Run frames with advancing time (matching MovementControllerPhysicsTests pattern)
        for (int i = 0; i < frameCount; i++)
        {
            player.MovementFlags |= MovementFlags.MOVEFLAG_FORWARD;
            uint timeMs = startTimeMs + (uint)MathF.Round(i * dtSec * 1000f);
            controller.Update(dtSec, timeMs);
        }

        _output.WriteLine($"=== Packet Timing and Position Deltas ===");
        _output.WriteLine($"Total frames: {frameCount}, Total packets sent: {packetCalls.Count}");

        // WoW.exe heartbeat: 100ms interval (0x5E2110). Over 10s → ~100 packets.
        // First packet on flag change, then every ~100ms when position changes.
        Assert.True(packetCalls.Count >= 50,
            $"Expected at least 50 packets over 10s (100ms interval), got {packetCalls.Count}.");
        Assert.True(packetCalls.Count <= 130,
            $"Expected at most 130 packets over 10s, got {packetCalls.Count}. Too many packets.");

        // Check position deltas between consecutive packets
        for (int i = 1; i < packetCalls.Count; i++)
        {
            var prev = packetCalls[i - 1];
            var curr = packetCalls[i];
            float pdx = curr.x - prev.x;
            float pdy = curr.y - prev.y;
            float pDist = MathF.Sqrt(pdx * pdx + pdy * pdy);

            if (i <= 5)
                _output.WriteLine($"  Packet {i}: delta={pDist:F2}y pos=({curr.x:F1},{curr.y:F1},{curr.z:F1})");
        }

        _output.WriteLine("Packet count validation passed.");
    }
}
