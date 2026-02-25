using GameData.Core.Enums;
using GameData.Core.Constants;
using PathfindingService.Repository;
using MovementFlags = GameData.Core.Enums.MovementFlags;
using GameData.Core.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit.Abstractions;

namespace PathfindingService.Tests
{
    public class PhysicsFixture : IDisposable
    {
        public Physics Physics { get; }

        public PhysicsFixture()
        {
            // Preflight checks similar to NavigationFixture
            VerifyNavigationDll();
            Physics = new Physics();
        }

        private static void VerifyNavigationDll()
        {
            var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var testOutputDir = Path.GetDirectoryName(assemblyLocation);

            if (testOutputDir == null)
                throw new InvalidOperationException("Cannot determine test output directory");

            var navigationDllPath = Path.Combine(testOutputDir, "Navigation.dll");

            if (!File.Exists(navigationDllPath))
            {
                throw new FileNotFoundException(
                    $"Navigation.dll not found in test output directory: {testOutputDir}");
            }
        }

        public void Dispose() { }
    }

    public class PhysicsEngineTests(PhysicsFixture fixture, ITestOutputHelper output) : IClassFixture<PhysicsFixture>
    {
        private readonly Physics _phy = fixture.Physics;
        private readonly ITestOutputHelper _output = output;
        private const float Dt = 0.05f;
        private const float Gravity = 19.2911f;
        private const uint PhysicsFlagTrustInputVelocity = 0x1;
        private const uint TeleportToPlaneFlag = 0x08000000;
        private const uint SplineElevationFlag = 0x04000000;
        private const uint AirborneFlagsMask = (uint)(MovementFlags.MOVEFLAG_JUMPING | MovementFlags.MOVEFLAG_FALLINGFAR);
        private const float TeleportDistanceThresholdSq = 2500f; // 50y
        private const string FallRecordingStem = "Dralrahgra_Orgrimmar_2026-02-08_11-32-44";
        private static readonly JsonSerializerOptions RecordingJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
        };

        [Theory]
        [InlineData(1u, -562.225f, -4189.092f, 70.789f, Race.Orc, 0f, MovementFlags.MOVEFLAG_NONE)]
        [InlineData(0u, -8949.950000f, -132.490000f, 83.229485f, Race.Human, 0f, MovementFlags.MOVEFLAG_NONE)]
        [InlineData(0u, -6240.320000f, 331.033000f, 382.619171f, Race.Human, 0f, MovementFlags.MOVEFLAG_NONE)]
        [InlineData(1u, -2917.580000f, -257.980000f, 53.362350f, Race.Orc, 0f, MovementFlags.MOVEFLAG_NONE)]
        [InlineData(1u, 1629.359985f, -4373.380377f, 31.255800f, Race.Orc, 3.548300f, MovementFlags.MOVEFLAG_NONE)]
        public void StepPhysics_IdleExpectations(
            uint mapId,
            float x, float y, float z,
            Race race,
            float orientation,
            MovementFlags expectedFlags)
        {
            // Default heights and radii for races
            float height = race == Race.Orc ? 2.0f : 1.8f;
            float radius = race == Race.Orc ? 0.6f : 0.5f;

            var input = new PhysicsInput
            {
                mapId = mapId,
                x = x,
                y = y,
                z = z,
                orientation = orientation,
                moveFlags = (uint)MovementFlags.MOVEFLAG_NONE,
                deltaTime = Dt,
                height = height,
                radius = radius,
                runSpeed = 7.0f,
                walkSpeed = 2.5f
            };

            const int frameCount = 20;
            var frames = RunFrames(input, frameCount, (frame, _, _) => (x, y, z));
            WriteFrameTrace(nameof(StepPhysics_IdleExpectations), mapId, frames);

            float maxHorizontalDrift = frames.Max(f =>
                MathF.Sqrt(MathF.Pow(f.X - x, 2) + MathF.Pow(f.Y - y, 2)));
            Assert.True(maxHorizontalDrift < 1.0f,
                $"Idle drift exceeded 1.0y at map={mapId}: max={maxHorizontalDrift:F3}. {DescribeWindow(frames, 0, 8)}");

            float maxVerticalDelta = frames.Max(f => MathF.Abs(f.Z - z));
            Assert.True(maxVerticalDelta < 5f,
                $"Idle Z drift exceeded 5y at map={mapId}: max={maxVerticalDelta:F3}. {DescribeWindow(frames, 0, 8)}");
        }

