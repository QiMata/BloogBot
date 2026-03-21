using BotRunner.Clients;
using GameData.Core.Enums;
using GameData.Core.Models;
using Pathfinding;
using Serilog;
using System;
using System.Numerics;
using WoWSharpClient.Client;
using WoWSharpClient.Models;
using WoWSharpClient.Parsers;

namespace WoWSharpClient.Movement
{
    public class MovementController(WoWClient client, PathfindingClient physics, WoWLocalPlayer player)
    {
        private readonly WoWClient _client = client;
        private readonly PathfindingClient _physics = physics;
        private readonly WoWLocalPlayer _player = player;

        // Physics state
        private Vector3 _velocity = Vector3.Zero;
        // Keep fall time in milliseconds to match WoW movement packet expectations.
        // PhysicsInput.fall_time in the proto is a float, but we track ms locally to avoid unit drift.
        private uint _fallTimeMs = 0;

        // StepV2 continuity state (must be round-tripped each tick)
        // Initialize prevGroundZ to player's current Z — NegativeInfinity fails the C++ hasPrevGround check
        private float _prevGroundZ = player.Position.Z;
        private Vector3 _prevGroundNormal = new Vector3(0, 0, 1);
        private Vector3 _pendingDepen = Vector3.Zero;
        private uint _standingOnInstanceId = 0;
        private Vector3 _standingOnLocal = Vector3.Zero;
        private float _stepUpBaseZ = -200000f;
        private uint _stepUpAge = 0;

        // Path-following state
        private Position[]? _currentPath;
        private int _currentWaypointIndex;
        private const float WAYPOINT_ARRIVE_DIST = 2.0f;
        // Callers (behavior tree) set a new target waypoint every tick. Only rebuild
        // the internal 2-waypoint path when the target shifts more than this threshold.
        // Too small (0.35y) → constant rebuilds as NavigationPath advances index → jitter.
        // Too large → slow reactions when the destination genuinely changes.
        private const float TARGET_WAYPOINT_REFRESH_DIST_2D = 3.0f;
        private const float TARGET_WAYPOINT_REFRESH_Z = 1.0f;

        // Post-teleport ground snap: when true, forces at least one physics step
        // even when the character is idle (MOVEFLAG_NONE), so gravity applies and
        // the character snaps to the real ground height after teleport/zone change.
        private bool _needsGroundSnap = false;
        /// <summary>
        /// True while post-teleport ground snap is in progress. Used by the game loop
        /// to allow physics updates even while _isBeingTeleported is true.
        /// </summary>
        public bool NeedsGroundSnap => _needsGroundSnap;
        private int _groundSnapFrames = 0;
        private const int GROUND_SNAP_MAX_FRAMES = 60; // ~2s at 30fps — safety limit
        // Server-authoritative Z from the teleport — used to clamp position.
        // If the physics engine doesn't have geometry (e.g. docks, bridges, beaches)
        // and gravity would pull us below the server's teleport Z, we clamp to prevent
        // fallthrough. The threshold is tight (3y) because even small navmesh/heightmap
        // mismatches at coasts and WMO surfaces cause cumulative gravity drift.
        private float _teleportZ = float.NaN;
        private const float GROUND_SNAP_MAX_DROP = 5.0f;
        // Max allowed ground Z descent per physics frame (large single-frame drops).
        private const float MaxGroundZDropPerFrame = 5.0f;

        // Slope ratio guard: tracks cumulative vertical descent vs horizontal distance.
        // ADT terrain has per-frame drops of 0.3y that individually pass the per-frame guard
        // but cumulate into 30y+ underground cascade over 3 seconds.
        // Max walkable slope: 60 degrees -> tan(60) = 1.73. Use 2.0 for stairs/ledges.
        // Once vertical/horizontal ratio exceeds this over accumulated travel, reject ground.
        private float _descentAnchorZ = float.NaN;
        private float _descentAnchorX = float.NaN;
        private float _descentAnchorY = float.NaN;
        private const float MAX_SLOPE_RATIO = 2.0f; // tan(63 degrees) - generous for stairs
        private const float SLOPE_CHECK_MIN_HORIZONTAL = 3.0f; // Minimum horizontal travel before checking

        // Path-aware ground validation: reject physics ground Z that is more than this many
        // units below the current path waypoint Z. Prevents gradual sinking into cave/gully
        // geometry below the navmesh walking surface.
        private const float PATH_GROUND_Z_TOLERANCE = 3.0f;

        private int _teleportClampFrames = 0;
        private const int TELEPORT_CLAMP_MAX_FRAMES = 300; // ~10s at 30fps — hard limit to avoid permanent clamping

        // Network timing
        private uint _lastPacketTime;
        private MovementFlags _lastSentFlags = player.MovementFlags;
        private bool _forceStopAfterReset;
        // When true, sync _lastPacketPosition to current player position on next Update().
        // Needed because Reset() runs before the teleport position is applied to _player.
        private bool _needsPacketPositionSync;
        private const uint PACKET_INTERVAL_MS = 200;
        private uint _latestGameTimeMs;
        private uint _staleForwardSuppressUntilMs;
        private int _staleForwardNoDisplacementTicks;
        private int _staleForwardRecoveryCount;
        private const float STALE_FORWARD_DISPLACEMENT_EPSILON = 0.05f;
        private const int STALE_FORWARD_NO_DISPLACEMENT_THRESHOLD = 15;
        private const uint STALE_FORWARD_SUPPRESS_AFTER_RESET_MS = 2000;
        private const uint STALE_FORWARD_SUPPRESS_AFTER_RECOVERY_MS = 1500;
        private const MovementFlags STALE_RECOVERY_MOVEMENT_MASK =
            MovementFlags.MOVEFLAG_FORWARD |
            MovementFlags.MOVEFLAG_BACKWARD |
            MovementFlags.MOVEFLAG_STRAFE_LEFT |
            MovementFlags.MOVEFLAG_STRAFE_RIGHT;
        private const MovementFlags FORCED_STOP_CLEAR_MASK =
            MovementFlags.MOVEFLAG_FORWARD |
            MovementFlags.MOVEFLAG_BACKWARD |
            MovementFlags.MOVEFLAG_STRAFE_LEFT |
            MovementFlags.MOVEFLAG_STRAFE_RIGHT |
            MovementFlags.MOVEFLAG_TURN_LEFT |
            MovementFlags.MOVEFLAG_TURN_RIGHT |
            MovementFlags.MOVEFLAG_PITCH_UP |
            MovementFlags.MOVEFLAG_PITCH_DOWN |
            MovementFlags.MOVEFLAG_PENDING_STOP |
            MovementFlags.MOVEFLAG_PENDING_UNSTRAFE |
            MovementFlags.MOVEFLAG_PENDING_FORWARD |
            MovementFlags.MOVEFLAG_PENDING_BACKWARD |
            MovementFlags.MOVEFLAG_PENDING_STR_LEFT |
            MovementFlags.MOVEFLAG_PENDING_STR_RGHT;

        // Wall contact state — updated each frame from physics output
        public bool LastHitWall { get; private set; }
        public Vector3 LastWallNormal { get; private set; } = new Vector3(0, 0, 1);
        public float LastBlockedFraction { get; private set; } = 1.0f;

        // Escalating stuck recovery (Phase 6)
        // Level 1: clear path + callback (15 frames, 0.05y)
        // Level 2: nearest-waypoint index warp (no full replan) — fires if stuck again in <30 frames
        // Level 3: request perpendicular strafe via callback — fires if still stuck
        private int _consecutiveStuckLevels = 0;
        private Position _lastKnownGoodPosition = new(player.Position.X, player.Position.Y, player.Position.Z);
        private const uint STALE_FORWARD_SUPPRESS_L2_MS = 800;  // shorter grace before L2 triggers
        private const uint STALE_FORWARD_SUPPRESS_L3_MS = 600;  // even shorter for L3

        /// <summary>
        /// Fired when a stuck condition is escalated. Level 1=path cleared, Level 2=corridor reset,
        /// Level 3=recovery strafe requested. Callers (BotRunner/StateManager) can use this to
        /// trigger higher-level recovery (unstuck, respawn, GM fallback).
        /// </summary>
        public event Action<int /*level*/, Position /*position*/>? OnStuckRecoveryRequired;

        // Debug tracking
        private Vector3 _lastPhysicsPosition = new Vector3(player.Position.X, player.Position.Y, player.Position.Z);
        // _accumulatedDelta removed — was causing π speed multiplier bug in dead-reckoning
        private int _frameCounter = 0;
        private int _movementDiagCounter = 0;
        private Vector3 _lastPacketPosition = new Vector3(player.Position.X, player.Position.Y, player.Position.Z);

        // ======== FRAME RECORDING (parity diagnostics) ========
        // Per-frame capture of physics input/output and all guard decisions.
        // Opt-in: callers set IsRecording=true, then retrieve frames via GetRecordedFrames().
        public bool IsRecording { get; set; }
        private const int MAX_RECORDED_FRAMES = 4000; // ~2 min at 30fps
        private readonly List<PhysicsFrameRecord> _recordedFrames = new(MAX_RECORDED_FRAMES);

