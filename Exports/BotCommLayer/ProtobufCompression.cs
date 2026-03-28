using System;
using System.IO;
using System.IO.Compression;

namespace BotCommLayer
{
    /// <summary>
    /// Length-prefixed wire format with optional GZip compression.
    ///
    /// Wire layout:
    ///   [4 bytes: payload length (little-endian int32)]
    ///   [1 byte:  compression flag — 0x00 = raw, 0x01 = GZip]
    ///   [N bytes: payload (raw protobuf or GZip-compressed protobuf)]
    ///
    /// The length field includes the compression flag byte, so total wire bytes = 4 + length.
    /// Compression is applied when the raw protobuf payload exceeds <see cref="CompressionThresholdBytes"/>.
    /// </summary>
    public static class ProtobufCompression
    {
        private const byte FlagRaw = 0x00;
        private const byte FlagGzip = 0x01;

        /// <summary>
        /// Payloads smaller than this are sent uncompressed.
        /// Protobuf messages under 1KB don't benefit from GZip overhead.
        /// </summary>
        public static int CompressionThresholdBytes { get; set; } = 1024;

        /// <summary>
        /// Wrap a serialized protobuf byte[] into the wire format:
        /// [4-byte length][1-byte flag][payload].
        /// Compresses with GZip if payload exceeds threshold.
        /// </summary>
        public static byte[] Encode(byte[] rawProtobuf)
        {
            byte[] payload;
            byte flag;

            if (rawProtobuf.Length > CompressionThresholdBytes)
            {
                var compressed = GZipCompress(rawProtobuf);
                // Only use compressed version if it's actually smaller
                if (compressed.Length < rawProtobuf.Length)
                {
                    payload = compressed;
                    flag = FlagGzip;
                }
                else
                {
                    payload = rawProtobuf;
                    flag = FlagRaw;
                }
            }
            else
            {
                payload = rawProtobuf;
                flag = FlagRaw;
            }

            // Wire: [4-byte length of (flag + payload)][flag][payload]
            var wireLength = 1 + payload.Length;
            var result = new byte[4 + wireLength];
            BitConverter.TryWriteBytes(result.AsSpan(0, 4), wireLength);
            result[4] = flag;
            Buffer.BlockCopy(payload, 0, result, 5, payload.Length);
            return result;
        }

        /// <summary>
        /// Decode payload bytes (after the 4-byte length prefix has been read and stripped).
        /// The first byte is the compression flag; remaining bytes are the payload.
        /// Backward-compatible: if the first byte is not a known compression flag (0x00 or 0x01),
        /// the entire buffer is treated as a legacy raw protobuf message (no flag byte).
        /// </summary>
        public static byte[] Decode(byte[] wirePayload)
        {
            if (wirePayload.Length < 1)
                return wirePayload;

            var flag = wirePayload[0];

            return flag switch
            {
                FlagRaw => wirePayload.AsSpan(1, wirePayload.Length - 1).ToArray(),
                FlagGzip => GZipDecompress(wirePayload, 1, wirePayload.Length - 1),
                _ => wirePayload  // Legacy format: no flag byte, entire buffer is raw protobuf
            };
        }

        private static byte[] GZipCompress(byte[] data)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
            {
                gzip.Write(data, 0, data.Length);
            }
            return output.ToArray();
        }

        private static byte[] GZipDecompress(byte[] data, int offset, int count)
        {
            using var input = new MemoryStream(data, offset, count);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }
    }
}
