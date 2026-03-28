using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorObjectRasterConsumerPrepassTests
{
    [Fact]
    public void EvaluateWoWSelectorObjectRasterPrepassOutcodeLoop_WritesFourOutcodesForSingleCellWindow()
    {
        SelectorObjectRasterPrefixTrace prefix = BuildAcceptedPrefix();
        Vector3[] pointGrid =
        [
            new(0.0f, 0.0f, 0.0f), new(1.0f, 0.0f, 0.0f), new(2.0f, 0.0f, 0.0f),
            new(0.0f, 1.0f, 0.0f), new(1.0f, 1.0f, 0.0f), new(2.0f, 1.0f, 0.0f),
            new(0.0f, 2.0f, 0.0f), new(1.0f, 2.0f, 0.0f), new(2.0f, 2.0f, 0.0f),
        ];
        uint[] pointOutcodes = new uint[4];

        uint result = EvaluateWoWSelectorObjectRasterPrepassOutcodeLoop(
            prefix,
            pointGridRowStride: 3,
            objectTranslation: new Vector3(1.0f, 1.0f, 0.0f),
            sourcePayload: CreatePayload(),
            pointGrid,
            pointGrid.Length,
            pointOutcodes,
            pointOutcodes.Length,
            out SelectorObjectRasterPrepassTrace trace);

        Assert.Equal(4u, result);
        Assert.Equal(4u, trace.PointWriteCount);
        Assert.Equal(4u, trace.OutputWriteCount);
        Assert.Equal(0u, trace.PointIndexOutOfRangeCount);
        Assert.Equal(0u, trace.FirstPointIndex);
        Assert.Equal(4u, trace.LastPointIndex);
        Assert.Equal(new uint[] { 0u, 0u, 0u, 0u }, pointOutcodes);
    }

    [Fact]
    public void EvaluateWoWSelectorObjectRasterPrepassOutcodeLoop_ZeroesOutcodeWhenPointIndexFallsPastGrid()
    {
        SelectorObjectRasterPrefixTrace prefix = BuildAcceptedPrefix();
        Vector3[] pointGrid =
        [
            new(0.0f, 0.0f, 0.0f), new(1.0f, 0.0f, 0.0f), new(2.0f, 0.0f, 0.0f),
            new(0.0f, 1.0f, 0.0f),
        ];
        uint[] pointOutcodes = new uint[4];

        uint result = EvaluateWoWSelectorObjectRasterPrepassOutcodeLoop(
            prefix,
            pointGridRowStride: 3,
            objectTranslation: new Vector3(1.0f, 1.0f, 0.0f),
            sourcePayload: CreatePayload(),
            pointGrid,
            pointGrid.Length,
            pointOutcodes,
            pointOutcodes.Length,
            out SelectorObjectRasterPrepassTrace trace);

        Assert.Equal(4u, result);
        Assert.Equal(4u, trace.PointWriteCount);
        Assert.Equal(4u, trace.OutputWriteCount);
        Assert.Equal(1u, trace.PointIndexOutOfRangeCount);
        Assert.Equal(4u, trace.LastPointIndex);
        Assert.Equal(0u, pointOutcodes[3]);
    }

    [Fact]
    public void EvaluateWoWSelectorObjectRasterPrepassOutcodeLoop_TruncatesWritesWhenOutputCapacityIsSmallerThanLoopCount()
    {
        SelectorObjectRasterPrefixTrace prefix = BuildAcceptedPrefix();
        Vector3[] pointGrid =
        [
            new(0.0f, 0.0f, 0.0f), new(1.0f, 0.0f, 0.0f), new(2.0f, 0.0f, 0.0f),
            new(0.0f, 1.0f, 0.0f), new(1.0f, 1.0f, 0.0f), new(2.0f, 1.0f, 0.0f),
            new(0.0f, 2.0f, 0.0f), new(1.0f, 2.0f, 0.0f), new(2.0f, 2.0f, 0.0f),
        ];
        uint[] pointOutcodes = [111u, 222u, 333u, 444u];

        uint result = EvaluateWoWSelectorObjectRasterPrepassOutcodeLoop(
            prefix,
            pointGridRowStride: 3,
            objectTranslation: new Vector3(1.0f, 1.0f, 0.0f),
            sourcePayload: CreatePayload(),
            pointGrid,
            pointGrid.Length,
            pointOutcodes,
            pointOutcodeCapacity: 2,
            out SelectorObjectRasterPrepassTrace trace);

        Assert.Equal(4u, result);
        Assert.Equal(4u, trace.PointWriteCount);
        Assert.Equal(2u, trace.OutputWriteCount);
        Assert.Equal(333u, pointOutcodes[2]);
        Assert.Equal(444u, pointOutcodes[3]);
    }

    [Fact]
    public void EvaluateWoWSelectorObjectRasterPrepassOutcodeLoop_AllowsNullOutputBufferWhileStillTracingLoop()
    {
        SelectorObjectRasterPrefixTrace prefix = BuildAcceptedPrefix();
        Vector3[] pointGrid =
        [
            new(0.0f, 0.0f, 0.0f), new(1.0f, 0.0f, 0.0f), new(2.0f, 0.0f, 0.0f),
            new(0.0f, 1.0f, 0.0f), new(1.0f, 1.0f, 0.0f), new(2.0f, 1.0f, 0.0f),
            new(0.0f, 2.0f, 0.0f), new(1.0f, 2.0f, 0.0f), new(2.0f, 2.0f, 0.0f),
        ];

        uint result = EvaluateWoWSelectorObjectRasterPrepassOutcodeLoop(
            prefix,
            pointGridRowStride: 3,
            objectTranslation: new Vector3(1.0f, 1.0f, 0.0f),
            sourcePayload: CreatePayload(),
            pointGrid,
            pointGrid.Length,
            pointOutcodes: null,
            pointOutcodeCapacity: 0,
            out SelectorObjectRasterPrepassTrace trace);

        Assert.Equal(4u, result);
        Assert.Equal(4u, trace.PointWriteCount);
        Assert.Equal(0u, trace.OutputWriteCount);
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
        Assert.Equal(2u, prefix.PrepassPointCountX);
        Assert.Equal(2u, prefix.PrepassPointCountY);
        return prefix;
    }

    private static SelectorObjectRasterPayload CreatePayload(
        SelectorSupportPlane[]? planes = null,
        Vector3[]? supportPoints = null)
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
            SupportPoints = supportPoints ??
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
