using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using WoWSharpClient.Networking.Abstractions;

namespace WoWSharpClient.Networking.Implementation
{
    /// <summary>
    /// Routes messages with opcodes to registered handlers.
    /// </summary>
    /// <typeparam name="TOpcode">The type of opcode to route.</typeparam>
    public sealed class MessageRouter<TOpcode> : IMessageRouter<TOpcode> where TOpcode : Enum
    {
        private readonly ConcurrentDictionary<TOpcode, Func<ReadOnlyMemory<byte>, Task>> _handlers = new();

        public void Register(TOpcode opcode, Func<ReadOnlyMemory<byte>, Task> handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            _handlers.AddOrUpdate(opcode, handler, (_, _) => handler);
        }

        public async Task RouteAsync(TOpcode opcode, ReadOnlyMemory<byte> payload)
        {
            if (_handlers.TryGetValue(opcode, out var handler))
            {
                try
                {
                    await handler(payload);
                }
                catch (Exception ex)
                {
                    // Log the exception but don't let it crash the router
                    Console.WriteLine($"Error handling opcode {opcode}: {ex}");
                }
            }
            else
            {
                // No handler registered for this opcode
                Console.WriteLine($"No handler registered for opcode {opcode}");
            }
        }
    }
}