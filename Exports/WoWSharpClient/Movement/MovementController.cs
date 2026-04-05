using GameData.Core.Enums;
using GameData.Core.Models;
using Pathfinding;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using WoWSharpClient.Client;
using WoWSharpClient.Models;
using WoWSharpClient.Parsers;

namespace WoWSharpClient.Movement
{
    public class MovementController(WoWClient client, WoWLocalPlayer player, SceneDataClient? sceneDataClient = null)
    {
        private readonly WoWClient _client = client;
        private readonly WoWLocalPlayer _player = player;
        private readonly SceneDataClient? _sceneDataClient = sceneDataClient;
        private readonly bool _nativeSceneModeConfigured = ConfigureNativeSceneMode(sceneDataClient);
        // Physics state
        private Vector3 _velocity = Vector3.Zero;
        // Keep fall time in milliseconds to match WoW movement packet expectations.
        // PhysicsInput.fall_time in the proto is a float, but we track ms locally to avoid unit drift.
        private uint _fallTimeMs = 0;

        // StepV2 continuity state (must be round-tripped each tick).
        // Binary parity note: static ground is re-derived from collision every frame;
        // the standingOn token is only meaningful for moving-base continuity.
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

        // Removed: _teleportClampFrames, TELEPORT_CLAMP_MAX_FRAMES, _teleportZGraceFrames,
        // _noGroundFrameCount — workarounds that broke binary parity. Restored original
        // ground snap logic from parity baseline (70c72973).

        // Network timing
        private uint _lastPacketTime;
        private MovementFlags _lastSentFlags = player.MovementFlags;
        private bool _forceStopAfterReset;
        // When true, sync _lastPacketPosition to current player position on next Update().
        // Needed because Reset() runs before the teleport position is applied to _player.
        private bool _needsPacketPositionSync;
        // Packet-backed FG traces show the local client emits movement heartbeats on an
        // approximately 500ms cadence while moving, with state-change packets in between.
        private const uint PACKET_INTERVAL_MS = 500;
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

        private static bool ConfigureNativeSceneMode(SceneDataClient? sceneDataClient)
        {
            // Do NOT enable scene slice mode eagerly — it prevents local VMAP loading
            // which is needed for ground detection until the SceneDataService actually
            // delivers scene data for the current map. Let the BG bot load local terrain
            // data as a fallback; scene slices will be injected on top when they arrive.
            NativeLocalPhysics.SetSceneSliceMode(false);

            return true;
        }

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

            // Consume pending knockback impulse from SMSG_MOVE_KNOCK_BACK.
            // Must happen before any early exit so knockback is never dropped.
            if (WoWSharpObjectManager.Instance?.TryConsumePendingKnockback(out float kbVx, out float kbVy, out float kbVz) == true)
            {
                _velocity = new Vector3(kbVx, kbVy, kbVz);
                _fallTimeMs = 0;
                // FALLINGFAR is already set by the event handler; ensure FORWARD/BACKWARD cleared
                _player.MovementFlags |= MovementFlags.MOVEFLAG_FALLINGFAR;
                _player.MovementFlags &= ~(MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_BACKWARD
                    | MovementFlags.MOVEFLAG_STRAFE_LEFT | MovementFlags.MOVEFLAG_STRAFE_RIGHT);
                Log.Information("[MovementController] Applied knockback impulse vel=({VelX:F2},{VelY:F2},{VelZ:F2})",
                    kbVx, kbVy, kbVz);
            }

            // Idle guard: skip physics when no movement intent, no pending ground snap,
            // and not auto-attacking. Prevents unnecessary airborne detection on idle frames.
            // This was present in the 100% parity commit (70c72973) and was accidentally removed.
            if (_lastSentFlags == MovementFlags.MOVEFLAG_NONE
                && _player.MovementFlags == MovementFlags.MOVEFLAG_NONE
                && !_needsGroundSnap
                && !_player.IsAutoAttacking)
            {
                CapturePhysicsFrameRecord(output: null, deltaSec: deltaSec, gameTimeMs: gameTimeMs);
                return;
            }

