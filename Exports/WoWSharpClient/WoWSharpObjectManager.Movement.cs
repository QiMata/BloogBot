using BotRunner.Clients;
using GameData.Core.Enums;
using GameData.Core.Frames;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using WoWSharpClient.Client;
using WoWSharpClient.Models;
using WoWSharpClient.Movement;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Parsers;
using WoWSharpClient.Screens;
using WoWSharpClient.Utils;
using static GameData.Core.Enums.UpdateFields;
using Enum = System.Enum;
using Timer = System.Timers.Timer;

namespace WoWSharpClient
{
    /// <summary>
    /// Movement input handling, facing, speed ACKs, teleport ACKs, knockback, and physics recording.
    /// KEPT AS PARTIAL: Deeply coupled with the main class's movement state (_isInControl,
    /// _isBeingTeleported, _movementController, _worldTimeTracker, _woWClient). Every method
    /// reads and writes multiple fields atomically. Extracting would require exposing 10+
    /// mutable fields or creating a movement-state struct — high risk for a ~780-line file.
    /// </summary>
    public partial class WoWSharpObjectManager
    {
        public bool IsPlayerMoving => !Player.MovementFlags.Equals(MovementFlags.MOVEFLAG_NONE);

        /// <summary>
        /// True when the character is on a taxi flight path.
        /// In 1.12.1, taxi rides set MOVEFLAG_ONTRANSPORT with a non-zero transport GUID.
        /// The flight master component can also set this directly.
        /// </summary>
        public bool IsInFlight
        {
            get => _isInFlight || (Player != null
                && Player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_ONTRANSPORT)
                && Player.TransportGuid != 0);
            set => _isInFlight = value;
        }
        private bool _isInFlight;

        private bool _isInControl = false;
        private const float FacingPacketThresholdRadians = 0.1f; // WoW.exe 0x80C408

        private bool _isBeingTeleported = true;

        /// <summary>
        /// True when BG bot is in a map transition (teleport in progress).
        /// Overrides the IObjectManager default (false) so snapshots report real state.
        /// </summary>
        public bool IsInMapTransition => _isBeingTeleported;

        private long _teleportFlagSetTicks;  // Stopwatch.GetTimestamp() when _isBeingTeleported was last set true

        private uint _teleportSequence;  // Local counter for MSG_MOVE_TELEPORT_ACK (server increments on each teleport)


        private TimeSpan _lastPositionUpdate = TimeSpan.Zero;


        // ============= INPUT HANDLERS =============

        /// <summary>
        /// True when the player is airborne (jumping or falling). While airborne,
        /// directional input and facing changes are locked to prevent mid-air steering
        /// that would cause the bot to spiral and send illegal movement packets.
        /// </summary>
        private static bool IsPlayerAirborne(WoWLocalPlayer player)
            => (player.MovementFlags & (MovementFlags.MOVEFLAG_FALLINGFAR | MovementFlags.MOVEFLAG_JUMPING)) != 0;

        public void StartMovement(ControlBits bits)
        {
            var player = (WoWLocalPlayer)Player;
            if (player == null) return;

            // While airborne, don't allow new directional input — keep pre-jump direction.
            // Jump input is allowed (double-jump protection is elsewhere).
            if (IsPlayerAirborne(player) && (bits & (ControlBits.Front | ControlBits.Back | ControlBits.StrafeLeft | ControlBits.StrafeRight)) != 0)
                return;

            // Convert control bits to movement flags and update player state
            MovementFlags flags = ConvertControlBitsToFlags(bits, player.MovementFlags, true);
            player.MovementFlags = flags;
        }


