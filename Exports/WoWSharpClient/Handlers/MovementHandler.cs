using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using WoWSharpClient.Client;
using WoWSharpClient.Models;
using WoWSharpClient.Parsers;
using Serilog;
using WoWSharpClient.Utils;
using System.IO;
using System.Linq;
using System;
using System.Collections.Generic;

namespace WoWSharpClient.Handlers
{
    public static class MovementHandler
    {
        public static void HandleUpdateMovement(Opcode opcode, byte[] data)
        {
            if (opcode == Opcode.SMSG_COMPRESSED_MOVES)
                data = PacketManager.Decompress([.. data.Skip(4)]);

            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);

            if (opcode == Opcode.SMSG_COMPRESSED_MOVES)
            {
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    ParseCompressedMove(reader);
                }
            }
            else
            {
                try
                {
                    if (opcode == Opcode.MSG_MOVE_TELEPORT || opcode == Opcode.MSG_MOVE_TELEPORT_ACK)
                        Log.Information("[MovementHandler] Received teleport opcode: {Opcode} ({Size} bytes)", opcode, data.Length);

                    switch (opcode)
                    {
                        case Opcode.MSG_MOVE_TELEPORT:
                        {
                            // MSG_MOVE_TELEPORT format: packed_guid + MovementInfo (NO counter).
                            // Different from MSG_MOVE_TELEPORT_ACK which includes a counter field.
                            ulong teleportGuid = ReaderUtils.ReadPackedGuid(reader);
                            MovementInfoUpdate teleportData =
                                MovementPacketHandler.ParseMovementInfo(reader);

                            Log.Information(
                                "[MovementHandler] MSG_MOVE_TELEPORT: guid={Guid:X} pos=({X:F1},{Y:F1},{Z:F1})",
                                teleportGuid, teleportData.X, teleportData.Y, teleportData.Z);

                            // Only process as a player teleport if the GUID matches our player.
                            // MSG_MOVE_TELEPORT can also be sent for creatures (MaNGOS sends these
                            // when mobs are moved). Processing creature teleports as player teleports
                            // triggers NotifyTeleportIncoming → _isBeingTeleported=true → movement
                            // state reset → auto-attack heartbeat disruption → mob evades.
                            var teleportPlayer = WoWSharpObjectManager.Instance.Player;
                            bool isPlayerTeleport = teleportPlayer != null && teleportPlayer.Guid == teleportGuid;

                            if (isPlayerTeleport)
                            {
                                WoWSharpObjectManager.Instance.NotifyTeleportIncoming(teleportData.Z);
                            }

                            WoWSharpObjectManager.Instance.QueueUpdate(
                                new WoWSharpObjectManager.ObjectStateUpdate(
                                    teleportGuid,
                                    WoWSharpObjectManager.ObjectUpdateOperation.Update,
                                    isPlayerTeleport ? WoWObjectType.Player : WoWObjectType.Unit,
                                    teleportData,
                                    []
                                )
                            );

                            // Directly update player position so MovementController uses the
                            // teleported position in its very next heartbeat/stop packet.
                            if (isPlayerTeleport)
                            {
                                teleportPlayer!.Position.X = teleportData.X;
                                teleportPlayer.Position.Y = teleportData.Y;
                                teleportPlayer.Position.Z = teleportData.Z;
                                Log.Information(
                                    "[MovementHandler] Teleport: directly updated player position to ({X:F1},{Y:F1},{Z:F1})",
                                    teleportData.X, teleportData.Y, teleportData.Z);
                            }

                            // Only ACK player teleports. Creature MSG_MOVE_TELEPORT packets
                            // must NOT be ACKed — doing so sets _isBeingTeleported=true in
                            // EventEmitter_OnTeleport, which blocks MovementController updates
                            // and disrupts auto-attack heartbeats → mob evades. (BT-COMBAT-002)
                            if (isPlayerTeleport)
                            {
                                var teleportCounter = WoWSharpObjectManager.Instance.IncrementTeleportSequence();
                                WoWSharpEventEmitter.Instance.FireOnTeleport(
                                    new RequiresAcknowledgementArgs(teleportGuid, teleportCounter)
                                );
                            }
                            else
                            {
                                Log.Debug(
                                    "[MovementHandler] Skipping teleport ACK for non-player GUID {Guid:X}",
                                    teleportGuid);
                            }
                            break;
                        }
                        case Opcode.MSG_MOVE_TELEPORT_ACK:
                            ulong guid = ReaderUtils.ReadPackedGuid(reader);
                            uint movementCounter = reader.ReadUInt32();
                            MovementInfoUpdate movementUpdateData =
                                MovementPacketHandler.ParseMovementInfo(reader);
                            movementUpdateData.MovementCounter = movementCounter;

                            // Queue the position update so the local player position reflects the teleport.
                            // Pass destination Z from packet so _teleportZ clamp is set to the correct
                            // post-teleport height (not the pre-teleport position captured from _player.Position.Z).
                            WoWSharpObjectManager.Instance.NotifyTeleportIncoming(movementUpdateData.Z);
                            WoWSharpObjectManager.Instance.QueueUpdate(
                                new WoWSharpObjectManager.ObjectStateUpdate(
                                    guid,
                                    WoWSharpObjectManager.ObjectUpdateOperation.Update,
                                    WoWObjectType.Player,
                                    movementUpdateData,
                                    []
                                )
                            );

                            // Also directly update the player position immediately.
                            // ProcessUpdatesAsync may not run before the next MovementController tick,
                            // causing heartbeats to send the OLD position to the server (overwriting
                            // the server-side teleport). This direct write ensures the MovementController
                            // uses the teleported position in its very next heartbeat.
                            {
                                var player = WoWSharpObjectManager.Instance.Player;
                                if (player != null && player.Guid == guid)
                                {
                                    player.Position.X = movementUpdateData.X;
                                    player.Position.Y = movementUpdateData.Y;
                                    player.Position.Z = movementUpdateData.Z;
                                    Log.Information("[MovementHandler] Teleport: directly updated player position to ({X:F1},{Y:F1},{Z:F1})",
                                        movementUpdateData.X, movementUpdateData.Y, movementUpdateData.Z);
                                }
                            }

                            WoWSharpEventEmitter.Instance.FireOnTeleport(
                                new RequiresAcknowledgementArgs(guid, movementCounter)
                            );
                            break;
                        case Opcode.SMSG_FORCE_MOVE_ROOT:
                            WoWSharpEventEmitter.Instance.FireOnForceMoveRoot(
                                ParseGuidCounterPacket(reader)
                            );
                            break;
                        case Opcode.SMSG_FORCE_MOVE_UNROOT:
                            WoWSharpEventEmitter.Instance.FireOnForceMoveUnroot(
                                ParseGuidCounterPacket(reader)
                            );
                            break;
                        case Opcode.SMSG_MOVE_WATER_WALK:
                            WoWSharpEventEmitter.Instance.FireOnMoveWaterWalk(
                                ParseGuidCounterPacket(reader)
                            );
                            break;
                        case Opcode.SMSG_MOVE_LAND_WALK:
                            WoWSharpEventEmitter.Instance.FireOnMoveLandWalk(
                                ParseGuidCounterPacket(reader)
                            );
                            break;
                        case Opcode.SMSG_MOVE_SET_HOVER:
                            WoWSharpEventEmitter.Instance.FireOnMoveSetHover(
                                ParseGuidCounterPacket(reader)
                            );
                            break;
                        case Opcode.SMSG_MOVE_UNSET_HOVER:
                            WoWSharpEventEmitter.Instance.FireOnMoveUnsetHover(
                                ParseGuidCounterPacket(reader)
                            );
                            break;
                        case Opcode.SMSG_MOVE_FEATHER_FALL:
                            WoWSharpEventEmitter.Instance.FireOnMoveFeatherFall(
                                ParseGuidCounterPacket(reader)
                            );
                            break;
                        case Opcode.SMSG_MOVE_NORMAL_FALL:
                            WoWSharpEventEmitter.Instance.FireOnMoveNormalFall(
                                ParseGuidCounterPacket(reader)
                            );
                            break;
                        case Opcode.SMSG_FORCE_WALK_SPEED_CHANGE:
                            WoWSharpEventEmitter.Instance.FireOnForceWalkSpeedChange(
                                ParseGuidCounterSpeedPacket(reader)
                            );
                            break;
                        case Opcode.SMSG_FORCE_RUN_SPEED_CHANGE:
                            WoWSharpEventEmitter.Instance.FireOnForceRunSpeedChange(
                                ParseGuidCounterSpeedPacket(reader)
                            );
                            break;
                        case Opcode.SMSG_FORCE_RUN_BACK_SPEED_CHANGE:
                            WoWSharpEventEmitter.Instance.FireOnForceRunBackSpeedChange(
                                ParseGuidCounterSpeedPacket(reader)
                            );
                            break;
                        case Opcode.SMSG_FORCE_SWIM_SPEED_CHANGE:
                            WoWSharpEventEmitter.Instance.FireOnForceSwimSpeedChange(
                                ParseGuidCounterSpeedPacket(reader)
                            );
                            break;
                        case Opcode.SMSG_FORCE_SWIM_BACK_SPEED_CHANGE:
                            WoWSharpEventEmitter.Instance.FireOnForceSwimBackSpeedChange(
                                ParseGuidCounterSpeedPacket(reader)
                            );
                            break;
                        case Opcode.SMSG_FORCE_TURN_RATE_CHANGE:
                            WoWSharpEventEmitter.Instance.FireOnForceTurnRateChange(
                                ParseGuidCounterSpeedPacket(reader)
                            );
                            break;
                        case Opcode.SMSG_MOVE_KNOCK_BACK:
                            WoWSharpEventEmitter.Instance.FireOnForceMoveKnockBack(
                                ParseKnockBackPacket(reader)
                            );
                            break;
                        case Opcode.SMSG_MONSTER_MOVE:
                        {
                            ulong moverGuid = ReaderUtils.ReadPackedGuid(reader);
                            var moveData = ParseMonsterMove(reader);
                            QueueMonsterMoveUpdate(moverGuid, moveData);
                            break;
                        }
                        case Opcode.SMSG_MONSTER_MOVE_TRANSPORT:
                        {
                            ulong moverGuid = ReaderUtils.ReadPackedGuid(reader);
                            ulong transportGuid = ReaderUtils.ReadPackedGuid(reader);
                            var moveData = ParseMonsterMove(reader);
                            ApplyTransportMoveState(moveData, transportGuid);
                            QueueMonsterMoveUpdate(moverGuid, moveData);
                            break;
                        }
                        case Opcode.SMSG_SPLINE_MOVE_SET_RUN_MODE:
                            ApplySplineFlagToggle(reader, MovementFlags.MOVEFLAG_WALK_MODE, apply: false, "set run mode");
                            break;
                        case Opcode.SMSG_SPLINE_MOVE_SET_WALK_MODE:
                            ApplySplineFlagToggle(reader, MovementFlags.MOVEFLAG_WALK_MODE, apply: true, "set walk mode");
                            break;
                        case Opcode.SMSG_SPLINE_MOVE_ROOT:
                            ApplySplineFlagToggle(reader, MovementFlags.MOVEFLAG_ROOT, apply: true, "rooted");
                            break;
                        case Opcode.SMSG_SPLINE_MOVE_UNROOT:
                            ApplySplineFlagToggle(reader, MovementFlags.MOVEFLAG_ROOT, apply: false, "unrooted");
                            break;
                        case Opcode.SMSG_SPLINE_MOVE_WATER_WALK:
                            ApplySplineFlagToggle(reader, MovementFlags.MOVEFLAG_WATERWALKING, apply: true, "water walk");
                            break;
                        case Opcode.SMSG_SPLINE_MOVE_LAND_WALK:
                            ApplySplineFlagToggle(reader, MovementFlags.MOVEFLAG_WATERWALKING, apply: false, "land walk");
                            break;
                        case Opcode.SMSG_SPLINE_MOVE_FEATHER_FALL:
                            ApplySplineFlagToggle(reader, MovementFlags.MOVEFLAG_SAFE_FALL, apply: true, "feather fall");
                            break;
                        case Opcode.SMSG_SPLINE_MOVE_NORMAL_FALL:
                            ApplySplineFlagToggle(reader, MovementFlags.MOVEFLAG_SAFE_FALL, apply: false, "normal fall");
                            break;
                        case Opcode.SMSG_SPLINE_MOVE_SET_HOVER:
                            ApplySplineFlagToggle(reader, MovementFlags.MOVEFLAG_HOVER, apply: true, "set hover");
                            break;
                        case Opcode.SMSG_SPLINE_MOVE_UNSET_HOVER:
                            ApplySplineFlagToggle(reader, MovementFlags.MOVEFLAG_HOVER, apply: false, "unset hover");
                            break;
                        case Opcode.SMSG_SPLINE_MOVE_START_SWIM:
                            ApplySplineFlagToggle(reader, MovementFlags.MOVEFLAG_SWIMMING, apply: true, "start swim");
                            break;
                        case Opcode.SMSG_SPLINE_MOVE_STOP_SWIM:
                            ApplySplineFlagToggle(reader, MovementFlags.MOVEFLAG_SWIMMING, apply: false, "stop swim");
                            break;
                        case Opcode.SMSG_SPLINE_SET_RUN_SPEED:
                            ApplySplineSpeedChange(reader, (unit, speed) => unit.RunSpeed = speed, "run speed");
                            break;
                        case Opcode.SMSG_SPLINE_SET_RUN_BACK_SPEED:
                            ApplySplineSpeedChange(reader, (unit, speed) => unit.RunBackSpeed = speed, "run back speed");
                            break;
                        case Opcode.SMSG_SPLINE_SET_SWIM_SPEED:
                            ApplySplineSpeedChange(reader, (unit, speed) => unit.SwimSpeed = speed, "swim speed");
                            break;
                        case Opcode.SMSG_SPLINE_SET_WALK_SPEED:
                            ApplySplineSpeedChange(reader, (unit, speed) => unit.WalkSpeed = speed, "walk speed");
                            break;
                        case Opcode.SMSG_SPLINE_SET_SWIM_BACK_SPEED:
                            ApplySplineSpeedChange(reader, (unit, speed) => unit.SwimBackSpeed = speed, "swim back speed");
                            break;
                        case Opcode.SMSG_SPLINE_SET_TURN_RATE:
                            ApplySplineSpeedChange(reader, (unit, speed) => unit.TurnRate = speed, "turn rate");
                            break;
                        case Opcode.MSG_MOVE_TIME_SKIPPED:
                            WoWSharpEventEmitter.Instance.FireOnMoveTimeSkipped(
                                ParseGuidCounterPacket(reader)
                            );
                            break;
                        case Opcode.MSG_MOVE_JUMP:
                            WoWSharpEventEmitter.Instance.FireOnCharacterJumpStart(
                                ParseMessageMove(reader)
                            );
                            break;
                        case Opcode.MSG_MOVE_FALL_LAND:
                            WoWSharpEventEmitter.Instance.FireOnCharacterFallLand(
                                ParseMessageMove(reader)
                            );
                            break;
                        case Opcode.MSG_MOVE_START_FORWARD:
                            WoWSharpEventEmitter.Instance.FireOnCharacterStartForward(
                                ParseMessageMove(reader)
                            );
                            break;
                        case Opcode.MSG_MOVE_STOP:
                            WoWSharpEventEmitter.Instance.FireOnCharacterMoveStop(
                                ParseMessageMove(reader)
                            );
                            break;
                        case Opcode.MSG_MOVE_START_STRAFE_LEFT:
                            WoWSharpEventEmitter.Instance.FireOnCharacterStartStrafeLeft(
                                ParseMessageMove(reader)
                            );
                            break;
                        case Opcode.MSG_MOVE_START_STRAFE_RIGHT:
                            WoWSharpEventEmitter.Instance.FireOnCharacterStartStrafeRight(
                                ParseMessageMove(reader)
                            );
                            break;
                        case Opcode.MSG_MOVE_STOP_STRAFE:
                            WoWSharpEventEmitter.Instance.FireOnCharacterStopStrafe(
                                ParseMessageMove(reader)
                            );
                            break;
                        case Opcode.MSG_MOVE_START_TURN_LEFT:
                            WoWSharpEventEmitter.Instance.FireOnCharacterStartTurnLeft(
                                ParseMessageMove(reader)
                            );
                            break;
                        case Opcode.MSG_MOVE_START_TURN_RIGHT:
                            WoWSharpEventEmitter.Instance.FireOnCharacterStartTurnRight(
                                ParseMessageMove(reader)
                            );
                            break;
                        case Opcode.MSG_MOVE_STOP_TURN:
                            WoWSharpEventEmitter.Instance.FireOnCharacterStopTurn(
                                ParseMessageMove(reader)
                            );
                            break;
                        case Opcode.MSG_MOVE_SET_FACING:
                            WoWSharpEventEmitter.Instance.FireOnCharacterSetFacing(
                                ParseMessageMove(reader)
                            );
                            break;
                        case Opcode.MSG_MOVE_START_PITCH_UP:
                        case Opcode.MSG_MOVE_START_PITCH_DOWN:
                        case Opcode.MSG_MOVE_STOP_PITCH:
                        case Opcode.MSG_MOVE_START_SWIM:
                        case Opcode.MSG_MOVE_STOP_SWIM:
                        case Opcode.MSG_MOVE_SET_RUN_MODE:
                        case Opcode.MSG_MOVE_SET_WALK_MODE:
                        case Opcode.MSG_MOVE_ROOT:
                        case Opcode.MSG_MOVE_UNROOT:
                        case Opcode.MSG_MOVE_FEATHER_FALL:
                        case Opcode.MSG_MOVE_HOVER:
                        case Opcode.MSG_MOVE_WATER_WALK:
                            ParseMessageMove(reader);
                            break;
                        case Opcode.MSG_MOVE_SET_PITCH:
                            ParseMessageMove(reader);
                            break;
                        case Opcode.MSG_MOVE_SET_RUN_BACK_SPEED:
                            ParseMessageMoveWithTrailingSpeed(
                                reader,
                                (movementBlock, speed) => movementBlock.RunBackSpeed = speed
                            );
                            break;
                        case Opcode.MSG_MOVE_SET_WALK_SPEED:
                            ParseMessageMoveWithTrailingSpeed(
                                reader,
                                (movementBlock, speed) => movementBlock.WalkSpeed = speed
                            );
                            break;
                        case Opcode.MSG_MOVE_SET_RUN_SPEED:
                            ParseMessageMoveWithTrailingSpeed(
                                reader,
                                (movementBlock, speed) => movementBlock.RunSpeed = speed
                            );
                            break;
                        case Opcode.MSG_MOVE_SET_SWIM_BACK_SPEED:
                            ParseMessageMoveWithTrailingSpeed(
                                reader,
                                (movementBlock, speed) => movementBlock.SwimBackSpeed = speed
                            );
                            break;
                        case Opcode.MSG_MOVE_SET_SWIM_SPEED:
                            ParseMessageMoveWithTrailingSpeed(
                                reader,
                                (movementBlock, speed) => movementBlock.SwimSpeed = speed
                            );
                            break;
                        case Opcode.MSG_MOVE_SET_TURN_RATE:
                            ParseMessageMoveWithTrailingSpeed(
                                reader,
                                (movementBlock, speed) => movementBlock.TurnRate = speed
                            );
                            break;
                        case Opcode.MSG_MOVE_START_BACKWARD:
                            WoWSharpEventEmitter.Instance.FireOnCharacterStartBackwards(
                                ParseMessageMove(reader)
                            );
                            break;
                        case Opcode.MSG_MOVE_HEARTBEAT:
                            ParseMessageMove(reader);
                            break;
                        default:
                            Log.Information($"{opcode} not handled");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Log.Information($"[MovementHandler] {ex}");
                }
            }
        }

