using WoWSharpClient.Utils;

namespace WoWSharpClient.Tests.Util
{
    public class ReaderUtilsTests
    {
        // ======== WritePackedGuid / ReadPackedGuid Round-trip ========

        [Theory]
        [InlineData(0UL)]
        [InlineData(1UL)]
        [InlineData(42UL)]
        [InlineData(255UL)]
        [InlineData(256UL)]
        [InlineData(0xFFFFUL)]
        [InlineData(0xFFFFFFFFUL)]
        [InlineData(0x100000000UL)]
        [InlineData(0x0006_0000_0000_002AUL)]
        [InlineData(ulong.MaxValue)]
        public void PackedGuid_RoundTrip(ulong guid)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            ReaderUtils.WritePackedGuid(writer, guid);

            stream.Position = 0;
            using var reader = new BinaryReader(stream);
            var result = ReaderUtils.ReadPackedGuid(reader);

            Assert.Equal(guid, result);
        }

        // ======== WritePackedGuid encoding details ========

        [Fact]
        public void WritePackedGuid_Zero_WritesOnlyMask()
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            ReaderUtils.WritePackedGuid(writer, 0);

            Assert.Equal(1, stream.Position); // Only the mask byte
            stream.Position = 0;
            Assert.Equal(0, stream.ReadByte()); // mask = 0
        }

        [Fact]
        public void WritePackedGuid_SmallValue_CompactEncoding()
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            ReaderUtils.WritePackedGuid(writer, 42); // 0x2A — fits in 1 byte

            Assert.Equal(2, stream.Position); // mask + 1 byte
            stream.Position = 0;
            Assert.Equal(0x01, stream.ReadByte()); // mask: bit 0 set
            Assert.Equal(42, stream.ReadByte());   // value
        }

        [Fact]
        public void WritePackedGuid_TwoBytes_SkipsZeros()
        {
            // 0x0102 → byte 0 = 0x02, byte 1 = 0x01
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            ReaderUtils.WritePackedGuid(writer, 0x0102);

            Assert.Equal(3, stream.Position); // mask + 2 bytes
            stream.Position = 0;
            Assert.Equal(0x03, stream.ReadByte()); // mask: bits 0 and 1 set
        }

        [Fact]
        public void WritePackedGuid_HighBytesOnly()
        {
            // GUID = 0x0100000000000000 → only byte 7 is set
            ulong guid = 0x01_00000000_000000UL;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            ReaderUtils.WritePackedGuid(writer, guid);

            Assert.Equal(2, stream.Position); // mask + 1 byte
            stream.Position = 0;
            var mask = stream.ReadByte();
            Assert.Equal(0x80, mask); // bit 7 set
        }

        // ======== ReadPackedGuid edge cases ========

        [Fact]
        public void ReadPackedGuid_ZeroMask_ReturnsZero()
        {
            using var stream = new MemoryStream([0x00]);
            using var reader = new BinaryReader(stream);
            Assert.Equal(0UL, ReaderUtils.ReadPackedGuid(reader));
        }

        [Fact]
        public void ReadPackedGuid_AllBytesPresent()
        {
            // mask = 0xFF, all 8 bytes present
            var data = new byte[] { 0xFF, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };
            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);
            var result = ReaderUtils.ReadPackedGuid(reader);

            ulong expected = 0x08_07_06_05_04_03_02_01UL;
            Assert.Equal(expected, result);
        }

        // ======== ReadCString ========

        [Fact]
        public void ReadCString_SimpleString()
        {
            var data = System.Text.Encoding.ASCII.GetBytes("Hello\0");
            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);
            Assert.Equal("Hello", ReaderUtils.ReadCString(reader));
        }

        [Fact]
        public void ReadCString_EmptyString()
        {
            var data = new byte[] { 0x00 }; // Just null terminator
            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);
            Assert.Equal("", ReaderUtils.ReadCString(reader));
        }

        [Fact]
        public void ReadCString_SingleChar()
        {
            var data = new byte[] { (byte)'A', 0x00 };
            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);
            Assert.Equal("A", ReaderUtils.ReadCString(reader));
        }

        [Fact]
        public void ReadCString_MultipleStrings()
        {
            var data = System.Text.Encoding.ASCII.GetBytes("first\0second\0");
            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);
            Assert.Equal("first", ReaderUtils.ReadCString(reader));
            Assert.Equal("second", ReaderUtils.ReadCString(reader));
        }

        [Fact]
        public void ReadCString_CharacterName()
        {
            var data = System.Text.Encoding.ASCII.GetBytes("Thrall\0");
            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);
            Assert.Equal("Thrall", ReaderUtils.ReadCString(reader));
        }

        // ======== ReadString ========

        [Fact]
        public void ReadString_ZeroLength_ReturnsEmpty()
        {
            using var stream = new MemoryStream([0x41, 0x42]); // Has data but length=0
            using var reader = new BinaryReader(stream);
            Assert.Equal("", ReaderUtils.ReadString(reader, 0));
        }

        [Fact]
        public void ReadString_FixedLength()
        {
            var data = System.Text.Encoding.UTF8.GetBytes("Hello World");
            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);
            Assert.Equal("Hello World", ReaderUtils.ReadString(reader, 11));
        }

        [Fact]
        public void ReadString_WithNullTerminator_TruncatesAtNull()
        {
            var data = new byte[] { (byte)'H', (byte)'i', 0x00, (byte)'X', (byte)'Y' };
            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);
            Assert.Equal("Hi", ReaderUtils.ReadString(reader, 5));
        }

        [Fact]
        public void ReadString_NoNullTerminator_ReadsAll()
        {
            var data = System.Text.Encoding.UTF8.GetBytes("ABCDE");
            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);
            Assert.Equal("ABCDE", ReaderUtils.ReadString(reader, 5));
        }

        [Fact]
        public void ReadString_SingleByte()
        {
            var data = new byte[] { (byte)'Z' };
            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);
            Assert.Equal("Z", ReaderUtils.ReadString(reader, 1));
        }
    }
}