        public List<PhysicsFrameRecord> GetRecordedFrames() => new(_recordedFrames);
        public void ClearRecordedFrames() => _recordedFrames.Clear();

        // ======== MAIN UPDATE - Called every frame ========
        public void Update(float deltaSec, uint gameTimeMs)
        {
            _frameCounter++;
            _latestGameTimeMs = gameTimeMs;

            // Deferred position sync: Reset() captured pre-teleport position.
            // Now that the teleport destination has been applied to _player, sync it.
            if (_needsPacketPositionSync)
            {
                _lastPacketPosition = new Vector3(_player.Position.X, _player.Position.Y, _player.Position.Z);
                _lastPacketTime = gameTimeMs;
                _needsPacketPositionSync = false;
                Log.Information("[MovementController] Synced _lastPacketPosition to post-teleport ({X:F1},{Y:F1},{Z:F1})",
                    _player.Position.X, _player.Position.Y, _player.Position.Z);
            }

            if (_forceStopAfterReset)
            {
                SendForcedStopPacket(gameTimeMs);
            }

            if (_lastSentFlags == MovementFlags.MOVEFLAG_NONE
                && _player.MovementFlags == MovementFlags.MOVEFLAG_NONE
                && !_needsGroundSnap
                && !_player.IsAutoAttacking)
            {
                return;
            }

            // Suppress all movement during channeling or casting. MaNGOS interprets
            // any movement packet (including idle heartbeats) as a channel interrupt.
            // This prevents crafting, fishing, and other channeled spells from being
            // cancelled by physics-generated position updates.
            if (_player.IsChanneling || _player.IsCasting)
            {
                return;
            }

            Log.Verbose("[MovementController] Frame {Frame} dt={Delta:F4}s Pos=({X:F1},{Y:F1},{Z:F1}) Flags={Flags}",
                _frameCounter, deltaSec, _player.Position.X, _player.Position.Y, _player.Position.Z, _player.MovementFlags);

            // 1. Run physics based on current player state
            var physicsResult = RunPhysics(deltaSec);

            // Check for position mismatch (external teleport, server correction, etc.)
            var physicsPosDiff = new Vector3(
                _player.Position.X - _lastPhysicsPosition.X,
                _player.Position.Y - _lastPhysicsPosition.Y,
                _player.Position.Z - _lastPhysicsPosition.Z
            );
            if (_frameCounter > 1 && physicsPosDiff.Length() > 100.0f)
            {
                // Large jump = teleport. Reset physics state so we don't carry stale velocity/ground.
                Log.Warning("[MovementController] Teleport detected ({Dist:F1} units). Resetting physics state.", physicsPosDiff.Length());
                Reset();
                _lastPhysicsPosition = new Vector3(_player.Position.X, _player.Position.Y, _player.Position.Z);
                return; // Skip this frame — let next frame start fresh
            }
            else if (physicsPosDiff.Length() > 0.01f)
            {
                Log.Warning("[MovementController] Position changed outside physics by {Dist:F3} units", physicsPosDiff.Length());
            }

            if (physicsResult == null)
            {
                Log.Warning("[MovementController] Physics returned null — skipping frame {Frame}", _frameCounter);
                return;
            }
            ApplyPhysicsResult(physicsResult, deltaSec);
            var newPhysicsPos = new Vector3(physicsResult.NewPosX, physicsResult.NewPosY, physicsResult.NewPosZ);
            var frameDelta = (newPhysicsPos - _lastPhysicsPosition).Length();
            // Log every 100th frame to avoid spam
            if (_frameCounter % 100 == 1 && _player.MovementFlags != MovementFlags.MOVEFLAG_NONE)
            {
                Log.Information("[MovementController] Frame#{Frame} dt={DeltaSec:F4}s frameDelta={FrameDelta:F3}y expected={Expected:F3}y flags=0x{Flags:X}",
                    _frameCounter, deltaSec, frameDelta, deltaSec * _player.RunSpeed, (uint)_player.MovementFlags);
            }

            ObserveStaleForwardAndRecover(frameDelta, gameTimeMs);
            _lastPhysicsPosition = new Vector3(_player.Position.X, _player.Position.Y, _player.Position.Z);

            // Post-teleport ground snap: keep running physics until the character reaches
            // ground (no FALLINGFAR flag). One frame of gravity at 33ms only drops ~0.01y,
            // so clearing immediately would strand the character in the air.
            if (_needsGroundSnap)
            {
                _groundSnapFrames++;

                // During ground snap, only clamp to teleport Z when physics has NO ground geometry.
                // If physics finds valid ground below the teleport position (e.g. teleported above a
                // rooftop), allow gravity to pull the bot down naturally — this is correct WoW behavior.
                // Only clamp when there's no ground at all (missing navmesh/collision), which prevents
                // fallthrough at docks/bridges/beaches where the navmesh sees the ocean floor.
                bool physicsFoundGround = _prevGroundZ > -50000f;
                if (!float.IsNaN(_teleportZ) && _player.Position.Z < _teleportZ && !physicsFoundGround)
                {
                    _player.Position = new Position(_player.Position.X, _player.Position.Y, _teleportZ);
                    _player.MovementFlags &= ~(MovementFlags.MOVEFLAG_FALLINGFAR | MovementFlags.MOVEFLAG_JUMPING);
                    _velocity = Vector3.Zero;
                    _fallTimeMs = 0;
                }

                // Guard against physics finding geometry ABOVE the teleport target (roofs, WMO
                // surfaces, bridges). The server is authoritative on post-teleport position —
                // if the player is significantly above teleport Z, physics found a ceiling/roof,
                // not the intended ground surface. Clamp back to teleport Z.
                if (!float.IsNaN(_teleportZ) && _player.Position.Z > _teleportZ + GROUND_SNAP_MAX_DROP)
                {
                    Log.Warning("[MovementController] Ground snap found geometry above teleport target: " +
                        "posZ={PosZ:F1} teleportZ={TeleZ:F1} groundZ={GroundZ:F1}. Clamping to teleport Z.",
                        _player.Position.Z, _teleportZ, _prevGroundZ);
                    _player.Position = new Position(_player.Position.X, _player.Position.Y, _teleportZ);
                    _player.MovementFlags &= ~(MovementFlags.MOVEFLAG_FALLINGFAR | MovementFlags.MOVEFLAG_JUMPING);
                    _velocity = Vector3.Zero;
                    _fallTimeMs = 0;
                }

                bool stillFalling = (_player.MovementFlags & MovementFlags.MOVEFLAG_FALLINGFAR) != 0;
                if (!stillFalling || _groundSnapFrames >= GROUND_SNAP_MAX_FRAMES)
                {
                    _needsGroundSnap = false;

                    Log.Information("[MovementController] Post-teleport ground snap complete: Z={Z:F3} groundZ={GroundZ:F3} flags=0x{Flags:X} frames={Frames}",
                        _player.Position.Z, _prevGroundZ, (uint)_player.MovementFlags, _groundSnapFrames);

                    // Force a stop packet with the corrected position so the server knows
                    // where we actually landed.
                    if (_player.MovementFlags == MovementFlags.MOVEFLAG_NONE)
                    {
                        SendStopPacket(gameTimeMs);
                    }
                }
            }

            // 2. Send network packet if needed.
            // Suppress packets during post-teleport ground snap — physics is still settling
            // and sending transient FALLINGFAR heartbeats confuses the server.
            if (!_needsGroundSnap && ShouldSendPacket(gameTimeMs))
            {
                SendMovementPacket(gameTimeMs);
            }
        }

        // ======== PHYSICS ========
        private PhysicsOutput RunPhysics(float deltaSec)
        {
            // Build physics input from current player state
            var input = new PhysicsInput
            {
                DeltaTime = deltaSec,  // Physics expects seconds, not milliseconds
                MapId = _player.MapId,
                MovementFlags = (uint)_player.MovementFlags,

                PosX = _player.Position.X,
                PosY = _player.Position.Y,
                PosZ = _player.Position.Z,
                Facing = _player.Facing,
                SwimPitch = _player.SwimPitch,

                VelX = _velocity.X,
                VelY = _velocity.Y,
                VelZ = _velocity.Z,

                WalkSpeed = _player.WalkSpeed,
                RunSpeed = _player.RunSpeed,
                RunBackSpeed = _player.RunBackSpeed,
                SwimSpeed = _player.SwimSpeed,
                SwimBackSpeed = _player.SwimBackSpeed,

                // Proto field is float; preserve ms to match existing packet conventions in this client.
                FallTime = _fallTimeMs,

                Race = (uint)_player.Race,
                Gender = (uint)_player.Gender,

                FrameCounter = (uint)_frameCounter,

                // Transport (boats, zeppelins, elevators)
                TransportGuid = _player.TransportGuid,
                TransportOffsetX = _player.TransportOffset.X,
                TransportOffsetY = _player.TransportOffset.Y,
                TransportOffsetZ = _player.TransportOffset.Z,
                TransportOrientation = _player.TransportOrientation,

                // StepV2 continuity inputs
                PrevGroundZ = _prevGroundZ,
                PrevGroundNx = _prevGroundNormal.X,
                PrevGroundNy = _prevGroundNormal.Y,
                PrevGroundNz = _prevGroundNormal.Z,

                PendingDepenX = _pendingDepen.X,
                PendingDepenY = _pendingDepen.Y,
                PendingDepenZ = _pendingDepen.Z,

                StandingOnInstanceId = _standingOnInstanceId,
                StandingOnLocalX = _standingOnLocal.X,
                StandingOnLocalY = _standingOnLocal.Y,
                StandingOnLocalZ = _standingOnLocal.Z,

                StepUpBaseZ = _stepUpBaseZ,
                StepUpAge = _stepUpAge,
            };

            return _physics.PhysicsStep(input);
        }