        private static RequiresAcknowledgementArgs ParseGuidCounterPacket(BinaryReader reader)
        {
            var guid = ReaderUtils.ReadPackedGuid(reader);
            var counter = reader.ReadUInt32();

            return new(guid, counter);
        }

        private static RequiresAcknowledgementArgs ParseGuidCounterPacket(ulong guid, BinaryReader reader)
        {
            var counter = reader.ReadUInt32();
            return new(guid, counter);
        }

        private static RequiresAcknowledgementArgs ParseGuidCounterSpeedPacket(BinaryReader reader)
        {
            var guid = ReaderUtils.ReadPackedGuid(reader);
            var counter = reader.ReadUInt32();
            var speed = reader.ReadSingle();

            return new(guid, counter, speed);
        }

        private static RequiresAcknowledgementArgs ParseGuidCounterSpeedPacket(ulong guid, BinaryReader reader)
        {
            var counter = reader.ReadUInt32();
            var speed = reader.ReadSingle();
            return new(guid, counter, speed);
        }

        /// <summary>
        /// SMSG_MOVE_KNOCK_BACK: packed_guid + counter + vsin + vcos + hspeed + vspeed.
        /// WoW.exe inbound handler at 0x5E59B0. The sin/cos define the XY direction
        /// of the knockback, hspeed is horizontal magnitude, vspeed is vertical launch.
        /// </summary>
        private static KnockBackArgs ParseKnockBackPacket(BinaryReader reader)
        {
            var guid = ReaderUtils.ReadPackedGuid(reader);
            return ParseKnockBackPacket(guid, reader);
        }

