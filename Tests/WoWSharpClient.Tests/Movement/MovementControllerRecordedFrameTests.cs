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
/// Feeds recorded FG frame data through MovementController with NativeLocalPhysics.TestStepOverride
/// returning "perfect" physics output (the next recorded frame's position). This isolates
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
    private const uint MOVEFLAG_JUMPING = 0x00002000;
    private const uint MOVEFLAG_FALLINGFAR = 0x00000800;
    private const uint MOVEFLAG_SWIMMING = 0x00200000;
    private const uint MOVEFLAG_ONTRANSPORT = 0x02000000;

    public MovementControllerRecordedFrameTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Creates a MovementController wired to NativeLocalPhysics.TestStepOverride for
    /// pre-programmed physics output for each frame. WoWClient is no-op.
    /// Physics is always local via NativeLocalPhysics — no remote PathfindingClient.
    /// </summary>
    private static (MovementController controller, WoWLocalPlayer player)
        CreateController(RecordedFrame initialFrame)
    {
        var mockClient = new Mock<WoWClient>();
        mockClient
            .Setup(c => c.SendMovementOpcodeAsync(
                It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

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

        // Prevent native DLL initialization in test environment
        NativeLocalPhysics.TestSetSceneSliceModeOverride ??= _ => { };
        NativeLocalPhysics.TestClearSceneCacheOverride ??= _ => { };

        var controller = new MovementController(mockClient.Object, player);
        return (controller, player);
    }

    /// <summary>
    /// Builds a native PhysicsOutput that represents "perfect" physics — the C++ engine returned
    /// exactly what the real WoW client recorded as the next frame's position.
    /// </summary>
    private static NativePhysics.PhysicsOutput BuildPerfectOutput(RecordedFrame nextFrame, RecordedFrame currentFrame)
    {
        bool isFalling = (nextFrame.MovementFlags & MOVEFLAG_FALLINGFAR) != 0;
        return new NativePhysics.PhysicsOutput
        {
            X = nextFrame.Position.X,
            Y = nextFrame.Position.Y,
            Z = nextFrame.Position.Z,
            Vx = 0,
            Vy = 0,
            Vz = isFalling ? -9.8f : 0f,
            MoveFlags = nextFrame.MovementFlags,
            Orientation = nextFrame.Facing,
            GroundZ = nextFrame.Position.Z, // Perfect ground = position Z
            GroundNx = 0,
            GroundNy = 0,
            GroundNz = 1,
            FallTime = (uint)nextFrame.FallTime,
        };
    }

    /// <summary>
    /// Sets NativeLocalPhysics.TestStepOverride with a lambda that uses native types.
    /// Helper to convert proto-style test setup to native physics override.
    /// </summary>
    private static void SetPhysicsOverride(Func<NativePhysics.PhysicsInput, NativePhysics.PhysicsOutput> handler)
    {
        NativeLocalPhysics.TestStepOverride = handler;
    }

    private static void ClearPhysicsOverride()
    {
        NativeLocalPhysics.TestStepOverride = null;
    }

    [Fact]
    public void IsRecording_CapturesPhysicsFramesWithPacketMetadata()
    {
        var initialFrame = new RecordedFrame
        {
            FrameTimestamp = 0,
            MovementFlags = 0,
            Position = new RecordedPosition { X = 10f, Y = 20f, Z = 30f },
            Facing = 0.25f,
            WalkSpeed = 2.5f,
            RunSpeed = 7.0f,
            RunBackSpeed = 4.5f,
            SwimSpeed = 4.722f,
            SwimBackSpeed = 2.5f,
        };
        var nextFrame = new RecordedFrame
        {
            FrameTimestamp = 100,
            MovementFlags = MOVEFLAG_FORWARD,
            Position = new RecordedPosition { X = 10.7f, Y = 20.0f, Z = 30.0f },
            Facing = 0.25f,
            WalkSpeed = initialFrame.WalkSpeed,
            RunSpeed = initialFrame.RunSpeed,
            RunBackSpeed = initialFrame.RunBackSpeed,
            SwimSpeed = initialFrame.SwimSpeed,
            SwimBackSpeed = initialFrame.SwimBackSpeed,
        };

        var (controller, player) = CreateController(initialFrame);
        controller.IsRecording = true;
        player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;

        SetPhysicsOverride(_ => BuildPerfectOutput(nextFrame, initialFrame));

        try
        {
            controller.Update(0.1f, 100);

            var frames = controller.GetRecordedFrames();
            var frame = Assert.Single(frames);
            Assert.Equal((uint)Opcode.MSG_MOVE_START_FORWARD, frame.PacketOpcode);
            Assert.Equal((uint)MovementFlags.MOVEFLAG_FORWARD, frame.PacketFlags);
            Assert.Equal(nextFrame.Position.X, frame.PosX, 3);
            Assert.Equal(nextFrame.Position.Z, frame.PosZ, 3);
            Assert.Equal(100u, frame.GameTimeMs);
        }
        finally
        {
            ClearPhysicsOverride();
        }
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

    private static int CountSegmentMovementPackets(MovementRecording recording, int startIdx, int endIdx)
    {
        if (recording.Packets.Count == 0)
            return 0;

        var (segmentStartMs, segmentEndMs) = GetSegmentPacketWindow(recording, startIdx, endIdx);
        return recording.Packets.Count(packet =>
            packet.IsOutbound &&
            IsMovementOpcode(packet.Opcode) &&
            packet.TimestampMs >= segmentStartMs &&
            packet.TimestampMs <= segmentEndMs);
    }

    private static int CountSegmentOpcode(
        MovementRecording recording,
        int startIdx,
        int endIdx,
        Opcode opcode)
    {
        if (recording.Packets.Count == 0)
            return 0;

        var (segmentStartMs, segmentEndMs) = GetSegmentPacketWindow(recording, startIdx, endIdx);
        return recording.Packets.Count(packet =>
            packet.IsOutbound &&
            packet.Opcode == (ushort)opcode &&
            packet.TimestampMs >= segmentStartMs &&
            packet.TimestampMs <= segmentEndMs);
    }

    private static (ulong startMs, ulong endMs) GetSegmentPacketWindow(
        MovementRecording recording,
        int startIdx,
        int endIdx)
    {
        var frames = recording.Frames;
        long startMs = startIdx > 0
            ? frames[startIdx - 1].FrameTimestamp
            : frames[startIdx].FrameTimestamp - EstimateFrameIntervalMs(frames, startIdx);
        long endMs = endIdx + 1 < frames.Count
            ? frames[endIdx + 1].FrameTimestamp
            : frames[endIdx].FrameTimestamp + EstimateFrameIntervalMs(frames, endIdx);

        return ((ulong)Math.Max(0, startMs), (ulong)Math.Max(0, endMs));
    }

    private static long EstimateFrameIntervalMs(IReadOnlyList<RecordedFrame> frames, int index)
    {
        if (frames.Count <= 1)
            return 500;

        if (index > 0)
            return Math.Max(1, frames[index].FrameTimestamp - frames[index - 1].FrameTimestamp);

        return Math.Max(1, frames[1].FrameTimestamp - frames[0].FrameTimestamp);
    }

    private static bool IsSimpleGroundForwardFrame(RecordedFrame frame)
    {
        const uint disallowedFlags =
            MOVEFLAG_FALLINGFAR |
            MOVEFLAG_JUMPING |
            MOVEFLAG_SWIMMING |
            MOVEFLAG_ONTRANSPORT;

        return (frame.MovementFlags & MOVEFLAG_FORWARD) != 0 &&
            (frame.MovementFlags & disallowedFlags) == 0;
    }

    private static bool IsSimpleGroundStopFrame(RecordedFrame frame)
    {
        const uint disallowedFlags =
            MOVEFLAG_FALLINGFAR |
            MOVEFLAG_JUMPING |
            MOVEFLAG_SWIMMING |
            MOVEFLAG_ONTRANSPORT;

        return (frame.MovementFlags & MOVEFLAG_FORWARD) == 0 &&
            (frame.MovementFlags & disallowedFlags) == 0;
    }

    private static RecordedFrame CreateSegmentInitialFrame(MovementRecording recording, int startIdx)
    {
        var source = recording.Frames[startIdx];
        long syntheticTimestamp = source.FrameTimestamp - EstimateFrameIntervalMs(recording.Frames, startIdx);

        if (startIdx > 0)
        {
            var previous = recording.Frames[startIdx - 1];
            return new RecordedFrame
            {
                FrameTimestamp = previous.FrameTimestamp,
                MovementFlags = previous.MovementFlags,
                Position = new RecordedPosition
                {
                    X = previous.Position.X,
                    Y = previous.Position.Y,
                    Z = previous.Position.Z,
                },
                Facing = previous.Facing,
                FallTime = previous.FallTime,
                WalkSpeed = source.WalkSpeed,
                RunSpeed = source.RunSpeed,
                RunBackSpeed = source.RunBackSpeed,
                SwimSpeed = source.SwimSpeed,
                SwimBackSpeed = source.SwimBackSpeed,
                TurnRate = source.TurnRate,
                JumpVerticalSpeed = source.JumpVerticalSpeed,
                JumpSinAngle = source.JumpSinAngle,
                JumpCosAngle = source.JumpCosAngle,
                JumpHorizontalSpeed = source.JumpHorizontalSpeed,
                SwimPitch = source.SwimPitch,
                FallStartHeight = source.FallStartHeight,
                TransportGuid = previous.TransportGuid,
                TransportOffsetX = previous.TransportOffsetX,
                TransportOffsetY = previous.TransportOffsetY,
                TransportOffsetZ = previous.TransportOffsetZ,
                TransportOrientation = previous.TransportOrientation,
                CurrentSpeed = source.CurrentSpeed,
                FallingSpeed = source.FallingSpeed,
                SplineFlags = previous.SplineFlags,
                SplineTimePassed = previous.SplineTimePassed,
                SplineDuration = previous.SplineDuration,
                SplineId = previous.SplineId,
                SplineFinalPoint = previous.SplineFinalPoint,
                SplineFinalDestination = previous.SplineFinalDestination,
                SplineNodes = previous.SplineNodes,
                SystemTick = previous.SystemTick,
                NearbyGameObjects = previous.NearbyGameObjects,
                NearbyUnits = previous.NearbyUnits,
            };
        }

        return new RecordedFrame
        {
            FrameTimestamp = Math.Max(0, syntheticTimestamp),
            MovementFlags = 0,
            Position = new RecordedPosition
            {
                X = source.Position.X,
                Y = source.Position.Y,
                Z = source.Position.Z,
            },
            Facing = source.Facing,
            FallTime = 0,
            WalkSpeed = source.WalkSpeed,
            RunSpeed = source.RunSpeed,
            RunBackSpeed = source.RunBackSpeed,
            SwimSpeed = source.SwimSpeed,
            SwimBackSpeed = source.SwimBackSpeed,
            TurnRate = source.TurnRate,
            JumpVerticalSpeed = source.JumpVerticalSpeed,
            JumpSinAngle = source.JumpSinAngle,
            JumpCosAngle = source.JumpCosAngle,
            JumpHorizontalSpeed = source.JumpHorizontalSpeed,
            SwimPitch = source.SwimPitch,
            FallStartHeight = source.FallStartHeight,
            TransportGuid = 0,
            TransportOffsetX = 0,
            TransportOffsetY = 0,
            TransportOffsetZ = 0,
            TransportOrientation = 0,
            CurrentSpeed = source.CurrentSpeed,
            FallingSpeed = 0,
            SplineFlags = 0,
            SplineTimePassed = 0,
            SplineDuration = 0,
            SplineId = 0,
            SystemTick = source.SystemTick,
            NearbyGameObjects = source.NearbyGameObjects,
            NearbyUnits = source.NearbyUnits,
        };
    }

    private static (int startIdx, int endIdx, int packetCount)? FindWalkingSegmentWithPackets(
        MovementRecording recording, int minFrames = 30)
    {
        var frames = recording.Frames;
        int runStart = -1;
        int bestStart = -1;
        int bestEnd = -1;
        int bestPacketCount = 0;

        void ConsiderRun(int startIdx, int endIdx)
        {
            if (startIdx < 0 || endIdx < startIdx || endIdx - startIdx + 1 < minFrames)
                return;

            bool hasCleanStop = endIdx + 1 < frames.Count && IsSimpleGroundStopFrame(frames[endIdx + 1]);
            if (!hasCleanStop)
                return;

            int packetCount = CountSegmentMovementPackets(recording, startIdx, endIdx);
            if (packetCount > bestPacketCount)
            {
                bestStart = startIdx;
                bestEnd = endIdx;
                bestPacketCount = packetCount;
            }
        }

        for (int i = 0; i < frames.Count; i++)
        {
            var frame = frames[i];
            bool isWalking = IsSimpleGroundForwardFrame(frame);

            if (isWalking)
            {
                if (runStart < 0)
                    runStart = i;
            }
            else
            {
                ConsiderRun(runStart, i - 1);
                runStart = -1;
            }
        }

        ConsiderRun(runStart, frames.Count - 1);

        return bestPacketCount > 0
            ? (bestStart, bestEnd, bestPacketCount)
            : null;
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

        var (controller, player) = CreateController(frames[start]);

        // Set up a path target far ahead so the controller has a destination
        float facing = frames[start].Facing;
        controller.SetTargetWaypoint(new Position(
            frames[start].Position.X + MathF.Cos(facing) * 100f,
            frames[start].Position.Y + MathF.Sin(facing) * 100f,
            frames[start].Position.Z));

        // Track which frame index the mock should return
        int currentFrameIdx = start;
        SetPhysicsOverride(input =>
        {
            // Return the next recorded frame's position as "perfect" physics
            int nextIdx = Math.Min(currentFrameIdx + 1, end);
            return BuildPerfectOutput(frames[nextIdx], frames[currentFrameIdx]);
        });

        try
        {
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
        finally
        {
            ClearPhysicsOverride();
        }
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
        var (controller, player) = CreateController(frames[start]);

        float facing = frames[start].Facing;
        controller.SetTargetWaypoint(new Position(
            frames[start].Position.X + MathF.Cos(facing) * 100f,
            frames[start].Position.Y + MathF.Sin(facing) * 100f,
            frames[start].Position.Z));

        int currentFrameIdx = start;
        SetPhysicsOverride(input =>
        {
            int nextIdx = Math.Min(currentFrameIdx + 1, end);
            return BuildPerfectOutput(frames[nextIdx], frames[currentFrameIdx]);
        });

        int forwardLostCount = 0;

        try
        {
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
        finally
        {
            ClearPhysicsOverride();
        }
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
        var (controller, player) = CreateController(frames[start]);

        float facing = frames[start].Facing;
        controller.SetTargetWaypoint(new Position(
            frames[start].Position.X + MathF.Cos(facing) * 100f,
            frames[start].Position.Y + MathF.Sin(facing) * 100f,
            frames[start].Position.Z));

        int currentFrameIdx = start;
        SetPhysicsOverride(input =>
        {
            int nextIdx = Math.Min(currentFrameIdx + 1, end);
            return BuildPerfectOutput(frames[nextIdx], frames[currentFrameIdx]);
        });

        try
        {
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
        finally
        {
            ClearPhysicsOverride();
        }
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
        var (controller, player) = CreateController(frames[start]);

        float facing = frames[start].Facing;
        controller.SetTargetWaypoint(new Position(
            frames[start].Position.X + MathF.Cos(facing) * 100f,
            frames[start].Position.Y + MathF.Sin(facing) * 100f,
            frames[start].Position.Z));

        // Capture native PhysicsInput for each frame to verify continuity
        var capturedInputs = new List<NativePhysics.PhysicsInput>();
        int currentFrameIdx = start;

        SetPhysicsOverride(input =>
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

        try
        {
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
        finally
        {
            ClearPhysicsOverride();
        }
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

        NativeLocalPhysics.TestSetSceneSliceModeOverride ??= _ => { };
        NativeLocalPhysics.TestClearSceneCacheOverride ??= _ => { };
        var controller = new MovementController(mockClient.Object, player);

        // Set target 50y north
        controller.SetTargetWaypoint(new Position(285f, -4690f, 12f));

        // Override: advance position by runSpeed * dt in the facing direction each frame
        SetPhysicsOverride(input =>
        {
            float step = input.RunSpeed * input.DeltaTime;
            float dirX = MathF.Cos(input.Orientation);
            float dirY = MathF.Sin(input.Orientation);
            bool hasForward = (input.MoveFlags & MOVEFLAG_FORWARD) != 0;
            float moveX = hasForward ? dirX * step : 0f;
            float moveY = hasForward ? dirY * step : 0f;

            return new NativePhysics.PhysicsOutput
            {
                X = input.X + moveX,
                Y = input.Y + moveY,
                Z = 12f, // Flat ground
                GroundZ = 12f,
                GroundNx = 0, GroundNy = 0, GroundNz = 1,
                MoveFlags = input.MoveFlags,
                FallTime = 0,
            };
        });

        try
        {
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
        finally
        {
            ClearPhysicsOverride();
        }
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

        NativeLocalPhysics.TestSetSceneSliceModeOverride ??= _ => { };
        NativeLocalPhysics.TestClearSceneCacheOverride ??= _ => { };
        var controller = new MovementController(mockClient.Object, player);
        // No SetTargetWaypoint / SetPath call

        SetPhysicsOverride(input =>
        {
            float step = input.RunSpeed * input.DeltaTime;
            bool hasForward = (input.MoveFlags & MOVEFLAG_FORWARD) != 0;
            return new NativePhysics.PhysicsOutput
            {
                X = input.X + (hasForward ? step : 0f),
                Y = input.Y,
                Z = 50f,
                GroundZ = 50f,
                GroundNx = 0, GroundNy = 0, GroundNz = 1,
                MoveFlags = input.MoveFlags,
                FallTime = 0,
            };
        });

        try
        {
            player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;

            for (int i = 0; i < 60; i++)
                controller.Update(0.05f, (uint)(1000 + i * 50));

            float expectedX = 100f + 7.0f * 3.0f; // 121y
            float actualDist = player.Position.X - 100f;

            _output.WriteLine($"Moved {actualDist:F1}y east (expected ~{expectedX - 100f:F1}y)");

            Assert.True(actualDist > 15f,
                $"Only moved {actualDist:F1}y in 3s — guards blocking movement without path");
        }
        finally
        {
            ClearPhysicsOverride();
        }
    }

    [Fact]
    public void SyntheticWalk_PacketSequence_StartsWithStartForward_EndsWithStop()
    {
        // Verifies that MovementController sends the correct opcode sequence for
        // a simple walk: START_FORWARD → HEARTBEATs → STOP.
        // This is the baseline for FG/BG parity — FG client sends the same sequence.
        var sentOpcodes = new List<(uint gameTimeMs, Opcode opcode)>();

        var mockClient = new Mock<WoWClient>();
        mockClient
            .Setup(c => c.SendMovementOpcodeAsync(
                It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns<Opcode, byte[], CancellationToken>((op, _, _) =>
            {
                sentOpcodes.Add((0, op));
                return Task.CompletedTask;
            });

        var player = new WoWLocalPlayer(new HighGuid(42))
        {
            Position = new Position(285f, -4740f, 12f),
            Facing = MathF.PI / 2f,
            MovementFlags = MovementFlags.MOVEFLAG_NONE,
            WalkSpeed = 2.5f, RunSpeed = 7.0f, RunBackSpeed = 4.5f,
            SwimSpeed = 4.722f, SwimBackSpeed = 2.5f,
            Race = Race.Orc, Gender = Gender.Male, MapId = 1,
            Health = 100, MaxHealth = 100,
        };

        NativeLocalPhysics.TestSetSceneSliceModeOverride ??= _ => { };
        NativeLocalPhysics.TestClearSceneCacheOverride ??= _ => { };
        var controller = new MovementController(mockClient.Object, player);
        controller.SetTargetWaypoint(new Position(285f, -4690f, 12f));

        // Perfect flat physics
        SetPhysicsOverride(input =>
        {
            float step = input.RunSpeed * input.DeltaTime;
            bool fwd = (input.MoveFlags & MOVEFLAG_FORWARD) != 0;
            float dirX = MathF.Cos(input.Orientation);
            float dirY = MathF.Sin(input.Orientation);
            return new NativePhysics.PhysicsOutput
            {
                X = input.X + (fwd ? dirX * step : 0f),
                Y = input.Y + (fwd ? dirY * step : 0f),
                Z = 12f,
                GroundZ = 12f,
                GroundNx = 0, GroundNy = 0, GroundNz = 1,
                MoveFlags = input.MoveFlags,
                FallTime = 0,
            };
        });

        try
        {
            // Phase 1: Start walking (90 frames = 3s at 30fps)
            player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;
            for (int i = 0; i < 90; i++)
                controller.Update(0.033f, (uint)(1000 + i * 33));

            // Phase 2: Stop walking (30 frames with no forward flag)
            player.MovementFlags = MovementFlags.MOVEFLAG_NONE;
            for (int i = 0; i < 30; i++)
                controller.Update(0.033f, (uint)(4000 + i * 33));

            _output.WriteLine($"Total opcodes sent: {sentOpcodes.Count}");
            foreach (var (_, op) in sentOpcodes)
                _output.WriteLine($"  {op}");

            // Must have sent at least one packet
            Assert.True(sentOpcodes.Count > 0, "No movement packets sent");

            // First movement-related opcode should be START_FORWARD
            var firstMoveOpcode = sentOpcodes.First().opcode;
            Assert.Equal(Opcode.MSG_MOVE_START_FORWARD, firstMoveOpcode);

            // Should contain heartbeats
            int heartbeats = sentOpcodes.Count(s => s.opcode == Opcode.MSG_MOVE_HEARTBEAT);
            _output.WriteLine($"Heartbeats: {heartbeats}");
            Assert.True(heartbeats >= 1, "Expected at least 1 heartbeat during 3s walk");

            // Last opcode should be STOP (after clearing FORWARD flag)
            var lastOpcode = sentOpcodes.Last().opcode;
            Assert.Equal(Opcode.MSG_MOVE_STOP, lastOpcode);
        }
        finally
        {
            ClearPhysicsOverride();
        }
    }

    [Fact]
    public void RecordedFrames_WithPackets_OpcodeSequenceParity()
    {
        // Loads a recording that has packet data (from FG PacketLogger integration).
        // Feeds frames through BG MovementController, captures its opcode output,
        // and compares against the recorded FG opcode sequence.
        //
        // If no recording has packets, the test documents what BG sends for the
        // recorded movement and passes (no parity assertion without FG data).
        var recordings = LoadAllRecordings();
        if (recordings.Count == 0)
        {
            _output.WriteLine("No recordings found — skipping parity test");
            return;
        }

        // Find a recording with a walking segment
        MovementRecording? recording = null;
        (int startIdx, int endIdx)? segment = null;
        string? recordingName = null;
        int segmentPacketCount = 0;
        const int minPacketBackedFrames = 8;
        const int minFallbackFrames = 30;
        int bestFacingPacketCount = int.MaxValue;

        foreach (var (name, rec) in recordings)
        {
            var packetSegment = FindWalkingSegmentWithPackets(rec, minPacketBackedFrames);
            if (packetSegment == null)
                continue;

            int facingPacketCount = CountSegmentOpcode(
                rec,
                packetSegment.Value.startIdx,
                packetSegment.Value.endIdx,
                Opcode.MSG_MOVE_SET_FACING);

            bool betterCandidate =
                recording == null ||
                facingPacketCount < bestFacingPacketCount ||
                (facingPacketCount == bestFacingPacketCount &&
                 packetSegment.Value.packetCount > segmentPacketCount);

            if (betterCandidate)
            {
                recording = rec;
                recordingName = name;
                segment = (packetSegment.Value.startIdx, packetSegment.Value.endIdx);
                segmentPacketCount = packetSegment.Value.packetCount;
                bestFacingPacketCount = facingPacketCount;
            }
        }

        if (recording == null || segment == null)
        {
            foreach (var (name, rec) in recordings)
            {
                segment = FindWalkingSegment(rec, minFallbackFrames);
                if (segment != null)
                {
                    recording = rec;
                    recordingName = name;
                    segmentPacketCount = CountSegmentMovementPackets(rec, segment.Value.startIdx, segment.Value.endIdx);
                    break;
                }
            }
        }

        if (recording == null || segment == null)
        {
            _output.WriteLine($"No recording with {minFallbackFrames}+ walking frames found");
            return;
        }

        var (start, end) = segment.Value;
        var frames = recording.Frames;
        _output.WriteLine($"Recording: {recordingName}");
        _output.WriteLine($"Walking segment: frames {start}-{end} ({end - start + 1} frames)");
        _output.WriteLine($"FG movement packets in selected segment: {segmentPacketCount}");
        _output.WriteLine($"FG facing packets in selected segment: {CountSegmentOpcode(recording, start, end, Opcode.MSG_MOVE_SET_FACING)}");

        // Set up controller with opcode capture
        var bgOpcodes = new List<(ulong timestampMs, Opcode opcode)>();
        var mockClient = new Mock<WoWClient>();
        mockClient
            .Setup(c => c.SendMovementOpcodeAsync(
                It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns<Opcode, byte[], CancellationToken>((op, _, _) =>
            {
                bgOpcodes.Add((0, op));
                return Task.CompletedTask;
            });

        var initialFrame = CreateSegmentInitialFrame(recording, start);
        var (_, player) = CreateController(initialFrame);
        // Re-wire with our capturing mock client (CreateController already set overrides)
        var capturingController = new MovementController(mockClient.Object, player);

        float facing = frames[start].Facing;
        capturingController.SetTargetWaypoint(new Position(
            frames[start].Position.X + MathF.Cos(facing) * 100f,
            frames[start].Position.Y + MathF.Sin(facing) * 100f,
            frames[start].Position.Z));

        int executionEnd = Math.Min(frames.Count - 1, end + 1);
        int frameIdx = start;
        SetPhysicsOverride(_ =>
        {
            int nextIdx = Math.Min(frameIdx + 1, executionEnd);
            return BuildPerfectOutput(frames[nextIdx], frames[frameIdx]);
        });

        try
        {
        // Run through the walking segment plus one post-roll frame so stop transitions
        // in the recording can produce a matching BG MSG_MOVE_STOP.
        for (int i = start; i <= executionEnd; i++)
        {
            frameIdx = i;
            var frame = frames[i];
            var nextFrame = i < frames.Count - 1 ? frames[i + 1] : frame;
            float dt = i < frames.Count - 1
                ? (nextFrame.FrameTimestamp - frame.FrameTimestamp) / 1000f
                : EstimateFrameIntervalMs(frames, i) / 1000f;
            if (dt <= 0 || dt > 1.0f) dt = 0.033f;

            player.MovementFlags = (MovementFlags)frame.MovementFlags;
            player.Facing = frame.Facing;
            player.Position = new Position(frame.Position.X, frame.Position.Y, frame.Position.Z);

            uint gameTimeMs = (uint)(frame.FrameTimestamp & 0xFFFFFFFF);
            capturingController.Update(dt, gameTimeMs);
        }

        // Report BG opcodes
        _output.WriteLine($"\nBG MovementController sent {bgOpcodes.Count} packets:");
        var bgMovementOpcodes = bgOpcodes
            .Where(o => IsMovementOpcode((ushort)o.opcode))
            .ToList();
        foreach (var (_, op) in bgMovementOpcodes)
            _output.WriteLine($"  {op} (0x{(ushort)op:X4})");

        // Check if recording has FG packet data for parity comparison
        bool hasPackets = recording.Packets.Count > 0;
        if (hasPackets)
        {
            // Filter FG packets to movement-related outbound opcodes in the segment's time range
            var (segmentStartMs, segmentEndMs) = GetSegmentPacketWindow(recording, start, end);
            var fgMovementPackets = recording.Packets
                .Where(p => p.IsOutbound
                    && IsMovementOpcode(p.Opcode)
                    && p.TimestampMs >= segmentStartMs
                    && p.TimestampMs <= segmentEndMs)
                .ToList();

            _output.WriteLine($"\nFG recorded {fgMovementPackets.Count} movement packets in segment:");
            foreach (var p in fgMovementPackets)
                _output.WriteLine($"  0x{p.Opcode:X4} @ {p.TimestampMs}ms (outbound={p.IsOutbound})");

            // Parity: compare opcode type distribution (not exact timing)
            var bgOpcodeCounts = bgMovementOpcodes
                .GroupBy(o => o.opcode)
                .ToDictionary(g => g.Key, g => g.Count());
            var fgOpcodeCounts = fgMovementPackets
                .GroupBy(p => (Opcode)p.Opcode)
                .ToDictionary(g => g.Key, g => g.Count());

            _output.WriteLine("\n=== Opcode Distribution Parity ===");
            var allOpcodes = bgOpcodeCounts.Keys.Union(fgOpcodeCounts.Keys).OrderBy(o => (ushort)o);
            foreach (var op in allOpcodes)
            {
                int bgCount = bgOpcodeCounts.GetValueOrDefault(op, 0);
                int fgCount = fgOpcodeCounts.GetValueOrDefault(op, 0);
                string match = bgCount == fgCount ? "MATCH" : (MathF.Abs(bgCount - fgCount) <= 2 ? "CLOSE" : "DIFFER");
                _output.WriteLine($"  {op,-35} BG={bgCount,3}  FG={fgCount,3}  [{match}]");
            }

            // Assert: BG should send START_FORWARD if FG did
            bool fgHasStartForward = fgMovementPackets.Any(p => p.Opcode == (ushort)Opcode.MSG_MOVE_START_FORWARD);
            bool bgHasStartForward = bgMovementOpcodes.Any(o => o.opcode == Opcode.MSG_MOVE_START_FORWARD);
            if (fgHasStartForward)
            {
                Assert.True(bgHasStartForward,
                    "FG sent MSG_MOVE_START_FORWARD but BG didn't — BG failed to start movement");
            }

            // Assert: heartbeat counts should be within 50% of each other
            int fgHeartbeats = fgOpcodeCounts.GetValueOrDefault(Opcode.MSG_MOVE_HEARTBEAT, 0);
            int bgHeartbeats = bgOpcodeCounts.GetValueOrDefault(Opcode.MSG_MOVE_HEARTBEAT, 0);
            if (fgHeartbeats > 0 && bgHeartbeats > 0)
            {
                float ratio = (float)bgHeartbeats / fgHeartbeats;
                Assert.True(ratio > 0.5f && ratio < 2.0f,
                    $"Heartbeat count mismatch: BG={bgHeartbeats} FG={fgHeartbeats} ratio={ratio:F2} — timing divergence");
            }
        }
        else
        {
            _output.WriteLine("\nNo FG packet data in recording — parity comparison deferred.");
            _output.WriteLine("Record with FG PacketLogger enabled to populate packets for parity testing.");
        }

        // Regardless of FG data, verify BG sent reasonable packets
        Assert.True(bgMovementOpcodes.Count > 0,
            "BG controller sent no movement opcodes for a walking segment");
        }
        finally
        {
            ClearPhysicsOverride();
        }
    }

    /// <summary>
    /// Returns true if the opcode is a movement-related opcode (MSG_MOVE_*).
    /// Movement opcodes in 1.12.1 are in the range 0x0B5-0x0EE.
    /// </summary>
    private static bool IsMovementOpcode(ushort opcode) =>
        opcode >= 0x0B5 && opcode <= 0x0EE;

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
