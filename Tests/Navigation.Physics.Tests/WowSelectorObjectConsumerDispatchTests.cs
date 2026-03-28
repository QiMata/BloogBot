using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorObjectConsumerDispatchTests
{
    [Fact]
    public void EvaluateWoWSelectorObjectConsumerDispatch_SkipsRasterWithoutPayloadBitsAndReturnsZeroWhenQueueIsUnchanged()
    {
        ushort[] pendingIds = { 3, 1, 7 };
        byte[] stateBytes = new byte[16];
        stateBytes[6] = 0xA5;
        stateBytes[2] = 0x80;
        stateBytes[14] = 0xC0;

        uint pendingCount = 2;
        uint acceptedCount = 5;

        uint result = EvaluateWoWSelectorObjectConsumerDispatch(
            inputFlags: 0x00000040u,
            queueMutationCountBefore: 9u,
            queueMutationCountAfterConsumers: 9u,
            pendingIds,
            pendingIds.Length,
            ref pendingCount,
            ref acceptedCount,
            stateBytes,
            stateBytes.Length,
            out SelectorObjectConsumerDispatchTrace trace);

        Assert.Equal(0u, result);
        Assert.Equal(1u, trace.CalledTraversal);
        Assert.Equal(1u, trace.CalledAcceptedListConsumer);
        Assert.Equal(0u, trace.CalledRasterConsumer);
        Assert.Equal(2u, trace.PendingCountBeforeCleanup);
        Assert.Equal(5u, trace.AcceptedCountBeforeCleanup);
        Assert.Equal(0u, trace.PendingCountAfterCleanup);
        Assert.Equal(0u, trace.AcceptedCountAfterCleanup);
        Assert.Equal(0u, pendingCount);
        Assert.Equal(0u, acceptedCount);
        Assert.Equal(2u, trace.ClearedQueuedVisitedBits);
        Assert.Equal((byte)0x25, stateBytes[6]);
        Assert.Equal((byte)0x00, stateBytes[2]);
        Assert.Equal((byte)0xC0, stateBytes[14]);
        Assert.Equal(0u, trace.QueueMutationObserved);
    }

    [Fact]
    public void EvaluateWoWSelectorObjectConsumerDispatch_RasterGateUsesHighPayloadBitsAndReturnsMutationFlag()
    {
        ushort[] pendingIds = { 2, 4 };
        byte[] stateBytes = new byte[12];
        stateBytes[4] = 0xFF;

        uint pendingCount = 1;
        uint acceptedCount = 3;

        uint result = EvaluateWoWSelectorObjectConsumerDispatch(
            inputFlags: 0x00010000u,
            queueMutationCountBefore: 12u,
            queueMutationCountAfterConsumers: 13u,
            pendingIds,
            pendingIds.Length,
            ref pendingCount,
            ref acceptedCount,
            stateBytes,
            stateBytes.Length,
            out SelectorObjectConsumerDispatchTrace trace);

        Assert.Equal(1u, result);
        Assert.Equal(1u, trace.CalledTraversal);
        Assert.Equal(1u, trace.CalledAcceptedListConsumer);
        Assert.Equal(1u, trace.CalledRasterConsumer);
        Assert.Equal(1u, trace.QueueMutationObserved);
        Assert.Equal(1u, trace.ClearedQueuedVisitedBits);
        Assert.Equal((byte)0x7F, stateBytes[4]);
    }

    [Fact]
    public void EvaluateWoWSelectorObjectConsumerDispatch_CleanupStopsAtPendingCapacityAndIgnoresMissingStateBuffer()
    {
        ushort[] pendingIds = { 1, 5, 9 };
        uint pendingCount = 3;
        uint acceptedCount = 2;

        uint result = EvaluateWoWSelectorObjectConsumerDispatch(
            inputFlags: 0x00020000u,
            queueMutationCountBefore: 1u,
            queueMutationCountAfterConsumers: 1u,
            pendingIds,
            pendingIdCapacity: 1,
            ref pendingCount,
            ref acceptedCount,
            stateBytes: null!,
            stateByteCount: 0,
            out SelectorObjectConsumerDispatchTrace trace);

        Assert.Equal(0u, result);
        Assert.Equal(1u, trace.CalledRasterConsumer);
        Assert.Equal(3u, trace.PendingCountBeforeCleanup);
        Assert.Equal(0u, trace.ClearedQueuedVisitedBits);
        Assert.Equal(0u, pendingCount);
        Assert.Equal(0u, acceptedCount);
    }
}