        private int _deadReckonCount = 0;
        private int _noGroundFrameCount = 0;
        private int _falseFreefallCount = 0;
        private float _ffsStartZ = float.NaN;  // Z where FFS first engaged — tracks cumulative drift
        // Track whether _prevGroundZ was established from actual physics ground contact
        // (not just constructor initialization from player.Position.Z). The hysteresis guard
        // must NOT engage until we've confirmed real ground contact — otherwise elevated spawns
        // cause _prevGroundZ = spawnZ, making the guard think we're "close to ground" at altitude.
        private bool _hasPhysicsGroundContact = false;
        private int _teleportZGraceFrames = 0;
        private const int TELEPORT_Z_GRACE_DURATION = 30; // ~1 second at 30 FPS
        private int _physicsMovedCount = 0;
        // Per-frame packet tracking for recording — set by SendMovementPacket,
        // captured by the recording in the NEXT frame's physics tick, then reset.
        private uint _frameSentOpcode = 0;
        private uint _frameSentFlags = 0;
        private float _frameSentFacing = 0;
        private bool _frameSentPending = false;

        private void ApplyPhysicsResult(PhysicsOutput output, float deltaSec)
        {
            // Capture wall contact feedback for the path layer (Phase 1: physics-to-path feedback)
            LastHitWall = output.HitWall;
            LastWallNormal = new Vector3(output.WallNormalX, output.WallNormalY, output.WallNormalZ);
            LastBlockedFraction = output.BlockedFraction;

            if (output.HitWall)
            {
                Log.Verbose("[MovementController] Wall contact: normal=({Nx:F2},{Ny:F2},{Nz:F2}) blocked={Frac:P0}",
                    output.WallNormalX, output.WallNormalY, output.WallNormalZ, 1.0f - output.BlockedFraction);
            }

            var oldPos = _player.Position;

            // Diagnostic: detect if physics returned unchanged position while movement was expected.
            // Dead-reckoning fallback has been REMOVED — the physics engine should handle all movement.
            // If physics returns same position, it means: no movement flags, collision blocked, or engine error.
            float dx = output.NewPosX - oldPos.X;
            float dy = output.NewPosY - oldPos.Y;
            bool physicsMovedUs = MathF.Abs(dx) >= 0.001f || MathF.Abs(dy) >= 0.001f;

            if (!physicsMovedUs && _player.MovementFlags != MovementFlags.MOVEFLAG_NONE)
            {
                _deadReckonCount++;
                if (_deadReckonCount % 50 == 1)
                {
                    Log.Warning("[MovementController] Physics returned same position (no dead-reckoning). " +
                        "Count={Count} flags=0x{Flags:X} dt={Dt:F4}",
                        _deadReckonCount, (uint)_player.MovementFlags, deltaSec);
                }
                if (_deadReckonCount == 1 || _deadReckonCount == 50)
                {
                    Log.Warning("[MovementController] Physics zero-delta diagnostic: " +
                        "mapId={MapId} in=({InX:F1},{InY:F1},{InZ:F1}) out=({OutX:F1},{OutY:F1},{OutZ:F1}) " +
                        "facing={Facing:F2} runSpeed={Speed:F1} prevGZ={PrevGZ:F1} groundZ={GroundZ:F1}",
                        _player.MapId, oldPos.X, oldPos.Y, oldPos.Z,
                        output.NewPosX, output.NewPosY, output.NewPosZ,
                        _player.Facing, _player.RunSpeed, _prevGroundZ, output.GroundZ);
                }
            }
            else if (physicsMovedUs)
            {
                _physicsMovedCount++;
            }

            // When physics doesn't provide valid ground, keep current Z rather than interpolating
            // from path waypoints. Path Z interpolation was masking missing collision geometry —
            // the physics engine should find ground on its own. The teleport Z clamp (#4) handles
            // the post-teleport geometry-loading window.
            bool physicsHasGround = output.GroundZ > -50000f;
            float rawPosZ = output.NewPosZ; // Before any guard modifications
            bool undergroundSnapFired = false;
            bool falseFreefallSuppressed = false;
            if (!physicsHasGround)
            {
                _noGroundFrameCount++;

                // When no collision geometry exists (dead-reckoning fallback), interpolate Z
                // toward the current target waypoint. The waypoint Z comes from the navmesh
                // (mmaps) and is correct even when vmtile collision data is missing.
                // Without this, the bot's Z freezes at the teleport height while walking
                // through dungeons with sloping floors, causing server-side position desync
                // and mob evade behavior.
                if (_currentPath != null
                    && _currentWaypointIndex >= 0
                    && _currentWaypointIndex < _currentPath.Length)
                {
                    var waypoint = _currentPath[_currentWaypointIndex];
                    float targetZ = waypoint.Z;

                    // Lerp Z toward waypoint based on 2D proximity
                    float dist2D = HorizontalDistance(output.NewPosX, output.NewPosY, waypoint.X, waypoint.Y);
                    float startDist = _currentPath.Length >= 2 && _currentWaypointIndex > 0
                        ? HorizontalDistance(_currentPath[_currentWaypointIndex - 1].X,
                                             _currentPath[_currentWaypointIndex - 1].Y,
                                             waypoint.X, waypoint.Y)
                        : dist2D + 1f;

                    if (startDist > 0.1f)
                    {
                        float progress = 1f - MathF.Min(dist2D / startDist, 1f);
                        output.NewPosZ = oldPos.Z + (targetZ - oldPos.Z) * MathF.Min(progress * 2f, 1f);
                    }
                    else
                    {
                        output.NewPosZ = targetZ;
                    }
                }
                else
                {
                    output.NewPosZ = oldPos.Z;
                }

                // When physics has no ground, the engine can't simulate horizontal movement.
                // Apply software dead-reckoning for XY based on movement flags + orientation.
                // Without this, bots in dungeons without vmtile data (e.g. RFC map 389) are
                // completely stuck — position never changes even though MOVEFLAG_FORWARD is set.
                bool isMovingForward = (output.MovementFlags & (uint)MovementFlags.MOVEFLAG_FORWARD) != 0;
                if (isMovingForward && _currentPath != null
                    && _currentWaypointIndex >= 0
                    && _currentWaypointIndex < _currentPath.Length)
                {
                    var wp = _currentPath[_currentWaypointIndex];
                    float drDx = wp.X - output.NewPosX;
                    float drDy = wp.Y - output.NewPosY;
                    float drDist = MathF.Sqrt(drDx * drDx + drDy * drDy);
                    if (drDist > 0.5f)
                    {
                        // Use player's actual run speed for dead-reckoning.
                        // Previously hardcoded at 2.5 y/s (~36% of normal 7 y/s),
                        // causing BG bots to move far too slowly in dungeons.
                        float drSpeed = _player.RunSpeed * deltaSec;
                        output.NewPosX += (drDx / drDist) * drSpeed;
                        output.NewPosY += (drDy / drDist) * drSpeed;
                    }
                }

                output.NewVelX = 0;
                output.NewVelY = 0;
                output.NewVelZ = 0;
                output.FallTime = 0;

                // Strip FALLINGFAR when there's no collision geometry at all.
                // Without ground data the physics engine reports "falling" but the bot
                // is really walking on a surface the vmtile data doesn't know about.
                // Leaving FALLINGFAR set blocks StartMovement(ControlBits.Front) via
                // IsPlayerAirborne(), which prevents MOVEFLAG_FORWARD from being set,
                // which prevents dead-reckoning from ever activating → bot stuck.
                output.MovementFlags = output.MovementFlags
                    & ~((uint)MovementFlags.MOVEFLAG_FALLINGFAR | (uint)MovementFlags.MOVEFLAG_JUMPING);

                if (_noGroundFrameCount == 30) // ~1 second at 30 FPS
                {
                    Log.Warning("[MovementController] No ground for 30 frames at ({X:F1}, {Y:F1}, {Z:F1}). " +
                        "Physics may be missing collision geometry here.",
                        output.NewPosX, output.NewPosY, output.NewPosZ);
                }
            }
            else
            {
                _noGroundFrameCount = 0;

                // When physics HAS valid ground but still reports FALLINGFAR, the DOWN pass
                // capsule sweep missed the ground surface (e.g. Valley of Trials slope terrain).
                // Strip FALLINGFAR to prevent IsPlayerAirborne() from blocking MoveToward(),
                // which would prevent MOVEFLAG_FORWARD from being set on subsequent frames.
                // Without this, the bot enters a stuck loop: physics returns FALLINGFAR →
                // MoveToward blocked → no FORWARD flag → zero movement → stuck recovery →
                // MoveToward sets FORWARD → physics returns FALLINGFAR again → repeat.
                if ((output.MovementFlags & (uint)MovementFlags.MOVEFLAG_FALLINGFAR) != 0)
                {
                    output.MovementFlags &= ~(uint)(MovementFlags.MOVEFLAG_FALLINGFAR | MovementFlags.MOVEFLAG_JUMPING);
                    output.FallTime = 0;
                    output.NewVelZ = 0;
                    _falseFreefallCount++;
                    if (_falseFreefallCount <= 3 || _falseFreefallCount % 100 == 0)
                    {
                        Log.Warning("[MovementController] Stripped false FALLINGFAR — ground exists at Z={GroundZ:F1} " +
                            "but physics flagged airborne. Count={Count}",
                            output.GroundZ, _falseFreefallCount);
                    }
                }
                else
                {
                    _falseFreefallCount = 0;
                }
            }
            // Guard against falling through objects that the navmesh doesn't model (docks, bridges,
            // WMO platforms). After a teleport, if gravity would pull us more than GROUND_SNAP_MAX_DROP
            // below the server-authoritative teleport Z, clamp position. This persists across frames
            // until the player intentionally moves (FORWARD/BACKWARD/STRAFE/JUMP/SWIM), which clears
            // the teleport Z clamp. The navmesh may see the ocean floor through a dock surface.
            if (!float.IsNaN(_teleportZ))
            {
                _teleportClampFrames++;

                // Hard limit: after ~10s, clear the teleport Z clamp unconditionally.
                // This prevents permanent clamping if physics never finds matching ground.
                if (_teleportClampFrames >= TELEPORT_CLAMP_MAX_FRAMES)
                {
                    Log.Warning("[MovementController] Teleport Z clamp expired after {Frames} frames. Clearing.", _teleportClampFrames);
                    _teleportZ = float.NaN;
                    _teleportClampFrames = 0;
                    _teleportZGraceFrames = 0;
                }
                else if (output.NewPosZ < _teleportZ)
                {
                    // If physics found real ground below the teleport Z, allow the fall — the bot
                    // was teleported above real terrain (e.g. above a rooftop) and should descend
                    // naturally via gravity, matching WoW client behavior. Only clamp when physics
                    // has NO ground geometry (navmesh gap at docks/bridges/beaches).
                    if (physicsHasGround && output.GroundZ >= _teleportZ - 0.1f)
                    {
                        // Ground is essentially at teleport Z (sub-0.1y difference, floating-point noise).
                        // Clear the clamp — the bot has landed.
                        _teleportZ = float.NaN;
                        _teleportClampFrames = 0;
                        _teleportZGraceFrames = 0;
                    }
                    else if (physicsHasGround && output.GroundZ < _teleportZ - 1.0f)
                    {
                        // Physics sees ground well below teleport Z — clear the clamp and let the
                        // bot fall to it. This is the "teleported above terrain" case.
                        Log.Information("[MovementController] Teleport Z clamp cleared: ground at {GroundZ:F1} is below teleportZ={TeleZ:F1}. Allowing fall.",
                            output.GroundZ, _teleportZ);
                        // Reset _prevGroundZ to teleportZ so the slope guard doesn't see the stale
                        // pre-teleport ground height as a reference. Without this, the slope guard
                        // rejects the legitimate fall (e.g. pre-teleport Z=61 → teleportZ=32 →
                        // groundZ=28 looks like a 33y drop from _prevGroundZ=61, triggering rejection).
                        _prevGroundZ = _teleportZ;
                        _descentAnchorZ = float.NaN;
                        _descentAnchorX = float.NaN;
                        _descentAnchorY = float.NaN;
                        _teleportZ = float.NaN;
                        _teleportClampFrames = 0;
                        _teleportZGraceFrames = 0;
                    }
                    else
                    {
                        bool hasIntentionalMovement = (_player.MovementFlags & (MovementFlags.MOVEFLAG_FORWARD
                            | MovementFlags.MOVEFLAG_BACKWARD | MovementFlags.MOVEFLAG_STRAFE_LEFT
                            | MovementFlags.MOVEFLAG_STRAFE_RIGHT | MovementFlags.MOVEFLAG_JUMPING
                            | MovementFlags.MOVEFLAG_SWIMMING)) != 0;
                        if (!hasIntentionalMovement)
                        {
                            Log.Warning("[MovementController] Teleport Z clamp: suppressed drop to {NewZ:F1} (teleportZ={TeleZ:F1}, groundZ={GroundZ:F1}). Navmesh may lack geometry here.",
                                output.NewPosZ, _teleportZ, output.GroundZ);
                            output.NewPosZ = _teleportZ;
                            output.NewVelX = 0;
                            output.NewVelY = 0;
                            output.NewVelZ = 0;
                            output.FallTime = 0;
                            // Clear falling/jumping flags — player is clamped to teleport Z, not falling.
                            // Without this, MOVEFLAG_FALLINGFAR causes heartbeats that interrupt channeled
                            // spells (e.g. fishing) because the server sees the player as "moving".
                            output.MovementFlags = output.MovementFlags
                                & ~((uint)MovementFlags.MOVEFLAG_FALLINGFAR | (uint)MovementFlags.MOVEFLAG_JUMPING);
                        }
                        else
                        {
                            // Player started intentionally moving — begin grace period countdown.
                            if (_teleportZGraceFrames == 0)
                                _teleportZGraceFrames = TELEPORT_Z_GRACE_DURATION;

                            _teleportZGraceFrames--;
                            if (_teleportZGraceFrames <= 0)
                            {
                                _teleportZ = float.NaN;
                                _teleportClampFrames = 0;
                                _teleportZGraceFrames = 0;
                            }
                        }
                    }
                }
                else if (physicsHasGround && output.GroundZ >= _teleportZ - 0.5f
                    && output.GroundZ <= _teleportZ + GROUND_SNAP_MAX_DROP)
                {
                    // Physics found terrain-level ground near teleport Z — this is normal operation.
                    // Only clear when groundZ (not just position Z) is near the teleport level.
                    // This prevents premature clearing when cave geometry exists below terrain but
                    // the position is still held near teleport Z by the ground snap phase.
                    // Also reject if ground is far ABOVE teleport Z — that's a roof/WMO, not terrain.
                    _teleportZ = float.NaN;
                    _teleportClampFrames = 0;
                    _teleportZGraceFrames = 0;
                }
            }

            // Advance waypoint if we've arrived
            AdvanceWaypointIfNeeded(output.NewPosX, output.NewPosY);

            // Slope guard: two-tier protection against underground cascade.
            // Tier 1 (per-frame): Reject ground Z drops > MaxGroundZDropPerFrame.
            // Tier 2 (slope ratio): Track vertical descent vs horizontal distance.
            //   ADT at Valley of Trials has per-frame drops of 0.3y that pass Tier 1 but
            //   cumulate to 30y+ over 3 seconds. By comparing descent/distance to max walkable
            //   slope ratio (tan 63 deg = 2.0), we detect when descent is physically impossible
            //   for grounded movement.
            bool slopeGuardRejected = false;
            bool isFalling = (output.MovementFlags & (uint)(MovementFlags.MOVEFLAG_FALLINGFAR | MovementFlags.MOVEFLAG_JUMPING)) != 0;

            if (physicsHasGround && float.IsNaN(_teleportZ)
                && !float.IsNaN(_prevGroundZ) && _prevGroundZ > -99000f)
            {
                // Tier 1: large per-frame drop
                if (output.GroundZ < _prevGroundZ - MaxGroundZDropPerFrame)
                {
                    slopeGuardRejected = true;
                }

                // Tier 2: slope ratio check over accumulated travel
                if (!slopeGuardRejected && output.GroundZ < _prevGroundZ)
                {
                    // Initialize anchor at start of descent
                    if (float.IsNaN(_descentAnchorZ))
                    {
                        _descentAnchorZ = _prevGroundZ;
                        _descentAnchorX = output.NewPosX;
                        _descentAnchorY = output.NewPosY;
                    }

                    float verticalDescent = _descentAnchorZ - output.GroundZ;
                    float hdx = output.NewPosX - _descentAnchorX;
                    float hdy = output.NewPosY - _descentAnchorY;
                    float horizontalDist = MathF.Sqrt(hdx * hdx + hdy * hdy);

                    // Only check slope ratio after sufficient horizontal travel
                    if (horizontalDist >= SLOPE_CHECK_MIN_HORIZONTAL && verticalDescent > 0)
                    {
                        float slopeRatio = verticalDescent / horizontalDist;
                        if (slopeRatio > MAX_SLOPE_RATIO)
                        {
                            slopeGuardRejected = true;
                            Log.Warning("[MovementController] Slope ratio guard: descent {Descent:F1}y over {Horiz:F1}y horizontal " +
                                "(ratio={Ratio:F2}, max={Max:F1}). Holding ground at Z={PrevZ:F1}.",
                                verticalDescent, horizontalDist, slopeRatio, MAX_SLOPE_RATIO, _prevGroundZ);
                        }
                    }
                }
                else if (!slopeGuardRejected && output.GroundZ >= _prevGroundZ)
                {
                    // Ground is level or ascending — reset slope tracker
                    _descentAnchorZ = float.NaN;
                    _descentAnchorX = float.NaN;
                    _descentAnchorY = float.NaN;
                }
            }
            // Reset descent anchors when entering freefall so stale accumulation
            // doesn't cause false slope guard rejections after landing.
            if (isFalling)
            {
                _descentAnchorZ = float.NaN;
                _descentAnchorX = float.NaN;
                _descentAnchorY = float.NaN;
            }

            // Update position from physics — clamp Z when slope guard rejected the ground.
            // When the slope guard triggers, use navmesh path Z as fallback instead of
            // holding at _prevGroundZ (which gets the bot stuck at the rejection point).
            // The navmesh path Z represents the walkable surface, allowing continued progress.
            var finalPosZ = output.NewPosZ;
            bool pathGroundGuardActive = false;
            if (slopeGuardRejected && output.NewPosZ < _prevGroundZ)
            {
                // Try path Z interpolation as a smooth descent reference
                float pathZ = InterpolatePathZ(output.NewPosX, output.NewPosY, _prevGroundZ);
                // Use the path Z if it's reasonable, otherwise hold at prevGroundZ
                finalPosZ = pathZ > _prevGroundZ - MaxGroundZDropPerFrame ? pathZ : _prevGroundZ;
                output.NewVelZ = 0;
                output.FallTime = 0;
            }
            else if (physicsHasGround && !float.IsNaN(_prevGroundZ) && _prevGroundZ > -99000f
                && output.NewPosZ < _prevGroundZ - MaxGroundZDropPerFrame)
            {
                finalPosZ = _prevGroundZ;
            }
            // Path-aware position clamp DISABLED: navmesh waypoint Z can be grossly wrong
            // (e.g., 61.4 vs actual terrain 56.8 on hills). The physics engine's ground
            // detection is the correct source of truth — it matches WoW's native terrain
            // height. Previously this guard would snap the bot UP to the wrong navmesh Z,
            // causing +3-6y Z offset vs the FG gold standard.
            // N.5 fix: Path-based underground snap for freefall. When following a navmesh path and
            // the character falls more than 10y below the path waypoint Z, it has fallen through
            // terrain (physics gap on steep slopes where DownPass can't find ground within its 4y
            // snap range). Snap back to the path waypoint Z and clear fall state. This is path-based
            // (not _prevGroundZ based) to avoid interfering with legitimate falls from heights.
            if (isFalling && _currentPath != null && _currentWaypointIndex < _currentPath.Length)
            {
                float pathZ = _currentPath[_currentWaypointIndex].Z;
                if (finalPosZ < pathZ - 10f)
                {
                    Log.Warning("[MovementController] Underground snap: fell {Drop:F1}y below path waypoint Z={PathZ:F1}. " +
                        "Snapping to path Z.", pathZ - finalPosZ, pathZ);
                    finalPosZ = pathZ;
                    pathGroundGuardActive = true;
                    undergroundSnapFired = true;
                    output.NewVelZ = 0;
                    output.FallTime = 0;
                }
            }
            _player.Position = new Position(output.NewPosX, output.NewPosY, finalPosZ);
            _player.SwimPitch = output.Pitch;

            // Update velocity
            _velocity = new Vector3(output.NewVelX, output.NewVelY, output.NewVelZ);

            // Update fall time — zero it when physics reports grounded (no falling flags).
            // The C++ physics engine may accumulate fall time even after ground contact
            // is re-established; trusting that stale value causes packets with non-zero
            // FallTime while grounded, which confuses the server.
            if (isFalling)
                _fallTimeMs = (uint)MathF.Max(0, output.FallTime);
            else
                _fallTimeMs = 0;

            // Persist StepV2 continuity outputs for next tick.
            // Mark ground contact established once physics confirms grounded with valid geometry.
            // This prevents the hysteresis guard from engaging on elevated spawns where _prevGroundZ
            // was initialized to the spawn position rather than actual terrain.
            if (physicsHasGround && (output.MovementFlags & (uint)(MovementFlags.MOVEFLAG_FALLINGFAR | MovementFlags.MOVEFLAG_JUMPING)) == 0)
                _hasPhysicsGroundContact = true;
            if (physicsHasGround)
            {
                if (!float.IsNaN(_teleportZ) && output.GroundZ < _teleportZ - 1.5f)
                {
                    _prevGroundZ = _teleportZ;
                    _prevGroundNormal = new Vector3(0, 0, 1);
                }
                else if (slopeGuardRejected)
                {
                    // Use the path-interpolated Z as the new ground reference so the bot
                    // can continue descending along the navmesh path instead of getting stuck.
                    _prevGroundZ = finalPosZ;
                    _prevGroundNormal = new Vector3(0, 0, 1);
                }
                // Path-aware ground rejection DISABLED: navmesh waypoint Z can be wrong
                // by 3-6y on hills, causing the bot to hold _prevGroundZ at the inflated
                // waypoint level, which then cascades into false freefall suppression
                // clamping position to the wrong height.
                else
                {
                    _prevGroundZ = output.GroundZ;
                    _prevGroundNormal = new Vector3(output.GroundNx, output.GroundNy, output.GroundNz);
                }
            }
            _pendingDepen = new Vector3(output.PendingDepenX, output.PendingDepenY, output.PendingDepenZ);
            _standingOnInstanceId = output.StandingOnInstanceId;
            _standingOnLocal = new Vector3(output.StandingOnLocalX, output.StandingOnLocalY, output.StandingOnLocalZ);
            _stepUpBaseZ = output.StepUpBaseZ;
            _stepUpAge = output.StepUpAge;

            // Apply physics state flags (falling, swimming, etc)
            const MovementFlags PhysicsFlags =
                MovementFlags.MOVEFLAG_JUMPING |
                MovementFlags.MOVEFLAG_SWIMMING |
                MovementFlags.MOVEFLAG_FLYING |
                MovementFlags.MOVEFLAG_LEVITATING |
                MovementFlags.MOVEFLAG_FALLINGFAR;

            // Preserve input flags, update physics flags
            var inputFlags = _player.MovementFlags & ~PhysicsFlags;
            var newPhysicsFlags = (MovementFlags)(output.MovementFlags) & PhysicsFlags;

            // When slope guard or path ground guard rejected the ground, strip falling flags.
            if (slopeGuardRejected || pathGroundGuardActive)
            {
                newPhysicsFlags &= ~(MovementFlags.MOVEFLAG_FALLINGFAR | MovementFlags.MOVEFLAG_JUMPING);
            }

            // Grounded→Falling hysteresis: When the physics engine transitions from grounded
            // to FALLINGFAR, check if we're still close to the last known ground. If so,
            // this is a transient capsule sweep miss (ADT gully, navmesh edge), not a real fall.
            // Suppress the flag transition to prevent grounded/falling flicker that causes
            // server rubber-banding. Real falls (large gap from ground) proceed normally.
            bool wasGrounded = (_player.MovementFlags & (MovementFlags.MOVEFLAG_FALLINGFAR | MovementFlags.MOVEFLAG_JUMPING)) == 0;
            bool nowFalling = (newPhysicsFlags & (MovementFlags.MOVEFLAG_FALLINGFAR | MovementFlags.MOVEFLAG_JUMPING)) != 0;
            if (wasGrounded && nowFalling && !slopeGuardRejected && _currentPath != null)
            {
                // Only engage when following a navmesh path — the path Z represents the
                // walkable surface and confirms the bot SHOULD be grounded. Without a path,
                // let physics handle terrain transitions naturally (avoids corrupting ground
                // tracking on steep slopes where transient FALLINGFAR is actually informative).
                //
                // Additional safety: require confirmed physics ground contact to prevent
                // the guard from engaging on elevated path starts where _prevGroundZ was
                // initialized to the spawn position, not actual terrain.
                float gapFromGround = MathF.Abs(output.NewPosZ - _prevGroundZ);
                bool closeToGround = _hasPhysicsGroundContact
                    && !float.IsNaN(_prevGroundZ) && _prevGroundZ > -99000f
                    && gapFromGround < 3.0f;

                if (closeToGround)
                {
                    // FFS engages: suppress FALLINGFAR flag and zero fall state.
                    // The physics engine's DOWN pass sometimes fails to find ground on
                    // moderately steep terrain (~30-45°) where FG stays grounded.
                    // We suppress the flag to prevent server rubber-banding, but allow
                    // physics Z to descend naturally — never clamp Z upward.
                    _falseFreefallCount++;

                    // FFS descent ceiling: descend toward the waypoint Z at max walkable
                    // slope rate. Only descend when waypoint is below current position.
                    // Physics Z takes priority when lower (natural terrain following).
                    if (float.IsNaN(_ffsStartZ))
                        _ffsStartZ = _player.Position.Z;
                    float ffsZ = _ffsStartZ;
                    if (_currentPath != null && _currentWaypointIndex < _currentPath.Length)
                    {
                        float wpZ = _currentPath[_currentWaypointIndex].Z;
                        if (wpZ < _ffsStartZ)
                        {
                            float maxDropPerFrame = _player.RunSpeed * deltaSec * 1.732f;
                            float desiredDrop = _ffsStartZ - wpZ;
                            float drop = MathF.Min(maxDropPerFrame, desiredDrop);
                            ffsZ = _ffsStartZ - drop;
                        }
                    }
                    _ffsStartZ = ffsZ;

                    // Use the lower of FFS descent ceiling and physics output.
                    // This ensures FFS never clamps Z upward — physics terrain
                    // following takes priority when it finds lower ground.
                    float finalZ = MathF.Min(ffsZ, output.NewPosZ);

                    if (_falseFreefallCount <= 3 || _falseFreefallCount % 100 == 0)
                        Log.Information("[MovementController] FFS (x{Count}): physZ={PhysZ:F2}, ffsZ={FfsZ:F2}, finalZ={FinalZ:F2}, gap={Gap:F2}",
                            _falseFreefallCount, output.NewPosZ, ffsZ, finalZ, gapFromGround);
                    newPhysicsFlags &= ~(MovementFlags.MOVEFLAG_FALLINGFAR | MovementFlags.MOVEFLAG_JUMPING);
                    _player.Position = new Position(output.NewPosX, output.NewPosY, finalZ);
                    _velocity = new Vector3(output.NewVelX, output.NewVelY, 0);
                    _fallTimeMs = 0;
                    falseFreefallSuppressed = true;
                    _prevGroundZ = finalZ;
                }
                else
                {
                    // Real fall: large gap from ground, allow transition normally
                    _falseFreefallCount = 0;
                    _ffsStartZ = float.NaN;
                }
            }
            else
            {
                _falseFreefallCount = 0;
                _ffsStartZ = float.NaN;
            }

            _player.MovementFlags = inputFlags | newPhysicsFlags;

            Log.Verbose("[MovementController] Applied: ({OldX:F1},{OldY:F1},{OldZ:F1}) -> ({NewX:F1},{NewY:F1},{NewZ:F1}) Flags={Flags}",
                oldPos.X, oldPos.Y, oldPos.Z, _player.Position.X, _player.Position.Y, _player.Position.Z, _player.MovementFlags);

            // Record frame for parity diagnostics
            if (IsRecording && _recordedFrames.Count < MAX_RECORDED_FRAMES)
            {
                float pathWpZ = (_currentPath != null && _currentWaypointIndex < _currentPath.Length)
                    ? _currentPath[_currentWaypointIndex].Z : float.NaN;
                int pathWpIdx = (_currentPath != null) ? _currentWaypointIndex : -1;
                float prevZ = _recordedFrames.Count > 0 ? _recordedFrames[^1].PosZ : _player.Position.Z;

                _recordedFrames.Add(new PhysicsFrameRecord
                {
                    FrameNumber = _frameCounter,
                    GameTimeMs = _latestGameTimeMs,
                    DeltaSec = deltaSec,
                    PosX = _player.Position.X,
                    PosY = _player.Position.Y,
                    PosZ = _player.Position.Z,
                    RawPosZ = rawPosZ,
                    PhysicsGroundZ = output.GroundZ,
                    PrevGroundZ = _prevGroundZ,
                    HasPhysicsGroundContact = _hasPhysicsGroundContact,
                    VelX = _velocity.X,
                    VelY = _velocity.Y,
                    VelZ = _velocity.Z,
                    FallTimeMs = _fallTimeMs,
                    IsFalling = isFalling,
                    MovementFlags = (uint)_player.MovementFlags,
                    SlopeGuardRejected = slopeGuardRejected,
                    PathGroundGuardActive = pathGroundGuardActive,
                    FalseFreefallSuppressed = falseFreefallSuppressed,
                    TeleportClampActive = !float.IsNaN(_teleportZ),
                    UndergroundSnapFired = undergroundSnapFired,
                    HitWall = output.HitWall,
                    WallNormalX = output.WallNormalX,
                    WallNormalY = output.WallNormalY,
                    BlockedFraction = output.BlockedFraction,
                    PathWaypointZ = pathWpZ,
                    PathWaypointIndex = pathWpIdx,
                    ZDeltaFromPrev = _player.Position.Z - prevZ,
                    PrevGroundNx = _prevGroundNormal.X,
                    PrevGroundNy = _prevGroundNormal.Y,
                    PrevGroundNz = _prevGroundNormal.Z,
                    PacketOpcode = _frameSentPending ? _frameSentOpcode : 0,
                    PacketFlags = _frameSentPending ? _frameSentFlags : 0,
                    PacketFacing = _frameSentPending ? _frameSentFacing : 0,
                });
                _frameSentPending = false;
            }
        }