        private static KnockBackArgs ParseKnockBackPacket(ulong guid, BinaryReader reader)
        {
            var counter = reader.ReadUInt32();
            var vSin = reader.ReadSingle();
            var vCos = reader.ReadSingle();
            var hSpeed = reader.ReadSingle();
            var vSpeed = reader.ReadSingle();

            return new KnockBackArgs(guid, counter, vSin, vCos, hSpeed, vSpeed);
        }

        private static ulong ParseMessageMove(BinaryReader reader)
        {
            var (packedGuid, movementData) = ParseMessageMoveData(reader);
            QueueMovementUpdate(packedGuid, movementData);
            return packedGuid;
        }

        private static ulong ParseMessageMoveWithTrailingSpeed(
            BinaryReader reader,
            Action<MovementBlockUpdate, float> applySpeed
        )
        {
            var (packedGuid, movementData) = ParseMessageMoveData(reader);

            if (reader.BaseStream.Length - reader.BaseStream.Position >= sizeof(float))
            {
                float speed = reader.ReadSingle();
                movementData.MovementBlockUpdate ??= new MovementBlockUpdate();
                applySpeed(movementData.MovementBlockUpdate, speed);
            }

            QueueMovementUpdate(packedGuid, movementData);
            return packedGuid;
        }

        private static (ulong PackedGuid, MovementInfoUpdate MovementData) ParseMessageMoveData(
            BinaryReader reader
        )
        {
            var packedGuid = ReaderUtils.ReadPackedGuid(reader);
            var movementData = MovementPacketHandler.ParseMovementInfo(reader);
            return (packedGuid, movementData);
        }