[Collection("PhysicsEngine")]
public class WowSelectorAcceptedListConsumerVisibleBodyTests
{
    [Fact]
    public void EvaluateWoWSelectorAcceptedListConsumerVisibleBody_PreprocessesBothQueuesAndCopiesTriangleWordsOnSuccessfulReservation()
    {
        ushort[] pendingIds = { 5, 7 };
        ushort[] acceptedIds = { 1, 3 };
        ushort[] triangleVertexIndices = { 10, 11, 12, 20, 21, 22, 30, 31, 32, 40, 41, 42 };
        ushort[] outputAcceptedIds = new ushort[16];
        ushort[] outputTriangleWords = new ushort[16];
        uint recordReservationCount = 2;
        uint triangleWordCount = 5;
        uint acceptedIdCount = 8;

        uint reserved = EvaluateWoWSelectorAcceptedListConsumerVisibleBody(
            globalFlags: 0x00200000u,
            inputQueueFlags: 0x10u,
            inputConsumerFlags: 0x200u,
            ref recordReservationCount,
            ref triangleWordCount,
            ref acceptedIdCount,
            pendingIds,
            pendingIds.Length,
            acceptedIds,
            acceptedIds.Length,
            triangleVertexIndices,
            triangleVertexIndices.Length,
            outputAcceptedIds,
            outputAcceptedIds.Length,
            outputTriangleWords,
            outputTriangleWords.Length,
            out SelectorAcceptedListConsumerTrace trace);

        Assert.Equal(1u, reserved);
        Assert.Equal(1u, trace.PreprocessedPendingQueue);
        Assert.Equal(1u, trace.PreprocessedAcceptedQueue);
        Assert.Equal(2u, trace.PendingPreprocessIterations);
        Assert.Equal(2u, trace.AcceptedPreprocessIterations);
        Assert.Equal(4u, trace.Helper6acdd0CallCount);
        Assert.Equal(12u, trace.Helper7bca80CallCount);
        Assert.Equal(4u, trace.Helper6bce50CallCount);
        Assert.Equal(4u, trace.Helper6a98e0CallCount);
        Assert.Equal(0x210u, trace.OutputQueueFlags);
        Assert.Equal(1u, trace.RecordSlotReserved);
        Assert.Equal(1u, trace.TriangleWordSpanReserved);
        Assert.Equal(1u, trace.AcceptedIdSpanReserved);
        Assert.Equal(5u, trace.ReservedTriangleWordStart);
        Assert.Equal(6u, trace.ReservedTriangleWordCount);
        Assert.Equal(8u, trace.ReservedAcceptedIdStart);
        Assert.Equal(2u, trace.ReservedAcceptedIdCount);
        Assert.Equal(6u, trace.CopiedTriangleWordCount);
        Assert.Equal(2u, trace.CopiedAcceptedIdCount);
        Assert.Equal(20u, trace.MinTriangleVertexIndex);
        Assert.Equal(42u, trace.MaxTriangleVertexIndex);
        Assert.Equal((uint)3, recordReservationCount);
        Assert.Equal((uint)11, triangleWordCount);
        Assert.Equal((uint)10, acceptedIdCount);
        Assert.Equal(new ushort[] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 3, 0, 0, 0, 0, 0, 0 }, outputAcceptedIds);
        Assert.Equal(new ushort[] { 0, 0, 0, 0, 0, 20, 21, 22, 40, 41, 42, 0, 0, 0, 0, 0 }, outputTriangleWords);
    }

    [Fact]
    public void EvaluateWoWSelectorAcceptedListConsumerVisibleBody_SkipsPendingPreprocessWhenGlobalGateIsClear()
    {
        ushort[] pendingIds = { 9 };
        ushort[] acceptedIds = { 0 };
        ushort[] triangleVertexIndices = { 50, 51, 52 };
        ushort[] outputAcceptedIds = new ushort[1];
        ushort[] outputTriangleWords = new ushort[5];
        uint recordReservationCount = 0;
        uint triangleWordCount = 0;
        uint acceptedIdCount = 0;

        _ = EvaluateWoWSelectorAcceptedListConsumerVisibleBody(
            globalFlags: 0u,
            inputQueueFlags: 0u,
            inputConsumerFlags: 0x40u,
            ref recordReservationCount,
            ref triangleWordCount,
            ref acceptedIdCount,
            pendingIds,
            pendingIds.Length,
            acceptedIds,
            acceptedIds.Length,
            triangleVertexIndices,
            triangleVertexIndices.Length,
            outputAcceptedIds,
            outputAcceptedIds.Length,
            outputTriangleWords,
            outputTriangleWords.Length,
            out SelectorAcceptedListConsumerTrace trace);

        Assert.Equal(0u, trace.PreprocessedPendingQueue);
        Assert.Equal(0u, trace.PendingPreprocessIterations);
        Assert.Equal(1u, trace.PreprocessedAcceptedQueue);
        Assert.Equal(1u, trace.AcceptedPreprocessIterations);
        Assert.Equal(1u, trace.Helper6acdd0CallCount);
        Assert.Equal(3u, trace.Helper7bca80CallCount);
    }

    [Fact]
    public void EvaluateWoWSelectorAcceptedListConsumerVisibleBody_ZeroAcceptedCountOnlyOrsQueueFlags()
    {
        ushort[] empty = Array.Empty<ushort>();
        uint recordReservationCount = 6;
        uint triangleWordCount = 12;
        uint acceptedIdCount = 4;

        uint reserved = EvaluateWoWSelectorAcceptedListConsumerVisibleBody(
            globalFlags: 0x00200000u,
            inputQueueFlags: 0x08u,
            inputConsumerFlags: 0x100u,
            ref recordReservationCount,
            ref triangleWordCount,
            ref acceptedIdCount,
            empty,
            0,
            empty,
            0,
            empty,
            0,
            empty,
            0,
            empty,
            0,
            out SelectorAcceptedListConsumerTrace trace);

        Assert.Equal(0u, reserved);
        Assert.Equal(0x108u, trace.OutputQueueFlags);
        Assert.Equal(0u, trace.RecordSlotReserved);
        Assert.Equal((uint)6, recordReservationCount);
        Assert.Equal((uint)12, triangleWordCount);
        Assert.Equal((uint)4, acceptedIdCount);
    }

    [Fact]
    public void EvaluateWoWSelectorAcceptedListConsumerVisibleBody_RecordReservationOverflowSetsGlobalOverflowFlagAndReturnsEarly()
    {
        ushort[] acceptedIds = { 1 };
        ushort[] triangleVertexIndices = { 1, 2, 3 };
        ushort[] outputAcceptedIds = new ushort[1];
        ushort[] outputTriangleWords = new ushort[3];
        uint recordReservationCount = 31;
        uint triangleWordCount = 4;
        uint acceptedIdCount = 7;

        uint reserved = EvaluateWoWSelectorAcceptedListConsumerVisibleBody(
            globalFlags: 0u,
            inputQueueFlags: 0x20u,
            inputConsumerFlags: 0x40u,
            ref recordReservationCount,
            ref triangleWordCount,
            ref acceptedIdCount,
            pendingIds: Array.Empty<ushort>(),
            pendingCount: 0,
            acceptedIds,
            acceptedIds.Length,
            triangleVertexIndices,
            triangleVertexIndices.Length,
            outputAcceptedIds,
            outputAcceptedIds.Length,
            outputTriangleWords,
            outputTriangleWords.Length,
            out SelectorAcceptedListConsumerTrace trace);

        Assert.Equal(0u, reserved);
        Assert.Equal(1u, trace.RecordOverflowFlagSet);
        Assert.Equal(0x61u, trace.OutputQueueFlags);
        Assert.Equal(0u, trace.TriangleWordSpanReserved);
        Assert.Equal(0u, trace.AcceptedIdSpanReserved);
        Assert.Equal((uint)31, recordReservationCount);
        Assert.Equal((uint)4, triangleWordCount);
        Assert.Equal((uint)7, acceptedIdCount);
    }

    [Fact]
    public void EvaluateWoWSelectorAcceptedListConsumerVisibleBody_TriangleWordOverflowStillCopiesAcceptedIdsWhenThatSidecarReserves()
    {
        ushort[] acceptedIds = { 1 };
        ushort[] triangleVertexIndices = { 3, 4, 5, 6, 7, 8 };
        ushort[] outputAcceptedIds = new ushort[1];
        ushort[] outputTriangleWords = new ushort[0xC000];
        uint recordReservationCount = 0;
        uint triangleWordCount = 0xBFFDu;
        uint acceptedIdCount = 0;

        uint reserved = EvaluateWoWSelectorAcceptedListConsumerVisibleBody(
            globalFlags: 0u,
            inputQueueFlags: 0x10u,
            inputConsumerFlags: 0x04u,
            ref recordReservationCount,
            ref triangleWordCount,
            ref acceptedIdCount,
            pendingIds: Array.Empty<ushort>(),
            pendingCount: 0,
            acceptedIds,
            acceptedIds.Length,
            triangleVertexIndices,
            triangleVertexIndices.Length,
            outputAcceptedIds,
            outputAcceptedIds.Length,
            outputTriangleWords,
            outputTriangleWords.Length,
            out SelectorAcceptedListConsumerTrace trace);

        Assert.Equal(1u, reserved);
        Assert.Equal(1u, trace.RecordSlotReserved);
        Assert.Equal(1u, trace.TriangleWordOverflowFlagSet);
        Assert.Equal(0u, trace.TriangleWordSpanReserved);
        Assert.Equal(1u, trace.AcceptedIdSpanReserved);
        Assert.Equal(1u, trace.CopiedAcceptedIdCount);
        Assert.Equal(0u, trace.CopiedTriangleWordCount);
        Assert.Equal(0x15u, trace.OutputQueueFlags);
        Assert.Equal((ushort)1, outputAcceptedIds[0]);
        Assert.Equal((uint)1, acceptedIdCount);
    }

    [Fact]
    public void EvaluateWoWSelectorAcceptedListConsumerVisibleBody_AcceptedIdOverflowStillReservesTriangleWords()
    {
        ushort[] acceptedIds = { 2 };
        ushort[] triangleVertexIndices = { 1, 2, 3, 7, 8, 9, 13, 14, 15 };
        ushort[] outputAcceptedIds = new ushort[1];
        ushort[] outputTriangleWords = new ushort[3];
        uint recordReservationCount = 0;
        uint triangleWordCount = 2;
        uint acceptedIdCount = 0x3FFFu;

        uint reserved = EvaluateWoWSelectorAcceptedListConsumerVisibleBody(
            globalFlags: 0u,
            inputQueueFlags: 0x08u,
            inputConsumerFlags: 0x02u,
            ref recordReservationCount,
            ref triangleWordCount,
            ref acceptedIdCount,
            pendingIds: Array.Empty<ushort>(),
            pendingCount: 0,
            acceptedIds,
            acceptedIds.Length,
            triangleVertexIndices,
            triangleVertexIndices.Length,
            outputAcceptedIds,
            outputAcceptedIds.Length,
            outputTriangleWords,
            outputTriangleWords.Length,
            out SelectorAcceptedListConsumerTrace trace);

        Assert.Equal(1u, reserved);
        Assert.Equal(1u, trace.RecordSlotReserved);
        Assert.Equal(1u, trace.TriangleWordSpanReserved);
        Assert.Equal(0u, trace.AcceptedIdSpanReserved);
        Assert.Equal(1u, trace.AcceptedIdOverflowFlagSet);
        Assert.Equal(0u, trace.TriangleWordOverflowFlagSet);
        Assert.Equal(0x0Bu, trace.OutputQueueFlags);
        Assert.Equal((uint)1, recordReservationCount);
        Assert.Equal((uint)5, triangleWordCount);
        Assert.Equal(0x3FFFu, acceptedIdCount);
        Assert.Equal(0u, trace.CopiedAcceptedIdCount);
        Assert.True(trace.CopiedTriangleWordCount > 0u);
        Assert.Equal(0u, outputAcceptedIds[0]);
        Assert.Equal((ushort)13, outputTriangleWords[2]);
    }
}