        public void StopMovement(ControlBits bits)
        {
            var player = (WoWLocalPlayer)Player;
            if (player == null) return;

            // While airborne, don't allow clearing directional flags — keep pre-jump direction.
            // Clearing FORWARD mid-fall would send MSG_MOVE_STOP while airborne.
            if (IsPlayerAirborne(player) && (bits & (ControlBits.Front | ControlBits.Back | ControlBits.StrafeLeft | ControlBits.StrafeRight)) != 0)
                return;

            // Clear the corresponding movement flags.
            // MovementController (game loop, 50ms) detects the flag change
            // and sends MSG_MOVE_STOP automatically.
            MovementFlags flags = ConvertControlBitsToFlags(bits, player.MovementFlags, false);
            player.MovementFlags = flags;

            // Clear path when forward movement stops
            if (bits.HasFlag(ControlBits.Front))
                _movementController?.ClearPath();
        }

        public void StopAllMovement()
        {
            var player = (WoWLocalPlayer)Player;
            if (player == null) return;

            const ControlBits stopBits =
                ControlBits.Front |
                ControlBits.Back |
                ControlBits.Left |
                ControlBits.Right |
                ControlBits.StrafeLeft |
                ControlBits.StrafeRight;

            if (IsPlayerAirborne(player))
            {
                _movementController?.ClearPath();
                _movementController?.RequestGroundedStop();
                return;
            }

            StopMovement(stopBits);
        }

        /// <summary>
        /// Clears directional movement intent and immediately sends the appropriate movement opcode.
        /// Active physics state (for example falling or swimming) is preserved.
        /// Use before interactions that require the player to stop advancing (CMSG_GAMEOBJ_USE, etc.).
        /// </summary>
        void IObjectManager.ForceStopImmediate()
        {
            var player = (WoWLocalPlayer)Player;
            if (player == null) return;

            StopMovement(ControlBits.Front | ControlBits.Back | ControlBits.Left | ControlBits.Right | ControlBits.StrafeLeft | ControlBits.StrafeRight);
            _movementController?.ClearPath();
            _movementController?.SendStopPacket((uint)_worldTimeTracker.NowMS.TotalMilliseconds);
            Log.Information("[ForceStopImmediate] Cleared directional movement intent and sent immediate stop/update packet");
        }


        private MovementFlags ConvertControlBitsToFlags(ControlBits bits, MovementFlags currentFlags, bool add)
        {
            MovementFlags flags = currentFlags;

            if (bits.HasFlag(ControlBits.Front))
            {
                if (add) flags |= MovementFlags.MOVEFLAG_FORWARD;
                else flags &= ~MovementFlags.MOVEFLAG_FORWARD;
            }
            if (bits.HasFlag(ControlBits.Back))
            {
                if (add) flags |= MovementFlags.MOVEFLAG_BACKWARD;
                else flags &= ~MovementFlags.MOVEFLAG_BACKWARD;
            }
            if (bits.HasFlag(ControlBits.StrafeLeft))
            {
                if (add) flags |= MovementFlags.MOVEFLAG_STRAFE_LEFT;
                else flags &= ~MovementFlags.MOVEFLAG_STRAFE_LEFT;
            }
            if (bits.HasFlag(ControlBits.StrafeRight))
            {
                if (add) flags |= MovementFlags.MOVEFLAG_STRAFE_RIGHT;
                else flags &= ~MovementFlags.MOVEFLAG_STRAFE_RIGHT;
            }
            if (bits.HasFlag(ControlBits.Left))
            {
                if (add) flags |= MovementFlags.MOVEFLAG_TURN_LEFT;
                else flags &= ~MovementFlags.MOVEFLAG_TURN_LEFT;
            }
            if (bits.HasFlag(ControlBits.Right))
            {
                if (add) flags |= MovementFlags.MOVEFLAG_TURN_RIGHT;
                else flags &= ~MovementFlags.MOVEFLAG_TURN_RIGHT;
            }
            if (bits.HasFlag(ControlBits.Jump))
            {
                if (add) flags |= MovementFlags.MOVEFLAG_JUMPING;
                else flags &= ~MovementFlags.MOVEFLAG_JUMPING;
            }

            return flags;
        }