        // ======== NETWORKING ========
        private bool ShouldSendPacket(uint gameTimeMs)
        {
            // Send if movement state changed
            if (_player.MovementFlags != _lastSentFlags)
                return true;

            // Send periodic heartbeat while moving
            if (_player.MovementFlags != MovementFlags.MOVEFLAG_NONE &&
                gameTimeMs - _lastPacketTime >= PACKET_INTERVAL_MS)
                return true;

            // Send periodic heartbeat while auto-attacking (even if stationary).
            // MaNGOS requires movement updates to process melee swing timer.
            if (_player.IsAutoAttacking &&
                gameTimeMs - _lastPacketTime >= PACKET_INTERVAL_MS)
                return true;

            return false;
        }

        private void SendMovementPacket(uint gameTimeMs)
        {
            if (_forceStopAfterReset)
            {
                SendForcedStopPacket(gameTimeMs);
                return;
            }

            var opcode = DetermineOpcode(_player.MovementFlags, _lastSentFlags);

            // Build and send packet
            var buffer = MovementPacketHandler.BuildMovementInfoBuffer(_player, gameTimeMs, _fallTimeMs);
            _ = _client.SendMovementOpcodeAsync(opcode, buffer);

            // Track for recording (captured in NEXT frame's physics recording)
            _frameSentOpcode = (uint)opcode;
            _frameSentFlags = (uint)_player.MovementFlags;
            _frameSentFacing = _player.Facing;
            _frameSentPending = true;

            // Diagnostic: log position delta between packets
            _movementDiagCounter++;
            var curPos = new Vector3(_player.Position.X, _player.Position.Y, _player.Position.Z);
            var packetDelta = (curPos - _lastPacketPosition).Length();
            var timeSinceLastMs = gameTimeMs - _lastPacketTime;
            if (_movementDiagCounter % 5 == 1)  // Every 5th packet
            {
                Log.Information("[MovementController] {Opcode} Pos=({X:F1},{Y:F1},{Z:F1}) delta={Delta:F2}y dt={DeltaMs}ms speed={Speed:F1} flags=0x{Flags:X}",
                    opcode, _player.Position.X, _player.Position.Y, _player.Position.Z,
                    packetDelta, timeSinceLastMs, _player.RunSpeed, (uint)_player.MovementFlags);
            }
            _lastPacketPosition = curPos;

            // Update tracking
            _lastPacketTime = gameTimeMs;
            _lastSentFlags = _player.MovementFlags;
            _forceStopAfterReset = false;
        }