[Collection("PhysicsEngine")]
public class WowSelectorAcceptedListConsumerRecordWriteTests
{
    [Fact]
    public void EvaluateWoWSelectorAcceptedListConsumerRecordWrite_WritesRecordSlotAndBothBufferTokensOnFullReservation()
    {
        ushort[] pendingIds = { 5, 7 };
        ushort[] acceptedIds = { 1, 3 };
        ushort[] triangleVertexIndices = { 10, 11, 12, 20, 21, 22, 30, 31, 32, 40, 41, 42 };
        ushort[] outputAcceptedIds = new ushort[16];
        ushort[] outputTriangleWords = new ushort[16];
        uint recordReservationCount = 2;
        uint triangleWordCount = 5;
        uint acceptedIdCount = 8;

        uint reserved = EvaluateWoWSelectorAcceptedListConsumerRecordWrite(
            globalFlags: 0x00200000u,
            inputQueueFlags: 0x10u,
            inputConsumerFlags: 0x200u,
            ownerContextToken: 0x5000u,
            vertexStreamToken: 0x7100u,
            metadataToken: 0x7200u,
            outputTriangleWordBaseToken: 0xC63298u,
            outputAcceptedIdBaseToken: 0xC5A508u,
            ref recordReservationCount,
            ref triangleWordCount,
            ref acceptedIdCount,
            pendingIds,
            pendingIds.Length,
            acceptedIds,
            acceptedIds.Length,
            triangleVertexIndices,
            triangleVertexIndices.Length,
            outputAcceptedIds,
            outputAcceptedIds.Length,
            outputTriangleWords,
            outputTriangleWords.Length,
            out SelectorAcceptedListConsumerRecordSlotTrace recordTrace,
            out SelectorAcceptedListConsumerTrace trace);

        Assert.Equal(1u, reserved);
        Assert.Equal(1u, recordTrace.RecordReserved);
        Assert.Equal(8u, recordTrace.ZeroInitializedDwordCount);
        Assert.Equal(2u, recordTrace.RecordIndex);
        Assert.Equal(0x5094u, recordTrace.OwnerPayloadToken);
        Assert.Equal(0x7100u, recordTrace.VertexStreamToken);
        Assert.Equal(0x7200u, recordTrace.MetadataToken);
        Assert.Equal(0xC632A2u, recordTrace.TriangleWordBufferToken);
        Assert.Equal(0xC5A518u, recordTrace.AcceptedIdBufferToken);
        Assert.Equal(0x5000u, recordTrace.OwnerContextToken);
        Assert.Equal((ushort)6, recordTrace.TriangleWordCountField);
        Assert.Equal((ushort)2, recordTrace.AcceptedIdCountField);
        Assert.Equal((ushort)20, recordTrace.MinTriangleVertexIndex);
        Assert.Equal((ushort)42, recordTrace.MaxTriangleVertexIndex);
        Assert.Equal(1u, trace.RecordSlotReserved);
        Assert.Equal((uint)3, recordReservationCount);
        Assert.Equal((uint)11, triangleWordCount);
        Assert.Equal((uint)10, acceptedIdCount);
    }