        private static void QueueMovementUpdate(ulong packedGuid, MovementInfoUpdate movementData)
        {
            WoWSharpObjectManager.Instance.QueueUpdate(
                new WoWSharpObjectManager.ObjectStateUpdate(
                    packedGuid,
                    WoWSharpObjectManager.ObjectUpdateOperation.Update,
                    WoWObjectType.None,
                    movementData,
                    []
                )
            );
        }

        private static void ParseCompressedMove(BinaryReader reader)
        {
            long entryOffset = reader.BaseStream.Position;
            if (entryOffset >= reader.BaseStream.Length)
                return;

            int entrySize = reader.ReadByte();
            long remaining = reader.BaseStream.Length - reader.BaseStream.Position;
            if (entrySize > remaining)
            {
                Log.Warning(
                    "[MovementHandler] Truncated compressed move entry at offset {Offset}: size={Size}, remaining={Remaining}.",
                    entryOffset,
                    entrySize,
                    remaining
                );
                reader.BaseStream.Position = reader.BaseStream.Length;
                return;
            }

            byte[] entryData = reader.ReadBytes(entrySize);
            using var entryStream = new MemoryStream(entryData);
            using var entryReader = new BinaryReader(entryStream);

            try
            {
                var compressedOpCode = (Opcode)entryReader.ReadUInt16();
                var guid = ReaderUtils.ReadPackedGuid(entryReader);

                //Log.Information($"[MovementHandler] {compressedOpCode}");
                switch (compressedOpCode)
                {
                    case Opcode.SMSG_FORCE_MOVE_ROOT:
                        WoWSharpEventEmitter.Instance.FireOnForceMoveRoot(
                            ParseGuidCounterPacket(guid, entryReader));
                        break;
                    case Opcode.SMSG_FORCE_MOVE_UNROOT:
                        WoWSharpEventEmitter.Instance.FireOnForceMoveUnroot(
                            ParseGuidCounterPacket(guid, entryReader));
                        break;
                    case Opcode.SMSG_MOVE_WATER_WALK:
                        WoWSharpEventEmitter.Instance.FireOnMoveWaterWalk(
                            ParseGuidCounterPacket(guid, entryReader));
                        break;
                    case Opcode.SMSG_MOVE_LAND_WALK:
                        WoWSharpEventEmitter.Instance.FireOnMoveLandWalk(
                            ParseGuidCounterPacket(guid, entryReader));
                        break;
                    case Opcode.SMSG_MOVE_SET_HOVER:
                        WoWSharpEventEmitter.Instance.FireOnMoveSetHover(
                            ParseGuidCounterPacket(guid, entryReader));
                        break;
                    case Opcode.SMSG_MOVE_UNSET_HOVER:
                        WoWSharpEventEmitter.Instance.FireOnMoveUnsetHover(
                            ParseGuidCounterPacket(guid, entryReader));
                        break;
                    case Opcode.SMSG_MOVE_FEATHER_FALL:
                        WoWSharpEventEmitter.Instance.FireOnMoveFeatherFall(
                            ParseGuidCounterPacket(guid, entryReader));
                        break;
                    case Opcode.SMSG_MOVE_NORMAL_FALL:
                        WoWSharpEventEmitter.Instance.FireOnMoveNormalFall(
                            ParseGuidCounterPacket(guid, entryReader));
                        break;
                    case Opcode.SMSG_FORCE_WALK_SPEED_CHANGE:
                        WoWSharpEventEmitter.Instance.FireOnForceWalkSpeedChange(
                            ParseGuidCounterSpeedPacket(guid, entryReader));
                        break;
                    case Opcode.SMSG_FORCE_RUN_SPEED_CHANGE:
                        WoWSharpEventEmitter.Instance.FireOnForceRunSpeedChange(
                            ParseGuidCounterSpeedPacket(guid, entryReader));
                        break;
                    case Opcode.SMSG_FORCE_RUN_BACK_SPEED_CHANGE:
                        WoWSharpEventEmitter.Instance.FireOnForceRunBackSpeedChange(
                            ParseGuidCounterSpeedPacket(guid, entryReader));
                        break;
                    case Opcode.SMSG_FORCE_SWIM_SPEED_CHANGE:
                        WoWSharpEventEmitter.Instance.FireOnForceSwimSpeedChange(
                            ParseGuidCounterSpeedPacket(guid, entryReader));
                        break;
                    case Opcode.SMSG_FORCE_SWIM_BACK_SPEED_CHANGE:
                        WoWSharpEventEmitter.Instance.FireOnForceSwimBackSpeedChange(
                            ParseGuidCounterSpeedPacket(guid, entryReader));
                        break;
                    case Opcode.SMSG_FORCE_TURN_RATE_CHANGE:
                        WoWSharpEventEmitter.Instance.FireOnForceTurnRateChange(
                            ParseGuidCounterSpeedPacket(guid, entryReader));
                        break;
                    case Opcode.SMSG_MOVE_KNOCK_BACK:
                        WoWSharpEventEmitter.Instance.FireOnForceMoveKnockBack(
                            ParseKnockBackPacket(guid, entryReader));
                        break;
                    case Opcode.SMSG_MONSTER_MOVE:
                        var moveData = ParseMonsterMove(entryReader);
                        QueueMonsterMoveUpdate(guid, moveData);
                        break;
                    case Opcode.SMSG_MONSTER_MOVE_TRANSPORT:
                        ulong transportGuid = ReaderUtils.ReadPackedGuid(entryReader);
                        moveData = ParseMonsterMove(entryReader);
                        ApplyTransportMoveState(moveData, transportGuid);
                        QueueMonsterMoveUpdate(guid, moveData);
                        break;
                    case Opcode.SMSG_SPLINE_SET_RUN_SPEED:
                        ApplySplineSpeedChange(guid, entryReader.ReadSingle(), (unit, speed) => unit.RunSpeed = speed, "run speed");
                        break;
                    case Opcode.SMSG_SPLINE_SET_RUN_BACK_SPEED:
                        ApplySplineSpeedChange(guid, entryReader.ReadSingle(), (unit, speed) => unit.RunBackSpeed = speed, "run back speed");
                        break;
                    case Opcode.SMSG_SPLINE_SET_SWIM_SPEED:
                        ApplySplineSpeedChange(guid, entryReader.ReadSingle(), (unit, speed) => unit.SwimSpeed = speed, "swim speed");
                        break;
                    case Opcode.SMSG_SPLINE_SET_WALK_SPEED:
                        ApplySplineSpeedChange(guid, entryReader.ReadSingle(), (unit, speed) => unit.WalkSpeed = speed, "walk speed");
                        break;
                    case Opcode.SMSG_SPLINE_SET_SWIM_BACK_SPEED:
                        ApplySplineSpeedChange(guid, entryReader.ReadSingle(), (unit, speed) => unit.SwimBackSpeed = speed, "swim back speed");
                        break;
                    case Opcode.SMSG_SPLINE_SET_TURN_RATE:
                        ApplySplineSpeedChange(guid, entryReader.ReadSingle(), (unit, speed) => unit.TurnRate = speed, "turn rate");
                        break;
                    case Opcode.SMSG_SPLINE_MOVE_SET_RUN_MODE:
                        ApplySplineFlagToggle(guid, MovementFlags.MOVEFLAG_WALK_MODE, apply: false, "set run mode");
                        break;
                    case Opcode.SMSG_SPLINE_MOVE_SET_WALK_MODE:
                        ApplySplineFlagToggle(guid, MovementFlags.MOVEFLAG_WALK_MODE, apply: true, "set walk mode");
                        break;
                    case Opcode.SMSG_SPLINE_MOVE_ROOT:
                        ApplySplineFlagToggle(guid, MovementFlags.MOVEFLAG_ROOT, apply: true, "rooted");
                        break;
                    case Opcode.SMSG_SPLINE_MOVE_UNROOT:
                        ApplySplineFlagToggle(guid, MovementFlags.MOVEFLAG_ROOT, apply: false, "unrooted");
                        break;
                    case Opcode.SMSG_SPLINE_MOVE_WATER_WALK:
                        ApplySplineFlagToggle(guid, MovementFlags.MOVEFLAG_WATERWALKING, apply: true, "water walk");
                        break;
                    case Opcode.SMSG_SPLINE_MOVE_LAND_WALK:
                        ApplySplineFlagToggle(guid, MovementFlags.MOVEFLAG_WATERWALKING, apply: false, "land walk");
                        break;
                    case Opcode.SMSG_SPLINE_MOVE_FEATHER_FALL:
                        ApplySplineFlagToggle(guid, MovementFlags.MOVEFLAG_SAFE_FALL, apply: true, "feather fall");
                        break;
                    case Opcode.SMSG_SPLINE_MOVE_NORMAL_FALL:
                        ApplySplineFlagToggle(guid, MovementFlags.MOVEFLAG_SAFE_FALL, apply: false, "normal fall");
                        break;
                    case Opcode.SMSG_SPLINE_MOVE_SET_HOVER:
                        ApplySplineFlagToggle(guid, MovementFlags.MOVEFLAG_HOVER, apply: true, "set hover");
                        break;
                    case Opcode.SMSG_SPLINE_MOVE_UNSET_HOVER:
                        ApplySplineFlagToggle(guid, MovementFlags.MOVEFLAG_HOVER, apply: false, "unset hover");
                        break;
                    case Opcode.SMSG_SPLINE_MOVE_START_SWIM:
                        ApplySplineFlagToggle(guid, MovementFlags.MOVEFLAG_SWIMMING, apply: true, "start swim");
                        break;
                    case Opcode.SMSG_SPLINE_MOVE_STOP_SWIM:
                        ApplySplineFlagToggle(guid, MovementFlags.MOVEFLAG_SWIMMING, apply: false, "stop swim");
                        break;
                    default:
                        // Keep parsing aligned: unsupported entries are safely skipped using entry size.
                        break;
                }
            }
            catch (EndOfStreamException ex)
            {
                Log.Warning(ex,
                    "[MovementHandler] Truncated compressed move payload at offset {Offset}.",
                    entryOffset);
            }
            catch (Exception ex)
            {
                Log.Warning(ex,
                    "[MovementHandler] Failed to parse compressed move payload at offset {Offset}.",
                    entryOffset);
            }
        }

