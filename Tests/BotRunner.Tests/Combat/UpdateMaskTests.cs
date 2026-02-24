using GameData.Core.Models;

namespace BotRunner.Tests.Combat
{
    public class UpdateMaskTests
    {
        // ======== SetCount ========

        [Fact]
        public void SetCount_SetsFieldCount()
        {
            var mask = new UpdateMask();
            mask.SetCount(64);
            Assert.Equal(64u, mask.FieldCount);
        }

        [Fact]
        public void SetCount_CalculatesBlockCount()
        {
            var mask = new UpdateMask();
            mask.SetCount(64);
            Assert.Equal(2u, mask.BlockCount); // 64/32 = 2 blocks
        }

        [Fact]
        public void SetCount_RoundsUpBlockCount()
        {
            var mask = new UpdateMask();
            mask.SetCount(33);
            Assert.Equal(2u, mask.BlockCount); // ceil(33/32) = 2
        }

        [Fact]
        public void SetCount_SingleBlock()
        {
            var mask = new UpdateMask();
            mask.SetCount(1);
            Assert.Equal(1u, mask.BlockCount);
        }

        [Fact]
        public void SetCount_ExactlyOneBlock()
        {
            var mask = new UpdateMask();
            mask.SetCount(32);
            Assert.Equal(1u, mask.BlockCount);
        }

        // ======== SetBit / GetBit ========

        [Fact]
        public void GetBit_DefaultIsFalse()
        {
            var mask = new UpdateMask();
            mask.SetCount(64);
            Assert.False(mask.GetBit(0));
            Assert.False(mask.GetBit(31));
            Assert.False(mask.GetBit(63));
        }

        [Fact]
        public void SetBit_ThenGetBit_ReturnsTrue()
        {
            var mask = new UpdateMask();
            mask.SetCount(64);
            mask.SetBit(5);
            Assert.True(mask.GetBit(5));
        }

        [Fact]
        public void SetBit_DoesNotAffectOtherBits()
        {
            var mask = new UpdateMask();
            mask.SetCount(64);
            mask.SetBit(5);
            Assert.False(mask.GetBit(4));
            Assert.False(mask.GetBit(6));
        }

        [Fact]
        public void SetBit_MultipleBits()
        {
            var mask = new UpdateMask();
            mask.SetCount(64);
            mask.SetBit(0);
            mask.SetBit(31);
            mask.SetBit(32);
            mask.SetBit(63);

            Assert.True(mask.GetBit(0));
            Assert.True(mask.GetBit(31));
            Assert.True(mask.GetBit(32));
            Assert.True(mask.GetBit(63));
            Assert.False(mask.GetBit(1));
            Assert.False(mask.GetBit(33));
        }

        [Fact]
        public void SetBit_CrossesBlockBoundary()
        {
            var mask = new UpdateMask();
            mask.SetCount(96);
            mask.SetBit(31); // Last bit of block 0
            mask.SetBit(32); // First bit of block 1
            mask.SetBit(63); // Last bit of block 1
            mask.SetBit(64); // First bit of block 2

            Assert.True(mask.GetBit(31));
            Assert.True(mask.GetBit(32));
            Assert.True(mask.GetBit(63));
            Assert.True(mask.GetBit(64));
        }

        // ======== UnsetBit ========

        [Fact]
        public void UnsetBit_ClearsPreviouslySetBit()
        {
            var mask = new UpdateMask();
            mask.SetCount(64);
            mask.SetBit(10);
            Assert.True(mask.GetBit(10));

            mask.UnsetBit(10);
            Assert.False(mask.GetBit(10));
        }

        [Fact]
        public void UnsetBit_DoesNotAffectOtherBits()
        {
            var mask = new UpdateMask();
            mask.SetCount(64);
            mask.SetBit(10);
            mask.SetBit(11);

            mask.UnsetBit(10);
            Assert.False(mask.GetBit(10));
            Assert.True(mask.GetBit(11));
        }

        [Fact]
        public void UnsetBit_OnAlreadyUnset_NoOp()
        {
            var mask = new UpdateMask();
            mask.SetCount(64);
            mask.UnsetBit(5); // Already unset
            Assert.False(mask.GetBit(5));
        }

        // ======== Clear ========

        [Fact]
        public void Clear_ResetsAllBits()
        {
            var mask = new UpdateMask();
            mask.SetCount(64);
            mask.SetBit(0);
            mask.SetBit(15);
            mask.SetBit(31);
            mask.SetBit(32);
            mask.SetBit(63);

            mask.Clear();

            for (uint i = 0; i < 64; i++)
            {
                Assert.False(mask.GetBit(i));
            }
        }

        [Fact]
        public void Clear_PreservesCountAndBlockCount()
        {
            var mask = new UpdateMask();
            mask.SetCount(64);
            mask.Clear();
            Assert.Equal(64u, mask.FieldCount);
            Assert.Equal(2u, mask.BlockCount);
        }

        // ======== AppendToPacket / ReadFromPacket round-trip ========

        [Fact]
        public void RoundTrip_WriteAndRead()
        {
            var original = new UpdateMask();
            original.SetCount(64);
            original.SetBit(0);
            original.SetBit(7);
            original.SetBit(31);
            original.SetBit(32);
            original.SetBit(63);

            // Write to stream
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            original.AppendToPacket(writer);

            // Read back
            stream.Position = 0;
            using var reader = new BinaryReader(stream);
            var restored = UpdateMask.ReadFromPacket(reader, 64);

            // Verify all bits match
            for (uint i = 0; i < 64; i++)
            {
                Assert.Equal(original.GetBit(i), restored.GetBit(i));
            }
        }

        [Fact]
        public void RoundTrip_EmptyMask()
        {
            var original = new UpdateMask();
            original.SetCount(32);

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            original.AppendToPacket(writer);

            stream.Position = 0;
            using var reader = new BinaryReader(stream);
            var restored = UpdateMask.ReadFromPacket(reader, 32);

            for (uint i = 0; i < 32; i++)
            {
                Assert.False(restored.GetBit(i));
            }
        }

        [Fact]
        public void RoundTrip_AllBitsSet()
        {
            var original = new UpdateMask();
            original.SetCount(32);
            for (uint i = 0; i < 32; i++)
                original.SetBit(i);

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            original.AppendToPacket(writer);

            stream.Position = 0;
            using var reader = new BinaryReader(stream);
            var restored = UpdateMask.ReadFromPacket(reader, 32);

            for (uint i = 0; i < 32; i++)
            {
                Assert.True(restored.GetBit(i));
            }
        }

        // ======== Default constructor ========

        [Fact]
        public void DefaultConstructor_ZeroCounts()
        {
            var mask = new UpdateMask();
            Assert.Equal(0u, mask.FieldCount);
            Assert.Equal(0u, mask.BlockCount);
        }
    }
}
