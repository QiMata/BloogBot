using BotRunner.Clients;
using GameData.Core.Enums;
using Pathfinding;

namespace BotRunner.Tests.Combat;

/// <summary>
/// Tests for PathfindingClient dead-reckoning fallback.
/// When the PathfindingService is unavailable, PhysicsStep falls back to
/// simple forward/backward movement + gravity.
/// The parameterless constructor leaves the underlying TCP connection null,
/// which forces SendMessage to throw, triggering the dead-reckoning path.
/// </summary>
public class PathfindingClientDeadReckoningTests
{
    private readonly PathfindingClient _client;

    public PathfindingClientDeadReckoningTests()
    {
        // Parameterless ctor → no TCP connection → SendMessage throws → dead reckoning
        _client = new PathfindingClient();
    }

    private static PhysicsInput MakeInput(
        float x = 0, float y = 0, float z = 100,
        float facing = 0, float runSpeed = 7f, float runBackSpeed = 4.5f,
        float dt = 0.016f, uint moveFlags = 0,
        float velX = 0, float velY = 0, float velZ = 0,
        float fallTime = 0, float swimPitch = 0)
    {
        return new PhysicsInput
        {
            PosX = x,
            PosY = y,
            PosZ = z,
            Facing = facing,
            RunSpeed = runSpeed,
            RunBackSpeed = runBackSpeed,
            DeltaTime = dt,
            MovementFlags = moveFlags,
            VelX = velX,
            VelY = velY,
            VelZ = velZ,
            FallTime = fallTime,
            SwimPitch = swimPitch,
        };
    }

    [Fact]
    public void Stationary_PositionUnchanged()
    {
        var input = MakeInput(x: 100, y: 200, z: 300, moveFlags: 0);
        var output = _client.PhysicsStep(input);

        Assert.Equal(100f, output.NewPosX);
        Assert.Equal(200f, output.NewPosY);
        Assert.Equal(300f, output.NewPosZ); // Z unchanged in dead reckoning
    }

    [Fact]
    public void ForwardMovement_East()
    {
        // Facing = 0 → cos(0)=1, sin(0)=0 → moves in +X direction
        var input = MakeInput(
            x: 0, y: 0, z: 100,
            facing: 0f, runSpeed: 7f, dt: 1f,
            moveFlags: (uint)MovementFlags.MOVEFLAG_FORWARD);

        var output = _client.PhysicsStep(input);

        // dx = cos(0) * 7 * 1 = 7
        Assert.Equal(7f, output.NewPosX, 0.01f);
        Assert.Equal(0f, output.NewPosY, 0.01f);
    }

    [Fact]
    public void ForwardMovement_North()
    {
        // Facing = π/2 → cos(π/2)≈0, sin(π/2)=1 → moves in +Y direction
        float facing = MathF.PI / 2f;
        var input = MakeInput(
            x: 0, y: 0, z: 100,
            facing: facing, runSpeed: 7f, dt: 1f,
            moveFlags: (uint)MovementFlags.MOVEFLAG_FORWARD);

        var output = _client.PhysicsStep(input);

        Assert.Equal(0f, output.NewPosX, 0.01f);
        Assert.Equal(7f, output.NewPosY, 0.01f);
    }

    [Fact]
    public void BackwardMovement_SubtractsPosition()
    {
        // Facing = 0, backward → dx = -cos(0) * 4.5 * 1 = -4.5
        var input = MakeInput(
            x: 10, y: 0, z: 100,
            facing: 0f, runBackSpeed: 4.5f, dt: 1f,
            moveFlags: (uint)MovementFlags.MOVEFLAG_BACKWARD);

        var output = _client.PhysicsStep(input);

        Assert.Equal(5.5f, output.NewPosX, 0.01f);
        Assert.Equal(0f, output.NewPosY, 0.01f);
    }

