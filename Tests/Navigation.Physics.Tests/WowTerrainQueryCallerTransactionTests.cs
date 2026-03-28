using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowTerrainQueryCallerTransactionTests
{
    [Fact]
    public void EvaluateWoWAabbOverlapInclusive_TreatsSharedFacesAsIntersecting()
    {
        Vector3 boundsMinA = new(1.0f, 2.0f, 3.0f);
        Vector3 boundsMaxA = new(4.0f, 5.0f, 6.0f);
        Vector3 boundsMinB = new(4.0f, 1.0f, 0.0f);
        Vector3 boundsMaxB = new(7.0f, 4.0f, 8.0f);

        Assert.True(EvaluateWoWAabbOverlapInclusive(boundsMinA, boundsMaxA, boundsMinB, boundsMaxB));
    }

    [Fact]
    public void EvaluateWoWAabbOverlapInclusive_SeparatesGapAlongAnyAxis()
    {
        Vector3 boundsMinA = new(-2.0f, -2.0f, -2.0f);
        Vector3 boundsMaxA = new(-1.0f, -1.0f, -1.0f);
        Vector3 boundsMinB = new(-0.99f, -3.0f, -3.0f);
        Vector3 boundsMaxB = new(1.0f, 1.0f, 1.0f);

        Assert.False(EvaluateWoWAabbOverlapInclusive(boundsMinA, boundsMaxA, boundsMinB, boundsMaxB));
    }

    [Fact]
    public void EvaluateWoWTerrainQueryPayloadEnabled_ZeroPayloadUsesLowMovementBits()
    {
        TerrainQueryPairPayload payload = new() { First = 0.0f, Second = 0.0f };

        Assert.True(EvaluateWoWTerrainQueryPayloadEnabled(0x00000004u, payload));
        Assert.False(EvaluateWoWTerrainQueryPayloadEnabled(0x00100000u, payload));
    }

    [Fact]
    public void EvaluateWoWTerrainQueryPayloadEnabled_NegativeZeroStillUsesHighMovementBits()
    {
        TerrainQueryPairPayload payload = new()
        {
            First = BitConverter.Int32BitsToSingle(unchecked((int)0x80000000u)),
            Second = 0.0f,
        };

        Assert.True(EvaluateWoWTerrainQueryPayloadEnabled(0x00100000u, payload));
        Assert.False(EvaluateWoWTerrainQueryPayloadEnabled(0x00000001u, payload));
    }

    [Fact]
    public void EvaluateWoWShouldRunDynamicCallbackProducer_RequiresCallbackAndHighMovementBits()
    {
        Assert.True(EvaluateWoWShouldRunDynamicCallbackProducer(callbackPresent: true, movementFlags: 0x00100000u));
        Assert.False(EvaluateWoWShouldRunDynamicCallbackProducer(callbackPresent: false, movementFlags: 0x00100000u));
        Assert.False(EvaluateWoWShouldRunDynamicCallbackProducer(callbackPresent: true, movementFlags: 0x00000001u));
    }

    [Fact]
    public void EvaluateWoWShouldVisitTerrainQueryStampedEntry_RejectsMatchingVisitStamp()
    {
        Assert.False(EvaluateWoWShouldVisitTerrainQueryStampedEntry(entryVisitStamp: 42u, currentVisitStamp: 42u));
        Assert.True(EvaluateWoWShouldVisitTerrainQueryStampedEntry(entryVisitStamp: 41u, currentVisitStamp: 42u));
    }

    [Fact]
    public void BeginWoWTerrainQueryProducerPass_AdvancesStampAndClearsRecordCount()
    {
        uint clearedCount = BeginWoWTerrainQueryProducerPass(
            currentVisitStamp: 9u,
            inputRecordCount: 3,
            out uint nextVisitStamp);

        Assert.Equal(10u, nextVisitStamp);
        Assert.Equal(0u, clearedCount);
    }

    [Fact]
    public void BuildWoWTerrainQueryChunkSpan_QuantizesWorldBoundsToInclusiveCellAndChunkExtrema()
    {
        Assert.True(BuildWoWTerrainQueryChunkSpan(
            worldBoundsMin: new Vector3(17000.0f, 16920.0f, 0.0f),
            worldBoundsMax: new Vector3(17060.0f, 17040.0f, 0.0f),
            out TerrainQueryChunkSpan span));

        Assert.Equal(1, span.CellMinX);
        Assert.Equal(15, span.CellMaxX);
        Assert.Equal(5, span.CellMinY);
        Assert.Equal(34, span.CellMaxY);
        Assert.Equal(0, span.ChunkMinX);
        Assert.Equal(1, span.ChunkMaxX);
        Assert.Equal(0, span.ChunkMinY);
        Assert.Equal(4, span.ChunkMaxY);
    }

    [Fact]
    public void EnumerateWoWTerrainQueryChunkCoordinates_UsesInclusivePrimaryThenSecondaryOrder()
    {
        TerrainQueryChunkSpan span = new()
        {
            ChunkMinX = 0,
            ChunkMaxX = 1,
            ChunkMinY = 2,
            ChunkMaxY = 3,
        };
        TerrainQueryChunkCoordinate[] coordinates = new TerrainQueryChunkCoordinate[4];

        int count = EnumerateWoWTerrainQueryChunkCoordinates(
            span,
            coordinates,
            coordinates.Length);

        Assert.Equal(4, count);
        AssertCoordinate(0, 2, coordinates[0]);
        AssertCoordinate(0, 3, coordinates[1]);
        AssertCoordinate(1, 2, coordinates[2]);
        AssertCoordinate(1, 3, coordinates[3]);
    }

    [Fact]
    public void BuildWoWOptionalSelectorChildDispatchMask_UsesChildPresenceAndHighBits()
    {
        uint[] childPresenceFlags = [1u, 0u, 3u, 1u];

        uint dispatchMask = BuildWoWOptionalSelectorChildDispatchMask(
            childPresenceFlags,
            childPresenceFlags.Length,
            0x00010000u | 0x00020000u | 0x00040000u);

        Assert.Equal(0x00050000u, dispatchMask);
    }

    [Fact]
    public void EvaluateWoWTerrainQueryEntryDispatch_PayloadDisabledSkipsBeforeTraversalAbort()
    {
        uint action = EvaluateWoWTerrainQueryEntryDispatch(
            entryFlagMaskedOut: false,
            alreadyVisited: false,
            hasSourceGeometry: true,
            movementFlags: 0x00000000u,
            payload: new TerrainQueryPairPayload { First = 0.0f, Second = 0.0f },
            traversalAllowsDispatch: false,
            entryBoundsMin: new Vector3(0.0f, 0.0f, 0.0f),
            entryBoundsMax: new Vector3(10.0f, 10.0f, 10.0f),
            queryBoundsMin: new Vector3(2.0f, 2.0f, 2.0f),
            queryBoundsMax: new Vector3(4.0f, 4.0f, 4.0f));

        Assert.Equal(0u, action);
    }

    [Fact]
    public void EvaluateWoWTerrainQueryEntryDispatch_RelevantEntryWithoutTraversalPermissionAborts()
    {
        uint action = EvaluateWoWTerrainQueryEntryDispatch(
            entryFlagMaskedOut: false,
            alreadyVisited: false,
            hasSourceGeometry: true,
            movementFlags: 0x00000001u,
            payload: new TerrainQueryPairPayload { First = 0.0f, Second = 0.0f },
            traversalAllowsDispatch: false,
            entryBoundsMin: new Vector3(0.0f, 0.0f, 0.0f),
            entryBoundsMax: new Vector3(10.0f, 10.0f, 10.0f),
            queryBoundsMin: new Vector3(2.0f, 2.0f, 2.0f),
            queryBoundsMax: new Vector3(4.0f, 4.0f, 4.0f));

        Assert.Equal(2u, action);
    }

    [Fact]
    public void EvaluateWoWTerrainQueryEntryDispatch_OverlappingRelevantEntryDispatches()
    {
        uint action = EvaluateWoWTerrainQueryEntryDispatch(
            entryFlagMaskedOut: false,
            alreadyVisited: false,
            hasSourceGeometry: true,
            movementFlags: 0x00100000u,
            payload: new TerrainQueryPairPayload { First = 5.0f, Second = 6.0f },
            traversalAllowsDispatch: true,
            entryBoundsMin: new Vector3(-1.0f, -1.0f, -1.0f),
            entryBoundsMax: new Vector3(3.0f, 3.0f, 3.0f),
            queryBoundsMin: new Vector3(3.0f, 0.0f, 0.0f),
            queryBoundsMax: new Vector3(6.0f, 6.0f, 6.0f));

        Assert.Equal(1u, action);
    }

    [Fact]
    public void EvaluateWoWDynamicTerrainQueryEntryDispatch_RequiresFlagCallbackAndOverlap()
    {
        Assert.True(EvaluateWoWDynamicTerrainQueryEntryDispatch(
            entryFlagEnabled: true,
            alreadyVisited: false,
            callbackSucceeded: true,
            entryBoundsMin: new Vector3(0.0f, 0.0f, 0.0f),
            entryBoundsMax: new Vector3(1.0f, 1.0f, 1.0f),
            queryBoundsMin: new Vector3(1.0f, -2.0f, -2.0f),
            queryBoundsMax: new Vector3(2.0f, 2.0f, 2.0f)));

        Assert.False(EvaluateWoWDynamicTerrainQueryEntryDispatch(
            entryFlagEnabled: true,
            alreadyVisited: true,
            callbackSucceeded: true,
            entryBoundsMin: new Vector3(0.0f, 0.0f, 0.0f),
            entryBoundsMax: new Vector3(1.0f, 1.0f, 1.0f),
            queryBoundsMin: new Vector3(1.0f, -2.0f, -2.0f),
            queryBoundsMax: new Vector3(2.0f, 2.0f, 2.0f)));
    }

    [Fact]
    public void ZeroWoWTerrainQueryPairPayloadRange_PreservesExistingPairsAndZeroFillsGrowth()
    {
        TerrainQueryPairPayload[] inputPairs =
        [
            new TerrainQueryPairPayload { First = 1.0f, Second = 2.0f },
            new TerrainQueryPairPayload { First = 3.0f, Second = 4.0f },
        ];
        TerrainQueryPairPayload[] outputPairs = new TerrainQueryPairPayload[5];

        int count = ZeroWoWTerrainQueryPairPayloadRange(
            inputPairs,
            inputPairs.Length,
            previousRecordCount: 2u,
            currentRecordCount: 5u,
            outputPairs,
            outputPairs.Length);

        Assert.Equal(5, count);
        AssertPair(1.0f, 2.0f, outputPairs[0]);
        AssertPair(3.0f, 4.0f, outputPairs[1]);
        AssertPair(0.0f, 0.0f, outputPairs[2]);
        AssertPair(0.0f, 0.0f, outputPairs[3]);
        AssertPair(0.0f, 0.0f, outputPairs[4]);
    }

    private static void AssertPair(float first, float second, TerrainQueryPairPayload actual)
    {
        Assert.Equal(first, actual.First, 6);
        Assert.Equal(second, actual.Second, 6);
    }

    private static void AssertCoordinate(int primary, int secondary, TerrainQueryChunkCoordinate actual)
    {
        Assert.Equal(primary, actual.Primary);
        Assert.Equal(secondary, actual.Secondary);
    }
}
