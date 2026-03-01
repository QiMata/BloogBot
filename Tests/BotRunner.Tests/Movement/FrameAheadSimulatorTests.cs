using BotRunner.Clients;
using BotRunner.Movement;
using GameData.Core.Models;
using Pathfinding;
using System;

namespace BotRunner.Tests.Movement;

public class FrameAheadSimulatorTests
{
    private const float DT = 1f / 60f;
    private const float GRAVITY = 19.2911f;
    private const float JUMP_VELOCITY = 7.95577f;
    private const uint MOVEFLAG_FORWARD = 0x00000001;
    private const uint MOVEFLAG_JUMPING = 0x00002000;
    private const uint MOVEFLAG_FALLINGFAR = 0x00004000;

    // ===================== SimulateFrames =====================

    [Fact]
    public void SimulateFrames_StandingStill_PositionUnchanged()
    {
        var client = new FlatGroundPhysicsClient();
        var sim = new FrameAheadSimulator(client);

        var input = MakeInput(10, 20, 100, moveFlags: 0);
        var result = sim.SimulateFrames(input, frameCount: 60);

        Assert.Equal(60, result.FramesSimulated);
        Assert.True(result.Grounded);
        Assert.InRange(result.FinalPosition.X, 9.9f, 10.1f);
        Assert.InRange(result.FinalPosition.Y, 19.9f, 20.1f);
        Assert.InRange(result.FinalPosition.Z, 99.9f, 100.1f);
    }

    [Fact]
    public void SimulateFrames_ForwardWalk_AdvancesPosition()
    {
        float runSpeed = 7.0f;
        var client = new FlatGroundPhysicsClient(runSpeed: runSpeed);
        var sim = new FrameAheadSimulator(client);

        // Facing east (0 radians)
        var input = MakeInput(0, 0, 100, facing: 0f, moveFlags: MOVEFLAG_FORWARD);
        input.RunSpeed = runSpeed;

        int frames = 60; // 1 second
        var result = sim.SimulateFrames(input, frameCount: frames);

        // Should have moved ~7y east in 1 second
        float expectedX = runSpeed * frames * DT;
        Assert.Equal(frames, result.FramesSimulated);
        Assert.InRange(result.FinalPosition.X, expectedX - 1f, expectedX + 1f);
        Assert.InRange(result.FinalPosition.Y, -0.5f, 0.5f); // no lateral movement
    }

    [Fact]
    public void SimulateFrames_ClampsFrameCount_ToMaximum()
    {
        var client = new FlatGroundPhysicsClient();
        var sim = new FrameAheadSimulator(client);

        var input = MakeInput(0, 0, 100, moveFlags: 0);
        // Request way more than 300
        var result = sim.SimulateFrames(input, frameCount: 9999);

        Assert.Equal(300, result.FramesSimulated);
    }

    [Fact]
    public void SimulateFrames_OutputToInputChaining_GroundZFedBack()
    {
        int callCount = 0;
        float lastPrevGroundZ = 0;

        var client = new DelegatePhysicsClient(input =>
        {
            callCount++;
            lastPrevGroundZ = input.PrevGroundZ;

            return new PhysicsOutput
            {
                NewPosX = input.PosX,
                NewPosY = input.PosY,
                NewPosZ = input.PosZ,
                IsGrounded = true,
                GroundZ = input.PosZ + callCount * 0.1f, // changing ground Z each frame
                GroundNz = 1.0f,
            };
        });

        var sim = new FrameAheadSimulator(client);
        var input = MakeInput(0, 0, 50);
        sim.SimulateFrames(input, frameCount: 3);

        Assert.Equal(3, callCount);
        // Third call should receive the second call's output ground Z
        float expectedPrevGroundZ = 50 + 2 * 0.1f;
        Assert.InRange(lastPrevGroundZ, expectedPrevGroundZ - 0.01f, expectedPrevGroundZ + 0.01f);
    }

    [Fact]
    public void SimulateFrames_FallTrackingChained_FallTimePropagated()
    {
        int callCount = 0;
        float lastFallTime = 0;

        var client = new DelegatePhysicsClient(input =>
        {
            callCount++;
            lastFallTime = input.FallTime;

            return new PhysicsOutput
            {
                NewPosX = input.PosX,
                NewPosY = input.PosY,
                NewPosZ = input.PosZ - 1f,
                IsGrounded = false,
                FallTime = input.FallTime + DT * 1000f,
                FallStartZ = callCount == 1 ? input.PosZ : input.FallStartZ,
                GroundNz = 1.0f,
            };
        });

        var sim = new FrameAheadSimulator(client);
        var input = MakeInput(0, 0, 100, moveFlags: MOVEFLAG_FALLINGFAR);
        sim.SimulateFrames(input, frameCount: 5);

        Assert.Equal(5, callCount);
        // Fall time should have accumulated across frames
        Assert.True(lastFallTime > DT * 1000f * 3);
    }

