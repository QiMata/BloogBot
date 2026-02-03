namespace WoWSharpClient.Networking.Abstractions
{
    /// <summary>
    /// Routes messages with opcodes to appropriate handlers.
    /// </summary>
    /// <typeparam name="TOpcode">The type of opcode to route.</typeparam>
    public interface IMessageRouter<TOpcode> where TOpcode : Enum
    {
        /// <summary>
        /// Registers a handler for the specified opcode.
        /// </summary>
        /// <param name="opcode">The opcode to handle.</param>
        /// <param name="handler">The handler function to invoke.</param>
        void Register(TOpcode opcode, Func<ReadOnlyMemory<byte>, Task> handler);

        /// <summary>
        /// Routes a message with the specified opcode to its registered handler.
        /// </summary>
        /// <param name="opcode">The opcode of the message.</param>
        /// <param name="payload">The message payload.</param>
        /// <returns>A task representing the asynchronous routing operation.</returns>
        Task RouteAsync(TOpcode opcode, ReadOnlyMemory<byte> payload);
    }
}