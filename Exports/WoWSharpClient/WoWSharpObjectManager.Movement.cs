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
    public partial class WoWSharpObjectManager
    {
        private ControlBits _controlBits = ControlBits.Nothing;

        public bool IsPlayerMoving => !Player.MovementFlags.Equals(MovementFlags.MOVEFLAG_NONE);


        private bool _isInControl = false;

        private bool _isBeingTeleported = true;

        private long _teleportFlagSetTicks;  // Stopwatch.GetTimestamp() when _isBeingTeleported was last set true

        private uint _teleportSequence;  // Local counter for MSG_MOVE_TELEPORT_ACK (server increments on each teleport)


        private TimeSpan _lastPositionUpdate = TimeSpan.Zero;


        // ============= INPUT HANDLERS =============
        public void StartMovement(ControlBits bits)
        {
            var player = (WoWLocalPlayer)Player;
            if (player == null) return;

            // Convert control bits to movement flags and update player state
            MovementFlags flags = ConvertControlBitsToFlags(bits, player.MovementFlags, true);
            player.MovementFlags = flags;
        }


        public void StopMovement(ControlBits bits)
        {
            var player = (WoWLocalPlayer)Player;
            if (player == null) return;

            // Clear the corresponding movement flags.
            // MovementController (game loop, 50ms) detects the flag change
            // and sends MSG_MOVE_STOP automatically.
            MovementFlags flags = ConvertControlBitsToFlags(bits, player.MovementFlags, false);
            player.MovementFlags = flags;

            // Clear path when forward movement stops
            if (bits.HasFlag(ControlBits.Front))
                _movementController?.ClearPath();
        }

        /// <summary>
        /// Clears all movement flags AND immediately sends MSG_MOVE_STOP to the server.
        /// Use before interactions that require the player to be stationary (CMSG_GAMEOBJ_USE, etc.).
        /// </summary>
        void IObjectManager.ForceStopImmediate()
        {
            var player = (WoWLocalPlayer)Player;
            if (player == null) return;

            player.MovementFlags = MovementFlags.MOVEFLAG_NONE;
            _movementController?.ClearPath();
            _movementController?.SendStopPacket((uint)_worldTimeTracker.NowMS.TotalMilliseconds);
            Log.Information("[ForceStopImmediate] Cleared all movement flags and sent MSG_MOVE_STOP");
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
            RequiresAcknowledgementArgs e
        )
        {
            _ = _woWClient.SendMSGPackedAsync(
                Opcode.CMSG_MOVE_KNOCK_BACK_ACK,
                MovementPacketHandler.BuildForceMoveAck(
                    (WoWLocalPlayer)Player,
                    e.Counter,
                    (uint)_worldTimeTracker.NowMS.TotalMilliseconds
                )
            );
        }


        private void EventEmitter_OnForceSwimSpeedChange(
            object? sender,
            RequiresAcknowledgementArgs e
        )
        {
            _ = _woWClient.SendMSGPackedAsync(
                Opcode.CMSG_FORCE_SWIM_SPEED_CHANGE_ACK,
                MovementPacketHandler.BuildForceSpeedChangeAck(
                    (WoWLocalPlayer)Player,
                    e.Counter,
                    (uint)_worldTimeTracker.NowMS.TotalMilliseconds,
                    e.Speed
                )
            );
        }


        private void EventEmitter_OnForceRunBackSpeedChange(
            object? sender,
            RequiresAcknowledgementArgs e
        )
        {
            _ = _woWClient.SendMSGPackedAsync(
                Opcode.CMSG_FORCE_RUN_BACK_SPEED_CHANGE_ACK,
                MovementPacketHandler.BuildForceSpeedChangeAck(
                    (WoWLocalPlayer)Player,
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
            _ = _woWClient.SendMSGPackedAsync(
                Opcode.CMSG_FORCE_RUN_SPEED_CHANGE_ACK,
                MovementPacketHandler.BuildForceSpeedChangeAck(
                    (WoWLocalPlayer)Player,
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
            });
        }


        private void ResetMovementStateForTeleport(string source, float teleportDestZ = float.NaN)
        {
            if (Player is not WoWLocalPlayer player)
                return;

            _controlBits = ControlBits.Nothing;
            player.MovementFlags = MovementFlags.MOVEFLAG_NONE;
            _movementController?.Reset(teleportDestZ);

            Log.Information("[TeleportReset] source={Source} flags cleared; teleportDestZ={DestZ:F1}; pos=({X:F1},{Y:F1},{Z:F1})",
                source, teleportDestZ, player.Position.X, player.Position.Y, player.Position.Z);
        }


        private void EventEmitter_OnLoginVerifyWorld(object? sender, WorldInfo e)
        {
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
            StartGameLoop();

            _ = _woWClient.SendMoveWorldPortAcknowledgeAsync();
        }


        public void MoveToward(Position pos)
        {
            if (pos == null || Player == null) return;

            // Face the target
            if (!Player.IsFacing(pos))
                SetFacing(Player.GetFacingForPosition(pos));

            // Keep directional intent deterministic: clear lateral/back flags before driving forward.
            StopMovement(ControlBits.Back | ControlBits.StrafeLeft | ControlBits.StrafeRight);
            StartMovement(ControlBits.Front);

            // Always refresh the waypoint so path-driven callers can steer every tick.
            // A large refresh threshold can leave movement locked to an old heading into walls.
            if (_movementController != null)
            {
                _movementController.SetTargetWaypoint(pos);
            }
        }


        public void MoveToward(Position position, float facing)
        {
            var player = (WoWLocalPlayer)Player;
            if (player == null) return;

            // Set facing and movement flags.
            // The MovementController (running in the game loop every 50ms) handles:
            //   - Physics step (ground snapping, collision, gravity)
            //   - Position update
            //   - Network packet sending (MSG_MOVE_START_FORWARD, heartbeats, etc.)
            player.Facing = facing;
            player.MovementFlags &= ~(MovementFlags.MOVEFLAG_BACKWARD | MovementFlags.MOVEFLAG_STRAFE_LEFT | MovementFlags.MOVEFLAG_STRAFE_RIGHT);
            player.MovementFlags |= MovementFlags.MOVEFLAG_FORWARD;

            // Always refresh the waypoint so path-driven callers can steer every tick.
            // A large refresh threshold can leave movement locked to an old heading into walls.
            if (_movementController != null)
            {
                _movementController.SetTargetWaypoint(position);
            }
        }

        /// <summary>
        /// Sets a full navigation path on the movement controller for waypoint-based following.
        /// The controller will interpolate Z from path waypoints and auto-advance through them.
        /// </summary>


        /// <summary>
        /// Sets a full navigation path on the movement controller for waypoint-based following.
        /// The controller will interpolate Z from path waypoints and auto-advance through them.
        /// </summary>
        public void SetNavigationPath(Position[] path)
        {
            _movementController?.SetPath(path);
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


        public void ReleaseSpirit()
        {
            if (_woWClient == null) return;
            Log.Information("[OBJMGR] ReleaseSpirit: sending CMSG_REPOP_REQUEST");
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_REPOP_REQUEST, []);
        }


        public void RetrieveCorpse()
        {
            if (_woWClient == null) return;
            // CMSG_RECLAIM_CORPSE: 8-byte ObjectGuid (zero = server infers from session)
            // Matches DeadActorClientComponent.ResurrectAtCorpseAsync() pattern.
            Log.Information("[OBJMGR] RetrieveCorpse: sending CMSG_RECLAIM_CORPSE (Player.Guid=0x{Guid:X16})", Player?.Guid ?? 0);
            var payload = new byte[8];
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_RECLAIM_CORPSE, payload);
        }
    }
}