            // Suppress all movement during channeling or casting. MaNGOS interprets
            // any movement packet (including idle heartbeats) as a channel interrupt.
            // This prevents crafting, fishing, and other channeled spells from being
            // cancelled by physics-generated position updates.
            if (_player.IsChanneling || _player.IsCasting)
            {
                CapturePhysicsFrameRecord(output: null, deltaSec: deltaSec, gameTimeMs: gameTimeMs);
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
                CapturePhysicsFrameRecord(output: null, deltaSec: deltaSec, gameTimeMs: gameTimeMs);
                return;
            }
            ApplyPhysicsResult(physicsResult, deltaSec);
            var newPhysicsPos = new Vector3(physicsResult.NewPosX, physicsResult.NewPosY, physicsResult.NewPosZ);
            var frameDelta = (newPhysicsPos - _lastPhysicsPosition).Length();
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

            CapturePhysicsFrameRecord(physicsResult, deltaSec, gameTimeMs);
        }

        // ======== PHYSICS ========
        private PhysicsOutput RunPhysics(float deltaSec)
        {
            // Always run physics, even when MOVEFLAG_NONE. BG bots can be moved
            // externally or lose support while idle, and a synthetic grounded
            // result prevents the fall/settle sweep from ever happening.

            if (_player.TransportGuid != _lastTransportGuid)
            {
                ResetTransportContinuity();
                _lastTransportGuid = _player.TransportGuid;
            }

            var physicsPosition = _player.Position;
            var physicsFacing = _player.Facing;
            WoWGameObject? activeTransport = null;
            if (TryGetActiveTransport(out activeTransport))
            {
                _player.TransportOffset = TransportCoordinateHelper.WorldToLocal(
                    _player.Position,
                    activeTransport.Position,
                    activeTransport.Facing);
                _player.TransportOrientation = TransportCoordinateHelper.WorldToLocalFacing(
                    _player.Facing,
                    activeTransport.Facing);

                physicsPosition = _player.TransportOffset;
                physicsFacing = _player.TransportOrientation;
            }

            // Build physics input from current player state
            var input = new PhysicsInput
            {
                DeltaTime = deltaSec,  // Physics expects seconds, not milliseconds
                MapId = _player.MapId,
                MovementFlags = (uint)_player.MovementFlags,

                PosX = physicsPosition.X,
                PosY = physicsPosition.Y,
                PosZ = physicsPosition.Z,
                Facing = physicsFacing,
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

                // Binary parity: CMovement grounded state persistence
                WasGrounded = _wasGroundedLastFrame,
            };

            input.NearbyObjects.Add(BuildPhysicsNearbyObjects(_player.Position, activeTransport));

            // Save the position and flags we're sending to C++ for race-detection and diagnostics.
            // If a teleport modifies _player.Position during the gRPC call,
            // ApplyPhysicsResult will detect the divergence and discard the stale result.
            _physicsInputX = input.PosX;
            _physicsInputY = input.PosY;
            _physicsInputZ = input.PosZ;
            _physicsInputFlags = input.MovementFlags;

            // Physics is always local — NativeLocalPhysics.Step calls Navigation.dll directly.
            // No remote fallback. WoW.exe runs physics locally; so do we.
            _ = EnsureLocalSceneDataFresh();
            return NativeLocalPhysics.Step(input);
        }

        private bool EnsureLocalSceneDataFresh()
        {
            if (_sceneDataClient == null)
                return true;

            if (_lastSceneRefreshMapId != _player.MapId)
            {
                if (_lastSceneRefreshMapId != uint.MaxValue)
                    NativeLocalPhysics.ClearSceneCache(_lastSceneRefreshMapId);

                _lastSceneRefreshMapId = _player.MapId;
                _lastSceneRefreshX = float.NaN;
                _lastSceneRefreshY = float.NaN;
            }

            float dx = _player.Position.X - _lastSceneRefreshX;
            float dy = _player.Position.Y - _lastSceneRefreshY;
            if (!float.IsNaN(_lastSceneRefreshX)
                && !float.IsNaN(_lastSceneRefreshY)
                && (dx * dx) + (dy * dy) <= SceneRefreshDistance * SceneRefreshDistance)
            {
                return true;
            }

            if (!_sceneDataClient.EnsureSceneDataAround(_player.MapId, _player.Position.X, _player.Position.Y))
            {
                Log.Warning("[MovementController] SceneData refresh failed for map {MapId} at ({X:F1},{Y:F1})",
                    _player.MapId, _player.Position.X, _player.Position.Y);
                return false;
            }

            _lastSceneRefreshX = _player.Position.X;
            _lastSceneRefreshY = _player.Position.Y;
            return true;
        }
        private void ResetTransportContinuity()
        {
            _prevGroundZ = _player.Position.Z;
            _prevGroundNormal = new Vector3(0, 0, 1);
            _pendingDepen = Vector3.Zero;
            _standingOnInstanceId = 0;
            _standingOnLocal = Vector3.Zero;
            _stepUpBaseZ = -200000f;
            _stepUpAge = 0;

            if (_player.TransportGuid == 0)
            {
                _player.TransportOffset = new Position(0, 0, 0);
                _player.TransportOrientation = 0f;
            }
        }

