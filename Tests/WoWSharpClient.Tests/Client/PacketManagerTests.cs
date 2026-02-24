using System.Text;
using WoWSharpClient.Client;

namespace WoWSharpClient.Tests.Client;

public class PacketManagerTests
{
    // ======== Compress / Decompress ========

    [Fact]
    public void CompressDecompress_RoundTrip_Identical()
    {
        byte[] original = Encoding.UTF8.GetBytes("Hello, World! This is a test of compression.");
        byte[] compressed = PacketManager.Compress(original);
        byte[] decompressed = PacketManager.Decompress(compressed);

        Assert.Equal(original, decompressed);
    }

    [Fact]
    public void Compress_ProducesSmaller_ForRepetitiveData()
    {
        // Highly repetitive data should compress well
        byte[] original = new byte[1000];
        Array.Fill<byte>(original, 0x42);

        byte[] compressed = PacketManager.Compress(original);

        Assert.True(compressed.Length < original.Length);
    }

    [Fact]
    public void CompressDecompress_EmptyData()
    {
        byte[] original = Array.Empty<byte>();
        byte[] compressed = PacketManager.Compress(original);
        byte[] decompressed = PacketManager.Decompress(compressed);

        Assert.Empty(decompressed);
    }

    [Fact]
    public void CompressDecompress_LargePayload()
    {
        // Simulate a large packet payload
        var random = new Random(42);
        byte[] original = new byte[64 * 1024]; // 64KB
        random.NextBytes(original);

        byte[] compressed = PacketManager.Compress(original);
        byte[] decompressed = PacketManager.Decompress(compressed);

        Assert.Equal(original, decompressed);
    }

    [Fact]
    public void CompressDecompress_SingleByte()
    {
        byte[] original = new byte[] { 0xFF };
        byte[] compressed = PacketManager.Compress(original);
        byte[] decompressed = PacketManager.Decompress(compressed);

        Assert.Equal(original, decompressed);
    }

    // ======== GenerateAddonInfo ========

    [Fact]
    public void GenerateAddonInfo_NonEmpty()
    {
        byte[] result = PacketManager.GenerateAddonInfo();

        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public void GenerateAddonInfo_ContainsBlizzardAddons()
    {
        byte[] result = PacketManager.GenerateAddonInfo();
        string text = Encoding.UTF8.GetString(result);

        Assert.Contains("Blizzard_AuctionUI", text);
        Assert.Contains("Blizzard_TalentUI", text);
        Assert.Contains("Blizzard_TrainerUI", text);
    }

    [Fact]
    public void GenerateAddonInfo_Deterministic()
    {
        byte[] first = PacketManager.GenerateAddonInfo();
        byte[] second = PacketManager.GenerateAddonInfo();

        Assert.Equal(first, second);
    }

    // ======== GenerateClientProof ========

    [Fact]
    public void GenerateClientProof_Returns20Bytes()
    {
        // SHA1 always produces 20 bytes
        byte[] serverSeed = new byte[4];
        byte[] sessionKey = new byte[40];
        byte[] proof = PacketManager.GenerateClientProof("TESTUSER", 12345, serverSeed, sessionKey);

        Assert.Equal(20, proof.Length);
    }

    [Fact]
    public void GenerateClientProof_DifferentSeeds_DifferentProof()
    {
        byte[] serverSeed = new byte[4];
        byte[] sessionKey = new byte[40];

        byte[] proof1 = PacketManager.GenerateClientProof("USER", 1, serverSeed, sessionKey);
        byte[] proof2 = PacketManager.GenerateClientProof("USER", 2, serverSeed, sessionKey);

        Assert.NotEqual(proof1, proof2);
    }

    [Fact]
    public void GenerateClientProof_DifferentUsers_DifferentProof()
    {
        byte[] serverSeed = new byte[4];
        byte[] sessionKey = new byte[40];

        byte[] proof1 = PacketManager.GenerateClientProof("ALICE", 100, serverSeed, sessionKey);
        byte[] proof2 = PacketManager.GenerateClientProof("BOB", 100, serverSeed, sessionKey);

        Assert.NotEqual(proof1, proof2);
    }

    [Fact]
    public void GenerateClientProof_Deterministic()
    {
        byte[] serverSeed = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        byte[] sessionKey = new byte[40];
        for (int i = 0; i < 40; i++) sessionKey[i] = (byte)(i + 1);

        byte[] proof1 = PacketManager.GenerateClientProof("PLAYER", 42, serverSeed, sessionKey);
        byte[] proof2 = PacketManager.GenerateClientProof("PLAYER", 42, serverSeed, sessionKey);

        Assert.Equal(proof1, proof2);
    }

    // ======== Read ========

    [Fact]
    public void Read_ExactCount_ReturnsAllBytes()
    {
        byte[] data = new byte[] { 1, 2, 3, 4, 5 };
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);

        byte[] result = PacketManager.Read(reader, 5);

        Assert.Equal(data, result);
    }

    [Fact]
    public void Read_MoreThanAvailable_ReturnsShorterArray()
    {
        byte[] data = new byte[] { 10, 20, 30 };
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);

        byte[] result = PacketManager.Read(reader, 10);

        Assert.Equal(3, result.Length);
        Assert.Equal(data, result);
    }

    [Fact]
    public void Read_ZeroCount_ReturnsEmpty()
    {
        byte[] data = new byte[] { 1, 2, 3 };
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);

        byte[] result = PacketManager.Read(reader, 0);

        Assert.Empty(result);
    }

    [Fact]
    public void Read_EmptyStream_ReturnsEmpty()
    {
        using var ms = new MemoryStream(Array.Empty<byte>());
        using var reader = new BinaryReader(ms);

        byte[] result = PacketManager.Read(reader, 5);

        Assert.Empty(result);
    }

    // ======== ReadCString ========

    [Fact]
    public void ReadCString_SimpleString()
    {
        byte[] data = Encoding.UTF8.GetBytes("Hello\0");
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);

        string result = PacketManager.ReadCString(reader);

        Assert.Equal("Hello", result);
    }

    [Fact]
    public void ReadCString_EmptyString()
    {
        byte[] data = new byte[] { 0x00 }; // just null terminator
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);

        string result = PacketManager.ReadCString(reader);

        Assert.Equal("", result);
    }

    [Fact]
    public void ReadCString_MultipleConsecutive()
    {
        byte[] data = Encoding.UTF8.GetBytes("First\0Second\0");
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);

        string first = PacketManager.ReadCString(reader);
        string second = PacketManager.ReadCString(reader);

        Assert.Equal("First", first);
        Assert.Equal("Second", second);
    }

    [Fact]
    public void ReadCString_CharacterName()
    {
        byte[] data = Encoding.UTF8.GetBytes("Thunderfury\0");
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);

        string result = PacketManager.ReadCString(reader);

        Assert.Equal("Thunderfury", result);
    }
}