    [Fact]
    public void EvaluateWoWSelectorAcceptedListConsumerRecordWrite_AcceptedIdOverflowLeavesAcceptedPointerNull()
    {
        ushort[] acceptedIds = { 2 };
        ushort[] triangleVertexIndices = { 1, 2, 3, 7, 8, 9, 13, 14, 15 };
        ushort[] outputAcceptedIds = new ushort[1];
        ushort[] outputTriangleWords = new ushort[3];
        uint recordReservationCount = 0;
        uint triangleWordCount = 2;
        uint acceptedIdCount = 0x3FFFu;

        uint reserved = EvaluateWoWSelectorAcceptedListConsumerRecordWrite(
            globalFlags: 0u,
            inputQueueFlags: 0x08u,
            inputConsumerFlags: 0x02u,
            ownerContextToken: 0x6000u,
            vertexStreamToken: 0x7300u,
            metadataToken: 0x7400u,
            outputTriangleWordBaseToken: 0xC63298u,
            outputAcceptedIdBaseToken: 0xC5A508u,
            ref recordReservationCount,
            ref triangleWordCount,
            ref acceptedIdCount,
            pendingIds: Array.Empty<ushort>(),
            pendingCount: 0,
            acceptedIds,
            acceptedIds.Length,
            triangleVertexIndices,
            triangleVertexIndices.Length,
            outputAcceptedIds,
            outputAcceptedIds.Length,
            outputTriangleWords,
            outputTriangleWords.Length,
            out SelectorAcceptedListConsumerRecordSlotTrace recordTrace,
            out SelectorAcceptedListConsumerTrace trace);

        Assert.Equal(1u, reserved);
        Assert.Equal(1u, recordTrace.RecordReserved);
        Assert.Equal(0u, recordTrace.AcceptedIdBufferToken);
        Assert.Equal((ushort)0, recordTrace.AcceptedIdCountField);
        Assert.Equal(0xC6329Cu, recordTrace.TriangleWordBufferToken);
        Assert.Equal((ushort)3, recordTrace.TriangleWordCountField);
        Assert.Equal((ushort)13, recordTrace.MinTriangleVertexIndex);
        Assert.Equal((ushort)15, recordTrace.MaxTriangleVertexIndex);
        Assert.Equal(1u, trace.AcceptedIdOverflowFlagSet);
        Assert.Equal(0u, trace.AcceptedIdSpanReserved);
        Assert.Equal(1u, trace.TriangleWordSpanReserved);
    }