        private bool TryGetActiveTransport(out WoWGameObject? transport)
        {
            transport = null;
            if (_player.TransportGuid == 0)
                return false;

            if (_player.Transport is WoWGameObject cachedTransport &&
                cachedTransport.Guid == _player.TransportGuid &&
                cachedTransport.DisplayId != 0)
            {
                transport = cachedTransport;
            }
            else if (WoWSharpObjectManager.Instance.GetObjectByGuid(_player.TransportGuid) is WoWGameObject resolvedTransport)
            {
                transport = resolvedTransport;
            }

            if (transport != null)
                _player.Transport = transport;

            return transport != null;
        }

        private const float PhysicsNearbyObjectRadius = 40f;
        private const int MaxPhysicsNearbyObjectCount = 64;
        private static readonly GameObjectType[] PhysicsCollidableObjectTypes =
        [
            GameObjectType.Door,
            GameObjectType.Button,
            GameObjectType.Chest,
            GameObjectType.Generic,
            GameObjectType.Goober,
            GameObjectType.Transport,
            GameObjectType.MapObject,
            GameObjectType.MapObjectTransport,
            GameObjectType.Mailbox,
            GameObjectType.AuctionHouse,
            GameObjectType.SpellCaster,
            GameObjectType.MeetingStone,
            GameObjectType.FlagStand,
            GameObjectType.FlagDrop,
            GameObjectType.CapturePoint,
            GameObjectType.DestructibleBuilding,
            GameObjectType.GuildBank,
            GameObjectType.TrapDoor
        ];

        private DynamicObjectProto[] BuildPhysicsNearbyObjects(Position referencePosition, WoWGameObject? activeTransport)
        {
            var maxRangeSq = PhysicsNearbyObjectRadius * PhysicsNearbyObjectRadius;
            var nearbyObjects = new List<(int priority, float distanceSq, DynamicObjectProto proto)>();
            var seenGuids = new HashSet<ulong>();

            void AddGameObject(WoWGameObject gameObject, bool forceInclude)
            {
                var position = gameObject.Position;
                if (gameObject.Guid == 0
                    || position == null
                    || !IsFinitePosition(position)
                    || !float.IsFinite(gameObject.Facing)
                    || !seenGuids.Add(gameObject.Guid))
                {
                    return;
                }

                float dx = position.X - referencePosition.X;
                float dy = position.Y - referencePosition.Y;
                float dz = position.Z - referencePosition.Z;
                float distSq = dx * dx + dy * dy + dz * dz;

                if (!forceInclude)
                {
                    if (gameObject.DisplayId == 0
                        || !IsPhysicsCollidableType(gameObject.TypeId)
                        || distSq > maxRangeSq)
                    {
                        return;
                    }
                }

                nearbyObjects.Add((
                    priority: forceInclude ? 0 : 1,
                    distanceSq: distSq,
                    proto: new DynamicObjectProto
                    {
                        Guid = gameObject.Guid,
                        DisplayId = gameObject.DisplayId,
                        X = position.X,
                        Y = position.Y,
                        Z = position.Z,
                        Orientation = gameObject.Facing,
                        Scale = float.IsFinite(gameObject.ScaleX) && gameObject.ScaleX > 0f ? gameObject.ScaleX : 1f,
                        GoState = (uint)gameObject.GoState,
                    }));
            }

            if (activeTransport != null)
                AddGameObject(activeTransport, forceInclude: true);

            foreach (var gameObject in WoWSharpObjectManager.Instance.Objects.OfType<WoWGameObject>())
                AddGameObject(gameObject, forceInclude: gameObject.Guid == _player.TransportGuid);

            return nearbyObjects
                .OrderBy(candidate => candidate.priority)
                .ThenBy(candidate => candidate.distanceSq)
                .ThenBy(candidate => candidate.proto.Guid)
                .Take(MaxPhysicsNearbyObjectCount)
                .Select(candidate => candidate.proto)
                .ToArray();
        }

