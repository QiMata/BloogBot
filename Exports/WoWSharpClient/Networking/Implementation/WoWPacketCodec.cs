using GameData.Core.Enums;
using System;
using WoWSharpClient.Networking.Abstractions;

namespace WoWSharpClient.Networking.Implementation
{
    /// <summary>
    /// WoW protocol packet codec that handles encoding and decoding of WoW packets.
    /// Client→Server: 2-byte size (big-endian) + 4-byte opcode (little-endian) + payload
    /// Server→Client: 2-byte size (big-endian) + 2-byte opcode (little-endian) + payload
    /// </summary>
    public sealed class WoWPacketCodec : IPacketCodec<Opcode>
    {
        public ReadOnlyMemory<byte> Encode(Opcode opcode, ReadOnlyMemory<byte> payload)
        {
            // Client→Server header: 2 bytes size (big-endian) + 4 bytes opcode (little-endian)
            // size = opcode(4) + payload.Length (size field does not include itself)
            var packetSize = (ushort)(sizeof(uint) + payload.Length);
            var encoded = new byte[sizeof(ushort) + packetSize]; // size(2) + opcode(4) + payload

            // Write size as big-endian
            encoded[0] = (byte)((packetSize >> 8) & 0xFF);
            encoded[1] = (byte)(packetSize & 0xFF);

            // Write opcode as 4-byte little-endian
            var opcodeValue = (uint)(object)opcode;
            encoded[2] = (byte)(opcodeValue & 0xFF);
            encoded[3] = (byte)((opcodeValue >> 8) & 0xFF);
            encoded[4] = (byte)((opcodeValue >> 16) & 0xFF);
            encoded[5] = (byte)((opcodeValue >> 24) & 0xFF);

            // Write payload
            payload.Span.CopyTo(encoded.AsSpan(6));

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