        public void SetFacing(float facing)
        {
            var player = (WoWLocalPlayer)Player;
            if (player == null) return;

            player.Facing = facing;

            // Send facing update immediately via movement controller
            if (_movementController != null && _isInControl && !_isBeingTeleported)
            {
                _movementController.SendFacingUpdate((uint)_worldTimeTracker.NowMS.TotalMilliseconds);
            }
        }


        private void EventEmitter_OnForceTimeSkipped(
            object? sender,
            RequiresAcknowledgementArgs e
        )
        { }


        private void EventEmitter_OnForceMoveKnockBack(
            object? sender,
            KnockBackArgs e
        )
        {
            var player = (WoWLocalPlayer)Player;

            // Apply knockback velocity — WoW.exe 0x5E59B0.
            // vCos/vSin define the horizontal direction, hSpeed is magnitude, vSpeed is vertical launch.
            float velX = e.HSpeed * e.VCos;
            float velY = e.HSpeed * e.VSin;
            float velZ = e.VSpeed;  // positive = upward

            // Store pending knockback for MovementController to consume next frame
            _pendingKnockbackVelX = velX;
            _pendingKnockbackVelY = velY;
            _pendingKnockbackVelZ = velZ;
            _hasPendingKnockback = true;

            // Set FALLINGFAR — knockback initiates a fall trajectory
            player.MovementFlags |= MovementFlags.MOVEFLAG_FALLINGFAR;
            // Clear directional flags — knockback overrides player input
            player.MovementFlags &= ~(MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_BACKWARD
                | MovementFlags.MOVEFLAG_STRAFE_LEFT | MovementFlags.MOVEFLAG_STRAFE_RIGHT);

            Serilog.Log.Information("[KNOCKBACK] vel=({VelX:F2},{VelY:F2},{VelZ:F2}) hSpeed={HSpeed:F2} dir=({VCos:F3},{VSin:F3})",
                velX, velY, velZ, e.HSpeed, e.VCos, e.VSin);

            _ = _woWClient.SendMSGPackedAsync(
                Opcode.CMSG_MOVE_KNOCK_BACK_ACK,
                MovementPacketHandler.BuildForceMoveAck(
                    player,
                    e.Counter,
                    (uint)_worldTimeTracker.NowMS.TotalMilliseconds
                )
            );
        }

        // Knockback state — consumed by MovementController on next Update()
        private volatile bool _hasPendingKnockback;
        private float _pendingKnockbackVelX, _pendingKnockbackVelY, _pendingKnockbackVelZ;

        /// <summary>Returns and clears any pending knockback velocity impulse.</summary>
        internal bool TryConsumePendingKnockback(out float vx, out float vy, out float vz)
        {
            if (!_hasPendingKnockback)
            {
                vx = vy = vz = 0;
                return false;
            }
            vx = _pendingKnockbackVelX;
            vy = _pendingKnockbackVelY;
            vz = _pendingKnockbackVelZ;
            _hasPendingKnockback = false;
            return true;
        }


        private void EventEmitter_OnForceSwimSpeedChange(
            object? sender,
            RequiresAcknowledgementArgs e
        )
        {
            var player = (WoWLocalPlayer)Player;
            player.SwimSpeed = e.Speed;
            Serilog.Log.Information("[SPEED] SwimSpeed changed to {Speed:F2} y/s", e.Speed);

            _ = _woWClient.SendMSGPackedAsync(
                Opcode.CMSG_FORCE_SWIM_SPEED_CHANGE_ACK,
                MovementPacketHandler.BuildForceSpeedChangeAck(
                    player,
                    e.Counter,
                    (uint)_worldTimeTracker.NowMS.TotalMilliseconds,
                    e.Speed
                )
            );
        }

