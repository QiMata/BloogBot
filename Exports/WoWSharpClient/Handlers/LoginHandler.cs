using GameData.Core.Enums;
using GameData.Core.Interfaces;
using Serilog;
using System;
using System.IO;

namespace WoWSharpClient.Handlers
{
    public static class LoginHandler
    {
        public static void HandleLoginVerifyWorld(Opcode opcode, byte[] data)
        {
            using var reader = new BinaryReader(new MemoryStream(data));
            try
            {
                // Check if the packet length is correct
                if (reader.BaseStream.Length < 20)
                {
                    throw new EndOfStreamException("Packet is too short to contain all required fields.");
                }

                // Read MapId (4 bytes)
                uint mapId = reader.ReadUInt32();

                // Read X position (4 bytes)
                float positionX = reader.ReadSingle();

                // Read Y position (4 bytes)
                float positionY = reader.ReadSingle();

                // Read Z position (4 bytes)
                float positionZ = reader.ReadSingle();

                // Read Orientation (4 bytes)
                float facing = reader.ReadSingle();

                // Process the login verification as needed
                WoWSharpEventEmitter.Instance.FireOnLoginVerifyWorld(new WorldInfo
                {
                    MapId = mapId,
                    PositionX = positionX,
                    PositionY = positionY,
                    PositionZ = positionZ,
                    Facing = facing
                });
            }
            catch (EndOfStreamException e)
            {
                Log.Error($"Error reading login verify world packet: {e.Message}");
            }
            catch (Exception ex)
            {
                Log.Error($"Unexpected error: {ex.Message}");
            }
        }
        /// <summary>
        /// Handles SMSG_TRANSFER_PENDING (0x3F) — server is initiating a cross-map transfer.
        /// Format: uint32 mapId, [optional: uint32 transportId, uint32 transportMapId]
        /// The actual worldport ACK is deferred to HandleNewWorld (SMSG_NEW_WORLD).
        /// </summary>
        public static void HandleTransferPending(Opcode opcode, byte[] data)
        {
            using var reader = new BinaryReader(new MemoryStream(data));
            try
            {
                uint mapId = reader.ReadUInt32();
                Log.Information("[LoginHandler] SMSG_TRANSFER_PENDING: transferring to map {MapId} (ACK deferred to SMSG_NEW_WORLD)", mapId);
            }
            catch (Exception ex)
            {
                Log.Error("[LoginHandler] Error handling SMSG_TRANSFER_PENDING: {Error}", ex.Message);
            }
        }

        /// <summary>
        /// Handles SMSG_NEW_WORLD (0x3E) — server has placed us in a new map after a far teleport.
        /// Format: uint32 mapId, float x, float y, float z, float orientation
        /// We must respond with MSG_MOVE_WORLDPORT_ACK to finalize the transfer.
        /// </summary>
        public static void HandleNewWorld(Opcode opcode, byte[] data)
        {
            using var reader = new BinaryReader(new MemoryStream(data));
            try
            {
                uint mapId = reader.ReadUInt32();
                float x = reader.ReadSingle();
                float y = reader.ReadSingle();
                float z = reader.ReadSingle();
                float orientation = reader.ReadSingle();
                Log.Information("[LoginHandler] SMSG_NEW_WORLD: map={MapId} pos=({X:F1},{Y:F1},{Z:F1}) facing={O:F2}",
                    mapId, x, y, z, orientation);

                // NOW send the worldport ACK — this is the correct time per 1.12.1 protocol
                WoWSharpObjectManager.Instance.SendWorldportAck();
            }
            catch (Exception ex)
            {
                Log.Error("[LoginHandler] Error handling SMSG_NEW_WORLD: {Error}", ex.Message);
            }
        }

        public static void HandleSetTimeSpeed(Opcode opcode, byte[] data)
        {
            using var reader = new BinaryReader(new MemoryStream(data));
            try
            {
                // Check if the packet length is correct
                if (reader.BaseStream.Length < 8)
                {
                    throw new EndOfStreamException("Packet is too short to contain all required fields.");
                }

                // Read MapId (4 bytes)
                uint serverTime = reader.ReadUInt32();

                // Read X position (4 bytes)
                float timescale = reader.ReadSingle();

                // Process the login verification as needed
                WoWSharpEventEmitter.Instance.FireOnSetTimeSpeed(new OnSetTimeSpeedArgs(serverTime, timescale));
            }
            catch (EndOfStreamException e)
            {
                Log.Error($"Error reading login verify world packet: {e.Message}");
            }
            catch (Exception ex)
            {
                Log.Error($"Unexpected error: {ex.Message}");
            }
        }
        public static void HandleTimeQueryResponse(Opcode opcode, byte[] data)
        {
            using var reader = new BinaryReader(new MemoryStream(data));
            try
            {
                // Check if the packet length is correct
                if (reader.BaseStream.Length < 4)
                {
                    throw new EndOfStreamException("Packet is too short to contain all required fields.");
                }

                // Read MapId (4 bytes)
                uint serverTime = reader.ReadUInt32();
                Log.Error($"[LoginHandler] SMSG_QUERY_TIME_RESPONSE {serverTime}");
                // Process the login verification as needed
                //_woWSharpEventEmitter.FireOnSetTimeSpeed(new OnSetTimeSpeedArgs(serverTime, timescale));
            }
            catch (EndOfStreamException e)
            {
                Log.Error($"Error reading login verify world packet: {e.Message}");
            }
            catch (Exception ex)
            {
                Log.Error($"Unexpected error: {ex.Message}");
            }
        }
    }
}
