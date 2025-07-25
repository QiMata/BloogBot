﻿using GameData.Core.Enums;
using GameData.Core.Interfaces;

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
                Console.WriteLine($"Error reading login verify world packet: {e.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
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
                Console.WriteLine($"Error reading login verify world packet: {e.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
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
                Console.WriteLine($"[LoginHandler] SMSG_QUERY_TIME_RESPONSE {serverTime}");
                // Process the login verification as needed
                //_woWSharpEventEmitter.FireOnSetTimeSpeed(new OnSetTimeSpeedArgs(serverTime, timescale));
            }
            catch (EndOfStreamException e)
            {
                Console.WriteLine($"Error reading login verify world packet: {e.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
            }
        }
    }
}
