using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests.Helpers;

/// <summary>
/// Replays recorded movement data through the C++ PhysicsEngine (StepV2)
/// and tracks deviation between simulated and recorded positions.
///
/// The engine handles JUMPING flags correctly (applies impulse only on
/// first frame via fallTime==0 check) and outputs correct end-of-frame
/// velocity for airborne frames. The replay loop:
/// - Passes recorded moveFlags directly (no flag stripping needed)
/// - Carries engine output velocity for airborne continuation
/// - Engine handles jump impulse, gravity integration, and ground detection
/// </summary>
public static class ReplayEngine
{
    /// <summary>
    /// Replays a full recording through PhysicsStepV2 and returns calibration results.
    /// </summary>
    public static CalibrationResult Replay(MovementRecording recording)
    {
        var result = new CalibrationResult();
        var frames = recording.Frames;

        if (frames.Count < 2) return result;

        var (capsuleRadius, capsuleHeight) = RecordingTestHelpers.GetCapsuleDimensions(recording);

        var prevOutput = new PhysicsOutput();
        float prevGroundZ = frames[0].Position.Z;
        int fallStartFrameIndex = -1;

        for (int i = 0; i < frames.Count - 1; i++)
        {
            var currentFrame = frames[i];
            var nextFrame = frames[i + 1];
            float dt = (nextFrame.FrameTimestamp - currentFrame.FrameTimestamp) / 1000.0f;

            if (dt <= 0) continue;

            // Skip teleport frames: a >50y position jump in a single frame is a
            // server teleport, hearth, or loading screen — not a physics event.
            float tdx = nextFrame.Position.X - currentFrame.Position.X;
            float tdy = nextFrame.Position.Y - currentFrame.Position.Y;
            float tdz = nextFrame.Position.Z - currentFrame.Position.Z;
            if (tdx * tdx + tdy * tdy + tdz * tdz > 50f * 50f)
            {
                fallStartFrameIndex = -1;
                prevOutput = new PhysicsOutput();
                prevGroundZ = nextFrame.Position.Z;
                result.AddSkippedTeleport(i);
                continue;
            }

            // Compute fall duration from frame timestamps.
            bool isFalling = (currentFrame.MovementFlags & 0x6000) != 0;
            bool wasAirborne = fallStartFrameIndex >= 0;
            uint fallTimeMs = 0;
            if (isFalling)
            {
                if (fallStartFrameIndex < 0)
                    fallStartFrameIndex = i;
                fallTimeMs = (uint)(currentFrame.FrameTimestamp - frames[fallStartFrameIndex].FrameTimestamp);
            }
            else
            {
                fallStartFrameIndex = -1;
            }

            var input = new PhysicsInput
            {
                MoveFlags = currentFrame.MovementFlags,
                X = currentFrame.Position.X,
                Y = currentFrame.Position.Y,
                Z = currentFrame.Position.Z,
                Orientation = currentFrame.Facing,
                Pitch = currentFrame.SwimPitch,
                Vx = 0, Vy = 0, Vz = 0,
                WalkSpeed = currentFrame.WalkSpeed,
                RunSpeed = currentFrame.RunSpeed,
                RunBackSpeed = currentFrame.RunBackSpeed,
                SwimSpeed = currentFrame.SwimSpeed,
                SwimBackSpeed = currentFrame.SwimBackSpeed,
                FlightSpeed = 0,
                TurnSpeed = currentFrame.TurnRate,
                TransportGuid = currentFrame.TransportGuid,
                TransportX = currentFrame.TransportOffsetX,
                TransportY = currentFrame.TransportOffsetY,
                TransportZ = currentFrame.TransportOffsetZ,
                TransportO = currentFrame.TransportOrientation,
                FallTime = fallTimeMs,
                Height = capsuleHeight,
                Radius = capsuleRadius,
                MapId = recording.MapId,
                DeltaTime = dt,
                FrameCounter = (uint)i,
                PrevGroundZ = prevGroundZ,
                PrevGroundNx = prevOutput.GroundNx,
                PrevGroundNy = prevOutput.GroundNy,
                PrevGroundNz = prevOutput.GroundNz != 0 ? prevOutput.GroundNz : 1.0f,
                // Don't carry forward depenetration � position resets to recording each frame,
                // so the previous frame's depenetration vector is stale and causes drift.
                PendingDepenX = 0,
                PendingDepenY = 0,
                PendingDepenZ = 0,
                StandingOnInstanceId = prevOutput.StandingOnInstanceId,
                StandingOnLocalX = prevOutput.StandingOnLocalX,
                StandingOnLocalY = prevOutput.StandingOnLocalY,
                StandingOnLocalZ = prevOutput.StandingOnLocalZ,
            };

            // Velocity: carry engine output velocity for airborne frames.
            bool isAirborne = (currentFrame.MovementFlags & 0x6000) != 0;
            if (isAirborne && wasAirborne)
            {
                input.Vx = prevOutput.Vx;
                input.Vy = prevOutput.Vy;
                input.Vz = prevOutput.Vz;
            }
            else if (isAirborne && !wasAirborne)
            {
                // First airborne frame: estimate all velocity components from position delta.
                // Horizontal velocity is constant during air (no air control), so Vx/Vy = dx/dt.
                // Vertical velocity includes gravity correction: Vz = dz/dt + 0.5*g*dt.
                const float GRAVITY = 19.2911f;
                float deltaX = nextFrame.Position.X - currentFrame.Position.X;
                float deltaY = nextFrame.Position.Y - currentFrame.Position.Y;
                float deltaZ = nextFrame.Position.Z - currentFrame.Position.Z;
                input.Vx = deltaX / dt;
                input.Vy = deltaY / dt;
                input.Vz = deltaZ / dt + 0.5f * GRAVITY * dt;
                // Set fallTime > 0 to prevent engine from re-applying JUMP_VELOCITY.
                input.FallTime = 1;
            }

            var output = StepPhysicsV2(ref input);
            prevOutput = output;
            prevGroundZ = output.GroundZ;

            float dx = output.X - nextFrame.Position.X;
            float dy = output.Y - nextFrame.Position.Y;
            float dz = output.Z - nextFrame.Position.Z;
            float posError = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
            float horizError = MathF.Sqrt(dx * dx + dy * dy);
            float vertError = MathF.Abs(dz);

            result.AddFrame(i, posError, horizError, vertError,
                output.X, output.Y, output.Z,
                nextFrame.Position.X, nextFrame.Position.Y, nextFrame.Position.Z);
        }

        return result;
    }
}