        private static bool IsPhysicsCollidableType(uint typeId)
            => Enum.IsDefined(typeof(GameObjectType), (int)typeId)
                && Array.IndexOf(PhysicsCollidableObjectTypes, (GameObjectType)typeId) >= 0;

        private static bool IsFinitePosition(Position position)
            => float.IsFinite(position.X)
                && float.IsFinite(position.Y)
                && float.IsFinite(position.Z);

        private int _deadReckonCount = 0;
        private float _prevOutX, _prevOutY, _prevOutZ;
        private float _prev2OutX, _prev2OutY, _prev2OutZ;
        // Race-detection: position that was sent to the C++ physics engine.
        // If _player.Position diverges from this after RunPhysics returns,
        // a teleport occurred during the gRPC call and the result is stale.
        private float _physicsInputX, _physicsInputY, _physicsInputZ;
        private uint _physicsInputFlags;
        // _noGroundFrameCount removed — workaround
        private int _falseFreefallCount = 0;
        private float _ffsStartZ = float.NaN;  // Z where FFS first engaged — tracks cumulative drift
        // Track whether _prevGroundZ was established from actual physics ground contact
        // (not just constructor initialization from player.Position.Z). The hysteresis guard
        // must NOT engage until we've confirmed real ground contact — otherwise elevated spawns
        // cause _prevGroundZ = spawnZ, making the guard think we're "close to ground" at altitude.
        private bool _hasPhysicsGroundContact = false;
        // Binary parity: CMovement grounded state persistence across frames.
        // Set from physics output each frame, sent as input to next frame.
        private bool _wasGroundedLastFrame = true;

        /// <summary>
        /// Override the physics-derived grounded state for testing.
        /// In production, this is set by ApplyPhysicsResult from physics output.
        /// </summary>
        public void SetGroundedState(bool grounded) => _wasGroundedLastFrame = grounded;
        // _teleportZGraceFrames removed — workaround
        private const int TELEPORT_Z_GRACE_DURATION = 30; // ~1 second at 30 FPS
        private int _physicsMovedCount = 0;
        private ulong _lastTransportGuid = player.TransportGuid;
        private uint _lastSceneRefreshMapId = uint.MaxValue;
        private float _lastSceneRefreshX = float.NaN;
        private float _lastSceneRefreshY = float.NaN;
        private const float SceneRefreshDistance = 100f;
        // Per-frame packet tracking for recording — set by SendMovementPacket,
        // captured by the recording in the NEXT frame's physics tick, then reset.
        private uint _frameSentOpcode = 0;
        private uint _frameSentFlags = 0;
        private float _frameSentFacing = 0;
        private bool _frameSentPending = false;
        private bool _stopWhenGrounded = false;