        private void SendForcedStopPacket(uint gameTimeMs)
        {
            // Always emit a true MSG_MOVE_STOP before resuming movement after reset/recovery.
            // Clear directional intent, but preserve active physics state such as falling/swimming.
            // Do not immediately restore pre-stop movement flags in this tick.
            // Let bot logic re-issue movement intent on the next tick so stale forward/strafe
            // state is fully cleared server-side first.
            _player.MovementFlags = ClearForcedStopIntent(_player.MovementFlags);

            var opcode = DetermineOpcode(_player.MovementFlags, _lastSentFlags);
            var buffer = MovementPacketHandler.BuildMovementInfoBuffer(_player, gameTimeMs, _fallTimeMs);
            _ = _client.SendMovementOpcodeAsync(opcode, buffer);

            // Track for recording
            _frameSentOpcode = (uint)opcode;
            _frameSentFlags = (uint)_player.MovementFlags;
            _frameSentFacing = _player.Facing;
            _frameSentPending = true;

            _lastPacketPosition = new Vector3(_player.Position.X, _player.Position.Y, _player.Position.Z);
            _lastPacketTime = gameTimeMs;
            _lastSentFlags = _player.MovementFlags;
            _forceStopAfterReset = false;
            _staleForwardNoDisplacementTicks = 0;
            _staleForwardSuppressUntilMs = AddMs(gameTimeMs, STALE_FORWARD_SUPPRESS_AFTER_RECOVERY_MS);
            Log.Information("[MovementController] Forced {Opcode} dispatched; flags now 0x{Flags:X} for clean resume.",
                opcode,
                (uint)_player.MovementFlags);
        }