    // ===================== PredictJumpLanding =====================

    [Fact]
    public void PredictJumpLanding_FlatGround_ReturnsToStartZ()
    {
        var client = new JumpPhysicsClient(groundZ: 100f);
        var sim = new FrameAheadSimulator(client);

        var result = sim.PredictJumpLanding(
            new Position(0, 0, 100), facing: 0f, runSpeed: 7.0f, mapId: 0);

        Assert.True(result.Landed);
        // Should land near start Z
        Assert.InRange(result.LandingPosition.Z, 99.5f, 100.5f);
        // Should have moved forward
        Assert.True(result.LandingPosition.X > 1f);
        // Flight time should be reasonable (jump takes ~0.8s)
        Assert.InRange(result.FlightTimeMs, 200f, 2000f);
        // No fall damage from a jump on flat ground
        Assert.Equal(0f, result.EstimatedDamage);
    }

    [Fact]
    public void PredictJumpLanding_JumpOffCliff_DetectsLanding()
    {
        // Ground at Z=50 for the landing zone (50y drop from Z=100)
        var client = new JumpPhysicsClient(groundZ: 50f, cliffEdgeX: 5f);
        var sim = new FrameAheadSimulator(client);

        var result = sim.PredictJumpLanding(
            new Position(0, 0, 100), facing: 0f, runSpeed: 7.0f, mapId: 0);

        Assert.True(result.Landed);
        Assert.InRange(result.LandingPosition.Z, 49.5f, 50.5f);
        Assert.True(result.FallDistance > 40f); // fell ~50y
        Assert.True(result.EstimatedDamage > 0); // should take fall damage
    }

    [Fact]
    public void PredictJumpLanding_VoidFall_ReturnsFalse()
    {
        // No ground ever — simulate falling forever
        var client = new DelegatePhysicsClient(input => new PhysicsOutput
        {
            NewPosX = input.PosX + MathF.Cos(input.Facing) * input.RunSpeed * DT,
            NewPosY = input.PosY + MathF.Sin(input.Facing) * input.RunSpeed * DT,
            NewPosZ = input.PosZ - GRAVITY * DT * DT * 0.5f,
            IsGrounded = false,
            FallTime = input.FallTime + DT * 1000f,
            FallStartZ = input.FallStartZ > 0 ? input.FallStartZ : input.PosZ,
            GroundNz = 1.0f,
        });

        var sim = new FrameAheadSimulator(client);
        var result = sim.PredictJumpLanding(
            new Position(0, 0, 100), facing: 0f, runSpeed: 7.0f, mapId: 0);

        Assert.False(result.Landed);
        Assert.Equal(float.MaxValue, result.EstimatedDamage);
    }

    // ===================== IsJumpFeasible =====================

    [Fact]
    public void IsJumpFeasible_ShortGap_ReturnsTrue()
    {
        var client = new JumpPhysicsClient(groundZ: 100f);
        var sim = new FrameAheadSimulator(client);

        // 3y gap on flat ground — easily jumpable
        bool feasible = sim.IsJumpFeasible(
            new Position(0, 0, 100),
            new Position(3, 0, 100),
            runSpeed: 7.0f, mapId: 0);

        Assert.True(feasible);
    }

    [Fact]
    public void IsJumpFeasible_HugeGap_ReturnsFalse()
    {
        // Landing nowhere near the target
        var client = new JumpPhysicsClient(groundZ: 100f);
        var sim = new FrameAheadSimulator(client);

        // 50y gap — way beyond jump distance
        bool feasible = sim.IsJumpFeasible(
            new Position(0, 0, 100),
            new Position(50, 0, 100),
            runSpeed: 7.0f, mapId: 0);

        Assert.False(feasible);
    }

    // ===================== Helper: Build PhysicsInput =====================

    private static PhysicsInput MakeInput(
        float x, float y, float z,
        float facing = 0f,
        uint moveFlags = 0,
        float runSpeed = 7.0f)
    {
        return new PhysicsInput
        {
            PosX = x,
            PosY = y,
            PosZ = z,
            Facing = facing,
            MovementFlags = moveFlags,
            RunSpeed = runSpeed,
            WalkSpeed = 2.5f,
            RunBackSpeed = 4.5f,
            SwimSpeed = 4.7222f,
            PrevGroundZ = z,
            PrevGroundNz = 1.0f,
            MapId = 0,
            Race = 2,  // Orc
            Gender = 0, // Male
            DeltaTime = DT,
        };
    }