        [Fact]
        public void StepPhysics_RecordingReplay_FallFromHeight_FrameByFrameVariance()
        {
            string recordingPath;
            try
            {
                recordingPath = FindRecordingPath(FallRecordingStem);
            }
            catch (DirectoryNotFoundException ex)
            {
                _output.WriteLine($"SKIP: {ex.Message}");
                return;
            }
            catch (FileNotFoundException ex)
            {
                _output.WriteLine($"SKIP: {ex.Message}");
                return;
            }

            var recording = LoadRecording(recordingPath);
            Assert.True(recording.Frames.Count > 1, $"Recording has insufficient frames: {recording.Frames.Count}");

            var replay = ReplayRecordingFrames(recording);
            Assert.True(replay.Frames.Count > 0, "No replay frames were simulated.");

            WriteReplaySummary(nameof(StepPhysics_RecordingReplay_FallFromHeight_FrameByFrameVariance), replay);
            WriteReplayFrameTrace(nameof(StepPhysics_RecordingReplay_FallFromHeight_FrameByFrameVariance), replay.Frames);

            Assert.True(replay.AirborneFrameCount >= 80,
                $"Expected at least 80 airborne frames in replay, actual={replay.AirborneFrameCount}.");

            Assert.True(replay.MeanPositionError < 0.45f,
                $"Average position error too high: avg={replay.MeanPositionError:F4}y p95={replay.P95PositionError:F4}y p99={replay.P99PositionError:F4}y max={replay.MaxPositionError:F4}y");

            Assert.True(replay.P99PositionError < 2.5f,
                $"P99 position error too high: p99={replay.P99PositionError:F4}y max={replay.MaxPositionError:F4}y");
        }

        [Fact]
        public void LineOfSight_ShouldReturnTrue_WhenNoObstruction()
        {
            // Test line of sight in open area
            var from = new XYZ(-8949.95f, -132.49f, 83.53f);
            var to = new XYZ(-8945.0f, -132.0f, 83.53f);

            var result = _phy.LineOfSight(0, from, to);

            Assert.True(result);
        }

        [Fact]
        public void LineOfSight_ShouldReturnFalse_WhenObstructed()
        {
            // Deterministic blocked LOS route in Stormwind area (map 0).
            // Same-XY vertical probes are often clear in this engine path and are not stable as obstruction tests.
            var from = new XYZ(-8949.95f, -132.49f, 83.53f);
            var to = new XYZ(-8880.00f, -220.00f, 83.53f);

            var result = _phy.LineOfSight(0, from, to);

            Assert.False(result);
        }

        private List<PhysicsFrameSnapshot> RunFrames(
            PhysicsInput initialInput,
            int frameCount,
            Func<int, PhysicsInput, PhysicsOutput, (float expectedX, float expectedY, float expectedZ)> expectedSelector)
        {
            Assert.True(frameCount > 0, "frameCount must be positive");

            var frames = new List<PhysicsFrameSnapshot>(frameCount);
            var input = initialInput;
            var intentFlags = (MovementFlags)initialInput.moveFlags & IntentMoveMask;
            uint startFallTime = initialInput.fallTime;

            for (int i = 0; i < frameCount; i++)
            {
                input.deltaTime = Dt;
                input.frameCounter = (uint)i;

                var output = _phy.StepPhysicsV2(input, Dt);
                AssertFinite(output, i);

                var expected = expectedSelector(i, input, output);
                float deltaX = output.x - expected.expectedX;
                float deltaY = output.y - expected.expectedY;
                float deltaZ = output.z - expected.expectedZ;

                frames.Add(new PhysicsFrameSnapshot(
                    Frame: i,
                    TimeSec: (i + 1) * Dt,
                    X: output.x,
                    Y: output.y,
                    Z: output.z,
                    DeltaX: deltaX,
                    DeltaY: deltaY,
                    DeltaZ: deltaZ,
                    Vx: output.vx,
                    Vy: output.vy,
                    Vz: output.vz,
                    GroundZ: output.groundZ,
                    FallTimeSec: output.fallTime,
                    Flags: output.moveFlags,
                    ExpectedX: expected.expectedX,
                    ExpectedY: expected.expectedY,
                    ExpectedZ: expected.expectedZ));

                var runtimeFlags = (MovementFlags)output.moveFlags & RuntimeStateMask;
                input.x = output.x;
                input.y = output.y;
                input.z = output.z;
                input.orientation = output.orientation;
                input.pitch = output.pitch;
                input.vx = output.vx;
                input.vy = output.vy;
                input.vz = output.vz;
                input.moveFlags = (uint)(intentFlags | runtimeFlags);
                input.fallTime = startFallTime + (uint)MathF.Max(0f, output.fallTime * 1000f);
                input.prevGroundZ = output.groundZ;
                input.prevGroundNx = output.groundNx;
                input.prevGroundNy = output.groundNy;
                input.prevGroundNz = output.groundNz;
                input.pendingDepenX = output.pendingDepenX;
                input.pendingDepenY = output.pendingDepenY;
                input.pendingDepenZ = output.pendingDepenZ;
                input.standingOnInstanceId = output.standingOnInstanceId;
                input.standingOnLocalX = output.standingOnLocalX;
                input.standingOnLocalY = output.standingOnLocalY;
                input.standingOnLocalZ = output.standingOnLocalZ;
            }

            return frames;
        }

