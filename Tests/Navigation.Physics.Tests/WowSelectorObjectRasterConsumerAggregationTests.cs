using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorObjectRasterConsumerAggregationTests
{
    [Fact]
    public void EvaluateWoWSelectorObjectRasterCellLoopAggregation_AcceptsTwoTrianglesAndLinksCleanup()
    {
        SelectorObjectRasterPrefixTrace prefix = BuildAcceptedPrefix();
        byte[] cellModes = [0x00];
        uint[] pointOutcodes = [0u, 0u, 0u, 0u];
        ushort[] scratchWords = new ushort[6];

        uint result = EvaluateWoWSelectorObjectRasterCellLoopAggregation(
            prefix,
            pointGridRowStride: 3,
            cellModeRowStride: 1,
            cellModeMaskFlags: 0x00010000u,
            callerContextToken: 0x1234u,
            rasterSourceToken: 0x5678u,
            inputQueueEntryCount: 0u,
            inputScratchWordCount: 0u,
            deferredCleanupListPresent: 1u,
            cellModes,
            cellModes.Length,
            pointOutcodes,
            pointOutcodes.Length,
            scratchWords,
            scratchWords.Length,
            out SelectorObjectRasterAggregationTrace trace);

        Assert.Equal(1u, result);
        Assert.Equal(1u, trace.ReturnedAnyCandidate);
        Assert.Equal(1u, trace.VisitedRasterCellCount);
        Assert.Equal(2u, trace.AcceptedTriangleCount);
        Assert.Equal(0u, trace.RejectedTriangleCount);
        Assert.Equal(1u, trace.Entry.Allocated);
        Assert.Equal(1u, trace.Entry.ScratchBufferPresent);
        Assert.Equal((ushort)6, trace.Entry.AppendedWordCount);
        Assert.Equal((ushort)2, trace.Entry.AppendedTriangleCount);
        Assert.Equal(1u, trace.FinalQueueEntryListSpliceLinked);
        Assert.Equal(1u, trace.NormalCleanupLinked);
        Assert.Equal(new ushort[] { 0, 4, 3, 0, 1, 4 }, scratchWords);
    }

    [Fact]
    public void EvaluateWoWSelectorObjectRasterCellLoopAggregation_QueueLimitOverflowReturnsCandidateWithoutAllocatingEntry()
    {
        SelectorObjectRasterPrefixTrace prefix = BuildAcceptedPrefix();
        byte[] cellModes = [0x00];
        uint[] pointOutcodes = [0u, 0u, 0u, 0u];
        ushort[] scratchWords = new ushort[6];

        uint result = EvaluateWoWSelectorObjectRasterCellLoopAggregation(
            prefix,
            pointGridRowStride: 3,
            cellModeRowStride: 1,
            cellModeMaskFlags: 0x00010000u,
            callerContextToken: 0u,
            rasterSourceToken: 0u,
            inputQueueEntryCount: 31u,
            inputScratchWordCount: 0u,
            deferredCleanupListPresent: 0u,
            cellModes,
            cellModes.Length,
            pointOutcodes,
            pointOutcodes.Length,
            scratchWords,
            scratchWords.Length,
            out SelectorObjectRasterAggregationTrace trace);

        Assert.Equal(1u, result);
        Assert.Equal(1u, trace.ReturnedAnyCandidate);
        Assert.Equal(1u, trace.QueueLimitOverflowed);
        Assert.Equal(0u, trace.Entry.Allocated);
        Assert.Equal(31u, trace.QueueCountAfter);
        Assert.Equal(0u, trace.FinalQueueEntryListSpliceLinked);
    }

    [Fact]
    public void EvaluateWoWSelectorObjectRasterCellLoopAggregation_ScratchOverflowStillLinksAcceptedEntry()
    {
        SelectorObjectRasterPrefixTrace prefix = BuildAcceptedPrefix();
        byte[] cellModes = [0x00];
        uint[] pointOutcodes = [0u, 0u, 0u, 0u];
        ushort[] scratchWords = new ushort[3];

        uint result = EvaluateWoWSelectorObjectRasterCellLoopAggregation(
            prefix,
            pointGridRowStride: 3,
            cellModeRowStride: 1,
            cellModeMaskFlags: 0x00010000u,
            callerContextToken: 0x1u,
            rasterSourceToken: 0x2u,
            inputQueueEntryCount: 0u,
            inputScratchWordCount: 0u,
            deferredCleanupListPresent: 1u,
            cellModes,
            cellModes.Length,
            pointOutcodes,
            pointOutcodes.Length,
            scratchWords,
            scratchWords.Length,
            out SelectorObjectRasterAggregationTrace trace);

        Assert.Equal(1u, result);
        Assert.Equal(1u, trace.Entry.Allocated);
        Assert.Equal(0u, trace.Entry.ScratchBufferPresent);
        Assert.Equal(1u, trace.ScratchOverflowed);
        Assert.Equal(1u, trace.FinalQueueEntryListSpliceLinked);
        Assert.Equal(1u, trace.NormalCleanupLinked);
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
        return prefix;
    }

    private static SelectorObjectRasterPayload CreatePayload()
    {
        return new SelectorObjectRasterPayload
        {
            Planes =
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
