using System;

namespace WoWSharpClient.Networking.I
{
    /// <summary>
    /// Handles framing and de-framing of messages from raw byte streams.
    /// </summary>
    public interface IMessageFramer
    {
        /// <summary>
        /// Frames a payload by adding appropriate headers/footers.
        /// </summary>
        /// <param name="payload">The payload to frame.</param>
        /// <returns>The framed data ready for transmission.</returns>
        ReadOnlyMemory<byte> Frame(ReadOnlyMemory<byte> payload);

        /// <summary>
        /// Appends incoming bytes to the internal buffer.
        /// </summary>
        /// <param name="incoming">The incoming bytes to append.</param>
        void Append(ReadOnlyMemory<byte> incoming);

        /// <summary>
        /// Attempts to extract a complete message from the internal buffer.
        /// </summary>
        /// <param name="message">The complete message if available.</param>
        /// <returns>True if a complete message was extracted, false otherwise.</returns>
        bool TryPop(out ReadOnlyMemory<byte> message);
    }
}
