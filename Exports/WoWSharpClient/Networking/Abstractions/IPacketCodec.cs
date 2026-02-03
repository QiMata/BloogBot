namespace WoWSharpClient.Networking.Abstractions
{
    /// <summary>
    /// Handles encoding and decoding of packets with opcodes.
    /// </summary>
    /// <typeparam name="TOpcode">The type of opcode to use.</typeparam>
    public interface IPacketCodec<TOpcode> where TOpcode : Enum
    {
        /// <summary>
        /// Encodes an opcode and payload into a packet.
        /// </summary>
        /// <param name="opcode">The opcode to encode.</param>
        /// <param name="payload">The payload to encode.</param>
        /// <returns>The encoded packet data.</returns>
        ReadOnlyMemory<byte> Encode(TOpcode opcode, ReadOnlyMemory<byte> payload);

        /// <summary>
        /// Attempts to decode a message into an opcode and payload.
        /// </summary>
        /// <param name="message">The message to decode.</param>
        /// <param name="opcode">The decoded opcode if successful.</param>
        /// <param name="payload">The decoded payload if successful.</param>
        /// <returns>True if decoding was successful, false otherwise.</returns>
        bool TryDecode(ReadOnlyMemory<byte> message, out TOpcode opcode, out ReadOnlyMemory<byte> payload);
    }
}