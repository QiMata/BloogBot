using System.IO;
using GameData.Core.Enums;
using WoWSharpClient.Utils;

namespace WoWSharpClient.Handlers
{
    public static class ClientControlHandler
    {
        public static void HandleClientControlUpdate(Opcode opCode, byte[] payload, HandlerContext ctx)
        {
            BinaryReader reader = new(new MemoryStream(payload));
            ulong guid = ReaderUtils.ReadPackedGuid(reader);
            bool canControl = reader.ReadByte() != 0;

            ctx.EventEmitter.FireOnClientControlUpdate(new ClientControlUpdateArgs(guid, canControl));
        }
    }
}