    [Fact]
    public void EvaluateWoWSelectorAcceptedListConsumerRecordWrite_TriangleWordOverflowLeavesTrianglePointerNullButStillWritesAcceptedBuffer()
    {
        ushort[] acceptedIds = { 1 };
        ushort[] triangleVertexIndices = { 3, 4, 5, 6, 7, 8 };
        ushort[] outputAcceptedIds = new ushort[1];
        ushort[] outputTriangleWords = new ushort[0xC000];
        uint recordReservationCount = 0;
        uint triangleWordCount = 0xBFFDu;
        uint acceptedIdCount = 0;

        uint reserved = EvaluateWoWSelectorAcceptedListConsumerRecordWrite(
            globalFlags: 0u,
            inputQueueFlags: 0x10u,
            inputConsumerFlags: 0x04u,
            ownerContextToken: 0x7000u,
            vertexStreamToken: 0x7500u,
            metadataToken: 0x7600u,
            outputTriangleWordBaseToken: 0xC63298u,
            outputAcceptedIdBaseToken: 0xC5A508u,
            ref recordReservationCount,
            ref triangleWordCount,
            ref acceptedIdCount,
            pendingIds: Array.Empty<ushort>(),
            pendingCount: 0,
            acceptedIds,
            acceptedIds.Length,
            triangleVertexIndices,
            triangleVertexIndices.Length,
            outputAcceptedIds,
            outputAcceptedIds.Length,
            outputTriangleWords,
            outputTriangleWords.Length,
            out SelectorAcceptedListConsumerRecordSlotTrace recordTrace,
            out SelectorAcceptedListConsumerTrace trace);

        Assert.Equal(1u, reserved);
        Assert.Equal(1u, recordTrace.RecordReserved);
        Assert.Equal(0u, recordTrace.TriangleWordBufferToken);
        Assert.Equal((ushort)0, recordTrace.TriangleWordCountField);
        Assert.Equal((ushort)0xFFFF, recordTrace.MinTriangleVertexIndex);
        Assert.Equal((ushort)0, recordTrace.MaxTriangleVertexIndex);
        Assert.Equal(0xC5A508u, recordTrace.AcceptedIdBufferToken);
        Assert.Equal((ushort)1, recordTrace.AcceptedIdCountField);
        Assert.Equal(1u, trace.TriangleWordOverflowFlagSet);
        Assert.Equal(0u, trace.TriangleWordSpanReserved);
        Assert.Equal(1u, trace.AcceptedIdSpanReserved);
    }
}

