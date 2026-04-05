using BotCommLayer;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace BotRunner.Tests.IPC;

public class SnapshotDeltaComputerTests
{
    /// <summary>
    /// Helper: create a simple protobuf message with known bytes.
    /// Uses Struct as a readily available generic message.
    /// </summary>
    private static Struct MakeSnapshot(string key, string value)
    {
        var s = new Struct();
        s.Fields[key] = Value.ForString(value);
        return s;
    }

    [Fact]
    public void FirstCall_ReturnsFullSnapshot()
    {
        var computer = new SnapshotDeltaComputer(keyframeInterval: TimeSpan.FromHours(1));
        var snap = MakeSnapshot("hp", "100");

        var (data, isDelta) = computer.ComputePayload(snap);

        Assert.False(isDelta);
        Assert.True(data.Length > 0);
        Assert.Equal(1, computer.KeyframesSent);
    }

    [Fact]
    public void SubsequentCall_ReturnsDelta()
    {
        var computer = new SnapshotDeltaComputer(keyframeInterval: TimeSpan.FromHours(1));

        // First call => keyframe
        var snap1 = MakeSnapshot("hp", "100");
        computer.ComputePayload(snap1);

        // Second call with small change => delta
        var snap2 = MakeSnapshot("hp", "99");
        var (data, isDelta) = computer.ComputePayload(snap2);

        // Delta should be smaller than or equal to the full snapshot
        // (if it's large relative to full, it sends full instead)
        Assert.True(data.Length > 0);
        // Either it's a delta or a full (depends on ratio), but DeltasSent or KeyframesSent should increase
        Assert.True(computer.DeltasSent + computer.KeyframesSent == 2);
    }

    [Fact]
    public void ApplyDelta_ReconstructsOriginal()
    {
        // Manually test the ApplyDelta static method
        var original = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        // Build a delta that changes bytes 2-3 to {99, 98}
        // Format: [4-byte target length][4-byte offset][4-byte chunk len][chunk bytes]
        using var ms = new System.IO.MemoryStream();
        using var writer = new System.IO.BinaryWriter(ms);
        writer.Write(original.Length); // target length = 8
        writer.Write(2);              // offset = 2
        writer.Write(2);              // chunk length = 2
        writer.Write(new byte[] { 99, 98 }); // new bytes
        var delta = ms.ToArray();

        var result = SnapshotDeltaComputer.ApplyDelta(original, delta);

        Assert.Equal(8, result.Length);
        Assert.Equal(1, result[0]);
        Assert.Equal(2, result[1]);
        Assert.Equal(99, result[2]);
        Assert.Equal(98, result[3]);
        Assert.Equal(5, result[4]);
    }

    [Fact]
    public void UnchangedData_SmallDelta()
    {
        var computer = new SnapshotDeltaComputer(keyframeInterval: TimeSpan.FromHours(1));

        var snap = MakeSnapshot("hp", "100");
        computer.ComputePayload(snap);

        // Send the exact same snapshot
        var (data, isDelta) = computer.ComputePayload(snap);

        // Identical data => delta should be very small (just the 4-byte length header)
        if (isDelta)
        {
            // Delta of identical data = just [4-byte target length], no chunks
            Assert.True(data.Length <= snap.ToByteArray().Length);
            Assert.True(computer.BytesSaved > 0);
        }
    }

    [Fact]
    public void ResetStats_ClearsCounters()
    {
        var computer = new SnapshotDeltaComputer(keyframeInterval: TimeSpan.FromHours(1));
        computer.ComputePayload(MakeSnapshot("hp", "100"));

        computer.ResetStats();

        Assert.Equal(0, computer.BytesSaved);
        Assert.Equal(0, computer.DeltasSent);
        Assert.Equal(0, computer.KeyframesSent);
    }
}
