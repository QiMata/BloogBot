using GameData.Core.Models;

namespace BotRunner.Tests.Combat
{
    public class HighGuidTests
    {
        // ======== byte[] Constructor ========

        [Fact]
        public void ByteArrayConstructor_SetsFields()
        {
            var low = new byte[] { 0x01, 0x00, 0x00, 0x00 };
            var high = new byte[] { 0x00, 0x00, 0x00, 0x00 };
            var guid = new HighGuid(low, high);

            Assert.Equal(low, guid.LowGuidValue);
            Assert.Equal(high, guid.HighGuidValue);
        }

        [Fact]
        public void ByteArrayConstructor_WrongLowLength_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new HighGuid(new byte[3], new byte[4]));
        }

        [Fact]
        public void ByteArrayConstructor_WrongHighLength_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new HighGuid(new byte[4], new byte[5]));
        }

        // ======== ulong Constructor ========

        [Fact]
        public void UlongConstructor_Zero()
        {
            var guid = new HighGuid(0UL);
            Assert.Equal(0UL, guid.FullGuid);
        }

        [Fact]
        public void UlongConstructor_SmallValue()
        {
            var guid = new HighGuid(42UL);
            Assert.Equal(42UL, guid.FullGuid);
        }

        [Fact]
        public void UlongConstructor_MaxUint()
        {
            var guid = new HighGuid((ulong)uint.MaxValue);
            Assert.Equal((ulong)uint.MaxValue, guid.FullGuid);
        }

        [Fact]
        public void UlongConstructor_HighBits()
        {
            // Value with both high and low parts
            ulong value = 0x0006_0000_0000_002AUL;
            var guid = new HighGuid(value);
            Assert.Equal(value, guid.FullGuid);
        }

        [Fact]
        public void UlongConstructor_MaxValue()
        {
            var guid = new HighGuid(ulong.MaxValue);
            Assert.Equal(ulong.MaxValue, guid.FullGuid);
        }

        // ======== FullGuid ========

        [Fact]
        public void FullGuid_CombinesLowAndHigh()
        {
            // Low = 0x0000002A (42), High = 0x00000006
            var low = BitConverter.GetBytes(42u);
            var high = BitConverter.GetBytes(6u);
            var guid = new HighGuid(low, high);

            ulong expected = (6UL << 32) | 42UL;
            Assert.Equal(expected, guid.FullGuid);
        }

        [Fact]
        public void FullGuid_LowOnly()
        {
            var low = BitConverter.GetBytes(12345u);
            var high = new byte[4]; // all zeros
            var guid = new HighGuid(low, high);
            Assert.Equal(12345UL, guid.FullGuid);
        }

        [Fact]
        public void FullGuid_HighOnly()
        {
            var low = new byte[4]; // all zeros
            var high = BitConverter.GetBytes(1u);
            var guid = new HighGuid(low, high);
            Assert.Equal(1UL << 32, guid.FullGuid);
        }

        // ======== Round-trip ========

        [Theory]
        [InlineData(0UL)]
        [InlineData(1UL)]
        [InlineData(42UL)]
        [InlineData(0xFFFFFFFFUL)]
        [InlineData(0x100000000UL)]
        [InlineData(0x0006_0000_0000_002AUL)]
        [InlineData(ulong.MaxValue)]
        public void RoundTrip_UlongConstructor_FullGuid(ulong value)
        {
            var guid = new HighGuid(value);
            Assert.Equal(value, guid.FullGuid);
        }

        [Fact]
        public void RoundTrip_ByteArrays_MatchUlong()
        {
            ulong original = 0x0006_0000_0000_002AUL;
            var guid1 = new HighGuid(original);

            var guid2 = new HighGuid(guid1.LowGuidValue, guid1.HighGuidValue);
            Assert.Equal(original, guid2.FullGuid);
        }
    }
}