        private static void ApplySplineSpeedChange(
            BinaryReader reader,
            Action<WoWUnit, float> apply,
            string description)
        {
            ulong guid = ReaderUtils.ReadPackedGuid(reader);
            float speed = reader.ReadSingle();
            ApplySplineSpeedChange(guid, speed, apply, description);
        }

        private static void ApplySplineSpeedChange(
            ulong guid,
            float speed,
            Action<WoWUnit, float> apply,
            string description)
        {
            var unit = WoWSharpObjectManager.Instance?.GetUnitByGuid(guid);
            if (unit != null)
                apply(unit, speed);

            Log.Information("[SPLINE] {Guid:X} {Description}={Speed:F2}",
                guid, description, speed);
        }

        private static void ApplySplineFlagToggle(
            BinaryReader reader,
            MovementFlags flag,
            bool apply,
            string description)
        {
            ulong guid = ReaderUtils.ReadPackedGuid(reader);
            ApplySplineFlagToggle(guid, flag, apply, description);
        }

        private static void ApplySplineFlagToggle(
            ulong guid,
            MovementFlags flag,
            bool apply,
            string description)
        {
            var unit = WoWSharpObjectManager.Instance?.GetUnitByGuid(guid);
            if (unit != null)
            {
                if (apply)
                    unit.MovementFlags |= flag;
                else
                    unit.MovementFlags &= ~flag;
            }

            Log.Information("[SPLINE] {Guid:X} {Description}", guid, description);
        }

