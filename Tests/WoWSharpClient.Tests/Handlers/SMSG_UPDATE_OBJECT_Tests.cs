﻿using GameData.Core.Enums;
using WoWSharpClient.Handlers;
using WoWSharpClient.Tests.Util;

namespace WoWSharpClient.Tests.Handlers
{
    [Collection("Sequential ObjectManager tests")]
    public class SMSG_UPDATE_OBJECT_Tests(ObjectManagerFixture _) : IClassFixture<ObjectManagerFixture>
    {
        //TODO: Test might be useless or redundant
        [Fact]
        public void ShouldDecompressAndParseAllCompressedUpdateObjectPackets()
        {
            var opcode = Opcode.SMSG_UPDATE_OBJECT;
            var directoryPath = Path.Combine(Directory.GetCurrentDirectory(), "Resources", opcode.ToString());

            var files = Directory.GetFiles(directoryPath, "20240815_*.bin")
                .OrderBy(path =>
                {
                    var fileName = Path.GetFileNameWithoutExtension(path);
                    var parts = fileName.Split('_');
                    return parts.Length > 1 && int.TryParse(parts[1], out var index) ? index : int.MaxValue;
                });

            foreach (var filePath in files)
            {
                byte[] data = FileReader.ReadBinaryFile(filePath);
                ObjectUpdateHandler.HandleUpdateObject(opcode, data);
            }
        }
    }
}
