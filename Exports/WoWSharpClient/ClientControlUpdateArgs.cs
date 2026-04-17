using System;

namespace WoWSharpClient
{
    public sealed class ClientControlUpdateArgs(ulong guid, bool canControl) : EventArgs
    {
        public ulong Guid { get; } = guid;
        public bool CanControl { get; } = canControl;
    }
}
