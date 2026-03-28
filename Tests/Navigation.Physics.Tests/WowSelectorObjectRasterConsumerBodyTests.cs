using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorObjectRasterConsumerBodyTests
{
    [Fact]
    public void EvaluateWoWSelectorObjectRasterConsumerBody_WritesPrepassOutcodesAndAppendsAcceptedTriangles()
    {
        SelectorObjectRasterPayload payload = CreatePayload();
        Vector3[] pointGrid =
        [
            new(0.0f, 0.0f, 0.0f), new(1.0f, 0.0f, 0.0f), new(2.0f, 0.0f, 0.0f),
            new(0.0f, 1.0f, 0.0f), new(1.0f, 1.0f, 0.0f), new(2.0f, 1.0f, 0.0f),
            new(0.0f, 2.0f, 0.0f), new(1.0f, 2.0f, 0.0f), new(2.0f, 2.0f, 0.0f),
        ];
        byte[] cellModes = [0x00];
        uint[] pointOutcodes = new uint[4];
        ushort[] scratchWords = new ushort[6];

        uint result = EvaluateWoWSelectorObjectRasterConsumerBody(
            modeWord: 0x00001000u,
            rasterRowCount: 4,
            rasterColumnCount: 4,
            pointGridRowStride: 3,
            cellModeRowStride: 1,
            quantizeScale: -1.0f,
            cellModeMaskFlags: 0x00010000u,
            callerContextToken: 0x1234u,
            rasterSourceToken: 0x5678u,
            inputQueueEntryCount: 0u,
            inputScratchWordCount: 0u,
            deferredCleanupListPresent: 1u,
            objectTranslation: new Vector3(1.0f, 1.0f, 0.0f),
            sourcePayload: payload,
            pointGrid: pointGrid,
            pointGridPointCount: pointGrid.Length,
            cellModes: cellModes,
            cellModeCount: cellModes.Length,
            pointOutcodes: pointOutcodes,
            pointOutcodeCapacity: pointOutcodes.Length,
            scratchWords: scratchWords,
            outScratchWordCapacity: scratchWords.Length,
            out SelectorObjectRasterQueueEntry entry,
            out SelectorObjectRasterBodyTrace trace);

        Assert.Equal(1u, result);
        Assert.Equal(1u, trace.ReturnedAnyCandidate);
        Assert.Equal(4u, trace.PrepassPointWrites);
        Assert.Equal(1u, trace.VisitedRasterCellCount);
        Assert.Equal(0u, trace.RejectedTriangleCount);
        Assert.Equal(2u, trace.AcceptedTriangleCount);
        Assert.Equal(1u, entry.Allocated);
        Assert.Equal(1u, entry.ScratchBufferPresent);
        Assert.Equal((ushort)6, entry.AppendedWordCount);
        Assert.Equal((ushort)2, entry.AppendedTriangleCount);
        Assert.Equal((ushort)0, entry.MinAppendedWord);
        Assert.Equal((ushort)4, entry.MaxAppendedWord);
        Assert.Equal(6u, entry.ScratchWordReserved);
        Assert.Equal(6u, trace.AppendedWordCount);
        Assert.Equal(2u, trace.AppendedTriangleCount);
        Assert.Equal(1u, trace.FinalQueueEntryListSpliceLinked);
        Assert.Equal(1u, trace.NormalCleanupLinked);
        Assert.Equal(new uint[] { 0u, 0u, 0u, 0u }, pointOutcodes);
        Assert.Equal(new ushort[] { 0, 4, 3, 0, 1, 4 }, scratchWords);
    }

    [Fact]
    public void EvaluateWoWSelectorObjectRasterConsumerBody_RejectsSecondTriangleWhenPrepassOutcodesSharePlaneBit()
    {
        SelectorObjectRasterPayload payload = CreatePayload(
            planes:
            [
                new SelectorSupportPlane { Normal = new Vector3(-2.0f, 0.1f, 0.0f), PlaneDistance = 1.8f },
                new SelectorSupportPlane(), new SelectorSupportPlane(),
                new SelectorSupportPlane(), new SelectorSupportPlane(), new SelectorSupportPlane(),
            ]);
        Vector3[] pointGrid =
        [
            new(0.0f, 0.0f, 0.0f), new(1.0f, 0.0f, 0.0f), new(2.0f, 0.0f, 0.0f),
            new(0.0f, 1.0f, 0.0f), new(1.0f, 1.0f, 0.0f), new(2.0f, 1.0f, 0.0f),
            new(0.0f, 2.0f, 0.0f), new(1.0f, 2.0f, 0.0f), new(2.0f, 2.0f, 0.0f),
        ];
        byte[] cellModes = [0x00];
        uint[] pointOutcodes = new uint[4];
        ushort[] scratchWords = new ushort[6];

        uint result = EvaluateWoWSelectorObjectRasterConsumerBody(
            modeWord: 0x00001000u,
            rasterRowCount: 4,
            rasterColumnCount: 4,
            pointGridRowStride: 3,
            cellModeRowStride: 1,
            quantizeScale: -1.0f,
            cellModeMaskFlags: 0x00010000u,
            callerContextToken: 0u,
            rasterSourceToken: 0u,
            inputQueueEntryCount: 0u,
            inputScratchWordCount: 0u,
            deferredCleanupListPresent: 0u,
            objectTranslation: new Vector3(1.0f, 1.0f, 0.0f),
            sourcePayload: payload,
            pointGrid: pointGrid,
            pointGridPointCount: pointGrid.Length,
            cellModes: cellModes,
            cellModeCount: cellModes.Length,
            pointOutcodes: pointOutcodes,
            pointOutcodeCapacity: pointOutcodes.Length,
            scratchWords: scratchWords,
            outScratchWordCapacity: scratchWords.Length,
            out SelectorObjectRasterQueueEntry entry,
            out SelectorObjectRasterBodyTrace trace);

        Assert.Equal(1u, result);
        Assert.Equal(new uint[] { 1u, 1u, 0u, 1u }, pointOutcodes);
        Assert.Equal(1u, trace.RejectedTriangleCount);
        Assert.Equal(1u, trace.AcceptedTriangleCount);
        Assert.Equal((ushort)3, entry.AppendedWordCount);
        Assert.Equal((ushort)1, entry.AppendedTriangleCount);
        Assert.Equal(new ushort[] { 0, 4, 3, 0, 0, 0 }, scratchWords);
    }

    [Fact]
    public void EvaluateWoWSelectorObjectRasterConsumerBody_QueueLimitOverflowReturnsCandidateWithoutAppend()
    {
        SelectorObjectRasterPayload payload = CreatePayload();
        Vector3[] pointGrid =
        [
            new(0.0f, 0.0f, 0.0f), new(1.0f, 0.0f, 0.0f), new(2.0f, 0.0f, 0.0f),
            new(0.0f, 1.0f, 0.0f), new(1.0f, 1.0f, 0.0f), new(2.0f, 1.0f, 0.0f),
            new(0.0f, 2.0f, 0.0f), new(1.0f, 2.0f, 0.0f), new(2.0f, 2.0f, 0.0f),
        ];
        byte[] cellModes = [0x00];
        uint[] pointOutcodes = new uint[4];
        ushort[] scratchWords = new ushort[6];

        uint result = EvaluateWoWSelectorObjectRasterConsumerBody(
            modeWord: 0x00001000u,
            rasterRowCount: 4,
            rasterColumnCount: 4,
            pointGridRowStride: 3,
            cellModeRowStride: 1,
            quantizeScale: -1.0f,
            cellModeMaskFlags: 0x00010000u,
            callerContextToken: 0u,
            rasterSourceToken: 0u,
            inputQueueEntryCount: 31u,
            inputScratchWordCount: 0u,
            deferredCleanupListPresent: 0u,
            objectTranslation: new Vector3(1.0f, 1.0f, 0.0f),
            sourcePayload: payload,
            pointGrid: pointGrid,
            pointGridPointCount: pointGrid.Length,
            cellModes: cellModes,
            cellModeCount: cellModes.Length,
            pointOutcodes: pointOutcodes,
            pointOutcodeCapacity: pointOutcodes.Length,
            scratchWords: scratchWords,
            outScratchWordCapacity: scratchWords.Length,
            out SelectorObjectRasterQueueEntry entry,
            out SelectorObjectRasterBodyTrace trace);

        Assert.Equal(1u, result);
        Assert.Equal(1u, trace.ReturnedAnyCandidate);
        Assert.Equal(1u, trace.QueueLimitOverflowed);
        Assert.Equal(0u, entry.Allocated);
        Assert.Equal(0u, trace.AppendedWordCount);
        Assert.Equal(31u, trace.QueueCountAfter);
    }

    [Fact]
    public void EvaluateWoWSelectorObjectRasterConsumerBody_ScratchOverflowAllocatesSlotButLeavesBufferNull()
    {
        SelectorObjectRasterPayload payload = CreatePayload();
        Vector3[] pointGrid =
        [
            new(0.0f, 0.0f, 0.0f), new(1.0f, 0.0f, 0.0f), new(2.0f, 0.0f, 0.0f),
            new(0.0f, 1.0f, 0.0f), new(1.0f, 1.0f, 0.0f), new(2.0f, 1.0f, 0.0f),
            new(0.0f, 2.0f, 0.0f), new(1.0f, 2.0f, 0.0f), new(2.0f, 2.0f, 0.0f),
        ];
        byte[] cellModes = [0x00];
        uint[] pointOutcodes = new uint[4];
        ushort[] scratchWords = new ushort[3];

        uint result = EvaluateWoWSelectorObjectRasterConsumerBody(
            modeWord: 0x00001000u,
            rasterRowCount: 4,
            rasterColumnCount: 4,
            pointGridRowStride: 3,
            cellModeRowStride: 1,
            quantizeScale: -1.0f,
            cellModeMaskFlags: 0x00010000u,
            callerContextToken: 0x1u,
            rasterSourceToken: 0x2u,
            inputQueueEntryCount: 0u,
            inputScratchWordCount: 0u,
            deferredCleanupListPresent: 0u,
            objectTranslation: new Vector3(1.0f, 1.0f, 0.0f),
            sourcePayload: payload,
            pointGrid: pointGrid,
            pointGridPointCount: pointGrid.Length,
            cellModes: cellModes,
            cellModeCount: cellModes.Length,
            pointOutcodes: pointOutcodes,
            pointOutcodeCapacity: pointOutcodes.Length,
            scratchWords: scratchWords,
            outScratchWordCapacity: scratchWords.Length,
            out SelectorObjectRasterQueueEntry entry,
            out SelectorObjectRasterBodyTrace trace);

        Assert.Equal(1u, result);
        Assert.Equal(1u, entry.Allocated);
        Assert.Equal(0u, entry.ScratchBufferPresent);
        Assert.Equal(1u, trace.ScratchOverflowed);
        Assert.Equal(1u, trace.QueueCountAfter);
        Assert.Equal(0u, trace.AppendedWordCount);
    }

    [Fact]
    public void EvaluateWoWSelectorObjectRasterConsumerBody_FinalQueueEntryListSpliceLinksAcceptedOutputEvenWhenScratchBufferIsNull()
    {
        SelectorObjectRasterPayload payload = CreatePayload();
        Vector3[] pointGrid =
        [
            new(0.0f, 0.0f, 0.0f), new(1.0f, 0.0f, 0.0f), new(2.0f, 0.0f, 0.0f),
            new(0.0f, 1.0f, 0.0f), new(1.0f, 1.0f, 0.0f), new(2.0f, 1.0f, 0.0f),
            new(0.0f, 2.0f, 0.0f), new(1.0f, 2.0f, 0.0f), new(2.0f, 2.0f, 0.0f),
        ];
        byte[] cellModes = [0x00];
        uint[] pointOutcodes = new uint[4];
        ushort[] scratchWords = new ushort[3];

        uint result = EvaluateWoWSelectorObjectRasterConsumerBody(
            modeWord: 0x00001000u,
            rasterRowCount: 4,
            rasterColumnCount: 4,
            pointGridRowStride: 3,
            cellModeRowStride: 1,
            quantizeScale: -1.0f,
            cellModeMaskFlags: 0x00010000u,
            callerContextToken: 0x1u,
            rasterSourceToken: 0x2u,
            inputQueueEntryCount: 0u,
            inputScratchWordCount: 0u,
            deferredCleanupListPresent: 1u,
            objectTranslation: new Vector3(1.0f, 1.0f, 0.0f),
            sourcePayload: payload,
            pointGrid: pointGrid,
            pointGridPointCount: pointGrid.Length,
            cellModes: cellModes,
            cellModeCount: cellModes.Length,
            pointOutcodes: pointOutcodes,
            pointOutcodeCapacity: pointOutcodes.Length,
            scratchWords: scratchWords,
            outScratchWordCapacity: scratchWords.Length,
            out SelectorObjectRasterQueueEntry entry,
            out SelectorObjectRasterBodyTrace trace);

        Assert.Equal(1u, result);
        Assert.Equal(2u, trace.AcceptedTriangleCount);
        Assert.Equal(1u, entry.Allocated);
        Assert.Equal(0u, entry.ScratchBufferPresent);
        Assert.Equal(1u, trace.ScratchOverflowed);
        Assert.Equal(1u, trace.FinalQueueEntryListSpliceLinked);
        Assert.Equal(1u, trace.NormalCleanupLinked);
    }

    [Fact]
    public void EvaluateWoWSelectorObjectRasterConsumerBody_PrefixFailureTriggersVisibleFailureCleanup()
    {
        SelectorObjectRasterPayload payload = CreatePayload();
        Vector3[] pointGrid =
        [
            new(0.0f, 0.0f, 0.0f), new(1.0f, 0.0f, 0.0f), new(2.0f, 0.0f, 0.0f),
            new(0.0f, 1.0f, 0.0f), new(1.0f, 1.0f, 0.0f), new(2.0f, 1.0f, 0.0f),
            new(0.0f, 2.0f, 0.0f), new(1.0f, 2.0f, 0.0f), new(2.0f, 2.0f, 0.0f),
        ];
        byte[] cellModes = [0x00];
        uint[] pointOutcodes = new uint[4];
        ushort[] scratchWords = new ushort[6];

        uint result = EvaluateWoWSelectorObjectRasterConsumerBody(
            modeWord: 0x00001000u,
            rasterRowCount: 0,
            rasterColumnCount: 0,
            pointGridRowStride: 3,
            cellModeRowStride: 1,
            quantizeScale: -1.0f,
            cellModeMaskFlags: 0x00010000u,
            callerContextToken: 0u,
            rasterSourceToken: 0u,
            inputQueueEntryCount: 0u,
            inputScratchWordCount: 0u,
            deferredCleanupListPresent: 0u,
            objectTranslation: new Vector3(1.0f, 1.0f, 0.0f),
            sourcePayload: payload,
            pointGrid: pointGrid,
            pointGridPointCount: pointGrid.Length,
            cellModes: cellModes,
            cellModeCount: cellModes.Length,
            pointOutcodes: pointOutcodes,
            pointOutcodeCapacity: pointOutcodes.Length,
            scratchWords: scratchWords,
            outScratchWordCapacity: scratchWords.Length,
            out SelectorObjectRasterQueueEntry entry,
            out SelectorObjectRasterBodyTrace trace);

        Assert.Equal(0u, result);
        Assert.Equal(1u, trace.FailureCleanupExecuted);
        Assert.Equal(6u, trace.FailureCleanupDestroyedPayloadBlocks);
        Assert.Equal(0u, entry.Allocated);
    }

    [Fact]
    public void EvaluateWoWSelectorObjectRasterConsumerBody_NoAcceptedTrianglesDoesNotReportCleanupLink()
    {
        SelectorObjectRasterPayload payload = CreatePayload();
        Vector3[] pointGrid =
        [
            new(0.0f, 0.0f, 0.0f), new(1.0f, 0.0f, 0.0f), new(2.0f, 0.0f, 0.0f),
            new(0.0f, 1.0f, 0.0f), new(1.0f, 1.0f, 0.0f), new(2.0f, 1.0f, 0.0f),
            new(0.0f, 2.0f, 0.0f), new(1.0f, 2.0f, 0.0f), new(2.0f, 2.0f, 0.0f),
        ];
        byte[] cellModes = [0x00];
        uint[] pointOutcodes = new uint[4];
        ushort[] scratchWords = new ushort[6];

        uint result = EvaluateWoWSelectorObjectRasterConsumerBody(
            modeWord: 0x00001000u,
            rasterRowCount: 4,
            rasterColumnCount: 4,
            pointGridRowStride: 3,
            cellModeRowStride: 1,
            quantizeScale: -1.0f,
            cellModeMaskFlags: 0x00000000u,
            callerContextToken: 0u,
            rasterSourceToken: 0u,
            inputQueueEntryCount: 0u,
            inputScratchWordCount: 0u,
            deferredCleanupListPresent: 1u,
            objectTranslation: new Vector3(1.0f, 1.0f, 0.0f),
            sourcePayload: payload,
            pointGrid: pointGrid,
            pointGridPointCount: pointGrid.Length,
            cellModes: cellModes,
            cellModeCount: cellModes.Length,
            pointOutcodes: pointOutcodes,
            pointOutcodeCapacity: pointOutcodes.Length,
            scratchWords: scratchWords,
            outScratchWordCapacity: scratchWords.Length,
            out SelectorObjectRasterQueueEntry entry,
            out SelectorObjectRasterBodyTrace trace);

        Assert.Equal(0u, result);
        Assert.Equal(0u, trace.ReturnedAnyCandidate);
        Assert.Equal(0u, trace.AcceptedTriangleCount);
        Assert.Equal(0u, entry.Allocated);
        Assert.Equal(0u, trace.FinalQueueEntryListSpliceLinked);
        Assert.Equal(0u, trace.NormalCleanupLinked);
        Assert.Equal(new uint[] { 0u, 0u, 0u, 0u }, pointOutcodes);
    }

    [Fact]
    public void EvaluateWoWSelectorObjectRasterConsumerBody_CellModeFSkipsWithoutAllocatingEntry()
    {
        SelectorObjectRasterPayload payload = CreatePayload();
        Vector3[] pointGrid =
        [
            new(0.0f, 0.0f, 0.0f), new(1.0f, 0.0f, 0.0f), new(2.0f, 0.0f, 0.0f),
            new(0.0f, 1.0f, 0.0f), new(1.0f, 1.0f, 0.0f), new(2.0f, 1.0f, 0.0f),
            new(0.0f, 2.0f, 0.0f), new(1.0f, 2.0f, 0.0f), new(2.0f, 2.0f, 0.0f),
        ];
        byte[] cellModes = [0x0F];
        uint[] pointOutcodes = new uint[4];
        ushort[] scratchWords = new ushort[6];

        uint result = EvaluateWoWSelectorObjectRasterConsumerBody(
            modeWord: 0x00001000u,
            rasterRowCount: 4,
            rasterColumnCount: 4,
            pointGridRowStride: 3,
            cellModeRowStride: 1,
            quantizeScale: -1.0f,
            cellModeMaskFlags: 0x00010000u,
            callerContextToken: 0u,
            rasterSourceToken: 0u,
            inputQueueEntryCount: 0u,
            inputScratchWordCount: 0u,
            deferredCleanupListPresent: 0u,
            objectTranslation: new Vector3(1.0f, 1.0f, 0.0f),
            sourcePayload: payload,
            pointGrid: pointGrid,
            pointGridPointCount: pointGrid.Length,
            cellModes: cellModes,
            cellModeCount: cellModes.Length,
            pointOutcodes: pointOutcodes,
            pointOutcodeCapacity: pointOutcodes.Length,
            scratchWords: scratchWords,
            outScratchWordCapacity: scratchWords.Length,
            out SelectorObjectRasterQueueEntry entry,
            out SelectorObjectRasterBodyTrace trace);

        Assert.Equal(0u, result);
        Assert.Equal(0u, trace.ReturnedAnyCandidate);
        Assert.Equal(1u, trace.VisitedRasterCellCount);
        Assert.Equal(1u, trace.SkippedByCellModeValue);
        Assert.Equal(0u, trace.SkippedByCellModeMask);
        Assert.Equal(0u, trace.AcceptedTriangleCount);
        Assert.Equal(0u, entry.Allocated);
        Assert.Equal(0u, trace.NormalCleanupLinked);
        Assert.Equal(new uint[] { 0u, 0u, 0u, 0u }, pointOutcodes);
    }

    [Fact]
    public void EvaluateWoWSelectorObjectRasterConsumerBody_CellModeMaskSkipDoesNotAllocateEntry()
    {
        SelectorObjectRasterPayload payload = CreatePayload();
        Vector3[] pointGrid =
        [
            new(0.0f, 0.0f, 0.0f), new(1.0f, 0.0f, 0.0f), new(2.0f, 0.0f, 0.0f),
            new(0.0f, 1.0f, 0.0f), new(1.0f, 1.0f, 0.0f), new(2.0f, 1.0f, 0.0f),
            new(0.0f, 2.0f, 0.0f), new(1.0f, 2.0f, 0.0f), new(2.0f, 2.0f, 0.0f),
        ];
        byte[] cellModes = [0x00];
        uint[] pointOutcodes = new uint[4];
        ushort[] scratchWords = new ushort[6];

        uint result = EvaluateWoWSelectorObjectRasterConsumerBody(
            modeWord: 0x00001000u,
            rasterRowCount: 4,
            rasterColumnCount: 4,
            pointGridRowStride: 3,
            cellModeRowStride: 1,
            quantizeScale: -1.0f,
            cellModeMaskFlags: 0x00020000u,
            callerContextToken: 0u,
            rasterSourceToken: 0u,
            inputQueueEntryCount: 0u,
            inputScratchWordCount: 0u,
            deferredCleanupListPresent: 0u,
            objectTranslation: new Vector3(1.0f, 1.0f, 0.0f),
            sourcePayload: payload,
            pointGrid: pointGrid,
            pointGridPointCount: pointGrid.Length,
            cellModes: cellModes,
            cellModeCount: cellModes.Length,
            pointOutcodes: pointOutcodes,
            pointOutcodeCapacity: pointOutcodes.Length,
            scratchWords: scratchWords,
            outScratchWordCapacity: scratchWords.Length,
            out SelectorObjectRasterQueueEntry entry,
            out SelectorObjectRasterBodyTrace trace);

        Assert.Equal(0u, result);
        Assert.Equal(4u, trace.PrepassPointWrites);
        Assert.Equal(1u, trace.VisitedRasterCellCount);
        Assert.Equal(0u, trace.SkippedByCellModeValue);
        Assert.Equal(1u, trace.SkippedByCellModeMask);
        Assert.Equal(0u, trace.AcceptedTriangleCount);
        Assert.Equal(0u, entry.Allocated);
    }

    [Fact]
    public void EvaluateWoWSelectorObjectRasterConsumerBody_AllocatedEntryCarriesCallerTokensAndScratchOffsets()
    {
        SelectorObjectRasterPayload payload = CreatePayload();
        Vector3[] pointGrid =
        [
            new(0.0f, 0.0f, 0.0f), new(1.0f, 0.0f, 0.0f), new(2.0f, 0.0f, 0.0f),
            new(0.0f, 1.0f, 0.0f), new(1.0f, 1.0f, 0.0f), new(2.0f, 1.0f, 0.0f),
            new(0.0f, 2.0f, 0.0f), new(1.0f, 2.0f, 0.0f), new(2.0f, 2.0f, 0.0f),
        ];
        byte[] cellModes = [0x00];
        uint[] pointOutcodes = new uint[4];
        ushort[] scratchWords = new ushort[20];

        uint result = EvaluateWoWSelectorObjectRasterConsumerBody(
            modeWord: 0x00001000u,
            rasterRowCount: 4,
            rasterColumnCount: 4,
            pointGridRowStride: 3,
            cellModeRowStride: 1,
            quantizeScale: -1.0f,
            cellModeMaskFlags: 0x00010000u,
            callerContextToken: 0x1234u,
            rasterSourceToken: 0x5678u,
            inputQueueEntryCount: 5u,
            inputScratchWordCount: 12u,
            deferredCleanupListPresent: 0u,
            objectTranslation: new Vector3(1.0f, 1.0f, 0.0f),
            sourcePayload: payload,
            pointGrid: pointGrid,
            pointGridPointCount: pointGrid.Length,
            cellModes: cellModes,
            cellModeCount: cellModes.Length,
            pointOutcodes: pointOutcodes,
            pointOutcodeCapacity: pointOutcodes.Length,
            scratchWords: scratchWords,
            outScratchWordCapacity: scratchWords.Length,
            out SelectorObjectRasterQueueEntry entry,
            out SelectorObjectRasterBodyTrace trace);

        Assert.Equal(1u, result);
        Assert.Equal(5u, trace.QueueCountBefore);
        Assert.Equal(6u, trace.QueueCountAfter);
        Assert.Equal(12u, trace.ScratchWordsBefore);
        Assert.Equal(18u, trace.ScratchWordsAfter);
        Assert.Equal(1u, entry.Allocated);
        Assert.Equal(0x1234u, entry.CallerContextToken);
        Assert.Equal(0x5678u, entry.RasterSourceToken);
        Assert.Equal(12u, entry.ScratchWordStart);
        Assert.Equal(6u, entry.ScratchWordReserved);
        Assert.Equal(1u, entry.ScratchBufferPresent);
        Assert.Equal(new ushort[] { 0, 4, 3, 0, 1, 4 },
            new ushort[] { scratchWords[12], scratchWords[13], scratchWords[14], scratchWords[15], scratchWords[16], scratchWords[17] });
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
