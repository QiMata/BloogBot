using GameData.Core.Enums;
using System;

namespace WoWSharpClient.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class PacketHandlerAttribute : Attribute
    {
        public PacketHandlerAttribute(Opcode opcode)
        {
            Opcode = opcode;
        }

        public PacketHandlerAttribute(uint opcode)
        {
            Opcode = (Opcode)opcode;
        }

        public Opcode Opcode { get; private set; }
    }
}