        /// <summary>
        /// Applies physics output to player state. WoW.exe-style: trust the physics
        /// engine completely. No Z clamping, no dead reckoning, no slope guards,
        /// no teleport Z clamp, no grounded→falling hysteresis.
        /// The AABB CollisionStepWoW provides correct positions directly.
        /// </summary>
        private void ApplyPhysicsResult(PhysicsOutput output, float deltaSec)
        {
            // Wall contact feedback for path layer
            LastHitWall = output.HitWall;
            LastWallNormal = new Vector3(output.WallNormalX, output.WallNormalY, output.WallNormalZ);
            LastBlockedFraction = output.BlockedFraction;

            // Apply position directly from physics — no guards, no clamping
            _player.Position = new Position(output.NewPosX, output.NewPosY, output.NewPosZ);
            _player.Facing = output.Orientation;
            _player.SwimPitch = output.Pitch;

            // Apply velocity
            _velocity = new Vector3(output.NewVelX, output.NewVelY, output.NewVelZ);

            // Fall time: trust physics output
            bool isFalling = (output.MovementFlags & (uint)(MovementFlags.MOVEFLAG_FALLINGFAR | MovementFlags.MOVEFLAG_JUMPING)) != 0;
            _fallTimeMs = isFalling ? (uint)MathF.Max(0, output.FallTime) : 0;

            // Binary parity: persist grounded state for next frame's input
            _wasGroundedLastFrame = !isFalling;

            // Persist StepV2 continuity outputs
            bool physicsHasGround = output.GroundZ > -50000f;
            bool outputIsGrounded = !isFalling;
            if (physicsHasGround && outputIsGrounded)
            {
                _prevGroundZ = output.GroundZ;
                _prevGroundNormal = new Vector3(output.GroundNx, output.GroundNy, output.GroundNz);
                _hasPhysicsGroundContact = true;
            }
            _pendingDepen = new Vector3(output.PendingDepenX, output.PendingDepenY, output.PendingDepenZ);
            _standingOnInstanceId = output.StandingOnInstanceId;
            _standingOnLocal = new Vector3(output.StandingOnLocalX, output.StandingOnLocalY, output.StandingOnLocalZ);

            // Apply physics state flags directly — no hysteresis, no suppression
            const MovementFlags PhysicsFlags =
                MovementFlags.MOVEFLAG_JUMPING |
                MovementFlags.MOVEFLAG_SWIMMING |
                MovementFlags.MOVEFLAG_FLYING |
                MovementFlags.MOVEFLAG_LEVITATING |
                MovementFlags.MOVEFLAG_FALLINGFAR;

            var inputFlags = _player.MovementFlags & ~PhysicsFlags;
            var newPhysicsFlags = (MovementFlags)(output.MovementFlags) & PhysicsFlags;
            _player.MovementFlags = inputFlags | newPhysicsFlags;

            if (_stopWhenGrounded && !isFalling)
            {
                _player.MovementFlags = ClearForcedStopIntent(_player.MovementFlags);
                _stopWhenGrounded = false;
            }

            if (TryGetActiveTransport(out var activeTransport))
            {
                _player.TransportOffset = TransportCoordinateHelper.WorldToLocal(
                    _player.Position,
                    activeTransport.Position,
                    activeTransport.Facing);
                _player.TransportOrientation = TransportCoordinateHelper.WorldToLocalFacing(
                    _player.Facing,
                    activeTransport.Facing);
            }
            else if (_player.TransportGuid == 0)
            {
                _player.TransportOffset = new Position(0, 0, 0);
                _player.TransportOrientation = 0f;
            }

            // Advance waypoint if arrived
            AdvanceWaypointIfNeeded(output.NewPosX, output.NewPosY);
        }

        private void CapturePhysicsFrameRecord(PhysicsOutput? output, float deltaSec, uint gameTimeMs)
        {
            if (!IsRecording)
            {
                ClearFramePacketRecordState();
                return;
            }

            if (_recordedFrames.Count >= MAX_RECORDED_FRAMES)
                _recordedFrames.RemoveAt(0);

            float posZ = _player.Position.Z;
            float prevPosZ = _recordedFrames.Count > 0 ? _recordedFrames[^1].PosZ : posZ;
            bool isFalling = (_player.MovementFlags & (MovementFlags.MOVEFLAG_FALLINGFAR | MovementFlags.MOVEFLAG_JUMPING)) != 0;
            float currentWaypointZ = float.NaN;
            if (_currentPath != null &&
                _currentWaypointIndex >= 0 &&
                _currentWaypointIndex < _currentPath.Length)
            {
                currentWaypointZ = _currentPath[_currentWaypointIndex].Z;
            }

            uint packetOpcode = _frameSentPending ? _frameSentOpcode : 0;
            uint packetFlags = _frameSentPending ? _frameSentFlags : 0;
            float packetFacing = _frameSentPending ? _frameSentFacing : 0f;

            _recordedFrames.Add(new PhysicsFrameRecord
            {
                FrameNumber = _frameCounter,
                GameTimeMs = gameTimeMs,
                DeltaSec = deltaSec,
                PosX = _player.Position.X,
                PosY = _player.Position.Y,
                PosZ = posZ,
                RawPosZ = output?.NewPosZ ?? posZ,
                PhysicsGroundZ = output?.GroundZ ?? _prevGroundZ,
                PrevGroundZ = _prevGroundZ,
                HasPhysicsGroundContact = _hasPhysicsGroundContact,
                VelX = _velocity.X,
                VelY = _velocity.Y,
                VelZ = _velocity.Z,
                FallTimeMs = _fallTimeMs,
                IsFalling = isFalling,
                MovementFlags = (uint)_player.MovementFlags,
                SlopeGuardRejected = false,
                PathGroundGuardActive = false,
                FalseFreefallSuppressed = false,
                TeleportClampActive = false,
                UndergroundSnapFired = false,
                HitWall = LastHitWall,
                WallNormalX = LastWallNormal.X,
                WallNormalY = LastWallNormal.Y,
                BlockedFraction = LastBlockedFraction,
                PathWaypointZ = currentWaypointZ,
                PathWaypointIndex = _currentWaypointIndex,
                ZDeltaFromPrev = posZ - prevPosZ,
                PrevGroundNx = _prevGroundNormal.X,
                PrevGroundNy = _prevGroundNormal.Y,
                PrevGroundNz = _prevGroundNormal.Z,
                PacketOpcode = packetOpcode,
                PacketFlags = packetFlags,
                PacketFacing = packetFacing,
            });

            ClearFramePacketRecordState();
        }

