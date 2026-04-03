using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace BotRunner.Tests.LiveValidation;

internal static class PacketTraceArtifactHelper
{
    internal sealed record PacketTraceRow(
        int Index,
        long ElapsedMs,
        string Direction,
        ushort Opcode,
        string OpcodeName,
        int Size,
        bool IsMovement);

    internal static string? WaitForPacketTrace(string recordingDir, string account, TimeSpan timeout)
        => RecordingArtifactHelper.WaitForRecordingFile(recordingDir, "packets", account, "csv", timeout);

    internal static IReadOnlyList<PacketTraceRow> LoadPacketCsv(string path)
    {
        var result = new List<PacketTraceRow>();
        var lines = File.ReadAllLines(path);
        for (int i = 1; i < lines.Length; i++)
        {
            var parts = lines[i].Split(',');
            if (parts.Length < 8)
                continue;

            try
            {
                result.Add(new PacketTraceRow(
                    Index: int.Parse(parts[0], CultureInfo.InvariantCulture),
                    ElapsedMs: long.Parse(parts[1], CultureInfo.InvariantCulture),
                    Direction: parts[2],
                    Opcode: ushort.Parse(parts[3], CultureInfo.InvariantCulture),
                    OpcodeName: parts[5],
                    Size: int.Parse(parts[6], CultureInfo.InvariantCulture),
                    IsMovement: parts[7] == "1"));
            }
            catch
            {
                // Ignore malformed rows so live assertions can still use the remaining data.
            }
        }

        return result;
    }
}