        private void EventEmitter_OnForceSwimBackSpeedChange(
            object? sender,
            RequiresAcknowledgementArgs e
        )
        {
            var player = (WoWLocalPlayer)Player;
            player.SwimBackSpeed = e.Speed;
            Serilog.Log.Information("[SPEED] SwimBackSpeed changed to {Speed:F2} y/s", e.Speed);

            _ = _woWClient.SendMSGPackedAsync(
                Opcode.CMSG_FORCE_SWIM_BACK_SPEED_CHANGE_ACK,
                MovementPacketHandler.BuildForceSpeedChangeAck(
                    player,
                    e.Counter,
                    (uint)_worldTimeTracker.NowMS.TotalMilliseconds,
                    e.Speed
                )
            );
        }

        /// <summary>
        /// Called when SplineController finishes or removes a spline.
        /// If it's the local player, restore physics control.
        /// </summary>
        private void OnSplineCompleted(ulong guid)
        {
            if (Player != null && Player.Guid == guid && !_isInControl)
            {
                _isInControl = true;
                Log.Information("[SplineLockout] Spline completed for local player — restoring control");
            }
        }


        private void EventEmitter_OnForceRunBackSpeedChange(
            object? sender,
            RequiresAcknowledgementArgs e
        )
        {
            var player = (WoWLocalPlayer)Player;
            player.RunBackSpeed = e.Speed;
            Serilog.Log.Information("[SPEED] RunBackSpeed changed to {Speed:F2} y/s", e.Speed);

            _ = _woWClient.SendMSGPackedAsync(
                Opcode.CMSG_FORCE_RUN_BACK_SPEED_CHANGE_ACK,
                MovementPacketHandler.BuildForceSpeedChangeAck(
                    player,
                    e.Counter,
                    (uint)_worldTimeTracker.NowMS.TotalMilliseconds,
                    e.Speed
                )
            );
        }

        private void EventEmitter_OnForceWalkSpeedChange(
            object? sender,
            RequiresAcknowledgementArgs e
        )
        {
            var player = (WoWLocalPlayer)Player;
            player.WalkSpeed = e.Speed;
            Serilog.Log.Information("[SPEED] WalkSpeed changed to {Speed:F2} y/s", e.Speed);

            _ = _woWClient.SendMSGPackedAsync(
                Opcode.CMSG_FORCE_WALK_SPEED_CHANGE_ACK,
                MovementPacketHandler.BuildForceSpeedChangeAck(
                    player,
                    e.Counter,
                    (uint)_worldTimeTracker.NowMS.TotalMilliseconds,
                    e.Speed
                )
            );
        }


        private void EventEmitter_OnForceRunSpeedChange(
            object? sender,
            RequiresAcknowledgementArgs e
        )
        {
            var player = (WoWLocalPlayer)Player;
            player.RunSpeed = e.Speed;
            Serilog.Log.Information("[SPEED] RunSpeed changed to {Speed:F2} y/s", e.Speed);

            _ = _woWClient.SendMSGPackedAsync(
                Opcode.CMSG_FORCE_RUN_SPEED_CHANGE_ACK,
                MovementPacketHandler.BuildForceSpeedChangeAck(
                    player,
                    e.Counter,
                    (uint)_worldTimeTracker.NowMS.TotalMilliseconds,
                    e.Speed
                )
            );
        }

        private void EventEmitter_OnForceTurnRateChange(
            object? sender,
            RequiresAcknowledgementArgs e
        )
        {
            var player = (WoWLocalPlayer)Player;
            player.TurnRate = e.Speed;
            Serilog.Log.Information("[SPEED] TurnRate changed to {Speed:F2} rad/s", e.Speed);

            _ = _woWClient.SendMSGPackedAsync(
                Opcode.CMSG_FORCE_TURN_RATE_CHANGE_ACK,
                MovementPacketHandler.BuildForceSpeedChangeAck(
                    player,
                    e.Counter,
                    (uint)_worldTimeTracker.NowMS.TotalMilliseconds,
                    e.Speed
                )
            );
        }