[Collection("PhysicsEngine")]
public class WowSelectorAcceptedListConsumerPreprocessTests
{
    [Fact]
    public void EvaluateWoWSelectorAcceptedListConsumerPreprocessIteration_PendingLoopUsesRedDebugColorAndPendingLocalSlotOrder()
    {
        ushort[] pendingIds = { 1, 2 };
        ushort[] triangleVertexIndices = { 10, 11, 12, 20, 21, 22, 30, 31, 32 };

        uint result = EvaluateWoWSelectorAcceptedListConsumerPreprocessIteration(
            sourceKind: 0u,
            ownerContextToken: 0x5000u,
            vertexStreamToken: 0x7000u,
            sourceTriangleIds: pendingIds,
            sourceCount: pendingIds.Length,
            sourceIndex: 1,
            triangleVertexIndices: triangleVertexIndices,
            triangleVertexIndexCount: triangleVertexIndices.Length,
            out SelectorAcceptedListConsumerPreprocessTrace trace);

        Assert.Equal(1u, result);
        Assert.Equal(1u, trace.Executed);
        Assert.Equal(0u, trace.SourceKind);
        Assert.Equal(2u, trace.SourceTriangleIndex);
        Assert.Equal(6u, trace.SourceTriangleWordBase);
        Assert.Equal(0x7FFF0000u, trace.DebugColorToken);
        Assert.Equal(0x5094u, trace.OwnerPayloadToken);
        Assert.Equal(1u, trace.Helper6acdd0CallCount);
        Assert.Equal(3u, trace.Helper7bca80CallCount);
        Assert.Equal(1u, trace.Helper6bce50CallCount);
        Assert.Equal(1u, trace.Helper6a98e0CallCount);
        Assert.Equal(1u, trace.NormalizeHelperZeroArg);
        Assert.Equal(new ushort[] { 32, 31, 30 }, trace.SupportVertexIndices);
        Assert.Equal(new uint[] { 0x7180u, 0x7174u, 0x7168u }, trace.SupportVertexTokens);
        Assert.Equal(new uint[] { 0x1Cu, 0x28u, 0x34u }, trace.LocalSlotOffsets);
    }

