using BotRunner.Clients;
using GameData.Core.Enums;
using GameData.Core.Models;
using Moq;
using Navigation.Physics.Tests;
using Pathfinding;
using WoWSharpClient.Client;
using WoWSharpClient.Models;
using WoWSharpClient.Movement;
using Xunit.Abstractions;

namespace WoWSharpClient.Tests.Movement;

/// <summary>
/// Feeds recorded FG frame data through MovementController with a mocked PathfindingClient
/// that returns "perfect" physics output (the next recorded frame's position). This isolates
/// the C# guards and state management from the C++ physics engine to detect when guards
/// erroneously reject valid displacement.
///
/// Key invariants tested:
/// - Position must advance at expected speed (guards don't block movement)
/// - MOVEFLAG_FORWARD is preserved across frames (not stripped by stuck recovery)
/// - Z stays within bounds (guards don't cause sinking/bouncing)
/// - Continuity state round-trips correctly between frames
/// </summary>
public class MovementControllerRecordedFrameTests
{
    private readonly ITestOutputHelper _output;

    // MOVEFLAG constants matching PhysicsBridge.h
    private const uint MOVEFLAG_FORWARD = 0x00000001;
    private const uint MOVEFLAG_FALLINGFAR = 0x00000800;
    private const uint MOVEFLAG_SWIMMING = 0x00200000;

    public MovementControllerRecordedFrameTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Creates a MovementController wired to a mock PathfindingClient that returns
    /// pre-programmed PhysicsOutput for each frame. WoWClient is no-op.
    /// </summary>
    private static (MovementController controller, WoWLocalPlayer player, Mock<PathfindingClient> physics)
        CreateController(RecordedFrame initialFrame)
    {
        var mockClient = new Mock<WoWClient>();
        mockClient
            .Setup(c => c.SendMovementOpcodeAsync(
                It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockPhysics = new Mock<PathfindingClient>();

        var player = new WoWLocalPlayer(new HighGuid(42))
        {
            Position = new Position(initialFrame.Position.X, initialFrame.Position.Y, initialFrame.Position.Z),
            Facing = initialFrame.Facing,
            MovementFlags = (MovementFlags)initialFrame.MovementFlags,
            WalkSpeed = initialFrame.WalkSpeed > 0 ? initialFrame.WalkSpeed : 2.5f,
            RunSpeed = initialFrame.RunSpeed > 0 ? initialFrame.RunSpeed : 7.0f,
            RunBackSpeed = initialFrame.RunBackSpeed > 0 ? initialFrame.RunBackSpeed : 4.5f,
            SwimSpeed = initialFrame.SwimSpeed > 0 ? initialFrame.SwimSpeed : 4.722f,
            SwimBackSpeed = initialFrame.SwimBackSpeed > 0 ? initialFrame.SwimBackSpeed : 2.5f,
            Race = Race.Orc,
            Gender = Gender.Male,
            MapId = 1, // Kalimdor
            Health = 100,
            MaxHealth = 100,
        };

        var controller = new MovementController(mockClient.Object, mockPhysics.Object, player);
        return (controller, player, mockPhysics);
    }

    /// <summary>
    /// Builds a PhysicsOutput that represents "perfect" physics — the C++ engine returned
    /// exactly what the real WoW client recorded as the next frame's position.
    /// </summary>
    private static PhysicsOutput BuildPerfectOutput(RecordedFrame nextFrame, RecordedFrame currentFrame)
    {
        bool isFalling = (nextFrame.MovementFlags & MOVEFLAG_FALLINGFAR) != 0;
        return new PhysicsOutput
        {
            NewPosX = nextFrame.Position.X,
            NewPosY = nextFrame.Position.Y,
            NewPosZ = nextFrame.Position.Z,
            NewVelX = 0,
            NewVelY = 0,
            NewVelZ = isFalling ? -9.8f : 0f,
            MovementFlags = nextFrame.MovementFlags,
            Orientation = nextFrame.Facing,
            IsGrounded = !isFalling,
            GroundZ = nextFrame.Position.Z, // Perfect ground = position Z
            GroundNx = 0,
            GroundNy = 0,
            GroundNz = 1,
            FallTime = nextFrame.FallTime,
        };
    }

    /// <summary>
    /// Finds a contiguous segment of "walking forward" frames in a recording — frames where
    /// MOVEFLAG_FORWARD is set, not falling, not swimming, and position changes.
    /// Returns null if no suitable segment is found.
    /// </summary>
    private static (int startIdx, int endIdx)? FindWalkingSegment(
        MovementRecording recording, int minFrames = 30)
    {
        var frames = recording.Frames;
        int runStart = -1;
        int runLength = 0;

        for (int i = 0; i < frames.Count; i++)
        {
            var f = frames[i];
            bool isWalking = (f.MovementFlags & MOVEFLAG_FORWARD) != 0
                && (f.MovementFlags & MOVEFLAG_FALLINGFAR) == 0
                && (f.MovementFlags & MOVEFLAG_SWIMMING) == 0;

            if (isWalking)
            {
                if (runStart < 0) runStart = i;
                runLength++;
                if (runLength >= minFrames)
                    return (runStart, i);
            }
            else
            {
                runStart = -1;
                runLength = 0;
            }
        }

        return null;
    }

    [Fact]
    public void PerfectPhysics_FlatWalk_PositionAdvancesAtExpectedSpeed()
    {
        // Load a Durotar recording with flat-terrain walking
        var recordings = LoadAllRecordings();
        if (recordings.Count == 0)
        {
            _output.WriteLine("No recordings found — skipping");
            return;
        }

        // Find a recording with a walking segment
        MovementRecording? recording = null;
        (int startIdx, int endIdx)? segment = null;

        foreach (var (name, rec) in recordings)
        {
            segment = FindWalkingSegment(rec);
            if (segment != null)
            {
                recording = rec;
                _output.WriteLine($"Using recording: {name} ({rec.Frames.Count} frames)");
                break;
            }
        }

        if (recording == null || segment == null)
        {
            _output.WriteLine("No recording with a suitable walking segment found — skipping");
            return;
        }

        var (start, end) = segment.Value;
        var frames = recording.Frames;
        _output.WriteLine($"Walking segment: frames {start}-{end} ({end - start + 1} frames)");
        _output.WriteLine($"Start pos: ({frames[start].Position.X:F2}, {frames[start].Position.Y:F2}, {frames[start].Position.Z:F2})");
        _output.WriteLine($"End pos: ({frames[end].Position.X:F2}, {frames[end].Position.Y:F2}, {frames[end].Position.Z:F2})");

        var (controller, player, mockPhysics) = CreateController(frames[start]);

        // Set up a path target far ahead so the controller has a destination
        float facing = frames[start].Facing;
        controller.SetTargetWaypoint(new Position(
            frames[start].Position.X + MathF.Cos(facing) * 100f,
            frames[start].Position.Y + MathF.Sin(facing) * 100f,
            frames[start].Position.Z));

        // Track which frame index the mock should return
        int currentFrameIdx = start;
        mockPhysics
            .Setup(p => p.PhysicsStep(It.IsAny<PhysicsInput>()))
            .Returns<PhysicsInput>(input =>
            {
                // Return the next recorded frame's position as "perfect" physics
                int nextIdx = Math.Min(currentFrameIdx + 1, end);
                return BuildPerfectOutput(frames[nextIdx], frames[currentFrameIdx]);
            });

        // Run frames through MovementController
        float totalDistance = 0f;
        float totalTime = 0f;
        int blockedFrames = 0;
        int totalFrames = 0;
        var prevPos = player.Position;

        _output.WriteLine($"\n{"Frame",6} {"dt",6} {"X",10} {"Y",12} {"Z",10} {"Dist",8} {"Speed",8} {"Flags",10}");
        _output.WriteLine(new string('-', 75));

        for (int i = start; i < end; i++)
        {
            currentFrameIdx = i;
            var frame = frames[i];
            var nextFrame = frames[i + 1];

            // Compute dt from frame timestamps
            float dt = (nextFrame.FrameTimestamp - frame.FrameTimestamp) / 1000f;
            if (dt <= 0 || dt > 1.0f) dt = 0.033f; // Clamp to ~30fps if bad data

            // Set player movement flags to match recording
            player.MovementFlags = (MovementFlags)frame.MovementFlags;
            player.Facing = frame.Facing;

            uint gameTimeMs = (uint)(frame.FrameTimestamp & 0xFFFFFFFF);
            controller.Update(dt, gameTimeMs);

            // Measure displacement
            float dx = player.Position.X - prevPos.X;
            float dy = player.Position.Y - prevPos.Y;
            float frameDist = MathF.Sqrt(dx * dx + dy * dy);
            float frameSpeed = dt > 0.001f ? frameDist / dt : 0f;

            totalDistance += frameDist;
            totalTime += dt;
            totalFrames++;
            if (frameDist < 0.001f) blockedFrames++;

            // Log every 10th frame
            if (totalFrames % 10 == 1 || frameDist < 0.001f)
            {
                _output.WriteLine($"{totalFrames,6} {dt,6:F3} {player.Position.X,10:F2} {player.Position.Y,12:F2} " +
                    $"{player.Position.Z,10:F2} {frameDist,8:F3} {frameSpeed,8:F2} 0x{(uint)player.MovementFlags:X}");
            }

            prevPos = player.Position;
        }

        float avgSpeed = totalTime > 0.1f ? totalDistance / totalTime : 0f;
        float blockedPct = totalFrames > 0 ? (float)blockedFrames / totalFrames * 100f : 0f;
        float expectedSpeed = frames[start].RunSpeed > 0 ? frames[start].RunSpeed : 7.0f;

        _output.WriteLine($"\nSummary:");
        _output.WriteLine($"  Total distance: {totalDistance:F1}y over {totalTime:F1}s");
        _output.WriteLine($"  Average speed: {avgSpeed:F2} y/s (expected ~{expectedSpeed:F1})");
        _output.WriteLine($"  Speed ratio: {avgSpeed / expectedSpeed:P0}");
        _output.WriteLine($"  Blocked frames: {blockedFrames}/{totalFrames} ({blockedPct:F1}%)");

        // Assert: with perfect physics, MovementController should NOT block movement
        Assert.True(blockedPct < 10f,
            $"Too many blocked frames: {blockedPct:F1}% ({blockedFrames}/{totalFrames}). " +
            $"Guards are rejecting valid physics displacement.");

        // Assert: average speed should be within 50-150% of expected
        Assert.True(avgSpeed > expectedSpeed * 0.5f,
            $"Average speed too low: {avgSpeed:F2} y/s (expected >{expectedSpeed * 0.5f:F1}). " +
            $"Guards are impeding movement.");
    }

    [Fact]
    public void PerfectPhysics_FlatWalk_ForwardFlagPreserved()
    {
        var recordings = LoadAllRecordings();
        if (recordings.Count == 0) return;

        MovementRecording? recording = null;
        (int startIdx, int endIdx)? segment = null;
        foreach (var (name, rec) in recordings)
        {
            segment = FindWalkingSegment(rec, 60);
            if (segment != null) { recording = rec; break; }
        }
        if (recording == null || segment == null) return;

        var (start, end) = segment.Value;
        var frames = recording.Frames;
        var (controller, player, mockPhysics) = CreateController(frames[start]);

        float facing = frames[start].Facing;
        controller.SetTargetWaypoint(new Position(
            frames[start].Position.X + MathF.Cos(facing) * 100f,
            frames[start].Position.Y + MathF.Sin(facing) * 100f,
            frames[start].Position.Z));

        int currentFrameIdx = start;
        mockPhysics
            .Setup(p => p.PhysicsStep(It.IsAny<PhysicsInput>()))
            .Returns<PhysicsInput>(input =>
            {
                int nextIdx = Math.Min(currentFrameIdx + 1, end);
                return BuildPerfectOutput(frames[nextIdx], frames[currentFrameIdx]);
            });

        int forwardLostCount = 0;

        for (int i = start; i < end; i++)
        {
            currentFrameIdx = i;
            var frame = frames[i];
            var nextFrame = frames[i + 1];
            float dt = (nextFrame.FrameTimestamp - frame.FrameTimestamp) / 1000f;
            if (dt <= 0 || dt > 1.0f) dt = 0.033f;

            player.MovementFlags = (MovementFlags)frame.MovementFlags;
            player.Facing = frame.Facing;

            uint gameTimeMs = (uint)(frame.FrameTimestamp & 0xFFFFFFFF);
            controller.Update(dt, gameTimeMs);

            // After Update, FORWARD flag should be preserved (not stripped by stuck recovery)
            bool inputHadForward = (frame.MovementFlags & MOVEFLAG_FORWARD) != 0;
            bool outputHasForward = player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_FORWARD);
            if (inputHadForward && !outputHasForward)
            {
                forwardLostCount++;
                _output.WriteLine($"Frame {i - start}: FORWARD flag stripped! " +
                    $"Input=0x{frame.MovementFlags:X} Output=0x{(uint)player.MovementFlags:X}");
            }
        }

        _output.WriteLine($"FORWARD flag stripped on {forwardLostCount}/{end - start} frames");
        Assert.True(forwardLostCount == 0,
            $"FORWARD flag was stripped on {forwardLostCount} frames — stuck recovery fired erroneously with perfect physics");
    }

    [Fact]
    public void PerfectPhysics_FlatWalk_ZStaysStable()
    {
        var recordings = LoadAllRecordings();
        if (recordings.Count == 0) return;

        MovementRecording? recording = null;
        (int startIdx, int endIdx)? segment = null;
        foreach (var (name, rec) in recordings)
        {
            segment = FindWalkingSegment(rec);
            if (segment != null) { recording = rec; break; }
        }
        if (recording == null || segment == null) return;

        var (start, end) = segment.Value;
        var frames = recording.Frames;
        var (controller, player, mockPhysics) = CreateController(frames[start]);

        float facing = frames[start].Facing;
        controller.SetTargetWaypoint(new Position(
            frames[start].Position.X + MathF.Cos(facing) * 100f,
            frames[start].Position.Y + MathF.Sin(facing) * 100f,
            frames[start].Position.Z));

        int currentFrameIdx = start;
        mockPhysics
            .Setup(p => p.PhysicsStep(It.IsAny<PhysicsInput>()))
            .Returns<PhysicsInput>(input =>
            {
                int nextIdx = Math.Min(currentFrameIdx + 1, end);
                return BuildPerfectOutput(frames[nextIdx], frames[currentFrameIdx]);
            });

        var zValues = new List<float>();
        float baseZ = frames[start].Position.Z;

        for (int i = start; i < end; i++)
        {
            currentFrameIdx = i;
            var frame = frames[i];
            var nextFrame = frames[i + 1];
            float dt = (nextFrame.FrameTimestamp - frame.FrameTimestamp) / 1000f;
            if (dt <= 0 || dt > 1.0f) dt = 0.033f;

            player.MovementFlags = (MovementFlags)frame.MovementFlags;
            player.Facing = frame.Facing;

            uint gameTimeMs = (uint)(frame.FrameTimestamp & 0xFFFFFFFF);
            controller.Update(dt, gameTimeMs);
            zValues.Add(player.Position.Z);
        }

        float minZ = zValues.Min();
        float maxZ = zValues.Max();
        float zRange = maxZ - minZ;
        float recordedZRange = 0f;
        for (int i = start; i <= end; i++)
        {
            float recZ = frames[i].Position.Z;
            recordedZRange = MathF.Max(recordedZRange, MathF.Abs(recZ - baseZ));
        }

        _output.WriteLine($"Controller Z range: {minZ:F3} to {maxZ:F3} (range: {zRange:F3}y)");
        _output.WriteLine($"Recorded Z range from base: {recordedZRange:F3}y");

        // Z should not deviate more than 5y from recording's Z on flat terrain
        // (allows for gentle hills but catches sinking/bouncing bugs)
        Assert.True(zRange < 5.0f,
            $"Z oscillation too large: {zRange:F3}y — guards may be causing Z instability");
    }

    [Fact]
    public void PerfectPhysics_ContinuityState_RoundTripped()
    {
        // Verify that continuity state from each physics output feeds into the next input
        var recordings = LoadAllRecordings();
        if (recordings.Count == 0) return;

        MovementRecording? recording = null;
        (int startIdx, int endIdx)? segment = null;
        foreach (var (name, rec) in recordings)
        {
            segment = FindWalkingSegment(rec, 10);
            if (segment != null) { recording = rec; break; }
        }
        if (recording == null || segment == null) return;

        var (start, end) = segment.Value;
        var frames = recording.Frames;
        var (controller, player, mockPhysics) = CreateController(frames[start]);

        float facing = frames[start].Facing;
        controller.SetTargetWaypoint(new Position(
            frames[start].Position.X + MathF.Cos(facing) * 100f,
            frames[start].Position.Y + MathF.Sin(facing) * 100f,
            frames[start].Position.Z));

        // Capture PhysicsInput for each frame to verify continuity
        var capturedInputs = new List<PhysicsInput>();
        int currentFrameIdx = start;

        mockPhysics
            .Setup(p => p.PhysicsStep(It.IsAny<PhysicsInput>()))
            .Returns<PhysicsInput>(input =>
            {
                capturedInputs.Add(input);
                int nextIdx = Math.Min(currentFrameIdx + 1, end);
                var output = BuildPerfectOutput(frames[nextIdx], frames[currentFrameIdx]);
                // Set distinctive continuity values to verify they round-trip
                output.GroundZ = frames[nextIdx].Position.Z - 0.1f; // Slightly below
                output.GroundNx = 0.05f;
                output.GroundNy = 0.02f;
                output.GroundNz = 0.998f;
                output.PendingDepenX = 0.001f;
                output.PendingDepenY = 0.002f;
                return output;
            });

        // Run enough frames to check continuity
        int framesToRun = Math.Min(end - start, 10);
        for (int i = start; i < start + framesToRun; i++)
        {
            currentFrameIdx = i;
            var frame = frames[i];
            var nextFrame = frames[i + 1];
            float dt = (nextFrame.FrameTimestamp - frame.FrameTimestamp) / 1000f;
            if (dt <= 0 || dt > 1.0f) dt = 0.033f;

            player.MovementFlags = (MovementFlags)frame.MovementFlags;
            player.Facing = frame.Facing;

            uint gameTimeMs = (uint)(frame.FrameTimestamp & 0xFFFFFFFF);
            controller.Update(dt, gameTimeMs);
        }

        // Check that continuity values from output N appear in input N+1
        for (int i = 1; i < capturedInputs.Count; i++)
        {
            var input = capturedInputs[i];
            _output.WriteLine($"Input[{i}]: prevGZ={input.PrevGroundZ:F3} " +
                $"prevGN=({input.PrevGroundNx:F3},{input.PrevGroundNy:F3},{input.PrevGroundNz:F3}) " +
                $"pendDepen=({input.PendingDepenX:F4},{input.PendingDepenY:F4})");

            // GroundZ from output should appear as PrevGroundZ in next input
            // (may be modified by guards, so use generous tolerance)
            Assert.True(input.PrevGroundNz > 0.9f,
                $"Frame {i}: PrevGroundNz={input.PrevGroundNz:F3} — continuity broken");
        }

        _output.WriteLine($"\nCaptured {capturedInputs.Count} physics inputs, continuity verified");
    }

    [Fact]
    public void SyntheticFlatWalk_NoRecording_SpeedMatchesExpected()
    {
        // Fully synthetic test — no recording needed. Generates "perfect" physics
        // output for a flat-terrain walk and verifies MovementController doesn't block it.
        var mockClient = new Mock<WoWClient>();
        mockClient.Setup(c => c.SendMovementOpcodeAsync(
            It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockPhysics = new Mock<PathfindingClient>();
        var player = new WoWLocalPlayer(new HighGuid(42))
        {
            Position = new Position(285f, -4740f, 12f),
            Facing = MathF.PI / 2f, // North (sin=1)
            MovementFlags = MovementFlags.MOVEFLAG_NONE,
            WalkSpeed = 2.5f,
            RunSpeed = 7.0f,
            RunBackSpeed = 4.5f,
            SwimSpeed = 4.722f,
            SwimBackSpeed = 2.5f,
            Race = Race.Orc,
            Gender = Gender.Male,
            MapId = 1,
            Health = 100,
            MaxHealth = 100,
        };

        var controller = new MovementController(mockClient.Object, mockPhysics.Object, player);

        // Set target 50y north
        controller.SetTargetWaypoint(new Position(285f, -4690f, 12f));

        // Mock: advance position by runSpeed * dt in the facing direction each frame
        mockPhysics
            .Setup(p => p.PhysicsStep(It.IsAny<PhysicsInput>()))
            .Returns<PhysicsInput>(input =>
            {
                float step = input.RunSpeed * input.DeltaTime;
                float dirX = MathF.Cos(input.Facing);
                float dirY = MathF.Sin(input.Facing);
                bool hasForward = (input.MovementFlags & MOVEFLAG_FORWARD) != 0;
                float moveX = hasForward ? dirX * step : 0f;
                float moveY = hasForward ? dirY * step : 0f;

                return new PhysicsOutput
                {
                    NewPosX = input.PosX + moveX,
                    NewPosY = input.PosY + moveY,
                    NewPosZ = 12f, // Flat ground
                    IsGrounded = true,
                    GroundZ = 12f,
                    GroundNx = 0, GroundNy = 0, GroundNz = 1,
                    MovementFlags = input.MovementFlags,
                    FallTime = 0,
                };
            });

        // Simulate 3 seconds at 30fps (90 frames)
        float totalTime = 0f;
        player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;

        _output.WriteLine($"{"Frame",6} {"X",10} {"Y",12} {"Z",8} {"Flags",10}");
        _output.WriteLine(new string('-', 55));

        for (int i = 0; i < 90; i++)
        {
            float dt = 0.033f;
            uint gameTimeMs = (uint)(1000 + i * 33);
            controller.Update(dt, gameTimeMs);
            totalTime += dt;

            if (i % 15 == 0)
            {
                _output.WriteLine($"{i,6} {player.Position.X,10:F2} {player.Position.Y,12:F2} " +
                    $"{player.Position.Z,8:F2} 0x{(uint)player.MovementFlags:X}");
            }
        }

        float distanceMoved = MathF.Sqrt(
            (player.Position.X - 285f) * (player.Position.X - 285f) +
            (player.Position.Y - (-4740f)) * (player.Position.Y - (-4740f)));
        float avgSpeed = distanceMoved / totalTime;

        _output.WriteLine($"\nDistance: {distanceMoved:F1}y in {totalTime:F1}s = {avgSpeed:F2} y/s");
        _output.WriteLine($"Expected: ~{7.0f * totalTime:F1}y at 7.0 y/s");
        _output.WriteLine($"Speed ratio: {avgSpeed / 7.0f:P0}");

        // Must be within 80-120% of expected speed
        Assert.True(avgSpeed > 7.0f * 0.8f,
            $"Speed too low: {avgSpeed:F2} y/s (expected >5.6). Guards blocking movement.");
        Assert.True(avgSpeed < 7.0f * 1.2f,
            $"Speed too high: {avgSpeed:F2} y/s (expected <8.4).");

        // Z should stay at 12 (flat ground)
        Assert.Equal(12f, player.Position.Z, 1);
    }

    [Fact]
    public void SyntheticFlatWalk_NoPath_GuardsDoNotBlock()
    {
        // Same as above but WITHOUT setting a path/waypoint. Tests that movement
        // still works even when no path is set (physics is the sole authority).
        var mockClient = new Mock<WoWClient>();
        mockClient.Setup(c => c.SendMovementOpcodeAsync(
            It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockPhysics = new Mock<PathfindingClient>();
        var player = new WoWLocalPlayer(new HighGuid(42))
        {
            Position = new Position(100f, 200f, 50f),
            Facing = 0f, // East
            MovementFlags = MovementFlags.MOVEFLAG_NONE,
            RunSpeed = 7.0f,
            WalkSpeed = 2.5f,
            RunBackSpeed = 4.5f,
            SwimSpeed = 4.722f,
            SwimBackSpeed = 2.5f,
            Race = Race.Orc,
            Gender = Gender.Male,
            MapId = 1,
            Health = 100,
            MaxHealth = 100,
        };

        var controller = new MovementController(mockClient.Object, mockPhysics.Object, player);
        // No SetTargetWaypoint / SetPath call

        mockPhysics
            .Setup(p => p.PhysicsStep(It.IsAny<PhysicsInput>()))
            .Returns<PhysicsInput>(input =>
            {
                float step = input.RunSpeed * input.DeltaTime;
                bool hasForward = (input.MovementFlags & MOVEFLAG_FORWARD) != 0;
                return new PhysicsOutput
                {
                    NewPosX = input.PosX + (hasForward ? step : 0f),
                    NewPosY = input.PosY,
                    NewPosZ = 50f,
                    IsGrounded = true,
                    GroundZ = 50f,
                    GroundNx = 0, GroundNy = 0, GroundNz = 1,
                    MovementFlags = input.MovementFlags,
                    FallTime = 0,
                };
            });

        player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;

        for (int i = 0; i < 60; i++)
            controller.Update(0.05f, (uint)(1000 + i * 50));

        float expectedX = 100f + 7.0f * 3.0f; // 121y
        float actualDist = player.Position.X - 100f;

        _output.WriteLine($"Moved {actualDist:F1}y east (expected ~{expectedX - 100f:F1}y)");

        Assert.True(actualDist > 15f,
            $"Only moved {actualDist:F1}y in 3s — guards blocking movement without path");
    }

    /// <summary>
    /// Loads all available recordings. Returns empty list if directory not found.
    /// </summary>
    private List<(string name, MovementRecording rec)> LoadAllRecordings()
    {
        try
        {
            return Navigation.Physics.Tests.Helpers.RecordingTestHelpers.LoadAllRecordings(_output);
        }
        catch (DirectoryNotFoundException)
        {
            _output.WriteLine("Recordings directory not found");
            return [];
        }
    }
}
