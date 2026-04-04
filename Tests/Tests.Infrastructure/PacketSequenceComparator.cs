using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Tests.Infrastructure;

/// <summary>
/// Compares FG and BG packet traces from CSV files.
/// Input: two CSV files (opcode, size, timestamp columns).
/// Output: opcode sequence diff, missing/extra packets, timing delta.
/// Used by FG/BG parity tests to verify BG sends correct opcodes.
/// </summary>
public class PacketSequenceComparator
{
    public record PacketEntry(string Opcode, int Size, double TimestampMs);

    public record ComparisonResult
    {
        public List<string> FgOnlyOpcodes { get; init; } = [];
        public List<string> BgOnlyOpcodes { get; init; } = [];
        public List<(string Opcode, int FgCount, int BgCount)> CountMismatches { get; init; } = [];
        public List<(string Opcode, double FgMs, double BgMs, double DeltaMs)> TimingDeltas { get; init; } = [];
        public int FgPacketCount { get; init; }
        public int BgPacketCount { get; init; }
        public bool IsMatch => FgOnlyOpcodes.Count == 0 && BgOnlyOpcodes.Count == 0 && CountMismatches.Count == 0;
    }

    /// <summary>
    /// Parse a packet trace CSV file. Expected format: Opcode,Size,TimestampMs (header row optional).
    /// </summary>
    public static List<PacketEntry> ParseCsv(string filePath)
    {
        var entries = new List<PacketEntry>();
        foreach (var line in File.ReadAllLines(filePath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split(',');
            if (parts.Length < 3) continue;
            if (!int.TryParse(parts[1].Trim(), out var size)) continue; // skip header
            double.TryParse(parts[2].Trim(), out var ts);
            entries.Add(new PacketEntry(parts[0].Trim(), size, ts));
        }
        return entries;
    }

    /// <summary>
    /// Compare two packet traces. Identifies missing/extra opcodes and count mismatches.
    /// </summary>
    public static ComparisonResult Compare(
        List<PacketEntry> fgPackets,
        List<PacketEntry> bgPackets,
        HashSet<string>? ignoreOpcodes = null)
    {
        ignoreOpcodes ??= [];

        var fgCounts = fgPackets
            .Where(p => !ignoreOpcodes.Contains(p.Opcode))
            .GroupBy(p => p.Opcode)
            .ToDictionary(g => g.Key, g => g.Count());

        var bgCounts = bgPackets
            .Where(p => !ignoreOpcodes.Contains(p.Opcode))
            .GroupBy(p => p.Opcode)
            .ToDictionary(g => g.Key, g => g.Count());

        var allOpcodes = fgCounts.Keys.Union(bgCounts.Keys).OrderBy(o => o).ToList();

        var fgOnly = new List<string>();
        var bgOnly = new List<string>();
        var countMismatches = new List<(string, int, int)>();

        foreach (var opcode in allOpcodes)
        {
            var fgCount = fgCounts.GetValueOrDefault(opcode, 0);
            var bgCount = bgCounts.GetValueOrDefault(opcode, 0);

            if (fgCount > 0 && bgCount == 0)
                fgOnly.Add(opcode);
            else if (bgCount > 0 && fgCount == 0)
                bgOnly.Add(opcode);
            else if (fgCount != bgCount)
                countMismatches.Add((opcode, fgCount, bgCount));
        }

        // Timing comparison: for matching opcodes, compare first occurrence timestamps
        var timingDeltas = new List<(string, double, double, double)>();
        foreach (var opcode in allOpcodes)
        {
            var fgFirst = fgPackets.FirstOrDefault(p => p.Opcode == opcode);
            var bgFirst = bgPackets.FirstOrDefault(p => p.Opcode == opcode);
            if (fgFirst != null && bgFirst != null)
            {
                var delta = bgFirst.TimestampMs - fgFirst.TimestampMs;
                if (Math.Abs(delta) > 100) // Only report >100ms deltas
                    timingDeltas.Add((opcode, fgFirst.TimestampMs, bgFirst.TimestampMs, delta));
            }
        }

        return new ComparisonResult
        {
            FgOnlyOpcodes = fgOnly,
            BgOnlyOpcodes = bgOnly,
            CountMismatches = countMismatches,
            TimingDeltas = timingDeltas,
            FgPacketCount = fgPackets.Count,
            BgPacketCount = bgPackets.Count,
        };
    }

    /// <summary>
    /// Compare two CSV files directly.
    /// </summary>
    public static ComparisonResult CompareFiles(string fgCsvPath, string bgCsvPath, HashSet<string>? ignoreOpcodes = null)
        => Compare(ParseCsv(fgCsvPath), ParseCsv(bgCsvPath), ignoreOpcodes);

    /// <summary>
    /// Format a comparison result as a human-readable summary.
    /// </summary>
    public static string FormatResult(ComparisonResult result)
    {
        var lines = new List<string>
        {
            $"FG packets: {result.FgPacketCount}, BG packets: {result.BgPacketCount}",
            $"Match: {result.IsMatch}"
        };

        if (result.FgOnlyOpcodes.Count > 0)
            lines.Add($"FG-only opcodes: {string.Join(", ", result.FgOnlyOpcodes)}");
        if (result.BgOnlyOpcodes.Count > 0)
            lines.Add($"BG-only opcodes: {string.Join(", ", result.BgOnlyOpcodes)}");
        if (result.CountMismatches.Count > 0)
        {
            lines.Add("Count mismatches:");
            foreach (var (opcode, fg, bg) in result.CountMismatches)
                lines.Add($"  {opcode}: FG={fg} BG={bg}");
        }
        if (result.TimingDeltas.Count > 0)
        {
            lines.Add("Timing deltas (>100ms):");
            foreach (var (opcode, fgMs, bgMs, delta) in result.TimingDeltas)
                lines.Add($"  {opcode}: FG={fgMs:F0}ms BG={bgMs:F0}ms delta={delta:+0;-0}ms");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
