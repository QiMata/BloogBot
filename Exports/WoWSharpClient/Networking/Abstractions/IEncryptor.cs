namespace WoWSharpClient.Networking.Abstractions
{
    /// <summary>
    /// Provides optional encryption/decryption transforms for data.
    /// </summary>
    public interface IEncryptor
    {
        /// <summary>
        /// Encrypts the provided data.
        /// </summary>
        /// <param name="data">The data to encrypt.</param>
        /// <returns>The encrypted data.</returns>
        ReadOnlyMemory<byte> Encrypt(ReadOnlyMemory<byte> data);

        /// <summary>
        /// Decrypts the provided data.
        /// </summary>
        /// <param name="data">The data to decrypt.</param>
        /// <returns>The decrypted data.</returns>
        ReadOnlyMemory<byte> Decrypt(ReadOnlyMemory<byte> data);
    }
}