        private static MovementInfoUpdate ParseMonsterMove(BinaryReader reader)
        {
            MovementInfoUpdate data = new()
            {
                X = reader.ReadSingle(),
                Y = reader.ReadSingle(),
                Z = reader.ReadSingle(),
                // SMSG_MONSTER_MOVE header: start position + server spline start time.
                // Keep it on MovementInfoUpdate.LastUpdated so runtime spline playback can
                // align to the server's clock instead of local receipt time.
                LastUpdated = reader.ReadUInt32(),
                MovementBlockUpdate = new()
                {
                    SplineType = (SplineType)reader.ReadByte(),
                },
            };
            data.MovementBlockUpdate.SplineStartTime = data.LastUpdated;

            switch (data.MovementBlockUpdate.SplineType)
            {
                case SplineType.FacingTarget:
                    data.MovementBlockUpdate.FacingTargetGuid = reader.ReadUInt64();
                    break;
                case SplineType.FacingAngle:
                    data.MovementBlockUpdate.FacingAngle = reader.ReadSingle();
                    break;
                case SplineType.FacingSpot:
                    float fx = reader.ReadSingle();
                    float fy = reader.ReadSingle();
                    float fz = reader.ReadSingle();
                    data.MovementBlockUpdate.FacingSpot = new Position(fx, fy, fz);
                    break;
            }

            if (data.MovementBlockUpdate.SplineType != SplineType.Stop)
            {
                data.MovementBlockUpdate.SplineFlags = (SplineFlags)reader.ReadUInt32();
                data.MovementBlockUpdate.SplineTimestamp = reader.ReadUInt32();

                uint splineCount = reader.ReadUInt32();
                var startPosition = new Position(data.X, data.Y, data.Z);
                data.MovementBlockUpdate.SplinePoints =
                    ParseMonsterMoveSplinePoints(reader, startPosition, data.MovementBlockUpdate.SplineFlags.Value, splineCount);
            }

            return data;
        }

