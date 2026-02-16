using System;
using System.IO;
using WoWSharpClient.Networking.I;

namespace WoWSharpClient.Networking.Implementation
{
    /// <summary>
    /// WoW-specific message framer that handles WoW protocol headers.
    /// WoW uses a 4-byte header: 2 bytes for size (big-endian) + 2 bytes for opcode (little-endian).
    /// </summary>
    public sealed class WoWMessageFramer : IMessageFramer, IDisposable
    {
        private const int HeaderSize = 4;
        private readonly MemoryStream _buffer = new();
        private bool _disposed;

        public ReadOnlyMemory<byte> Frame(ReadOnlyMemory<byte> payload)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(WoWMessageFramer));

            // WoW uses size+opcode as the header, but the payload already contains this
            // so we just pass it through. This allows the encryption layer to handle the actual header.
            return payload;
        }

        public void Append(ReadOnlyMemory<byte> incoming)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(WoWMessageFramer));

            _buffer.Write(incoming.Span);
        }

        public bool TryPop(out ReadOnlyMemory<byte> message)
        {
            message = default;

            if (_disposed)
                throw new ObjectDisposedException(nameof(WoWMessageFramer));

            if (_buffer.Length < HeaderSize)
                return false;

            var bufferData = _buffer.ToArray();
            
            // WoW header format: 2 bytes size (big-endian) + 2 bytes opcode (little-endian)
            ushort size = (ushort)((bufferData[0] << 8) | bufferData[1]);
            
            // Size includes the opcode but not the size field itself
            var totalMessageSize = size + 2; // +2 for the size field

            if (_buffer.Length < totalMessageSize)
                return false;

            // Extract the complete message (including header)
            message = new ReadOnlyMemory<byte>(bufferData, 0, (int)totalMessageSize);

            // Remove the processed message from buffer
            var remainingData = new byte[_buffer.Length - totalMessageSize];
            Array.Copy(bufferData, (int)totalMessageSize, remainingData, 0, remainingData.Length);
            
            _buffer.SetLength(0);
            if (remainingData.Length > 0)
                _buffer.Write(remainingData);

            return true;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _buffer.Dispose();
                _disposed = true;
            }
        }
    }
}