    [Fact]
    public void EvaluateWoWSelectorAcceptedListConsumerPreprocessIteration_AcceptedLoopUsesGreenDebugColorAndAcceptedLocalSlotOrder()
    {
        ushort[] acceptedIds = { 0, 1 };
        ushort[] triangleVertexIndices = { 4, 5, 6, 14, 15, 16 };

        uint result = EvaluateWoWSelectorAcceptedListConsumerPreprocessIteration(
            sourceKind: 1u,
            ownerContextToken: 0x6000u,
            vertexStreamToken: 0x7200u,
            sourceTriangleIds: acceptedIds,
            sourceCount: acceptedIds.Length,
            sourceIndex: 1,
            triangleVertexIndices: triangleVertexIndices,
            triangleVertexIndexCount: triangleVertexIndices.Length,
            out SelectorAcceptedListConsumerPreprocessTrace trace);

        Assert.Equal(1u, result);
        Assert.Equal(1u, trace.Executed);
        Assert.Equal(1u, trace.SourceKind);
        Assert.Equal(1u, trace.SourceTriangleIndex);
        Assert.Equal(3u, trace.SourceTriangleWordBase);
        Assert.Equal(0x7F00FF00u, trace.DebugColorToken);
        Assert.Equal(0x6094u, trace.OwnerPayloadToken);
        Assert.Equal(new ushort[] { 16, 15, 14 }, trace.SupportVertexIndices);
        Assert.Equal(new uint[] { 0x72C0u, 0x72B4u, 0x72A8u }, trace.SupportVertexTokens);
        Assert.Equal(new uint[] { 0x34u, 0x28u, 0x1Cu }, trace.LocalSlotOffsets);
    }

    [Fact]
    public void EvaluateWoWSelectorAcceptedListConsumerPreprocessIteration_OutOfRangeSourceIndexReturnsZeroWithoutHelperCalls()
    {
        ushort[] acceptedIds = { 3 };
        ushort[] triangleVertexIndices = { 1, 2, 3 };

        uint result = EvaluateWoWSelectorAcceptedListConsumerPreprocessIteration(
            sourceKind: 1u,
            ownerContextToken: 0x4000u,
            vertexStreamToken: 0x7100u,
            sourceTriangleIds: acceptedIds,
            sourceCount: acceptedIds.Length,
            sourceIndex: 5,
            triangleVertexIndices: triangleVertexIndices,
            triangleVertexIndexCount: triangleVertexIndices.Length,
            out SelectorAcceptedListConsumerPreprocessTrace trace);

        Assert.Equal(0u, result);
        Assert.Equal(0u, trace.Executed);
        Assert.Equal(1u, trace.SourceKind);
        Assert.Equal(0x7F00FF00u, trace.DebugColorToken);
        Assert.Equal(0x4094u, trace.OwnerPayloadToken);
        Assert.Equal(0u, trace.Helper6acdd0CallCount);
        Assert.Equal(0u, trace.Helper7bca80CallCount);
        Assert.Equal(0u, trace.Helper6bce50CallCount);
        Assert.Equal(0u, trace.Helper6a98e0CallCount);
        Assert.Equal(new ushort[] { 0, 0, 0 }, trace.SupportVertexIndices);
        Assert.Equal(new uint[] { 0x34u, 0x28u, 0x1Cu }, trace.LocalSlotOffsets);
    }

    [Fact]
    public void EvaluateWoWSelectorAcceptedListConsumerPreprocessLoop_PendingGateDisabledSkipsAllIterations()
    {
        ushort[] pendingIds = { 1, 2 };
        ushort[] triangleVertexIndices = { 10, 11, 12, 20, 21, 22, 30, 31, 32 };
        SelectorAcceptedListConsumerPreprocessTrace[] iterationTraces = new SelectorAcceptedListConsumerPreprocessTrace[2];

        uint result = EvaluateWoWSelectorAcceptedListConsumerPreprocessLoop(
            sourceKind: 0u,
            preprocessEnabled: false,
            ownerContextToken: 0x5000u,
            vertexStreamToken: 0x7000u,
            sourceTriangleIds: pendingIds,
            sourceCount: pendingIds.Length,
            triangleVertexIndices: triangleVertexIndices,
            triangleVertexIndexCount: triangleVertexIndices.Length,
            iterationTraces,
            maxIterationTraces: iterationTraces.Length,
            out SelectorAcceptedListConsumerPreprocessLoopTrace trace);

        Assert.Equal(0u, result);
        Assert.Equal(0u, trace.PreprocessEnabled);
        Assert.Equal(0u, trace.ExecutedIterationCount);
        Assert.Equal(0u, trace.StoredIterationCount);
        Assert.Equal(0u, trace.Helper6acdd0CallCount);
        Assert.Equal(0u, trace.Helper7bca80CallCount);
        Assert.Equal(0u, trace.Helper6bce50CallCount);
        Assert.Equal(0u, trace.Helper6a98e0CallCount);
        Assert.Equal(0u, iterationTraces[0].Executed);
        Assert.Equal(0u, iterationTraces[1].Executed);
    }

