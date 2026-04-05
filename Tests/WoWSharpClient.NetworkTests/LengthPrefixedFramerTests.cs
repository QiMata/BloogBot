using WoWSharpClient.Networking.Implementation;

namespace WowSharpClient.NetworkTests;

public class LengthPrefixedFramerFrameTests
{
    [Fact]
    public void Frame_2ByteLE_ProducesCorrectHeader()
    {
        using var framer = new LengthPrefixedFramer(headerSize: 2, bigEndian: false);
        var payload = new byte[] { 0xAA, 0xBB, 0xCC };

        var framed = framer.Frame(payload);
        var bytes = framed.ToArray();

        // Header: length=3 as 2-byte little-endian → 0x03, 0x00
        Assert.Equal(5, bytes.Length); // 2 header + 3 payload
        Assert.Equal(0x03, bytes[0]); // low byte
        Assert.Equal(0x00, bytes[1]); // high byte
        Assert.Equal(0xAA, bytes[2]);
        Assert.Equal(0xBB, bytes[3]);
        Assert.Equal(0xCC, bytes[4]);
    }

    [Fact]
    public void Frame_2ByteBE_ProducesCorrectHeader()
    {
        using var framer = new LengthPrefixedFramer(headerSize: 2, bigEndian: true);
        var payload = new byte[] { 0xAA, 0xBB, 0xCC };

        var framed = framer.Frame(payload);
        var bytes = framed.ToArray();

        // Header: length=3 as 2-byte big-endian → 0x00, 0x03
        Assert.Equal(5, bytes.Length);
        Assert.Equal(0x00, bytes[0]); // high byte
        Assert.Equal(0x03, bytes[1]); // low byte
        Assert.Equal(0xAA, bytes[2]);
    }

    [Fact]
    public void Frame_4ByteLE_ProducesCorrectHeader()
    {
        using var framer = new LengthPrefixedFramer(headerSize: 4, bigEndian: false);
        var payload = new byte[] { 0x01, 0x02 };

        var framed = framer.Frame(payload);
        var bytes = framed.ToArray();

        // Header: length=2 as 4-byte little-endian → 0x02, 0x00, 0x00, 0x00
        Assert.Equal(6, bytes.Length);
        Assert.Equal(0x02, bytes[0]);
        Assert.Equal(0x00, bytes[1]);
        Assert.Equal(0x00, bytes[2]);
        Assert.Equal(0x00, bytes[3]);
        Assert.Equal(0x01, bytes[4]);
        Assert.Equal(0x02, bytes[5]);
    }

    [Fact]
    public void Frame_4ByteBE_ProducesCorrectHeader()
    {
        using var framer = new LengthPrefixedFramer(headerSize: 4, bigEndian: true);
        var payload = new byte[] { 0xFF };

        var framed = framer.Frame(payload);
        var bytes = framed.ToArray();

        // Header: length=1 as 4-byte big-endian → 0x00, 0x00, 0x00, 0x01
        Assert.Equal(5, bytes.Length);
        Assert.Equal(0x00, bytes[0]);
        Assert.Equal(0x00, bytes[1]);
        Assert.Equal(0x00, bytes[2]);
        Assert.Equal(0x01, bytes[3]);
        Assert.Equal(0xFF, bytes[4]);
    }

    [Fact]
    public void Frame_EmptyPayload()
    {
        using var framer = new LengthPrefixedFramer(headerSize: 2, bigEndian: false);
        var payload = ReadOnlyMemory<byte>.Empty;

        var framed = framer.Frame(payload);
        var bytes = framed.ToArray();

        Assert.Equal(2, bytes.Length); // Just header, no payload
        Assert.Equal(0x00, bytes[0]); // length = 0
        Assert.Equal(0x00, bytes[1]);
    }

    [Fact]
    public void Frame_LargerPayload_2ByteBE()
    {
        using var framer = new LengthPrefixedFramer(headerSize: 2, bigEndian: true);
        var payload = new byte[300]; // Length = 0x012C

        var framed = framer.Frame(payload);
        var bytes = framed.ToArray();

        Assert.Equal(302, bytes.Length);
        Assert.Equal(0x01, bytes[0]); // 300 >> 8 = 1
        Assert.Equal(0x2C, bytes[1]); // 300 & 0xFF = 44
    }

