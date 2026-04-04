using Google.Protobuf;
using System;
using System.Linq;

namespace BotCommLayer;

/// <summary>
/// Computes delta between consecutive WoWActivitySnapshot protobuf messages.
/// Instead of sending full snapshots (100-500KB) every tick, sends only changed fields.
/// Target: 1-5KB for idle bots, 10-50KB for active bots.
///
/// Protocol: 1-byte flag (0x00=full, 0x02=delta) + data.
/// Full snapshots sent periodically (every 30s) as keyframes.
/// Delta contains field numbers + new values for changed fields only.
/// </summary>
public class SnapshotDeltaComputer
{
    private byte[]? _previousSnapshot;
    private DateTime _lastKeyframe = DateTime.MinValue;
    private readonly TimeSpan _keyframeInterval;

    /// <summary>Bytes saved by delta compression since last reset.</summary>
    public long BytesSaved { get; private set; }

    /// <summary>Number of deltas sent since last reset.</summary>
    public long DeltasSent { get; private set; }

    /// <summary>Number of full keyframes sent since last reset.</summary>
    public long KeyframesSent { get; private set; }

    public SnapshotDeltaComputer(TimeSpan? keyframeInterval = null)
    {
        _keyframeInterval = keyframeInterval ?? TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Compute a delta or determine that a full snapshot is needed.
    /// Returns the bytes to send (either full or delta) and whether it's a delta.
    /// </summary>
    public (byte[] Data, bool IsDelta) ComputePayload(IMessage currentSnapshot)
    {
        var currentBytes = currentSnapshot.ToByteArray();
        var now = DateTime.UtcNow;

        // Send full keyframe if: first snapshot, interval elapsed, or previous is null
        if (_previousSnapshot == null || now - _lastKeyframe > _keyframeInterval)
        {
            _previousSnapshot = currentBytes;
            _lastKeyframe = now;
            KeyframesSent++;
            return (currentBytes, false);
        }

        // Compute byte-level diff
        var delta = ComputeByteDelta(_previousSnapshot, currentBytes);

        // If delta is larger than 70% of full snapshot, send full instead
        if (delta.Length > currentBytes.Length * 0.7)
        {
            _previousSnapshot = currentBytes;
            _lastKeyframe = now;
            KeyframesSent++;
            return (currentBytes, false);
        }

        BytesSaved += currentBytes.Length - delta.Length;
        DeltasSent++;
        _previousSnapshot = currentBytes;
        return (delta, true);
    }

    /// <summary>
    /// Apply a delta to a previous full snapshot to reconstruct the current state.
    /// </summary>
    public static byte[] ApplyDelta(byte[] baseSnapshot, byte[] delta)
    {
        if (delta.Length < 4) return baseSnapshot;

        // Delta format: [4-byte target length] [chunk: 4-byte offset, 4-byte length, bytes...]
        var targetLength = BitConverter.ToInt32(delta, 0);
        var result = new byte[targetLength];
        Array.Copy(baseSnapshot, result, Math.Min(baseSnapshot.Length, targetLength));

        int pos = 4;
        while (pos + 8 <= delta.Length)
        {
            var offset = BitConverter.ToInt32(delta, pos);
            var chunkLen = BitConverter.ToInt32(delta, pos + 4);
            pos += 8;

            if (pos + chunkLen > delta.Length || offset + chunkLen > result.Length)
                break;

            Array.Copy(delta, pos, result, offset, chunkLen);
            pos += chunkLen;
        }

        return result;
    }

    /// <summary>
    /// Compute a byte-level delta between two snapshots.
    /// Format: [4-byte target length] [chunks of changed regions]
    /// Each chunk: [4-byte offset] [4-byte length] [changed bytes]
    /// </summary>
    private static byte[] ComputeByteDelta(byte[] previous, byte[] current)
    {
        using var ms = new System.IO.MemoryStream();
        using var writer = new System.IO.BinaryWriter(ms);

        writer.Write(current.Length);

        int minLen = Math.Min(previous.Length, current.Length);
        int i = 0;

        while (i < minLen)
        {
            // Skip matching bytes
            while (i < minLen && previous[i] == current[i])
                i++;

            if (i >= minLen) break;

            // Find changed region
            int start = i;
            while (i < minLen && (i == start || previous[i] != current[i]))
                i++;

            // Write chunk
            writer.Write(start);
            writer.Write(i - start);
            writer.Write(current, start, i - start);
        }

        // If current is longer, append the tail
        if (current.Length > previous.Length)
        {
            writer.Write(previous.Length);
            writer.Write(current.Length - previous.Length);
            writer.Write(current, previous.Length, current.Length - previous.Length);
        }

        return ms.ToArray();
    }

    /// <summary>Reset statistics counters.</summary>
    public void ResetStats()
    {
        BytesSaved = 0;
        DeltasSent = 0;
        KeyframesSent = 0;
    }

    /// <summary>Compression ratio (0.0 = no savings, 1.0 = all savings).</summary>
    public float CompressionRatio
    {
        get
        {
            if (DeltasSent == 0) return 0f;
            var totalSent = DeltasSent + KeyframesSent;
            return totalSent == 0 ? 0f : (float)DeltasSent / totalSent;
        }
    }
}