    [Fact]
    public void EvaluateWoWSelectorAcceptedListConsumerPreprocessLoop_AggregatesPendingIterationCountsAndCopiesEachTrace()
    {
        ushort[] pendingIds = { 1, 2 };
        ushort[] triangleVertexIndices = { 10, 11, 12, 20, 21, 22, 30, 31, 32 };
        SelectorAcceptedListConsumerPreprocessTrace[] iterationTraces = new SelectorAcceptedListConsumerPreprocessTrace[2];

        uint result = EvaluateWoWSelectorAcceptedListConsumerPreprocessLoop(
            sourceKind: 0u,
            preprocessEnabled: true,
            ownerContextToken: 0x5000u,
            vertexStreamToken: 0x7000u,
            sourceTriangleIds: pendingIds,
            sourceCount: pendingIds.Length,
            triangleVertexIndices: triangleVertexIndices,
            triangleVertexIndexCount: triangleVertexIndices.Length,
            iterationTraces,
            maxIterationTraces: iterationTraces.Length,
            out SelectorAcceptedListConsumerPreprocessLoopTrace trace);

        Assert.Equal(2u, result);
        Assert.Equal(1u, trace.PreprocessEnabled);
        Assert.Equal(0u, trace.SourceKind);
        Assert.Equal(0x7FFF0000u, trace.DebugColorToken);
        Assert.Equal(0x5094u, trace.OwnerPayloadToken);
        Assert.Equal(2u, trace.SourceCount);
        Assert.Equal(2u, trace.ExecutedIterationCount);
        Assert.Equal(2u, trace.StoredIterationCount);
        Assert.Equal(2u, trace.Helper6acdd0CallCount);
        Assert.Equal(6u, trace.Helper7bca80CallCount);
        Assert.Equal(2u, trace.Helper6bce50CallCount);
        Assert.Equal(2u, trace.Helper6a98e0CallCount);

        Assert.Equal(1u, iterationTraces[0].Executed);
        Assert.Equal(1u, iterationTraces[0].SourceTriangleIndex);
        Assert.Equal(new ushort[] { 22, 21, 20 }, iterationTraces[0].SupportVertexIndices);
        Assert.Equal(1u, iterationTraces[1].Executed);
        Assert.Equal(2u, iterationTraces[1].SourceTriangleIndex);
        Assert.Equal(new ushort[] { 32, 31, 30 }, iterationTraces[1].SupportVertexIndices);
    }

    [Fact]
    public void EvaluateWoWSelectorAcceptedListConsumerPreprocessLoop_TruncatesStoredIterationTraceBufferButProcessesFullAcceptedLoop()
    {
        ushort[] acceptedIds = { 0, 1 };
        ushort[] triangleVertexIndices = { 4, 5, 6, 14, 15, 16 };
        SelectorAcceptedListConsumerPreprocessTrace[] iterationTraces = new SelectorAcceptedListConsumerPreprocessTrace[1];

        uint result = EvaluateWoWSelectorAcceptedListConsumerPreprocessLoop(
            sourceKind: 1u,
            preprocessEnabled: true,
            ownerContextToken: 0x6000u,
            vertexStreamToken: 0x7200u,
            sourceTriangleIds: acceptedIds,
            sourceCount: acceptedIds.Length,
            triangleVertexIndices: triangleVertexIndices,
            triangleVertexIndexCount: triangleVertexIndices.Length,
            iterationTraces,
            maxIterationTraces: iterationTraces.Length,
            out SelectorAcceptedListConsumerPreprocessLoopTrace trace);

        Assert.Equal(2u, result);
        Assert.Equal(1u, trace.SourceKind);
        Assert.Equal(0x7F00FF00u, trace.DebugColorToken);
        Assert.Equal(0x6094u, trace.OwnerPayloadToken);
        Assert.Equal(2u, trace.SourceCount);
        Assert.Equal(2u, trace.ExecutedIterationCount);
        Assert.Equal(1u, trace.StoredIterationCount);
        Assert.Equal(2u, trace.Helper6acdd0CallCount);
        Assert.Equal(6u, trace.Helper7bca80CallCount);
        Assert.Equal(2u, trace.Helper6bce50CallCount);
        Assert.Equal(2u, trace.Helper6a98e0CallCount);
        Assert.Equal(1u, iterationTraces[0].Executed);
        Assert.Equal(0u, iterationTraces[0].SourceTriangleIndex);
        Assert.Equal(new ushort[] { 6, 5, 4 }, iterationTraces[0].SupportVertexIndices);
    }
}
