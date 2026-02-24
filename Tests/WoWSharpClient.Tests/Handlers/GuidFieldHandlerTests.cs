using GameData.Core.Enums;
using System;
using System.IO;
using WoWSharpClient.Handlers;

namespace WoWSharpClient.Tests.Handlers
{
    public class GuidFieldHandlerTests
    {
        [Theory]
        [InlineData(UpdateFields.EItemFields.ITEM_FIELD_OWNER, true)]
        [InlineData(UpdateFields.EItemFields.ITEM_FIELD_CONTAINED, true)]
        [InlineData(UpdateFields.EItemFields.ITEM_FIELD_CREATOR, true)]
        [InlineData(UpdateFields.EItemFields.ITEM_FIELD_GIFTCREATOR, true)]
        [InlineData(UpdateFields.EItemFields.ITEM_FIELD_STACK_COUNT, false)]
        [InlineData(UpdateFields.EItemFields.ITEM_FIELD_DURATION, false)]
        [InlineData(UpdateFields.EItemFields.ITEM_FIELD_FLAGS, false)]
        public void IsGuidField_ReturnsCorrectly(UpdateFields.EItemFields field, bool expected)
        {
            Assert.Equal(expected, GuidFieldHandler.IsGuidField(field));
        }

        [Fact]
        public void ReadGuidField_CombinesLowAndHigh()
        {
            // Arrange: low = 0x12345678, high = 0xAABBCCDD
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write((uint)0x12345678); // low bytes
            writer.Write((uint)0xAABBCCDD); // high bytes
            ms.Position = 0;
            using var reader = new BinaryReader(ms);

            // Act
            byte[] guid = GuidFieldHandler.ReadGuidField(reader);

            // Assert
            Assert.Equal(8, guid.Length);
            // Low 4 bytes
            Assert.Equal(0x78, guid[0]);
            Assert.Equal(0x56, guid[1]);
            Assert.Equal(0x34, guid[2]);
            Assert.Equal(0x12, guid[3]);
            // High 4 bytes
            Assert.Equal(0xDD, guid[4]);
            Assert.Equal(0xCC, guid[5]);
            Assert.Equal(0xBB, guid[6]);
            Assert.Equal(0xAA, guid[7]);
        }

        [Fact]
        public void ReadGuidField_ZeroGuid()
        {
            using var ms = new MemoryStream(new byte[8]);
            using var reader = new BinaryReader(ms);

            byte[] guid = GuidFieldHandler.ReadGuidField(reader);

            Assert.Equal(8, guid.Length);
            Assert.All(guid, b => Assert.Equal(0, b));
        }

        [Fact]
        public void ReadGuidField_FullGuid_ReconstructsAsUlong()
        {
            // Arrange: GUID = 0x00000123_0000ABCD
            ulong expected = 0x000001230000ABCD;
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write((uint)(expected & 0xFFFFFFFF));         // low
            writer.Write((uint)((expected >> 32) & 0xFFFFFFFF)); // high
            ms.Position = 0;
            using var reader = new BinaryReader(ms);

            // Act
            byte[] guid = GuidFieldHandler.ReadGuidField(reader);

            // Assert — reconstruct ulong from bytes
            ulong actual = BitConverter.ToUInt64(guid, 0);
            Assert.Equal(expected, actual);
        }
    }
}