        private static bool IsBefore(uint nowMs, uint targetMs)
            => unchecked((int)(nowMs - targetMs)) < 0;

        private static uint AddMs(uint nowMs, uint durationMs)
            => unchecked(nowMs + durationMs);

        private void ObserveStaleForwardAndRecover(float frameDelta, uint gameTimeMs)
        {
            if (IsBefore(gameTimeMs, _staleForwardSuppressUntilMs))
            {
                _staleForwardNoDisplacementTicks = 0;
                return;
            }

            var flags = _player.MovementFlags;
            var hasHorizontalIntent = (flags & STALE_RECOVERY_MOVEMENT_MASK) != MovementFlags.MOVEFLAG_NONE;
            var transientMotionState =
                flags.HasFlag(MovementFlags.MOVEFLAG_ONTRANSPORT)
                || flags.HasFlag(MovementFlags.MOVEFLAG_JUMPING)
                || flags.HasFlag(MovementFlags.MOVEFLAG_FALLINGFAR);

            if (!hasHorizontalIntent || transientMotionState)
            {
                _staleForwardNoDisplacementTicks = 0;
                // Track last-known-good position when moving freely
                if (frameDelta >= STALE_FORWARD_DISPLACEMENT_EPSILON)
                    _lastKnownGoodPosition = new Position(_player.Position.X, _player.Position.Y, _player.Position.Z);
                return;
            }

            if (frameDelta >= STALE_FORWARD_DISPLACEMENT_EPSILON)
            {
                // Making progress — update last-known-good and reset stuck level counter
                _staleForwardNoDisplacementTicks = 0;
                _consecutiveStuckLevels = 0;
                _lastKnownGoodPosition = new Position(_player.Position.X, _player.Position.Y, _player.Position.Z);
                return;
            }

            _staleForwardNoDisplacementTicks++;
            if (_staleForwardNoDisplacementTicks < STALE_FORWARD_NO_DISPLACEMENT_THRESHOLD)
                return;

            // Stuck threshold reached — escalate recovery level
            _consecutiveStuckLevels++;
            var stuckLevel = Math.Min(_consecutiveStuckLevels, 3);
            var stuckFlags = flags;
            _staleForwardNoDisplacementTicks = 0;
            _staleForwardRecoveryCount++;

            switch (stuckLevel)
            {
                case 1:
                    // Level 1: Clear path and stop movement. Caller should replan.
                    Log.Warning("[MovementController][STUCK-L1] Stale forward: clearing path, stopping (recoveries={Count}, frameDelta={Delta:F3})",
                        _staleForwardRecoveryCount, frameDelta);
                    _velocity = Vector3.Zero;
                    _fallTimeMs = 0;
                    _pendingDepen = Vector3.Zero;
                    _standingOnInstanceId = 0;
                    _standingOnLocal = Vector3.Zero;
                    _currentPath = null;
                    _currentWaypointIndex = 0;
                    _player.MovementFlags = MovementFlags.MOVEFLAG_NONE;
                    _lastSentFlags = stuckFlags;
                    _forceStopAfterReset = _lastSentFlags != MovementFlags.MOVEFLAG_NONE;
                    _lastPacketTime = 0;
                    _staleForwardSuppressUntilMs = AddMs(gameTimeMs, STALE_FORWARD_SUPPRESS_AFTER_RECOVERY_MS);
                    OnStuckRecoveryRequired?.Invoke(1, new Position(_player.Position.X, _player.Position.Y, _player.Position.Z));
                    break;

                case 2:
                    // Level 2: Corridor reset — warp waypoint index to nearest waypoint without
                    // clearing the entire path. Gives the bot a fresh waypoint target without a
                    // full replan, which can resolve minor path-tracking divergence near walls.
                    Log.Warning("[MovementController][STUCK-L2] Stale forward: corridor reset (path len={Len}, index={Idx})",
                        _currentPath?.Length ?? 0, _currentWaypointIndex);
                    if (_currentPath != null && _currentPath.Length > 0)
                    {
                        // Find nearest waypoint ahead of current index (don't go backward)
                        var nearestIdx = _currentWaypointIndex;
                        var nearestDist = float.MaxValue;
                        for (int idx = _currentWaypointIndex; idx < _currentPath.Length; idx++)
                        {
                            var wp = _currentPath[idx];
                            var d = HorizontalDistance(_player.Position.X, _player.Position.Y, wp.X, wp.Y);
                            if (d < nearestDist) { nearestDist = d; nearestIdx = idx; }
                        }
                        _currentWaypointIndex = nearestIdx;
                        Log.Warning("[MovementController][STUCK-L2] Reset waypoint index to {Idx} (dist={Dist:F1}y)", nearestIdx, nearestDist);
                    }
                    _staleForwardSuppressUntilMs = AddMs(gameTimeMs, STALE_FORWARD_SUPPRESS_L2_MS);
                    OnStuckRecoveryRequired?.Invoke(2, new Position(_player.Position.X, _player.Position.Y, _player.Position.Z));
                    break;

                default:
                    // Level 3+: Signal caller to perform higher-level recovery (strafe, respawn, GM unstuck).
                    Log.Warning("[MovementController][STUCK-L3] Stale forward: escalated to caller (level={Level}). LastGoodPos=({X:F1},{Y:F1},{Z:F1})",
                        stuckLevel, _lastKnownGoodPosition.X, _lastKnownGoodPosition.Y, _lastKnownGoodPosition.Z);
                    _velocity = Vector3.Zero;
                    _pendingDepen = Vector3.Zero;
                    _currentPath = null;
                    _currentWaypointIndex = 0;
                    _player.MovementFlags = MovementFlags.MOVEFLAG_NONE;
                    _lastSentFlags = stuckFlags;
                    _forceStopAfterReset = _lastSentFlags != MovementFlags.MOVEFLAG_NONE;
                    _lastPacketTime = 0;
                    _staleForwardSuppressUntilMs = AddMs(gameTimeMs, STALE_FORWARD_SUPPRESS_AFTER_RECOVERY_MS);
                    OnStuckRecoveryRequired?.Invoke(stuckLevel, _lastKnownGoodPosition);
                    break;
            }
        }