        private void EventEmitter_OnForceMoveUnroot(object? sender, RequiresAcknowledgementArgs e)
        {
            // Clear MOVEFLAG_ROOT before ACK — MaNGOS validates the flag is absent
            var player = (WoWLocalPlayer)Player;
            player.MovementFlags &= ~MovementFlags.MOVEFLAG_ROOT;

            _ = _woWClient.SendMSGPackedAsync(
                Opcode.CMSG_FORCE_MOVE_UNROOT_ACK,
                MovementPacketHandler.BuildForceMoveAck(
                    player,
                    e.Counter,
                    (uint)_worldTimeTracker.NowMS.TotalMilliseconds
                )
            );
        }


        private void EventEmitter_OnForceMoveRoot(object? sender, RequiresAcknowledgementArgs e)
        {
            // Set MOVEFLAG_ROOT and clear movement flags incompatible with root
            var player = (WoWLocalPlayer)Player;
            player.MovementFlags |= MovementFlags.MOVEFLAG_ROOT;
            player.MovementFlags &= ~MovementFlags.MOVEFLAG_MASK_MOVING;

            _ = _woWClient.SendMSGPackedAsync(
                Opcode.CMSG_FORCE_MOVE_ROOT_ACK,
                MovementPacketHandler.BuildForceMoveAck(
                    player,
                    e.Counter,
                    (uint)_worldTimeTracker.NowMS.TotalMilliseconds
                )
            );
        }

        // VMaNGOS MovementPacketSender.cpp / MovementPacketSender.h:
        // - SMSG_MOVE_WATER_WALK / SMSG_MOVE_LAND_WALK -> CMSG_MOVE_WATER_WALK_ACK
        // - SMSG_MOVE_SET_HOVER / SMSG_MOVE_UNSET_HOVER -> CMSG_MOVE_HOVER_ACK
        // - SMSG_MOVE_FEATHER_FALL / SMSG_MOVE_NORMAL_FALL -> CMSG_MOVE_FEATHER_FALL_ACK
        // Payload is packed guid + counter on 1.12.1, and the ACK echoes full MovementInfo.

        private void EventEmitter_OnMoveWaterWalk(object? sender, RequiresAcknowledgementArgs e)
            => SendMovementFlagToggleAck(e, MovementFlags.MOVEFLAG_WATERWALKING, apply: true, Opcode.CMSG_MOVE_WATER_WALK_ACK);

        private void EventEmitter_OnMoveLandWalk(object? sender, RequiresAcknowledgementArgs e)
            => SendMovementFlagToggleAck(e, MovementFlags.MOVEFLAG_WATERWALKING, apply: false, Opcode.CMSG_MOVE_WATER_WALK_ACK);

        private void EventEmitter_OnMoveSetHover(object? sender, RequiresAcknowledgementArgs e)
            => SendMovementFlagToggleAck(e, MovementFlags.MOVEFLAG_HOVER, apply: true, Opcode.CMSG_MOVE_HOVER_ACK);

        private void EventEmitter_OnMoveUnsetHover(object? sender, RequiresAcknowledgementArgs e)
            => SendMovementFlagToggleAck(e, MovementFlags.MOVEFLAG_HOVER, apply: false, Opcode.CMSG_MOVE_HOVER_ACK);

        private void EventEmitter_OnMoveFeatherFall(object? sender, RequiresAcknowledgementArgs e)
            => SendMovementFlagToggleAck(e, MovementFlags.MOVEFLAG_SAFE_FALL, apply: true, Opcode.CMSG_MOVE_FEATHER_FALL_ACK);

        private void EventEmitter_OnMoveNormalFall(object? sender, RequiresAcknowledgementArgs e)
            => SendMovementFlagToggleAck(e, MovementFlags.MOVEFLAG_SAFE_FALL, apply: false, Opcode.CMSG_MOVE_FEATHER_FALL_ACK);