    [Fact]
    public void Gravity_AppliedToVelZ()
    {
        var input = MakeInput(velZ: 0f, dt: 1f);
        var output = _client.PhysicsStep(input);

        // NewVelZ = VelZ - 19.2911 * dt = 0 - 19.2911 = -19.2911
        Assert.Equal(-19.2911f, output.NewVelZ, 0.01f);
    }

    [Fact]
    public void Gravity_AccumulatesOverTime()
    {
        var input = MakeInput(velZ: -10f, dt: 0.5f);
        var output = _client.PhysicsStep(input);

        // NewVelZ = -10 - 19.2911 * 0.5 = -10 - 9.64555 = -19.64555
        Assert.Equal(-19.64555f, output.NewVelZ, 0.01f);
    }

    [Fact]
    public void FallTime_Increments()
    {
        var input = MakeInput(fallTime: 1000f, dt: 0.016f);
        var output = _client.PhysicsStep(input);

        // FallTime += dt * 1000 = 1000 + 16 = 1016
        Assert.Equal(1016f, output.FallTime, 0.1f);
    }

    [Fact]
    public void Orientation_Preserved()
    {
        var input = MakeInput(facing: 2.5f);
        var output = _client.PhysicsStep(input);

        Assert.Equal(2.5f, output.Orientation);
    }

    [Fact]
    public void SwimPitch_Preserved()
    {
        var input = MakeInput(swimPitch: 0.75f);
        var output = _client.PhysicsStep(input);

        Assert.Equal(0.75f, output.Pitch);
    }

    [Fact]
    public void MovementFlags_Preserved()
    {
        uint flags = (uint)(MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_JUMPING);
        var input = MakeInput(moveFlags: flags);
        var output = _client.PhysicsStep(input);

        Assert.Equal(flags, output.MovementFlags);
    }

    [Fact]
    public void VelXY_Preserved()
    {
        var input = MakeInput(velX: 3.5f, velY: -2.0f);
        var output = _client.PhysicsStep(input);

        Assert.Equal(3.5f, output.NewVelX);
        Assert.Equal(-2.0f, output.NewVelY);
    }

    [Fact]
    public void ZPosition_NotModified()
    {
        // Dead reckoning doesn't change Z (no terrain awareness)
        var input = MakeInput(z: 42.5f,
            moveFlags: (uint)MovementFlags.MOVEFLAG_FORWARD,
            facing: 0f, runSpeed: 7f, dt: 1f);
        var output = _client.PhysicsStep(input);

        Assert.Equal(42.5f, output.NewPosZ);
    }

    [Fact]
    public void SmallDeltaTime_SmallMovement()
    {
        // Typical frame: 16ms = 0.016s
        var input = MakeInput(
            x: 0, y: 0, z: 100,
            facing: 0f, runSpeed: 7f, dt: 0.016f,
            moveFlags: (uint)MovementFlags.MOVEFLAG_FORWARD);

        var output = _client.PhysicsStep(input);

        // dx = cos(0) * 7 * 0.016 = 0.112
        Assert.Equal(0.112f, output.NewPosX, 0.001f);
    }

    [Fact]
    public void IsAvailable_FalseAfterFailure()
    {
        // Before any calls, client has 0 failures
        // But parameterless ctor means SendMessage will fail
        Assert.True(_client.IsAvailable); // initially 0 failures

        _client.PhysicsStep(MakeInput());

        Assert.False(_client.IsAvailable); // 1 failure now
    }

    [Fact]
    public void DiagonalMovement_ForwardAtAngle()
    {
        // Facing = π/4 (45°) → cos=sin=√2/2 ≈ 0.7071
        float facing = MathF.PI / 4f;
        var input = MakeInput(
            x: 0, y: 0, z: 100,
            facing: facing, runSpeed: 10f, dt: 1f,
            moveFlags: (uint)MovementFlags.MOVEFLAG_FORWARD);

        var output = _client.PhysicsStep(input);

        float expected = MathF.Cos(facing) * 10f;
        Assert.Equal(expected, output.NewPosX, 0.01f);
        Assert.Equal(expected, output.NewPosY, 0.01f);
    }
}
