using System;
using System.IO;
using GameData.Core.Enums;
using WoWSharpClient.Networking.Abstractions;

namespace WoWSharpClient.Networking.Implementation
{
    /// <summary>
    /// WoW protocol packet codec that handles encoding and decoding of WoW packets.
    /// WoW packet format: 2-byte size (big-endian) + 2-byte opcode (little-endian) + payload
    /// </summary>
    public sealed class WoWPacketCodec : IPacketCodec<Opcode>
    {
        public ReadOnlyMemory<byte> Encode(Opcode opcode, ReadOnlyMemory<byte> payload)
        {
            // Calculate total size: opcode (2 bytes) + payload
            var packetSize = sizeof(ushort) + payload.Length;
            var encoded = new byte[sizeof(ushort) + packetSize]; // size field + packet

            // Write size as big-endian (WoW protocol)
            var sizeBytes = BitConverter.GetBytes((ushort)packetSize);
            if (BitConverter.IsLittleEndian)
            {
                encoded[0] = sizeBytes[1];
                encoded[1] = sizeBytes[0];
            }
            else
            {
                encoded[0] = sizeBytes[0];
                encoded[1] = sizeBytes[1];
            }

            // Write opcode as little-endian
            var opcodeBytes = BitConverter.GetBytes((ushort)opcode);
            encoded[2] = opcodeBytes[0];
            encoded[3] = opcodeBytes[1];

            // Write payload
            payload.Span.CopyTo(encoded.AsSpan(4));

            return encoded;
        }

        public bool TryDecode(ReadOnlyMemory<byte> message, out Opcode opcode, out ReadOnlyMemory<byte> payload)
        {
            opcode = default;
            payload = default;

            if (message.Length < 4) // Need at least size + opcode
                return false;

            // Read size (big-endian) - but we don't really need it since we have the complete message
            // ushort size = (ushort)((message.Span[0] << 8) | message.Span[1]);

            // Read opcode (little-endian) from bytes 2-3
            var opcodeValue = (ushort)(message.Span[2] | (message.Span[3] << 8));

            if (!Enum.IsDefined(typeof(Opcode), (uint)opcodeValue))
            {
                // Unknown opcode, but we can still try to process it
                Console.WriteLine($"Unknown opcode: {opcodeValue:X4}");
            }

            opcode = (Opcode)opcodeValue;
            
            // Payload starts after the header (skip size + opcode = 4 bytes)
            payload = message.Slice(4);

            return true;
        }
    }
}