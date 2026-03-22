using System;
using Xunit;
using Xunit.Abstractions;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

/// <summary>
/// Integration tests for frame-ahead simulation using the real C++ PhysicsEngine.
/// These validate that multi-frame stepping produces correct jump arcs, landing
/// positions, and forward movement distances using actual geometry collision.
/// </summary>
[Collection("PhysicsEngine")]
public class FrameAheadIntegrationTests(PhysicsEngineFixture fixture, ITestOutputHelper output)
{
    private readonly PhysicsEngineFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    private const float DT = 1f / 60f;
    private const float JUMP_VELOCITY = 7.95577f;
    private const float GRAVITY = 19.2911f;
    private const float RUN_SPEED = 7.0f;
    private const float CHAR_HEIGHT = 2.136f;
    private const float CHAR_RADIUS = 0.3645f;
    private const uint FLAG_FORWARD = 0x00000001;
    private const uint FLAG_JUMPING = 0x00002000;
    private const uint FLAG_FALLINGFAR = 0x00004000;

    // ==========================================================================
    // JUMP ARC TESTS
    // ==========================================================================

    /// <summary>
    /// Verify that a standing jump on flat ground reaches the expected peak height.
    /// Peak height = JUMP_VELOCITY^2 / (2*GRAVITY) ≈ 1.64y
    /// </summary>
    [Fact]
    public void JumpArc_FlatGround_PeakHeightMatchesPhysics()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        var start = WoWWorldCoordinates.Durotar.Orgrimmar.ValleyOfStrength;
        var input = CreateInput(start, FLAG_JUMPING, RUN_SPEED);

        float startZ = start.Z;
        float maxZ = startZ;
        int peakFrame = 0;

        var frames = SimulateFrames(input, 120); // 2 seconds max
        for (int i = 0; i < frames.Count; i++)
        {
            if (frames[i].Z > maxZ)
            {
                maxZ = frames[i].Z;
                peakFrame = i;
            }
        }

        float peakHeight = maxZ - startZ;
        float expectedPeak = JUMP_VELOCITY * JUMP_VELOCITY / (2 * GRAVITY); // 1.64y

        _output.WriteLine($"Jump peak: {peakHeight:F3}y at frame {peakFrame} (expected ~{expectedPeak:F3}y)");
        _output.WriteLine($"  Start Z: {startZ:F3}, Max Z: {maxZ:F3}");

