using System;
using WoWSharpClient.Networking.Abstractions;
using WowSrp.Header;

namespace WoWSharpClient.Networking.Implementation
{
    /// <summary>
    /// WoW 1.12.1 (Vanilla) header encryption/decryption using the session key.
    /// Only the packet header bytes are encrypted; payload data passes through unchanged.
    ///
    /// Server→Client header: 4 bytes (2 size big-endian + 2 opcode little-endian)
    /// Client→Server header: 6 bytes (2 size big-endian + 4 opcode little-endian)
    ///
    /// IMPORTANT: This encryptor is stateful (stream cipher). Each Encrypt/Decrypt call
    /// advances the internal cipher state. The caller must ensure that exactly the header
    /// bytes are processed per packet.
    ///
    /// Integration note: The current PacketPipeline calls Encrypt/Decrypt on full messages
    /// (header + payload). This encryptor handles that by only encrypting/decrypting the
    /// header portion and passing the payload through unchanged.
    ///
    /// However, for Decrypt to work correctly with TCP streaming (where received chunks
    /// don't align with message boundaries), the WoWMessageFramer must be modified to
    /// call Decrypt on header bytes after buffering. This is tracked as a future task.
    /// </summary>
    public sealed class VanillaHeaderEncryptor : IEncryptor
    {
        private readonly VanillaEncryption _encryption;
        private readonly VanillaDecryption _decryption;

        private const int ServerHeaderSize = 4; // S→C: 2 size + 2 opcode
        private const int ClientHeaderSize = 6; // C→S: 2 size + 4 opcode

        /// <summary>
        /// Creates a new VanillaHeaderEncryptor from the 40-byte session key.
        /// </summary>
        /// <param name="sessionKey">The 40-byte session key K from SRP6 authentication.</param>
        public VanillaHeaderEncryptor(byte[] sessionKey)
        {
            ArgumentNullException.ThrowIfNull(sessionKey);
            if (sessionKey.Length != 40)
                throw new ArgumentException($"Session key must be 40 bytes, got {sessionKey.Length}", nameof(sessionKey));

            _encryption = new VanillaEncryption(sessionKey);
            _decryption = new VanillaDecryption(sessionKey);
        }

        /// <summary>
        /// Encrypts the client→server packet header (first 6 bytes).
        /// The payload (remaining bytes) passes through unchanged.
        /// </summary>
        public ReadOnlyMemory<byte> Encrypt(ReadOnlyMemory<byte> data)
        {
            if (data.Length < ClientHeaderSize)
                return data;

            var encrypted = data.ToArray();
            _encryption.Encrypt(encrypted.AsSpan(0, ClientHeaderSize));
            return encrypted;
        }

        /// <summary>
        /// Decrypts the server→client packet header (first 4 bytes).
        /// The payload (remaining bytes) passes through unchanged.
        ///
        /// WARNING: This only works correctly when called with exactly one complete
        /// message at a time. For TCP streaming where data arrives in arbitrary chunks,
        /// the framer must handle decryption of header bytes after buffering.
        /// </summary>
        public ReadOnlyMemory<byte> Decrypt(ReadOnlyMemory<byte> data)
        {
            if (data.Length < ServerHeaderSize)
                return data;

            var decrypted = data.ToArray();
            _decryption.Decrypt(decrypted.AsSpan(0, ServerHeaderSize));
            return decrypted;
        }
    }
}