        private static string FindRecordingPath(string recordingStem)
        {
            var candidates = new[]
            {
                Environment.GetEnvironmentVariable("WWOW_RECORDINGS_DIR"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BloogBot", "MovementRecordings")
            };

            string? recordingsDir = candidates
                .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path));

            if (recordingsDir == null)
            {
                throw new DirectoryNotFoundException(
                    "Movement recordings directory not found. Set WWOW_RECORDINGS_DIR or use Documents/BloogBot/MovementRecordings.");
            }

            var matches = Directory.GetFiles(recordingsDir, "*.json")
                .Where(path => Path.GetFileNameWithoutExtension(path)
                    .Contains(recordingStem, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToList();

            if (matches.Count == 0)
            {
                throw new FileNotFoundException(
                    $"No recording found matching '{recordingStem}' in {recordingsDir}");
            }

            return matches[0];
        }

        private static MovementRecordingLite LoadRecording(string recordingPath)
        {
            var json = File.ReadAllText(recordingPath);
            var recording = JsonSerializer.Deserialize<MovementRecordingLite>(json, RecordingJsonOptions)
                ?? throw new InvalidOperationException($"Failed to deserialize recording at {recordingPath}");

            recording.Frames ??= [];
            return recording;
        }

        private ReplayRunResult ReplayRecordingFrames(MovementRecordingLite recording)
        {
            if (recording.Frames.Count < 2)
                return new ReplayRunResult([], 0, 0, 0, 0f, 0f, 0f, 0f, -1);

            var replayFrames = new List<ReplayFrameSnapshot>(recording.Frames.Count - 1);
            int airborneFrameCount = 0;
            int skippedDtCount = 0;
            int skippedTeleportCount = 0;

            var (capsuleRadius, capsuleHeight) = ResolveCapsule(recording.Race, recording.Gender);
            var prevOutput = new PhysicsOutput();
            float prevGroundZ = recording.Frames[0].Position.Z;
            int fallStartFrame = -1;

            for (int i = 0; i < recording.Frames.Count - 1; i++)
            {
                var current = recording.Frames[i];
                var next = recording.Frames[i + 1];
                float dtSec = (next.FrameTimestamp - current.FrameTimestamp) / 1000f;

                if (!float.IsFinite(dtSec) || dtSec <= 0f)
                {
                    skippedDtCount++;
                    continue;
                }

                float dxToNext = next.Position.X - current.Position.X;
                float dyToNext = next.Position.Y - current.Position.Y;
                float dzToNext = next.Position.Z - current.Position.Z;
                float stepDistanceSq = dxToNext * dxToNext + dyToNext * dyToNext + dzToNext * dzToNext;

                if (stepDistanceSq > TeleportDistanceThresholdSq)
                {
                    skippedTeleportCount++;
                    prevOutput = default;
                    prevGroundZ = next.Position.Z;
                    fallStartFrame = -1;
                    continue;
                }

                bool isAirborne = (current.MovementFlags & AirborneFlagsMask) != 0;
                bool nextIsAirborne = (next.MovementFlags & AirborneFlagsMask) != 0;
                bool transitionToAirborne = !isAirborne && nextIsAirborne;
                bool wasAirborne = fallStartFrame >= 0;
                uint fallTimeMs = 0;

                if (isAirborne)
                {
                    airborneFrameCount++;
                    if (fallStartFrame < 0)
                        fallStartFrame = i;

                    long elapsedMs = current.FrameTimestamp - recording.Frames[fallStartFrame].FrameTimestamp;
                    fallTimeMs = elapsedMs > 0 ? (uint)elapsedMs : 0u;
                }
                else
                {
                    fallStartFrame = -1;
                }

                uint inputFlags = current.MovementFlags & ~TeleportToPlaneFlag & ~SplineElevationFlag;
                if (transitionToAirborne)
                {
                    uint nextAirborneFlags = next.MovementFlags & AirborneFlagsMask;
                    inputFlags = (inputFlags & ~AirborneFlagsMask) | nextAirborneFlags;
                }

                bool stepIsAirborne = (inputFlags & AirborneFlagsMask) != 0;
                if (stepIsAirborne && !isAirborne)
                {
                    airborneFrameCount++;
                }

                var input = new PhysicsInput
                {
                    mapId = recording.MapId,
                    moveFlags = inputFlags,
                    x = current.Position.X,
                    y = current.Position.Y,
                    z = current.Position.Z,
                    orientation = current.Facing,
                    pitch = current.SwimPitch,
                    vx = 0f,
                    vy = 0f,
                    vz = 0f,
                    walkSpeed = current.WalkSpeed,
                    runSpeed = current.RunSpeed,
                    runBackSpeed = current.RunBackSpeed,
                    swimSpeed = current.SwimSpeed,
                    swimBackSpeed = current.SwimBackSpeed,
                    flightSpeed = 0f,
                    turnSpeed = current.TurnRate,
                    transportGuid = current.TransportGuid,
                    transportX = 0f,
                    transportY = 0f,
                    transportZ = 0f,
                    transportO = 0f,
                    fallTime = fallTimeMs,
                    height = capsuleHeight,
                    radius = capsuleRadius,
                    prevGroundZ = prevGroundZ,
                    prevGroundNx = prevOutput.groundNx,
                    prevGroundNy = prevOutput.groundNy,
                    prevGroundNz = prevOutput.groundNz != 0f ? prevOutput.groundNz : 1f,
                    pendingDepenX = prevOutput.pendingDepenX,
                    pendingDepenY = prevOutput.pendingDepenY,
                    pendingDepenZ = prevOutput.pendingDepenZ,
                    standingOnInstanceId = prevOutput.standingOnInstanceId,
                    standingOnLocalX = prevOutput.standingOnLocalX,
                    standingOnLocalY = prevOutput.standingOnLocalY,
                    standingOnLocalZ = prevOutput.standingOnLocalZ,
                    nearbyObjects = IntPtr.Zero,
                    nearbyObjectCount = 0,
                    deltaTime = dtSec,
                    frameCounter = (uint)i,
                    physicsFlags = 0u
                };

                // Drive replay with per-frame captured velocity deltas so grounded
                // duplicates and catch-up bursts can be reproduced exactly.
                input.vx = dxToNext / dtSec;
                input.vy = dyToNext / dtSec;
                input.physicsFlags = PhysicsFlagTrustInputVelocity;

                if (stepIsAirborne)
                {
                    input.vz = dzToNext / dtSec + (0.5f * Gravity * dtSec);

                    if (!wasAirborne || transitionToAirborne)
                        input.fallTime = 1u;
                }

                var output = _phy.StepPhysicsV2(input, dtSec);
                AssertFinite(output, i);

                prevOutput = output;
                prevGroundZ = output.groundZ;

                float errorX = output.x - next.Position.X;
                float errorY = output.y - next.Position.Y;
                float errorZ = output.z - next.Position.Z;
                float horizontalError = MathF.Sqrt(errorX * errorX + errorY * errorY);
                float verticalError = MathF.Abs(errorZ);
                float positionError = MathF.Sqrt(errorX * errorX + errorY * errorY + errorZ * errorZ);

                replayFrames.Add(new ReplayFrameSnapshot(
                    Frame: i,
                    TimeSec: next.FrameTimestamp / 1000f,
                    DtSec: dtSec,
                    SimX: output.x,
                    SimY: output.y,
                    SimZ: output.z,
                    ExpectedX: next.Position.X,
                    ExpectedY: next.Position.Y,
                    ExpectedZ: next.Position.Z,
                    DeltaX: errorX,
                    DeltaY: errorY,
                    DeltaZ: errorZ,
                    PositionError: positionError,
                    HorizontalError: horizontalError,
                    VerticalError: verticalError,
                    Vx: output.vx,
                    Vy: output.vy,
                    Vz: output.vz,
                    SimGroundZ: output.groundZ,
                    SimGroundNz: output.groundNz,
                    FallTimeSec: output.fallTime,
                    InputFlags: inputFlags,
                    OutputFlags: output.moveFlags,
                    IsAirborne: stepIsAirborne));
            }

            if (replayFrames.Count == 0)
                return new ReplayRunResult(replayFrames, airborneFrameCount, skippedDtCount, skippedTeleportCount, 0f, 0f, 0f, 0f, -1);

            var sortedErrors = replayFrames.Select(f => f.PositionError).OrderBy(value => value).ToArray();
            float meanError = sortedErrors.Average();
            float p95Error = ComputePercentile(sortedErrors, 0.95f);
            float p99Error = ComputePercentile(sortedErrors, 0.99f);
            float maxError = sortedErrors[^1];
            int maxErrorFrame = replayFrames.OrderByDescending(f => f.PositionError).First().Frame;

            return new ReplayRunResult(
                replayFrames,
                airborneFrameCount,
                skippedDtCount,
                skippedTeleportCount,
                meanError,
                p95Error,
                p99Error,
                maxError,
                maxErrorFrame);
        }

        private static (float radius, float height) ResolveCapsule(uint raceId, uint genderId)
        {
            Race? matchedRace = null;
            foreach (var value in Enum.GetValues<Race>())
            {
                if ((uint)value == raceId)
                {
                    matchedRace = value;
                    break;
                }
            }

            if (!matchedRace.HasValue)
                return (0.306f, 2.0313f);

            var gender = Gender.Male;
            foreach (var value in Enum.GetValues<Gender>())
            {
                if ((uint)value == genderId)
                {
                    gender = value;
                    break;
                }
            }

            return RaceDimensions.GetCapsuleForRace(matchedRace.Value, gender);
        }

        private void WriteFrameTrace(string scenario, uint mapId, IReadOnlyList<PhysicsFrameSnapshot> frames)
        {
            _output.WriteLine($"=== {scenario}: map={mapId}, frames={frames.Count} ===");
            foreach (var frame in frames)
            {
                string groundText = frame.GroundZ.ToString("F3", CultureInfo.InvariantCulture);
                _output.WriteLine(
                    $"  f={frame.Frame,3} t={frame.TimeSec,6:F3}s " +
                    $"actual=({frame.X:F3},{frame.Y:F3},{frame.Z:F3}) " +
                    $"expected=({frame.ExpectedX:F3},{frame.ExpectedY:F3},{frame.ExpectedZ:F3}) " +
                    $"d=({frame.DeltaX.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture)}," +
                    $"{frame.DeltaY.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture)}," +
                    $"{frame.DeltaZ.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture)}) " +
                    $"vz={frame.Vz.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture),8} " +
                    $"ground={groundText,8} fall={frame.FallTimeSec,6:F3}s flags=0x{frame.Flags:X8}");
            }
        }

        private void WriteReplaySummary(string scenario, ReplayRunResult replay)
        {
            _output.WriteLine($"=== {scenario}: recording replay summary ===");
            _output.WriteLine(
                $"  simulated={replay.Frames.Count} airborne={replay.AirborneFrameCount} " +
                $"skippedDt={replay.SkippedNonPositiveDtCount} skippedTeleport={replay.SkippedTeleportCount}");
            _output.WriteLine(
                $"  error(y): avg={replay.MeanPositionError:F4} p95={replay.P95PositionError:F4} " +
                $"p99={replay.P99PositionError:F4} max={replay.MaxPositionError:F4} " +
                $"(frame={replay.MaxErrorFrame})");

            foreach (var frame in replay.Frames
                .OrderByDescending(f => f.PositionError)
                .Take(12))
            {
                _output.WriteLine(
                    $"  worst f={frame.Frame,3} err={frame.PositionError:F4} " +
                    $"d=({frame.DeltaX.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture)}," +
                    $"{frame.DeltaY.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture)}," +
                    $"{frame.DeltaZ.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture)}) " +
                    $"air={(frame.IsAirborne ? 1 : 0)} in=0x{frame.InputFlags:X8} out=0x{frame.OutputFlags:X8}");
            }
        }

        private static string DescribeWindow(IReadOnlyList<PhysicsFrameSnapshot> frames, int start, int count)
        {
            int end = Math.Min(frames.Count, start + count);
            var parts = new List<string>(Math.Max(0, end - start));
            for (int i = start; i < end; i++)
            {
                var frame = frames[i];
                parts.Add(
                    $"f{frame.Frame}:z={frame.Z:F3},dz={frame.DeltaZ.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture)},flags=0x{frame.Flags:X8}");
            }

            return string.Join(" | ", parts);
        }

        private void WriteReplayFrameTrace(string scenario, IReadOnlyList<ReplayFrameSnapshot> frames)
        {
            _output.WriteLine($"=== {scenario}: frame-by-frame ({frames.Count} simulated frames) ===");

            foreach (var frame in frames)
            {
                _output.WriteLine(
                    $"  f={frame.Frame,3} t={frame.TimeSec,6:F3}s dt={frame.DtSec,5:F3}s " +
                    $"sim=({frame.SimX:F3},{frame.SimY:F3},{frame.SimZ:F3}) " +
                    $"exp=({frame.ExpectedX:F3},{frame.ExpectedY:F3},{frame.ExpectedZ:F3}) " +
                    $"d=({frame.DeltaX.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture)}," +
                    $"{frame.DeltaY.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture)}," +
                    $"{frame.DeltaZ.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture)}) " +
                    $"err={frame.PositionError,7:F4} hErr={frame.HorizontalError,7:F4} vErr={frame.VerticalError,7:F4} " +
                    $"vz={frame.Vz.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture),8} " +
                    $"gZ={frame.SimGroundZ,8:F3} gNz={frame.SimGroundNz,6:F3} " +
                    $"fall={frame.FallTimeSec,6:F3}s air={(frame.IsAirborne ? 1 : 0)} " +
                    $"in=0x{frame.InputFlags:X8} out=0x{frame.OutputFlags:X8}");
            }
        }

        private static float ComputePercentile(IReadOnlyList<float> sortedValues, float percentile)
        {
            if (sortedValues.Count == 0)
                return 0f;

            if (percentile <= 0f)
                return sortedValues[0];

            if (percentile >= 1f)
                return sortedValues[^1];

            float rank = percentile * (sortedValues.Count - 1);
            int low = (int)MathF.Floor(rank);
            int high = (int)MathF.Ceiling(rank);

            if (low == high)
                return sortedValues[low];

            float t = rank - low;
            return sortedValues[low] + ((sortedValues[high] - sortedValues[low]) * t);
        }

        private static void AssertFinite(PhysicsOutput output, int frame)
        {
            Assert.True(float.IsFinite(output.x), $"Frame {frame}: output.x is not finite ({output.x})");
            Assert.True(float.IsFinite(output.y), $"Frame {frame}: output.y is not finite ({output.y})");
            Assert.True(float.IsFinite(output.z), $"Frame {frame}: output.z is not finite ({output.z})");
            Assert.True(float.IsFinite(output.vx), $"Frame {frame}: output.vx is not finite ({output.vx})");
            Assert.True(float.IsFinite(output.vy), $"Frame {frame}: output.vy is not finite ({output.vy})");
            Assert.True(float.IsFinite(output.vz), $"Frame {frame}: output.vz is not finite ({output.vz})");
            Assert.True(float.IsFinite(output.groundZ), $"Frame {frame}: output.groundZ is not finite ({output.groundZ})");
            Assert.True(float.IsFinite(output.fallTime), $"Frame {frame}: output.fallTime is not finite ({output.fallTime})");
        }

        private const MovementFlags IntentMoveMask =
            MovementFlags.MOVEFLAG_FORWARD |
            MovementFlags.MOVEFLAG_BACKWARD |
            MovementFlags.MOVEFLAG_STRAFE_LEFT |
            MovementFlags.MOVEFLAG_STRAFE_RIGHT |
            MovementFlags.MOVEFLAG_TURN_LEFT |
            MovementFlags.MOVEFLAG_TURN_RIGHT |
            MovementFlags.MOVEFLAG_PITCH_UP |
            MovementFlags.MOVEFLAG_PITCH_DOWN |
            MovementFlags.MOVEFLAG_WALK_MODE |
            MovementFlags.MOVEFLAG_JUMPING;

        private const MovementFlags RuntimeStateMask =
            MovementFlags.MOVEFLAG_FALLINGFAR |
            MovementFlags.MOVEFLAG_SWIMMING |
            MovementFlags.MOVEFLAG_FLYING |
            MovementFlags.MOVEFLAG_ONTRANSPORT |
            MovementFlags.MOVEFLAG_LEVITATING;

        private readonly record struct PhysicsFrameSnapshot(
            int Frame,
            float TimeSec,
            float X,
            float Y,
            float Z,
            float DeltaX,
            float DeltaY,
            float DeltaZ,
            float Vx,
            float Vy,
            float Vz,
            float GroundZ,
            float FallTimeSec,
            uint Flags,
            float ExpectedX,
            float ExpectedY,
            float ExpectedZ);

        private readonly record struct ReplayFrameSnapshot(
            int Frame,
            float TimeSec,
            float DtSec,
            float SimX,
            float SimY,
            float SimZ,
            float ExpectedX,
            float ExpectedY,
            float ExpectedZ,
            float DeltaX,
            float DeltaY,
            float DeltaZ,
            float PositionError,
            float HorizontalError,
            float VerticalError,
            float Vx,
            float Vy,
            float Vz,
            float SimGroundZ,
            float SimGroundNz,
            float FallTimeSec,
            uint InputFlags,
            uint OutputFlags,
            bool IsAirborne);

        private readonly record struct ReplayRunResult(
            IReadOnlyList<ReplayFrameSnapshot> Frames,
            int AirborneFrameCount,
            int SkippedNonPositiveDtCount,
            int SkippedTeleportCount,
            float MeanPositionError,
            float P95PositionError,
            float P99PositionError,
            float MaxPositionError,
            int MaxErrorFrame);

        private sealed class MovementRecordingLite
        {
            [JsonPropertyName("mapId")]
            public uint MapId { get; set; }

            [JsonPropertyName("race")]
            public uint Race { get; set; }

            [JsonPropertyName("gender")]
            public uint Gender { get; set; }

            [JsonPropertyName("frames")]
            public List<RecordedFrameLite> Frames { get; set; } = [];
        }

        private sealed class RecordedFrameLite
        {
            [JsonPropertyName("frameTimestamp")]
            public long FrameTimestamp { get; set; }

            [JsonPropertyName("movementFlags")]
            public uint MovementFlags { get; set; }

            [JsonPropertyName("position")]
            public RecordedPositionLite Position { get; set; } = new();

            [JsonPropertyName("facing")]
            public float Facing { get; set; }

            [JsonPropertyName("walkSpeed")]
            public float WalkSpeed { get; set; }

            [JsonPropertyName("runSpeed")]
            public float RunSpeed { get; set; }

            [JsonPropertyName("runBackSpeed")]
            public float RunBackSpeed { get; set; }

            [JsonPropertyName("swimSpeed")]
            public float SwimSpeed { get; set; }

            [JsonPropertyName("swimBackSpeed")]
            public float SwimBackSpeed { get; set; }

            [JsonPropertyName("turnRate")]
            public float TurnRate { get; set; }

            [JsonPropertyName("swimPitch")]
            public float SwimPitch { get; set; }

            [JsonPropertyName("transportGuid")]
            public ulong TransportGuid { get; set; }
        }

        private sealed class RecordedPositionLite
        {
            [JsonPropertyName("x")]
            public float X { get; set; }

            [JsonPropertyName("y")]
            public float Y { get; set; }

            [JsonPropertyName("z")]
            public float Z { get; set; }
        }
    }
}
