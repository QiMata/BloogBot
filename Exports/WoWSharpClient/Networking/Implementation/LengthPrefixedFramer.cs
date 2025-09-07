using System;
using System.Buffers;
using System.IO;
using WoWSharpClient.Networking.I;

namespace WoWSharpClient.Networking.Implementation
{
    /// <summary>
    /// A message framer that uses length-prefixed framing.
    /// </summary>
    public sealed class LengthPrefixedFramer : IMessageFramer, IDisposable
    {
        private readonly int _headerSize;
        private readonly bool _bigEndian;
        private readonly MemoryStream _buffer = new();
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="LengthPrefixedFramer"/> class.
        /// </summary>
        /// <param name="headerSize">The size of the length header in bytes (2 or 4).</param>
        /// <param name="bigEndian">Whether to use big-endian byte order.</param>
        public LengthPrefixedFramer(int headerSize = 4, bool bigEndian = false)
        {
            if (headerSize != 2 && headerSize != 4)
                throw new ArgumentException("Header size must be 2 or 4 bytes", nameof(headerSize));

            _headerSize = headerSize;
            _bigEndian = bigEndian;
        }

        public ReadOnlyMemory<byte> Frame(ReadOnlyMemory<byte> payload)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(LengthPrefixedFramer));

            var totalLength = _headerSize + payload.Length;
            var framedData = new byte[totalLength];

            // Write length header
            if (_headerSize == 2)
            {
                var length = (ushort)payload.Length;
                if (_bigEndian)
                {
                    framedData[0] = (byte)(length >> 8);
                    framedData[1] = (byte)(length & 0xFF);
                }
                else
                {
                    framedData[0] = (byte)(length & 0xFF);
                    framedData[1] = (byte)(length >> 8);
                }
            }
            else // 4 bytes
            {
                var length = (uint)payload.Length;
                if (_bigEndian)
                {
                    framedData[0] = (byte)(length >> 24);
                    framedData[1] = (byte)((length >> 16) & 0xFF);
                    framedData[2] = (byte)((length >> 8) & 0xFF);
                    framedData[3] = (byte)(length & 0xFF);
                }
                else
                {
                    framedData[0] = (byte)(length & 0xFF);
                    framedData[1] = (byte)((length >> 8) & 0xFF);
                    framedData[2] = (byte)((length >> 16) & 0xFF);
                    framedData[3] = (byte)(length >> 24);
                }
            }

            // Copy payload
            payload.Span.CopyTo(framedData.AsSpan(_headerSize));

            return framedData;
        }

        public void Append(ReadOnlyMemory<byte> incoming)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(LengthPrefixedFramer));

            _buffer.Write(incoming.Span);
        }

        public bool TryPop(out ReadOnlyMemory<byte> message)
        {
            message = default;

            if (_disposed)
                throw new ObjectDisposedException(nameof(LengthPrefixedFramer));

            if (_buffer.Length < _headerSize)
                return false;

            // Read length from header
            var bufferData = _buffer.ToArray();
            uint messageLength;

            if (_headerSize == 2)
            {
                if (_bigEndian)
                    messageLength = (uint)((bufferData[0] << 8) | bufferData[1]);
                else
                    messageLength = (uint)(bufferData[0] | (bufferData[1] << 8));
            }
            else // 4 bytes
            {
                if (_bigEndian)
                    messageLength = (uint)((bufferData[0] << 24) | (bufferData[1] << 16) | (bufferData[2] << 8) | bufferData[3]);
                else
                    messageLength = (uint)(bufferData[0] | (bufferData[1] << 8) | (bufferData[2] << 16) | (bufferData[3] << 24));
            }

            // Check if we have the complete message
            var totalMessageSize = _headerSize + messageLength;
            if (_buffer.Length < totalMessageSize)
                return false;

            // Extract the message payload (without header)
            message = new ReadOnlyMemory<byte>(bufferData, _headerSize, (int)messageLength);

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