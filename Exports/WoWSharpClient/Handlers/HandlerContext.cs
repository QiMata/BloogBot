namespace WoWSharpClient.Handlers
{
    /// <summary>
    /// Context passed to legacy opcode handlers, providing instance-scoped access
    /// to the ObjectManager and EventEmitter instead of static singletons.
    /// </summary>
    public sealed class HandlerContext(WoWSharpObjectManager objectManager, WoWSharpEventEmitter eventEmitter)
    {
        public WoWSharpObjectManager ObjectManager { get; } = objectManager;
        public WoWSharpEventEmitter EventEmitter { get; } = eventEmitter;
    }
}
