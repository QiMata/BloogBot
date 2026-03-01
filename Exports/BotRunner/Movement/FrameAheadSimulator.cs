using BotRunner.Clients;
using GameData.Core.Models;
using Pathfinding;
using System;

namespace BotRunner.Movement;

/// <summary>
/// Production multi-frame physics stepping for predictive simulation.
/// Wraps PathfindingClient.PhysicsStep() to chain multiple frames forward.
/// Used for jump landing prediction and transport ride estimation.
/// </summary>
public class FrameAheadSimulator(PathfindingClient physics)
{
    private readonly PathfindingClient _physics = physics;

    private const int MAX_SIMULATION_FRAMES = 300;   // 5s at 60fps
    private const float DEFAULT_DT = 1f / 60f;       // 16.67ms
    private const float JUMP_VELOCITY = 7.95577f;
    private const float GRAVITY = 19.2911f;
    private const float DEFAULT_RUN_SPEED = 7.5f;
    private const float DEFAULT_WALK_SPEED = 2.5f;
    private const float DEFAULT_RUN_BACK_SPEED = 4.5f;
    private const float DEFAULT_SWIM_SPEED = 4.7222f;
    private const uint DEFAULT_RACE = 2;    // Orc
    private const uint DEFAULT_GENDER = 0;  // Male
    private const uint MOVEFLAG_FORWARD = 0x00000001;
    private const uint MOVEFLAG_JUMPING = 0x00002000;
    private const uint MOVEFLAG_FALLINGFAR = 0x00004000;

    /// <summary>
    /// Simulate N physics frames forward from the given initial state.
    /// Each frame chains the output of the previous step as input to the next.
    /// </summary>
    public SimulationResult SimulateFrames(PhysicsInput initialState, int frameCount, float dt = DEFAULT_DT)
    {
        frameCount = Math.Clamp(frameCount, 1, MAX_SIMULATION_FRAMES);
        var input = initialState.Clone();
        input.DeltaTime = dt;

        PhysicsOutput lastOutput = new();
        int framesSimulated = 0;

        for (int i = 0; i < frameCount; i++)
        {
            input.FrameCounter = (uint)i;
            lastOutput = _physics.PhysicsStep(input);
            framesSimulated++;

            ChainOutputToInput(input, lastOutput, dt);
        }

        return new SimulationResult(
            new Position(lastOutput.NewPosX, lastOutput.NewPosY, lastOutput.NewPosZ),
            lastOutput.NewVelZ,
            lastOutput.IsGrounded,
            lastOutput.FallDistance,
            framesSimulated);
    }

    /// <summary>
    /// Predict where a jump from the given position and facing will land.
    /// Simulates the jump arc frame-by-frame until the bot lands or the
    /// simulation cap is reached. Returns landing position and damage estimate.
    /// Race/gender determine capsule dimensions (Orc=2, Male=0 by default).
    /// </summary>
    public JumpLandingResult PredictJumpLanding(
        Position pos, float facing, float runSpeed, uint mapId,
        uint race = DEFAULT_RACE, uint gender = DEFAULT_GENDER)
    {
        var input = BuildBaseInput(pos, facing, mapId, race, gender);
        input.RunSpeed = runSpeed > 0 ? runSpeed : DEFAULT_RUN_SPEED;
        input.MovementFlags = MOVEFLAG_FORWARD | MOVEFLAG_JUMPING;

        float startZ = pos.Z;
        bool wasAirborne = false;
        int framesSimulated = 0;
        PhysicsOutput lastOutput = new();

        for (int i = 0; i < MAX_SIMULATION_FRAMES; i++)
        {
            input.FrameCounter = (uint)i;
            input.DeltaTime = DEFAULT_DT;

            // On the first airborne frame, fallTime must be 0 so the engine
            // applies JUMP_VELOCITY (C++ checks input.fallTime == 0 for new jumps).
            if (i == 0)
                input.FallTime = 0;

            lastOutput = _physics.PhysicsStep(input);
            framesSimulated++;

            bool isAirborne = !lastOutput.IsGrounded;

            // Bot was airborne and just landed
            if (wasAirborne && lastOutput.IsGrounded)
            {
                var landingPos = new Position(lastOutput.NewPosX, lastOutput.NewPosY, lastOutput.NewPosZ);
                float fallDistance = startZ - lastOutput.NewPosZ;
                float damage = NavigationPath.EstimateFallDamage(
                    fallDistance > 0 ? fallDistance : 0, 1000f);

                return new JumpLandingResult(
                    landingPos, fallDistance, damage,
                    framesSimulated * DEFAULT_DT * 1000f, true);
            }

            wasAirborne = isAirborne;
            ChainOutputToInput(input, lastOutput, DEFAULT_DT);
        }

        // Didn't land within simulation cap — fell into void or very long fall
        return new JumpLandingResult(
            new Position(lastOutput.NewPosX, lastOutput.NewPosY, lastOutput.NewPosZ),
            startZ - lastOutput.NewPosZ,
            float.MaxValue,
            framesSimulated * DEFAULT_DT * 1000f,
            false);
    }