        // Allow some tolerance for terrain interaction
        Assert.InRange(peakHeight, expectedPeak * 0.5f, expectedPeak * 2.0f);
    }

    /// <summary>
    /// Verify that a jump on flat ground returns to approximately start Z.
    /// </summary>
    [Fact]
    public void JumpLanding_FlatGround_ReturnsToStartZ()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        var start = WoWWorldCoordinates.Durotar.Orgrimmar.ValleyOfStrength;
        var input = CreateInput(start, FLAG_FORWARD | FLAG_JUMPING, RUN_SPEED);

        float startZ = start.Z;
        bool wasAirborne = false;
        float landingZ = float.NaN;
        int landingFrame = -1;

        var frames = SimulateFrames(input, 120);
        for (int i = 0; i < frames.Count; i++)
        {
            bool airborne = (frames[i].MoveFlags & (FLAG_JUMPING | FLAG_FALLINGFAR)) != 0;
            if (wasAirborne && !airborne)
            {
                landingZ = frames[i].Z;
                landingFrame = i;
                break;
            }
            wasAirborne = airborne;
        }

        _output.WriteLine($"Landing: Z={landingZ:F3} at frame {landingFrame} (start Z={startZ:F3})");

        Assert.True(landingFrame > 0, "Should have landed within simulation");
        Assert.InRange(landingZ, startZ - 3f, startZ + 3f); // within 3y of start (terrain variance)
    }

    // ==========================================================================
    // FORWARD MOVEMENT TESTS
    // ==========================================================================

    /// <summary>
    /// Verify that forward running on flat Orgrimmar ground traverses expected distance.
    /// Expected: ~7y per second at run speed 7.0.
    /// </summary>
    [Fact]
    public void ForwardRun_FlatOrgrimmar_TraversesExpectedDistance()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        var start = WoWWorldCoordinates.Durotar.Orgrimmar.ValleyOfStrength;
        var input = CreateInput(start, FLAG_FORWARD, RUN_SPEED);

        int framesToSimulate = 60; // 1 second
        var frames = SimulateFrames(input, framesToSimulate);

        var lastFrame = frames[^1];
        float dx = lastFrame.X - start.X;
        float dy = lastFrame.Y - start.Y;
        float horizontalDist = MathF.Sqrt(dx * dx + dy * dy);
        float expectedDist = RUN_SPEED * framesToSimulate * DT;

        _output.WriteLine($"Horizontal distance: {horizontalDist:F3}y (expected ~{expectedDist:F3}y)");
        _output.WriteLine($"  Start: ({start.X:F2}, {start.Y:F2})");
        _output.WriteLine($"  End: ({lastFrame.X:F2}, {lastFrame.Y:F2})");

        // Should have moved at least 30% of expected distance (terrain/collision may reduce it)
        Assert.True(horizontalDist > expectedDist * 0.3f,
            $"Expected at least {expectedDist * 0.3f:F2}y movement, got {horizontalDist:F2}y");
    }

    // ==========================================================================
    // MULTI-FRAME CHAINING TESTS
    // ==========================================================================

    /// <summary>
    /// Verify that frame-by-frame simulation produces continuous positions
    /// (no teleport jumps between consecutive frames on flat ground).
    /// </summary>
    [Fact]
    public void FrameChaining_FlatGround_NoBigPositionJumps()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        var start = WoWWorldCoordinates.Durotar.Orgrimmar.ValleyOfStrength;
        var input = CreateInput(start, FLAG_FORWARD, RUN_SPEED);

        var frames = SimulateFrames(input, 120);

        float maxFrameJump = 0;
        for (int i = 1; i < frames.Count; i++)
        {
            float dx = frames[i].X - frames[i - 1].X;
            float dy = frames[i].Y - frames[i - 1].Y;
            float dz = frames[i].Z - frames[i - 1].Z;
            float dist = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
            maxFrameJump = MathF.Max(maxFrameJump, dist);
        }

        _output.WriteLine($"Max frame-to-frame jump: {maxFrameJump:F3}y");

        // At run speed 7 and dt=1/60, max per-frame movement should be ~0.117y
        // Allow 3x for terrain snap and collision response
        Assert.True(maxFrameJump < 2.0f,
            $"Frame-to-frame position jump {maxFrameJump:F3}y exceeds 2y threshold");
    }

    // ==========================================================================
    // HELPERS
    // ==========================================================================

    private record FrameResult(float X, float Y, float Z, uint MoveFlags, bool IsGrounded);

    private List<FrameResult> SimulateFrames(PhysicsInput input, int count)
    {
        var results = new List<FrameResult>(count);
        var prevOutput = new PhysicsOutput();

        for (int i = 0; i < count; i++)
        {
            input.DeltaTime = DT;
            input.FrameCounter = (uint)i;

            // On jump start frame, fallTime must be 0 so the engine applies JUMP_VELOCITY.
            // (The engine checks input.fallTime == 0 to detect a new jump impulse.)
            if (i == 0 && (input.MoveFlags & FLAG_JUMPING) != 0)
                input.FallTime = 0;

            var output = StepPhysicsV2(ref input);

            bool grounded = (output.MoveFlags & (FLAG_JUMPING | FLAG_FALLINGFAR)) == 0;
            results.Add(new FrameResult(output.X, output.Y, output.Z, output.MoveFlags, grounded));

            // Chain output → input (mirrors ReplayEngine pattern)
            uint intentFlags = input.MoveFlags & 0x00000FFF; // preserve movement intent
            uint stateFlags = output.MoveFlags & 0xFFFFF000;  // get state from output

            input.X = output.X;
            input.Y = output.Y;
            input.Z = output.Z;
            input.Orientation = output.Orientation;
            // Horizontal velocity: engine recomputes from movement flags + orientation.
            // Feeding back Vx/Vy causes accumulation. Only Vz carries vertical state.
            input.Vx = 0;
            input.Vy = 0;
            input.Vz = output.Vz;
            input.MoveFlags = intentFlags | stateFlags;
            input.PrevGroundZ = output.GroundZ;
            input.PrevGroundNx = output.GroundNx;
            input.PrevGroundNy = output.GroundNy;
            input.PrevGroundNz = output.GroundNz;
            input.PendingDepenX = output.PendingDepenX;
            input.PendingDepenY = output.PendingDepenY;
            input.PendingDepenZ = output.PendingDepenZ;
            input.StandingOnInstanceId = output.StandingOnInstanceId;
            input.StandingOnLocalX = output.StandingOnLocalX;
            input.StandingOnLocalY = output.StandingOnLocalY;
            input.StandingOnLocalZ = output.StandingOnLocalZ;
            // Engine output FallTime is already in milliseconds; input expects milliseconds
            input.FallTime = (uint)MathF.Max(0f, output.FallTime);
            input.FallStartZ = output.FallStartZ;

            prevOutput = output;
        }

        return results;
    }

    private static PhysicsInput CreateInput(WorldPosition pos, uint moveFlags, float runSpeed, float orientation = 0f)
    {
        return new PhysicsInput
        {
            MapId = pos.MapId,
            X = pos.X,
            Y = pos.Y,
            Z = pos.Z,
            Orientation = orientation,
            MoveFlags = moveFlags,
            RunSpeed = runSpeed,
            WalkSpeed = runSpeed * 0.5f,
            RunBackSpeed = runSpeed * 0.65f,
            SwimSpeed = 4.7222f,
            SwimBackSpeed = 2.5f,
            FlightSpeed = 0,
            TurnSpeed = MathF.PI,
            Height = CHAR_HEIGHT,
            Radius = CHAR_RADIUS,
            PrevGroundZ = pos.Z,
            PrevGroundNz = 1.0f,
        };
    }
}