        private void SendMovementFlagToggleAck(
            RequiresAcknowledgementArgs e,
            MovementFlags flag,
            bool apply,
            Opcode ackOpcode)
        {
            var player = (WoWLocalPlayer)Player;
            if (apply)
                player.MovementFlags |= flag;
            else
                player.MovementFlags &= ~flag;

            _ = _woWClient.SendMSGPackedAsync(
                ackOpcode,
                MovementPacketHandler.BuildForceMoveAck(
                    player,
                    e.Counter,
                    (uint)_worldTimeTracker.NowMS.TotalMilliseconds
                )
            );
        }


        /// <summary>
        /// Called by MovementHandler BEFORE queuing a teleport position update,
        /// so the position write guard in ProcessUpdatesAsync allows it through.
        /// </summary>
        public void NotifyTeleportIncoming(float teleportDestZ = float.NaN)
        {
            _isBeingTeleported = true;
            _teleportFlagSetTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            ResetMovementStateForTeleport("notify-teleport-incoming", teleportDestZ);
        }

        /// <summary>
        /// Increment the local teleport sequence counter and return the new value.
        /// MSG_MOVE_TELEPORT packets don't include a counter, but the server tracks
        /// an internal m_sequenceIndex. The ACK must echo the matching counter.
        /// </summary>


        /// <summary>
        /// Increment the local teleport sequence counter and return the new value.
        /// MSG_MOVE_TELEPORT packets don't include a counter, but the server tracks
        /// an internal m_sequenceIndex. The ACK must echo the matching counter.
        /// </summary>
        public uint IncrementTeleportSequence()
        {
            return ++_teleportSequence;
        }


        private void EventEmitter_OnTeleport(object? sender, RequiresAcknowledgementArgs e)
        {
            // _isBeingTeleported is already set by NotifyTeleportIncoming() before the update was queued.
            // Movement state was ALREADY reset by NotifyTeleportIncoming() with the correct teleportDestZ.
            // Do NOT call ResetMovementStateForTeleport here — it would clobber the good Z with NaN,
            // causing teleportDestZ=NaN → float.MinValue Z corruption in MovementController.
            _isBeingTeleported = true;
            _teleportFlagSetTicks = System.Diagnostics.Stopwatch.GetTimestamp();

            var player = (WoWLocalPlayer)Player;
            var ackPayload = MovementPacketHandler.BuildMoveTeleportAckPayload(
                player,
                e.Counter,
                (uint)_worldTimeTracker.NowMS.TotalMilliseconds
            );

            Log.Information("[ACK] TELEPORT counter={Counter} guid=0x{Guid:X} pos=({X:F1},{Y:F1},{Z:F1}) payloadLen={Len}",
                e.Counter, player.Guid, player.Position.X, player.Position.Y, player.Position.Z, ackPayload.Length);

            _ = _woWClient.SendMSGPackedAsync(
                Opcode.MSG_MOVE_TELEPORT_ACK,
                ackPayload
            );

            // Clear after a delay so ProcessUpdatesAsync has time to apply the position update.
            // SMSG_CLIENT_CONTROL_UPDATE will also clear this if it arrives sooner.
            // Also send a stop packet so the server knows we're stationary after teleport
            // (prevents stale MOVEFLAG_FORWARD from persisting on the server side).
            Task.Delay(500).ContinueWith(_ =>
            {
                Log.Information("[ACK] TELEPORT 500ms fallback: clearing _isBeingTeleported");
                _isBeingTeleported = false;
                // Force one physics frame immediately to execute pending ground snap.
                // The game loop guard blocks Update() while _isBeingTeleported is true,
                // so we must explicitly run it here to avoid missing the ground snap window.
                if (_isInControl && Player != null && _movementController != null)
                {
                    _movementController.Update(0.016f, (uint)_worldTimeTracker.NowMS.TotalMilliseconds);
                }
                else
                {
                    _movementController?.SendStopPacket((uint)_worldTimeTracker.NowMS.TotalMilliseconds);
                }
            }, TaskScheduler.Default);
        }