        private static List<Position> ParseMonsterMoveSplinePoints(
            BinaryReader reader,
            Position startPosition,
            SplineFlags flags,
            uint splineCount)
        {
            if (splineCount == 0)
                return [];

            // Vanilla smooth paths (Flying flag / Catmull-Rom) serialize raw XYZ triplets.
            // Linear paths serialize the final destination plus packed offsets relative to it
            // via ByteBuffer::appendPackXYZ in VMaNGOS packet_builder.cpp.
            return flags.HasFlag(SplineFlags.Flying)
                ? ParseMonsterMoveCatmullRomPoints(reader, startPosition, flags, splineCount)
                : ParseMonsterMoveLinearPoints(reader, splineCount);
        }

        private static List<Position> ParseMonsterMoveCatmullRomPoints(
            BinaryReader reader,
            Position startPosition,
            SplineFlags flags,
            uint splineCount)
        {
            var rawPoints = new List<Position>((int)splineCount);
            for (int i = 0; i < splineCount; i++)
                rawPoints.Add(ReadMonsterMoveVector(reader));

            if (!flags.HasFlag(SplineFlags.Cyclic))
                return rawPoints;

            if (flags.HasFlag(SplineFlags.EnterCycle)
                && rawPoints.Count > 0
                && PositionsApproximatelyEqual(rawPoints[0], startPosition))
            {
                // Cyclic Catmull-Rom packets prepend a fake start vertex; the client erases it
                // after the first cycle, so normalize to the managed runtime's simpler
                // [start, ...nodes..., start] representation up front.
                rawPoints.RemoveAt(0);
            }

            if (rawPoints.Count > 0
                && PositionsApproximatelyEqual(rawPoints[0], startPosition))
            {
                Position closingLoopPoint = rawPoints[0];
                rawPoints.RemoveAt(0);
                rawPoints.Add(closingLoopPoint);
            }

            return rawPoints;
        }