        private Opcode DetermineOpcode(MovementFlags current, MovementFlags previous)
        {
            bool isAirborne = (current & (MovementFlags.MOVEFLAG_FALLINGFAR | MovementFlags.MOVEFLAG_JUMPING)) != 0;

            // Stopped moving entirely
            if (current == MovementFlags.MOVEFLAG_NONE && previous != MovementFlags.MOVEFLAG_NONE)
                return Opcode.MSG_MOVE_STOP;

            // Started jumping
            if (current.HasFlag(MovementFlags.MOVEFLAG_JUMPING) && !previous.HasFlag(MovementFlags.MOVEFLAG_JUMPING))
                return Opcode.MSG_MOVE_JUMP;

            // Landed (FALLINGFAR→grounded also counts as landing)
            if (!current.HasFlag(MovementFlags.MOVEFLAG_JUMPING) && previous.HasFlag(MovementFlags.MOVEFLAG_JUMPING))
                return Opcode.MSG_MOVE_FALL_LAND;
            if (!current.HasFlag(MovementFlags.MOVEFLAG_FALLINGFAR) && previous.HasFlag(MovementFlags.MOVEFLAG_FALLINGFAR)
                && !current.HasFlag(MovementFlags.MOVEFLAG_JUMPING))
                return Opcode.MSG_MOVE_FALL_LAND;

            // Started/stopped swimming
            if (current.HasFlag(MovementFlags.MOVEFLAG_SWIMMING) && !previous.HasFlag(MovementFlags.MOVEFLAG_SWIMMING))
                return Opcode.MSG_MOVE_START_SWIM;
            if (!current.HasFlag(MovementFlags.MOVEFLAG_SWIMMING) && previous.HasFlag(MovementFlags.MOVEFLAG_SWIMMING))
                return Opcode.MSG_MOVE_STOP_SWIM;

            // While airborne, never send directional START/STOP opcodes — use heartbeat only.
            // Directional flag transitions mid-air are illegal and trigger anticheat.
            if (isAirborne)
                return Opcode.MSG_MOVE_HEARTBEAT;

            // Started moving forward
            if (current.HasFlag(MovementFlags.MOVEFLAG_FORWARD) && !previous.HasFlag(MovementFlags.MOVEFLAG_FORWARD))
                return Opcode.MSG_MOVE_START_FORWARD;

            // Started moving backward
            if (current.HasFlag(MovementFlags.MOVEFLAG_BACKWARD) && !previous.HasFlag(MovementFlags.MOVEFLAG_BACKWARD))
                return Opcode.MSG_MOVE_START_BACKWARD;

            // Started strafing
            if (current.HasFlag(MovementFlags.MOVEFLAG_STRAFE_LEFT) && !previous.HasFlag(MovementFlags.MOVEFLAG_STRAFE_LEFT))
                return Opcode.MSG_MOVE_START_STRAFE_LEFT;
            if (current.HasFlag(MovementFlags.MOVEFLAG_STRAFE_RIGHT) && !previous.HasFlag(MovementFlags.MOVEFLAG_STRAFE_RIGHT))
                return Opcode.MSG_MOVE_START_STRAFE_RIGHT;

            // Stopped strafing (while still moving)
            if (!current.HasFlag(MovementFlags.MOVEFLAG_STRAFE_LEFT) && !current.HasFlag(MovementFlags.MOVEFLAG_STRAFE_RIGHT)
                && (previous.HasFlag(MovementFlags.MOVEFLAG_STRAFE_LEFT) || previous.HasFlag(MovementFlags.MOVEFLAG_STRAFE_RIGHT)))
                return Opcode.MSG_MOVE_STOP_STRAFE;

            // Default to heartbeat
            return Opcode.MSG_MOVE_HEARTBEAT;
        }