        private void ResetMovementStateForTeleport(string source, float teleportDestZ = float.NaN)
        {
            if (Player is not WoWLocalPlayer player)
                return;

            player.MovementFlags = MovementFlags.MOVEFLAG_NONE;
            _movementController?.Reset(teleportDestZ);

            Log.Information("[TeleportReset] source={Source} flags cleared; teleportDestZ={DestZ:F1}; pos=({X:F1},{Y:F1},{Z:F1})",
                source, teleportDestZ, player.Position.X, player.Position.Y, player.Position.Z);
        }


        private void EventEmitter_OnLoginVerifyWorld(object? sender, WorldInfo e)
        {
            ClearPendingWorldEntry();
            ((WoWLocalPlayer)Player).MapId = e.MapId;

            Player.Position.X = e.PositionX;
            Player.Position.Y = e.PositionY;
            Player.Position.Z = e.PositionZ;

            // Reset movement controller for zone/map change — clears stale continuity
            // state (prevGroundZ, standingOn, etc.) from the old map and sets
            // _needsGroundSnap so physics runs at least once to find the ground.
            _movementController?.Reset();

            _worldTimeTracker = new WorldTimeTracker();
            _lastPositionUpdate = _worldTimeTracker.NowMS;
            _physicsTimeAccumulator = 0f; // Clear sub-step accumulator on zone/map change
            StartGameLoop();

            _ = _woWClient.SendMoveWorldPortAcknowledgeAsync();
        }


        private int _moveTowardAirborneLogCount;
        public void MoveToward(Position pos)
        {
            if (pos == null || Player == null) return;

            var player = (WoWLocalPlayer)Player;
            if (IsPlayerAirborne(player))
            {
                _moveTowardAirborneLogCount++;
                if (_moveTowardAirborneLogCount <= 5 || _moveTowardAirborneLogCount % 50 == 0)
                    Log.Warning("[NAV-DIAG] MoveToward preserving airborne steering only (x{Count}): flags=0x{Flags:X}, pos=({X:F1},{Y:F1},{Z:F1}), map={Map}",
                        _moveTowardAirborneLogCount, (uint)player.MovementFlags,
                        player.Position.X, player.Position.Y, player.Position.Z, player.MapId);
                UpdateAirborneSteering(pos);
                return;
            }
            _moveTowardAirborneLogCount = 0;

            // Face the target
            if (!Player.IsFacing(pos))
                SetFacing(Player.GetFacingForPosition(pos));

            // Keep directional intent deterministic: clear lateral/back flags before driving forward.
            StopMovement(ControlBits.Back | ControlBits.StrafeLeft | ControlBits.StrafeRight);
            StartMovement(ControlBits.Front);

            // Always refresh the steering target so movement parity runs against the latest
            // BotRunner-selected waypoint.
            if (_movementController != null)
            {
                _movementController.SetTargetWaypoint(pos);
            }
        }


        public void MoveToward(Position position, float facing)
        {
            var player = (WoWLocalPlayer)Player;
            if (player == null) return;

            if (IsPlayerAirborne(player))
            {
                UpdateAirborneSteering(position);
                return;
            }

            // Set facing and movement flags.
            // The MovementController (running in the game loop every 50ms) handles:
            //   - Physics step (ground snapping, collision, gravity)
            //   - Position update
            //   - Network packet sending (MSG_MOVE_START_FORWARD, heartbeats, etc.)
            //
            // WoW.exe gates MSG_MOVE_SET_FACING at 0.1 rad in 0x60E1EA.
            // We still update the local facing immediately so the movement vector stays current.
            var facingDelta = MathF.Abs(facing - player.Facing);
            // Handle wrap-around (e.g. 6.2 → 0.1 = 0.18 rad, not 6.1 rad)
            if (facingDelta > MathF.PI)
                facingDelta = 2f * MathF.PI - facingDelta;
            if (facingDelta > 0.0f)
            {
                player.Facing = facing;
                bool sendFacingPacket = facingDelta > FacingPacketThresholdRadians;
                if (sendFacingPacket && _movementController != null && _isInControl && !_isBeingTeleported)
                {
                    _movementController.SendMovementStartFacingUpdate((uint)_worldTimeTracker.NowMS.TotalMilliseconds);
                }
            }
            player.MovementFlags &= ~(MovementFlags.MOVEFLAG_BACKWARD | MovementFlags.MOVEFLAG_STRAFE_LEFT | MovementFlags.MOVEFLAG_STRAFE_RIGHT);
            player.MovementFlags |= MovementFlags.MOVEFLAG_FORWARD;

            // Always refresh the steering target so movement parity runs against the latest
            // BotRunner-selected waypoint.
            if (_movementController != null)
            {
                _movementController.SetTargetWaypoint(position);
            }
        }