        private static List<Position> ParseMonsterMoveLinearPoints(BinaryReader reader, uint splineCount)
        {
            var destination = ReadMonsterMoveVector(reader);
            var points = new List<Position>((int)splineCount);

            for (int i = 1; i < splineCount; i++)
            {
                var offset = ReadMonsterMovePackedOffset(reader.ReadUInt32());
                points.Add(new Position(
                    destination.X - offset.X,
                    destination.Y - offset.Y,
                    destination.Z - offset.Z));
            }

            points.Add(destination);
            return points;
        }

        private static Position ReadMonsterMoveVector(BinaryReader reader) =>
            new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

        private static Position ReadMonsterMovePackedOffset(uint packed)
        {
            int dx = SignExtend(packed & 0x7FF, 11);
            int dy = SignExtend((packed >> 11) & 0x7FF, 11);
            int dz = SignExtend((packed >> 22) & 0x3FF, 10);

            return new Position(dx / 4.0f, dy / 4.0f, dz / 4.0f);
        }

        private static bool PositionsApproximatelyEqual(Position left, Position right) =>
            MathF.Abs(left.X - right.X) <= 0.01f
            && MathF.Abs(left.Y - right.Y) <= 0.01f
            && MathF.Abs(left.Z - right.Z) <= 0.01f;

        private static int SignExtend(uint value, int bits)
        {
            int shift = 32 - bits;
            return (int)(value << shift) >> shift;
        }

        private static void QueueMonsterMoveUpdate(ulong moverGuid, MovementInfoUpdate moveData)
        {
            WoWSharpObjectManager.Instance.QueueUpdate(
                new WoWSharpObjectManager.ObjectStateUpdate(
                    moverGuid,
                    WoWSharpObjectManager.ObjectUpdateOperation.Update,
                    WoWObjectType.Unit,
                    moveData,
                    []
                )
            );
        }

        private static void ApplyTransportMoveState(MovementInfoUpdate moveData, ulong transportGuid)
        {
            moveData.TransportGuid = transportGuid;
            moveData.TransportOffset = new Position(moveData.X, moveData.Y, moveData.Z);
        }
    }
}