        private void ClearFramePacketRecordState()
        {
            _frameSentOpcode = 0;
            _frameSentFlags = 0;
            _frameSentFacing = 0f;
            _frameSentPending = false;
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
        public void SendMovementStartFacingUpdate(uint gameTimeMs)
        {
            var buffer = MovementPacketHandler.BuildMovementInfoBuffer(_player, gameTimeMs, _fallTimeMs);
            _client.SendMovementOpcodeAsync(Opcode.MSG_MOVE_SET_FACING, buffer).GetAwaiter().GetResult();

            _frameSentOpcode = (uint)Opcode.MSG_MOVE_SET_FACING;
            _frameSentFlags = (uint)_player.MovementFlags;
            _frameSentFacing = _player.Facing;
            _frameSentPending = true;

            _lastSentFlags = _player.MovementFlags;
            _lastPacketTime = gameTimeMs;
            _lastPacketPosition = new Vector3(_player.Position.X, _player.Position.Y, _player.Position.Z);

            Log.Information("[MovementController] MSG_MOVE_SET_FACING (movement start) Facing={Facing:F2} Pos=({X:F1},{Y:F1},{Z:F1})",
                _player.Facing, _player.Position.X, _player.Position.Y, _player.Position.Z);
        }

        public void SendFacingUpdate(uint gameTimeMs)
        {
            var buffer = MovementPacketHandler.BuildMovementInfoBuffer(_player, gameTimeMs, _fallTimeMs);
            _client.SendMovementOpcodeAsync(Opcode.MSG_MOVE_SET_FACING, buffer).GetAwaiter().GetResult();

            _frameSentOpcode = (uint)Opcode.MSG_MOVE_SET_FACING;
            _frameSentFlags = (uint)_player.MovementFlags;
            _frameSentFacing = _player.Facing;
            _frameSentPending = true;

            _lastSentFlags = _player.MovementFlags;
            _lastPacketTime = gameTimeMs;
            _lastPacketPosition = new Vector3(_player.Position.X, _player.Position.Y, _player.Position.Z);
            Log.Information("[MovementController] MSG_MOVE_SET_FACING Facing={Facing:F2} Pos=({X:F1},{Y:F1},{Z:F1})",
                _player.Facing, _player.Position.X, _player.Position.Y, _player.Position.Z);
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

        internal void RequestGroundedStop()
        {
            _stopWhenGrounded = true;
        }

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
            // Don't assume grounded after teleport — the first physics frame will determine
            // the correct state. Setting true here prevents airborne tests from falling.
            // Setting false allows CollisionStepWoW to set grounded on the first frame
            // if ground is found within step height.
            _wasGroundedLastFrame = false;

            // After teleport/zone change, force at least one physics step even while idle
            // so gravity applies and the character snaps to the real ground height.
            // Use the packet's destination Z when provided — _player.Position.Z still holds
            // the pre-teleport value at the time Reset() is called (position is written AFTER).
            _needsGroundSnap = true;
            _groundSnapFrames = 0;
            _teleportZ = float.IsNaN(teleportDestZ) ? _player.Position.Z : teleportDestZ;
            // Ground snap state reset on new teleport

            _currentPath = null;
            _currentWaypointIndex = 0;
            _staleForwardNoDisplacementTicks = 0;
            _staleForwardSuppressUntilMs = AddMs(_latestGameTimeMs, STALE_FORWARD_SUPPRESS_AFTER_RESET_MS);
            _stopWhenGrounded = false;

            if (_forceStopAfterReset)
            {
                Log.Information("[MovementController] Reset scheduled stop packet to clear stale movement flags (seed=0x{Flags:X})",
                    (uint)_lastSentFlags);
            }
        }
    }
}