    // ===================== Mock Physics Clients =====================

    /// <summary>
    /// Simple flat-ground physics mock. Forward movement advances X by
    /// runSpeed * cos(facing) * dt. Always grounded.
    /// </summary>
    private sealed class FlatGroundPhysicsClient(float runSpeed = 7.0f) : PathfindingClient
    {
        public override PhysicsOutput PhysicsStep(PhysicsInput input)
        {
            float dx = 0, dy = 0;
            if ((input.MovementFlags & MOVEFLAG_FORWARD) != 0)
            {
                dx = MathF.Cos(input.Facing) * runSpeed * input.DeltaTime;
                dy = MathF.Sin(input.Facing) * runSpeed * input.DeltaTime;
            }

            return new PhysicsOutput
            {
                NewPosX = input.PosX + dx,
                NewPosY = input.PosY + dy,
                NewPosZ = input.PosZ,
                NewVelX = dx / MathF.Max(input.DeltaTime, 0.001f),
                NewVelY = dy / MathF.Max(input.DeltaTime, 0.001f),
                NewVelZ = 0,
                IsGrounded = true,
                GroundZ = input.PosZ,
                GroundNz = 1.0f,
                Orientation = input.Facing,
                MovementFlags = input.MovementFlags,
            };
        }
    }

    /// <summary>
    /// Jump-arc physics mock. Simulates JUMP_VELOCITY impulse + gravity.
    /// Lands when Z reaches groundZ.
    /// </summary>
    private sealed class JumpPhysicsClient(float groundZ, float cliffEdgeX = float.MaxValue) : PathfindingClient
    {
        private float _vz = JUMP_VELOCITY;
        private bool _hasJumped;

        public override PhysicsOutput PhysicsStep(PhysicsInput input)
        {
            float dt = input.DeltaTime;
            float dx = 0, dy = 0;

            if ((input.MovementFlags & MOVEFLAG_FORWARD) != 0)
            {
                dx = MathF.Cos(input.Facing) * input.RunSpeed * dt;
                dy = MathF.Sin(input.Facing) * input.RunSpeed * dt;
            }

            float newX = input.PosX + dx;
            float newY = input.PosY + dy;
            float newZ = input.PosZ;
            bool grounded = false;

            bool isJumping = (input.MovementFlags & MOVEFLAG_JUMPING) != 0;
            bool isFalling = (input.MovementFlags & MOVEFLAG_FALLINGFAR) != 0;

            if (isJumping && !_hasJumped)
            {
                _vz = JUMP_VELOCITY;
                _hasJumped = true;
            }

            if (_hasJumped || isFalling)
            {
                _vz -= GRAVITY * dt;
                newZ = input.PosZ + _vz * dt;

                // Determine ground level at this position
                float currentGroundZ = newX > cliffEdgeX ? groundZ : input.PrevGroundZ;

                if (newZ <= currentGroundZ)
                {
                    newZ = currentGroundZ;
                    _vz = 0;
                    grounded = true;
                    _hasJumped = false;
                }
            }
            else
            {
                grounded = true;
            }

            uint outFlags = input.MovementFlags;
            if (grounded)
                outFlags &= ~(MOVEFLAG_JUMPING | MOVEFLAG_FALLINGFAR);
            else if (_hasJumped && _vz < 0)
                outFlags = (outFlags & ~MOVEFLAG_JUMPING) | MOVEFLAG_FALLINGFAR;

            return new PhysicsOutput
            {
                NewPosX = newX,
                NewPosY = newY,
                NewPosZ = newZ,
                NewVelX = dx / MathF.Max(dt, 0.001f),
                NewVelY = dy / MathF.Max(dt, 0.001f),
                NewVelZ = _vz,
                IsGrounded = grounded,
                GroundZ = grounded ? newZ : input.PrevGroundZ,
                GroundNz = 1.0f,
                Orientation = input.Facing,
                MovementFlags = outFlags,
                FallTime = grounded ? 0 : input.FallTime + dt * 1000f,
                FallStartZ = _hasJumped && input.FallStartZ == 0 ? input.PosZ : input.FallStartZ,
                FallDistance = grounded && input.FallStartZ > 0 ? input.FallStartZ - newZ : 0,
            };
        }
    }

    /// <summary>
    /// Delegate-based physics client for custom per-frame behavior.
    /// </summary>
    private sealed class DelegatePhysicsClient(Func<PhysicsInput, PhysicsOutput> stepFunc) : PathfindingClient
    {
        public override PhysicsOutput PhysicsStep(PhysicsInput input) => stepFunc(input);
    }
}
