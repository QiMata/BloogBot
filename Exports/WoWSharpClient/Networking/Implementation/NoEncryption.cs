using System;
using WoWSharpClient.Networking.Abstractions;

namespace WoWSharpClient.Networking.Implementation
{
    /// <summary>
    /// A no-operation encryptor that passes data through unchanged.
    /// </summary>
    public sealed class NoEncryption : IEncryptor
    {
        public ReadOnlyMemory<byte> Encrypt(ReadOnlyMemory<byte> data)
        {
            return data;
        }

        public ReadOnlyMemory<byte> Decrypt(ReadOnlyMemory<byte> data)
        {
            return data;
        }
    }
}