        private void UpdateAirborneSteering(Position target)
        {
            // Route-following movement must not rewrite facing while airborne.
            // The original client preserves the current airborne trajectory until landing
            // instead of re-steering every tick from waypoint churn. Explicit facing
            // updates still go through SetFacing when higher-level code genuinely needs them.
            _movementController?.SetTargetWaypoint(target);
        }

        /// <summary>
        /// True if the most recent physics tick reported a wall contact during horizontal movement.
        /// Path layer uses this to suppress false stall detection when the bot is genuinely blocked.
        /// </summary>


        /// <summary>
        /// True if the most recent physics tick reported a wall contact during horizontal movement.
        /// Path layer uses this to suppress false stall detection when the bot is genuinely blocked.
        /// </summary>
        public bool PhysicsHitWall => _movementController?.LastHitWall ?? false;

        /// <summary>
        /// XY components of the wall surface normal from the most recent physics wall contact.
        /// Used by NavigationPath for geometric deflection away from obstacles.
        /// </summary>
        public (float X, float Y) PhysicsWallNormal2D =>
            _movementController != null
                ? (_movementController.LastWallNormal.X, _movementController.LastWallNormal.Y)
                : (0f, 0f);

        /// <summary>
        /// Fraction of intended horizontal movement that was completed (0 = fully blocked, 1 = unblocked).
        /// </summary>
        public float PhysicsBlockedFraction => _movementController?.LastBlockedFraction ?? 1.0f;

        // ======== Frame Recording (parity diagnostics) ========

        /// <summary>
        /// Enable/disable per-frame physics recording. When enabled, MovementController captures
        /// every physics frame with full guard decision state for parity analysis.
        /// </summary>
        public bool IsPhysicsRecording
        {
            get => _movementController?.IsRecording ?? false;
            set { if (_movementController != null) _movementController.IsRecording = value; }
        }

        /// <summary>
        /// Retrieve a snapshot of all recorded physics frames. Returns empty list if not recording.
        /// </summary>
        public List<PhysicsFrameRecord> GetPhysicsFrameRecording()
            => _movementController?.GetRecordedFrames() ?? [];

        /// <summary>
        /// Clear the recorded frame buffer (e.g. between test runs).
        /// </summary>
        public void ClearPhysicsFrameRecording()
            => _movementController?.ClearRecordedFrames();

        public void ReleaseSpirit()
        {
            if (_woWClient == null) return;
            Log.Information("[OBJMGR] ReleaseSpirit: sending CMSG_REPOP_REQUEST");
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_REPOP_REQUEST, []);
        }


        public void RetrieveCorpse()
        {
            if (_woWClient == null) return;
            // CMSG_RECLAIM_CORPSE (1.12.1): 8-byte ObjectGuid playerGuid
            // VMaNGOS validates the GUID matches the session player — zero is rejected silently.
            var guid = Player?.Guid ?? 0UL;
            Log.Information("[OBJMGR] RetrieveCorpse: sending CMSG_RECLAIM_CORPSE (Player.Guid=0x{Guid:X16})", guid);
            var payload = BitConverter.GetBytes(guid);
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_RECLAIM_CORPSE, payload);
        }
    }
}