        // ======== SPECIAL PACKETS ========
        public void SendFacingUpdate(uint gameTimeMs)
        {
            // Called by bot when it changes facing directly
            var buffer = MovementPacketHandler.BuildMovementInfoBuffer(_player, gameTimeMs, _fallTimeMs);
            _ = _client.SendMovementOpcodeAsync(Opcode.MSG_MOVE_SET_FACING, buffer);
            Log.Debug("[MovementController] MSG_MOVE_SET_FACING Facing={Facing:F2}", _player.Facing);
        }

        public void SendStopPacket(uint gameTimeMs)
        {
            // Clear directional intent while preserving active physics state such as falling/swimming.
            // This prevents stop requests from cancelling airborne state after shoreline overruns.
            _player.MovementFlags = ClearForcedStopIntent(_player.MovementFlags);
            var opcode = DetermineOpcode(_player.MovementFlags, _lastSentFlags);
            var buffer = MovementPacketHandler.BuildMovementInfoBuffer(_player, gameTimeMs, _fallTimeMs);
            _ = _client.SendMovementOpcodeAsync(opcode, buffer);
            _lastSentFlags = _player.MovementFlags;
            _forceStopAfterReset = false;
            _staleForwardNoDisplacementTicks = 0;
            _staleForwardSuppressUntilMs = AddMs(gameTimeMs, STALE_FORWARD_SUPPRESS_AFTER_RECOVERY_MS);
            Log.Debug("[MovementController] {Opcode} (forced stop) flags=0x{Flags:X}", opcode, (uint)_player.MovementFlags);
        }

        private static MovementFlags ClearForcedStopIntent(MovementFlags flags)
            => flags & ~FORCED_STOP_CLEAR_MASK;

        // ======== PATH FOLLOWING ========

        /// <summary>
        /// Sets a navigation path for the controller to follow.
        /// The controller will interpolate Z from waypoint heights and auto-advance waypoints.
        /// </summary>
        public void SetPath(Position[] path)
        {
            if (path == null || path.Length == 0)
            {
                _currentPath = null;
                _currentWaypointIndex = 0;
                return;
            }
            _currentPath = path;
            // Always start from waypoint 0; callers may provide paths that do not include current position.
            _currentWaypointIndex = 0;
            Log.Debug("[MovementController] Path set: {Count} waypoints, starting at index {Idx}", path.Length, _currentWaypointIndex);
        }

        /// <summary>
        /// Sets a single target waypoint (convenience for MoveToward calls).
        /// </summary>
        public void SetTargetWaypoint(Position target)
        {
            if (target == null)
            {
                _currentPath = null;
                _currentWaypointIndex = 0;
                return;
            }

            // Corpse-run and nav-driven loops call this every tick.
            // Skip rebuilding a one-segment path when the target is effectively unchanged.
            if (_currentPath != null
                && _currentPath.Length == 2
                && _currentWaypointIndex == 1)
            {
                var existingTarget = _currentPath[1];
                var targetDistance2D = HorizontalDistance(existingTarget.X, existingTarget.Y, target.X, target.Y);
                var targetDeltaZ = MathF.Abs(existingTarget.Z - target.Z);
                if (targetDistance2D <= TARGET_WAYPOINT_REFRESH_DIST_2D
                    && targetDeltaZ <= TARGET_WAYPOINT_REFRESH_Z)
                {
                    return;
                }
            }

            _currentPath = [new Position(_player.Position.X, _player.Position.Y, _player.Position.Z), target];
            _currentWaypointIndex = 1;
        }

        /// <summary>
        /// Clears the current path.
        /// </summary>
        public void ClearPath()
        {
            _currentPath = null;
            _currentWaypointIndex = 0;
        }

        /// <summary>
        /// Returns the current target waypoint, or null if no path is set.
        /// </summary>
        public Position? CurrentWaypoint =>
            _currentPath != null && _currentWaypointIndex < _currentPath.Length
                ? _currentPath[_currentWaypointIndex]
                : null;

        /// <summary>
        /// Interpolates Z height based on progress between the previous waypoint and current target.
        /// Uses the path waypoint Z values which come from the navmesh (correct ground height).
        /// </summary>
        private float InterpolatePathZ(float newX, float newY, float currentZ)
        {
            if (_currentPath == null || _currentWaypointIndex >= _currentPath.Length)
                return currentZ;

            var target = _currentPath[_currentWaypointIndex];

            // Get the previous waypoint (or use current path start)
            var prev = _currentWaypointIndex > 0
                ? _currentPath[_currentWaypointIndex - 1]
                : new Position(_player.Position.X, _player.Position.Y, _player.Position.Z);

            float totalDistXY = HorizontalDistance(prev.X, prev.Y, target.X, target.Y);
            if (totalDistXY < 0.5f)
                return target.Z; // Very close waypoints, just snap Z

            float currentDistXY = HorizontalDistance(newX, newY, target.X, target.Y);
            float t = 1.0f - MathF.Min(currentDistXY / totalDistXY, 1.0f);
            t = MathF.Max(0, t);

            return prev.Z + t * (target.Z - prev.Z);
        }

        /// <summary>
        /// Checks if we've arrived at the current waypoint and advances to the next one.
        /// Also updates facing toward the next waypoint.
        /// </summary>
        private void AdvanceWaypointIfNeeded(float posX, float posY)
        {
            if (_currentPath == null || _currentWaypointIndex >= _currentPath.Length)
                return;

            var wp = _currentPath[_currentWaypointIndex];
            float dist = HorizontalDistance(posX, posY, wp.X, wp.Y);

            if (dist <= WAYPOINT_ARRIVE_DIST)
            {
                _currentWaypointIndex++;
                if (_currentWaypointIndex < _currentPath.Length)
                {
                    // Update facing toward next waypoint
                    var next = _currentPath[_currentWaypointIndex];
                    _player.Facing = MathF.Atan2(next.Y - posY, next.X - posX);
                    Log.Debug("[MovementController] Advanced to waypoint {Idx}/{Total} ({X:F0},{Y:F0},{Z:F0})",
                        _currentWaypointIndex, _currentPath.Length, next.X, next.Y, next.Z);
                }
                else
                {
                    Log.Debug("[MovementController] Reached end of path");
                }
            }
        }

        private static float HorizontalDistance(float x1, float y1, float x2, float y2)
        {
            float dx = x2 - x1;
            float dy = y2 - y1;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        // ======== STATE MANAGEMENT ========
        public void Reset(float teleportDestZ = float.NaN)
        {
            // Reset physics state (after teleport, death, etc)
            var preResetFlags = _player.MovementFlags;
            var hadMovementToClear = _lastSentFlags != MovementFlags.MOVEFLAG_NONE || preResetFlags != MovementFlags.MOVEFLAG_NONE;
            var stopSeedFlags = _lastSentFlags != MovementFlags.MOVEFLAG_NONE
                ? _lastSentFlags
                : preResetFlags;

            _velocity = Vector3.Zero;
            _fallTimeMs = 0;
            _player.MovementFlags = MovementFlags.MOVEFLAG_NONE;
            _lastSentFlags = hadMovementToClear ? stopSeedFlags : MovementFlags.MOVEFLAG_NONE;
            _forceStopAfterReset = hadMovementToClear;
            // Sync packet tracking to current position immediately.
            // For login (OnLoginVerifyWorld), position is already set before Reset().
            // For teleport (NotifyTeleportIncoming), position may be pre-teleport here,
            // but _needsPacketPositionSync corrects it on the next Update() after the
            // teleport destination is written to _player.Position.
            _lastPacketPosition = new Vector3(_player.Position.X, _player.Position.Y, _player.Position.Z);
            _lastPacketTime = _latestGameTimeMs;
            _needsPacketPositionSync = true;

            _prevGroundZ = _player.Position.Z;
            _prevGroundNormal = new Vector3(0, 0, 1);
            _pendingDepen = Vector3.Zero;
            _standingOnInstanceId = 0;
            _standingOnLocal = Vector3.Zero;
            _stepUpBaseZ = -200000f;
            _stepUpAge = 0;
            _descentAnchorZ = float.NaN;
            _descentAnchorX = float.NaN;
            _descentAnchorY = float.NaN;
            _falseFreefallCount = 0;
            _ffsStartZ = float.NaN;
            _hasPhysicsGroundContact = false;

            // After teleport/zone change, force at least one physics step even while idle
            // so gravity applies and the character snaps to the real ground height.
            // Use the packet's destination Z when provided — _player.Position.Z still holds
            // the pre-teleport value at the time Reset() is called (position is written AFTER).
            _needsGroundSnap = true;
            _groundSnapFrames = 0;
            _teleportZ = float.IsNaN(teleportDestZ) ? _player.Position.Z : teleportDestZ;
            _teleportZGraceFrames = 0; // Reset grace countdown on new teleport
            _teleportClampFrames = 0;
            _noGroundFrameCount = 0;

            _currentPath = null;
            _currentWaypointIndex = 0;
            _staleForwardNoDisplacementTicks = 0;
            _staleForwardSuppressUntilMs = AddMs(_latestGameTimeMs, STALE_FORWARD_SUPPRESS_AFTER_RESET_MS);

            if (_forceStopAfterReset)
            {
                Log.Information("[MovementController] Reset scheduled stop packet to clear stale movement flags (seed=0x{Flags:X})",
                    (uint)_lastSentFlags);
            }
        }
    }
}
