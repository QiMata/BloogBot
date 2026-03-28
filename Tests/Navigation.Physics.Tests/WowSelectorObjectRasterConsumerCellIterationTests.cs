using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorObjectRasterConsumerCellIterationTests
{
    [Fact]
    public void EvaluateWoWSelectorObjectRasterCellIteration_AcceptsTwoTrianglesAndAllocatesQueueEntry()
    {
        SelectorObjectRasterPrefixTrace prefix = BuildAcceptedPrefix();
        SelectorObjectRasterQueueEntry entry = default;
        byte[] cellModes = [0x00];
        uint[] pointOutcodes = [0u, 0u, 0u, 0u];
        ushort[] scratchWords = new ushort[20];

        uint result = EvaluateWoWSelectorObjectRasterCellIteration(
            prefix,
            localRow: 0u,
            localColumn: 0u,
            pointGridRowStride: 3,
            cellModeRowStride: 1,
            cellModeMaskFlags: 0x00010000u,
            callerContextToken: 0x1234u,
            rasterSourceToken: 0x5678u,
            inputQueueEntryCount: 5u,
            inputScratchWordCount: 12u,
            cellModes,
            cellModes.Length,
            pointOutcodes,
            pointOutcodes.Length,
            scratchWords,
            scratchWords.Length,
            ref entry,
            out SelectorObjectRasterCellIterationTrace trace);

        Assert.Equal(1u, result);
        Assert.Equal(1u, trace.VisitedRasterCell);
        Assert.Equal(0u, trace.SkippedByCellModeValue);
        Assert.Equal(0u, trace.SkippedByCellModeMask);
        Assert.Equal(1u, trace.ReturnedAnyCandidate);
        Assert.Equal(0u, trace.RejectedTriangleCount);
        Assert.Equal(2u, trace.AcceptedTriangleCount);
        Assert.Equal(5u, trace.QueueCountBefore);
        Assert.Equal(6u, trace.QueueCountAfter);
        Assert.Equal(12u, trace.ScratchWordsBefore);
        Assert.Equal(18u, trace.ScratchWordsAfter);
        Assert.Equal(0u, trace.EntryAllocatedBefore);
        Assert.Equal(1u, trace.EntryAllocatedAfter);
        Assert.Equal(0u, trace.CellIndex);
        Assert.Equal(0u, trace.CellModeNibble);
        Assert.Equal(0u, trace.LocalPointBase);
        Assert.Equal(0u, trace.WorldPointBase);
        Assert.Equal(0u, trace.AppendedWordCountBefore);
        Assert.Equal(6u, trace.AppendedWordCountAfter);
        Assert.Equal(0u, trace.AppendedTriangleCountBefore);
        Assert.Equal(2u, trace.AppendedTriangleCountAfter);
        Assert.Equal(0u, trace.TriangleRejectMask);
        Assert.Equal(3u, trace.TriangleAcceptMask);

        Assert.Equal(1u, entry.Allocated);
        Assert.Equal(0x1234u, entry.CallerContextToken);
        Assert.Equal(0x5678u, entry.RasterSourceToken);
        Assert.Equal(12u, entry.ScratchWordStart);
        Assert.Equal(6u, entry.ScratchWordReserved);
        Assert.Equal((ushort)6, entry.AppendedWordCount);
        Assert.Equal((ushort)2, entry.AppendedTriangleCount);
        Assert.Equal((ushort)0, entry.MinAppendedWord);
        Assert.Equal((ushort)4, entry.MaxAppendedWord);
        Assert.Equal(1u, entry.ScratchBufferPresent);
        Assert.Equal(new ushort[] { 0, 4, 3, 0, 1, 4 },
            new ushort[] { scratchWords[12], scratchWords[13], scratchWords[14], scratchWords[15], scratchWords[16], scratchWords[17] });
    }

    [Fact]
    public void EvaluateWoWSelectorObjectRasterCellIteration_RejectsSecondTriangleWhenOutcodesSharePlaneBit()
    {
        SelectorObjectRasterPrefixTrace prefix = BuildAcceptedPrefix();
        SelectorObjectRasterQueueEntry entry = default;
        byte[] cellModes = [0x00];
        uint[] pointOutcodes = [1u, 1u, 0u, 1u];
        ushort[] scratchWords = new ushort[6];

        uint result = EvaluateWoWSelectorObjectRasterCellIteration(
            prefix,
            localRow: 0u,
            localColumn: 0u,
            pointGridRowStride: 3,
            cellModeRowStride: 1,
            cellModeMaskFlags: 0x00010000u,
            callerContextToken: 0u,
            rasterSourceToken: 0u,
            inputQueueEntryCount: 0u,
            inputScratchWordCount: 0u,
            cellModes,
            cellModes.Length,
            pointOutcodes,
            pointOutcodes.Length,
            scratchWords,
            scratchWords.Length,
            ref entry,
            out SelectorObjectRasterCellIterationTrace trace);

        Assert.Equal(1u, result);
        Assert.Equal(1u, trace.ReturnedAnyCandidate);
        Assert.Equal(1u, trace.RejectedTriangleCount);
        Assert.Equal(1u, trace.AcceptedTriangleCount);
        Assert.Equal(2u, trace.TriangleRejectMask);
        Assert.Equal(1u, trace.TriangleAcceptMask);
        Assert.Equal((ushort)3, entry.AppendedWordCount);
        Assert.Equal((ushort)1, entry.AppendedTriangleCount);
        Assert.Equal(new ushort[] { 0, 4, 3, 0, 0, 0 }, scratchWords);
    }

    [Fact]
    public void EvaluateWoWSelectorObjectRasterCellIteration_CellModeFSkipsWithoutAllocatingEntry()
    {
        SelectorObjectRasterPrefixTrace prefix = BuildAcceptedPrefix();
        SelectorObjectRasterQueueEntry entry = default;
        byte[] cellModes = [0x0F];
        uint[] pointOutcodes = [0u, 0u, 0u, 0u];
        ushort[] scratchWords = new ushort[6];

        uint result = EvaluateWoWSelectorObjectRasterCellIteration(
            prefix,
            localRow: 0u,
            localColumn: 0u,
            pointGridRowStride: 3,
            cellModeRowStride: 1,
            cellModeMaskFlags: 0x00010000u,
            callerContextToken: 0u,
            rasterSourceToken: 0u,
            inputQueueEntryCount: 0u,
            inputScratchWordCount: 0u,
            cellModes,
            cellModes.Length,
            pointOutcodes,
            pointOutcodes.Length,
            scratchWords,
            scratchWords.Length,
            ref entry,
            out SelectorObjectRasterCellIterationTrace trace);

        Assert.Equal(0u, result);
        Assert.Equal(1u, trace.VisitedRasterCell);
        Assert.Equal(1u, trace.SkippedByCellModeValue);
        Assert.Equal(0u, trace.SkippedByCellModeMask);
        Assert.Equal(0u, trace.ReturnedAnyCandidate);
        Assert.Equal(0u, entry.Allocated);
    }

    [Fact]
    public void EvaluateWoWSelectorObjectRasterCellIteration_CellModeMaskSkipLeavesEntryUnallocated()
    {
        SelectorObjectRasterPrefixTrace prefix = BuildAcceptedPrefix();
        SelectorObjectRasterQueueEntry entry = default;
        byte[] cellModes = [0x00];
        uint[] pointOutcodes = [0u, 0u, 0u, 0u];
        ushort[] scratchWords = new ushort[6];

        uint result = EvaluateWoWSelectorObjectRasterCellIteration(
            prefix,
            localRow: 0u,
            localColumn: 0u,
            pointGridRowStride: 3,
            cellModeRowStride: 1,
            cellModeMaskFlags: 0x00020000u,
            callerContextToken: 0u,
            rasterSourceToken: 0u,
            inputQueueEntryCount: 0u,
            inputScratchWordCount: 0u,
            cellModes,
            cellModes.Length,
            pointOutcodes,
            pointOutcodes.Length,
            scratchWords,
            scratchWords.Length,
            ref entry,
            out SelectorObjectRasterCellIterationTrace trace);

        Assert.Equal(0u, result);
        Assert.Equal(1u, trace.VisitedRasterCell);
        Assert.Equal(0u, trace.SkippedByCellModeValue);
        Assert.Equal(1u, trace.SkippedByCellModeMask);
        Assert.Equal(0u, trace.AcceptedTriangleCount);
        Assert.Equal(0u, entry.Allocated);
    }

    [Fact]
    public void EvaluateWoWSelectorObjectRasterCellIteration_ExistingAllocatedEntryAppendsWithoutReallocatingOrRetokenizing()
    {
        SelectorObjectRasterPrefixTrace prefix = BuildAcceptedPrefix();
        SelectorObjectRasterQueueEntry entry = new()
        {
            Allocated = 1u,
            CallerContextToken = 0x9u,
            RasterSourceToken = 0xAu,
            ScratchWordStart = 12u,
            ScratchWordReserved = 6u,
            AppendedWordCount = 0,
            AppendedTriangleCount = 0,
            MinAppendedWord = ushort.MaxValue,
            MaxAppendedWord = 0,
            ScratchBufferPresent = 1u,
        };
        byte[] cellModes = [0x00];
        uint[] pointOutcodes = [0u, 0u, 0u, 0u];
        ushort[] scratchWords = new ushort[20];

        uint result = EvaluateWoWSelectorObjectRasterCellIteration(
            prefix,
            localRow: 0u,
            localColumn: 0u,
            pointGridRowStride: 3,
            cellModeRowStride: 1,
            cellModeMaskFlags: 0x00010000u,
            callerContextToken: 0x1234u,
            rasterSourceToken: 0x5678u,
            inputQueueEntryCount: 5u,
            inputScratchWordCount: 12u,
            cellModes,
            cellModes.Length,
            pointOutcodes,
            pointOutcodes.Length,
            scratchWords,
            scratchWords.Length,
            ref entry,
            out SelectorObjectRasterCellIterationTrace trace);

        Assert.Equal(1u, result);
        Assert.Equal(1u, trace.EntryAllocatedBefore);
        Assert.Equal(1u, trace.EntryAllocatedAfter);
        Assert.Equal(5u, trace.QueueCountBefore);
        Assert.Equal(6u, trace.QueueCountAfter);
        Assert.Equal(12u, trace.ScratchWordsBefore);
        Assert.Equal(18u, trace.ScratchWordsAfter);
        Assert.Equal(0x9u, entry.CallerContextToken);
        Assert.Equal(0xAu, entry.RasterSourceToken);
        Assert.Equal((ushort)6, entry.AppendedWordCount);
        Assert.Equal((ushort)2, entry.AppendedTriangleCount);
        Assert.Equal(new ushort[] { 0, 4, 3, 0, 1, 4 },
            new ushort[] { scratchWords[12], scratchWords[13], scratchWords[14], scratchWords[15], scratchWords[16], scratchWords[17] });
    }

    private static SelectorObjectRasterPrefixTrace BuildAcceptedPrefix()
    {
        uint result = EvaluateWoWSelectorObjectRasterConsumerPrefix(
            modeWord: 0x00001000u,
            rasterRowCount: 4,
            rasterColumnCount: 4,
            rasterRowStride: 1,
            quantizeScale: -1.0f,
            objectTranslation: new Vector3(1.0f, 1.0f, 0.0f),
            sourcePayload: CreatePayload(),
            out SelectorObjectRasterPrefixTrace prefix);

        Assert.Equal(1u, result);
        Assert.Equal(1u, prefix.RasterCellCountX);
        Assert.Equal(1u, prefix.RasterCellCountY);
        Assert.Equal(2u, prefix.PrepassPointCountX);
        Assert.Equal(2u, prefix.PrepassPointCountY);
        return prefix;
    }

    private static SelectorObjectRasterPayload CreatePayload(SelectorSupportPlane[]? planes = null)
    {
        return new SelectorObjectRasterPayload
        {
            Planes = planes ??
            [
                new SelectorSupportPlane { Normal = new Vector3(1.0f, 0.0f, 0.0f), PlaneDistance = 100.0f },
                new SelectorSupportPlane { Normal = new Vector3(0.0f, 1.0f, 0.0f), PlaneDistance = 100.0f },
                new SelectorSupportPlane { Normal = new Vector3(0.0f, 0.0f, 1.0f), PlaneDistance = 100.0f },
                new SelectorSupportPlane { Normal = new Vector3(-1.0f, 0.0f, 0.0f), PlaneDistance = 100.0f },
                new SelectorSupportPlane { Normal = new Vector3(0.0f, -1.0f, 0.0f), PlaneDistance = 100.0f },
                new SelectorSupportPlane { Normal = new Vector3(0.0f, 0.0f, -1.0f), PlaneDistance = 100.0f },
            ],
            SupportPoints =
            [
                new Vector3(0.2f, 0.2f, 0.0f),
                new Vector3(0.2f, 0.8f, 0.0f),
                new Vector3(0.8f, 0.2f, 0.0f),
                new Vector3(0.8f, 0.8f, 0.0f),
                new Vector3(0.5f, 0.5f, 0.0f),
                new Vector3(0.7f, 0.3f, 0.0f),
                new Vector3(0.3f, 0.7f, 0.0f),
                new Vector3(0.5f, 0.3f, 0.0f),
            ],
            AnchorPoint0 = new Vector3(0.0f, 0.0f, 0.0f),
            AnchorPoint1 = new Vector3(1.0f, 1.0f, 0.0f),
        };
    }
}