    [Fact]
    public void Constructor_InvalidHeaderSize_Throws()
    {
        Assert.Throws<ArgumentException>(() => new LengthPrefixedFramer(headerSize: 3));
        Assert.Throws<ArgumentException>(() => new LengthPrefixedFramer(headerSize: 1));
    }

    [Fact]
    public void Frame_AfterDispose_Throws()
    {
        var framer = new LengthPrefixedFramer(headerSize: 2);
        framer.Dispose();

        Assert.Throws<ObjectDisposedException>(() => framer.Frame(new byte[] { 1, 2 }));
    }
}

public class LengthPrefixedFramerTryPopTests
{
    [Fact]
    public void TryPop_EmptyBuffer_ReturnsFalse()
    {
        using var framer = new LengthPrefixedFramer(headerSize: 2, bigEndian: false);

        Assert.False(framer.TryPop(out _));
    }

    [Fact]
    public void TryPop_InsufficientHeaderBytes_ReturnsFalse()
    {
        using var framer = new LengthPrefixedFramer(headerSize: 4, bigEndian: false);
        framer.Append(new byte[] { 0x01, 0x02 }); // Only 2 bytes, need 4

        Assert.False(framer.TryPop(out _));
    }

    [Fact]
    public void TryPop_IncompleteMessage_ReturnsFalse()
    {
        using var framer = new LengthPrefixedFramer(headerSize: 2, bigEndian: false);
        // Header says length=5, but only 2 payload bytes follow
        framer.Append(new byte[] { 0x05, 0x00, 0xAA, 0xBB });

        Assert.False(framer.TryPop(out _));
    }

    [Fact]
    public void TryPop_CompleteMessage_2ByteLE_ReturnsPayload()
    {
        using var framer = new LengthPrefixedFramer(headerSize: 2, bigEndian: false);
        // Header: length=3 (LE), payload: AA BB CC
        framer.Append(new byte[] { 0x03, 0x00, 0xAA, 0xBB, 0xCC });

        Assert.True(framer.TryPop(out var message));
        var bytes = message.ToArray();
        Assert.Equal(3, bytes.Length);
        Assert.Equal(0xAA, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xCC, bytes[2]);
    }

    [Fact]
    public void TryPop_CompleteMessage_2ByteBE_ReturnsPayload()
    {
        using var framer = new LengthPrefixedFramer(headerSize: 2, bigEndian: true);
        // Header: length=2 (BE: 0x00, 0x02), payload: DD EE
        framer.Append(new byte[] { 0x00, 0x02, 0xDD, 0xEE });

        Assert.True(framer.TryPop(out var message));
        var bytes = message.ToArray();
        Assert.Equal(2, bytes.Length);
        Assert.Equal(0xDD, bytes[0]);
        Assert.Equal(0xEE, bytes[1]);
    }

    [Fact]
    public void TryPop_CompleteMessage_4ByteLE_ReturnsPayload()
    {
        using var framer = new LengthPrefixedFramer(headerSize: 4, bigEndian: false);
        // Header: length=1 (LE: 01 00 00 00), payload: FF
        framer.Append(new byte[] { 0x01, 0x00, 0x00, 0x00, 0xFF });

        Assert.True(framer.TryPop(out var message));
        var bytes = message.ToArray();
        Assert.Single(bytes);
        Assert.Equal(0xFF, bytes[0]);
    }

    [Fact]
    public void TryPop_CompleteMessage_4ByteBE_ReturnsPayload()
    {
        using var framer = new LengthPrefixedFramer(headerSize: 4, bigEndian: true);
        // Header: length=2 (BE: 00 00 00 02), payload: AB CD
        framer.Append(new byte[] { 0x00, 0x00, 0x00, 0x02, 0xAB, 0xCD });

        Assert.True(framer.TryPop(out var message));
        var bytes = message.ToArray();
        Assert.Equal(2, bytes.Length);
        Assert.Equal(0xAB, bytes[0]);
        Assert.Equal(0xCD, bytes[1]);
    }