    /// <summary>
    /// Check if a jump from position A toward position B is feasible.
    /// Simulates the jump and checks if the landing is within reasonable
    /// distance of the target and doesn't result in lethal fall damage.
    /// </summary>
    public bool IsJumpFeasible(Position from, Position to, float runSpeed, uint mapId)
    {
        float facing = MathF.Atan2(to.Y - from.Y, to.X - from.X);
        var result = PredictJumpLanding(from, facing, runSpeed, mapId);

        if (!result.Landed)
            return false;

        // Landing must be within 5y of the target horizontally
        float dx = result.LandingPosition.X - to.X;
        float dy = result.LandingPosition.Y - to.Y;
        float horizontalDist = MathF.Sqrt(dx * dx + dy * dy);
        if (horizontalDist > 5f)
            return false;

        // Fall damage must be survivable (less than 50% of assumed max health)
        if (result.EstimatedDamage > 500f)
            return false;

        return true;
    }

    /// <summary>
    /// Chain the output of a physics step into the input for the next step.
    /// This mirrors the pattern used by ReplayEngine.cs for frame continuity.
    /// </summary>
    private static void ChainOutputToInput(PhysicsInput input, PhysicsOutput output, float dt)
    {
        // Position
        input.PosX = output.NewPosX;
        input.PosY = output.NewPosY;
        input.PosZ = output.NewPosZ;

        // Velocity: engine recomputes horizontal velocity from movement flags + orientation
        // when TRUST_INPUT_VELOCITY is not set. Only Vz carries vertical state (jump arc, gravity).
        input.VelX = 0;
        input.VelY = 0;
        input.VelZ = output.NewVelZ;

        // Orientation
        input.Facing = output.Orientation;
        input.SwimPitch = output.Pitch;

        // Movement state
        input.MovementFlags = output.MovementFlags;

        // Ground surface feedback
        input.PrevGroundZ = output.GroundZ;
        input.PrevGroundNx = output.GroundNx;
        input.PrevGroundNy = output.GroundNy;
        input.PrevGroundNz = output.GroundNz != 0 ? output.GroundNz : 1.0f;

        // Depenetration
        input.PendingDepenX = output.PendingDepenX;
        input.PendingDepenY = output.PendingDepenY;
        input.PendingDepenZ = output.PendingDepenZ;

        // Standing-on reference
        input.StandingOnInstanceId = output.StandingOnInstanceId;
        input.StandingOnLocalX = output.StandingOnLocalX;
        input.StandingOnLocalY = output.StandingOnLocalY;
        input.StandingOnLocalZ = output.StandingOnLocalZ;

        // Fall tracking
        input.FallTime = output.FallTime;
        input.FallStartZ = output.FallStartZ;
    }

    /// <summary>
    /// Build a baseline PhysicsInput with standard speed values.
    /// Race and gender are sent over IPC; the PathfindingService derives
    /// capsule height/radius from them via RaceDimensions.GetCapsuleForRace().
    /// </summary>
    private static PhysicsInput BuildBaseInput(
        Position pos, float facing, uint mapId,
        uint race = DEFAULT_RACE, uint gender = DEFAULT_GENDER)
    {
        return new PhysicsInput
        {
            PosX = pos.X,
            PosY = pos.Y,
            PosZ = pos.Z,
            Facing = facing,
            MapId = mapId,
            Race = race,
            Gender = gender,
            WalkSpeed = DEFAULT_WALK_SPEED,
            RunSpeed = DEFAULT_RUN_SPEED,
            RunBackSpeed = DEFAULT_RUN_BACK_SPEED,
            SwimSpeed = DEFAULT_SWIM_SPEED,
            SwimBackSpeed = DEFAULT_SWIM_SPEED * 0.5f,
            PrevGroundZ = pos.Z,
            PrevGroundNz = 1.0f, // flat ground normal
        };
    }
}

/// <summary>
/// Result of simulating N physics frames forward.
/// </summary>
public record SimulationResult(
    Position FinalPosition,
    float FinalVelocityZ,
    bool Grounded,
    float FallDistance,
    int FramesSimulated);

/// <summary>
/// Result of predicting where a jump will land.
/// </summary>
public record JumpLandingResult(
    Position LandingPosition,
    float FallDistance,
    float EstimatedDamage,
    float FlightTimeMs,
    bool Landed);