    [Fact]
    public void TryPop_MultipleMessages_PopsFirstOnly()
    {
        using var framer = new LengthPrefixedFramer(headerSize: 2, bigEndian: false);
        // Two messages: [len=1, AA] [len=1, BB]
        framer.Append(new byte[] { 0x01, 0x00, 0xAA, 0x01, 0x00, 0xBB });

        Assert.True(framer.TryPop(out var msg1));
        Assert.Equal(0xAA, msg1.ToArray()[0]);

        Assert.True(framer.TryPop(out var msg2));
        Assert.Equal(0xBB, msg2.ToArray()[0]);
    }

    [Fact]
    public void TryPop_AfterTwoMessages_BufferEmpty()
    {
        using var framer = new LengthPrefixedFramer(headerSize: 2, bigEndian: false);
        framer.Append(new byte[] { 0x01, 0x00, 0xAA, 0x01, 0x00, 0xBB });

        framer.TryPop(out _);
        framer.TryPop(out _);

        Assert.False(framer.TryPop(out _)); // No more messages
    }

    [Fact]
    public void TryPop_IncrementalAppend_WaitsForComplete()
    {
        using var framer = new LengthPrefixedFramer(headerSize: 2, bigEndian: false);

        // Append header only
        framer.Append(new byte[] { 0x03, 0x00 });
        Assert.False(framer.TryPop(out _));

        // Append partial payload
        framer.Append(new byte[] { 0xAA, 0xBB });
        Assert.False(framer.TryPop(out _));

        // Append rest of payload
        framer.Append(new byte[] { 0xCC });
        Assert.True(framer.TryPop(out var message));
        Assert.Equal(3, message.Length);
    }

    [Fact]
    public void TryPop_ZeroLengthMessage()
    {
        using var framer = new LengthPrefixedFramer(headerSize: 2, bigEndian: false);
        // Header says length=0, no payload
        framer.Append(new byte[] { 0x00, 0x00 });

        Assert.True(framer.TryPop(out var message));
        Assert.Equal(0, message.Length);
    }

    [Fact]
    public void Append_AfterDispose_Throws()
    {
        var framer = new LengthPrefixedFramer(headerSize: 2);
        framer.Dispose();

        Assert.Throws<ObjectDisposedException>(() => framer.Append(new byte[] { 1 }));
    }

    [Fact]
    public void TryPop_AfterDispose_Throws()
    {
        var framer = new LengthPrefixedFramer(headerSize: 2);
        framer.Dispose();

        Assert.Throws<ObjectDisposedException>(() => framer.TryPop(out _));
    }

    [Fact]
    public void Dispose_Twice_DoesNotThrow()
    {
        var framer = new LengthPrefixedFramer(headerSize: 2);
        framer.Dispose();
        framer.Dispose(); // Should not throw
    }
}

public class LengthPrefixedFramerRoundTripTests
{
    [Theory]
    [InlineData(2, false)]
    [InlineData(2, true)]
    [InlineData(4, false)]
    [InlineData(4, true)]
    public void RoundTrip_AllConfigurations(int headerSize, bool bigEndian)
    {
        using var framer = new LengthPrefixedFramer(headerSize, bigEndian);
        var originalPayload = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };

        // Frame → Append → TryPop should yield original payload
        var framed = framer.Frame(originalPayload);
        framer.Append(framed);

        Assert.True(framer.TryPop(out var recovered));
        Assert.Equal(originalPayload, recovered.ToArray());
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(2, true)]
    [InlineData(4, false)]
    [InlineData(4, true)]
    public void RoundTrip_EmptyPayload(int headerSize, bool bigEndian)
    {
        using var framer = new LengthPrefixedFramer(headerSize, bigEndian);
        var originalPayload = ReadOnlyMemory<byte>.Empty;

        var framed = framer.Frame(originalPayload);
        framer.Append(framed);

        Assert.True(framer.TryPop(out var recovered));
        Assert.Equal(0, recovered.Length);
    }
}
