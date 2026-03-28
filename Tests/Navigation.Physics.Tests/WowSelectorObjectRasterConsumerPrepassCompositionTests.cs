using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorObjectRasterConsumerPrepassCompositionTests
{
    [Fact]
    public void EvaluateWoWSelectorObjectRasterPrepassComposition_ComposesTranslatedPayloadAndPrepassLoop()
    {
        SelectorObjectRasterPrefixTrace prefix = BuildAcceptedPrefix();
        SelectorObjectRasterPayload payload = CreatePayload();
        Vector3[] pointGrid =
        [
            new(0.0f, 0.0f, 0.0f), new(1.0f, 0.0f, 0.0f), new(2.0f, 0.0f, 0.0f),
            new(0.0f, 1.0f, 0.0f), new(1.0f, 1.0f, 0.0f), new(2.0f, 1.0f, 0.0f),
            new(0.0f, 2.0f, 0.0f), new(1.0f, 2.0f, 0.0f), new(2.0f, 2.0f, 0.0f),
        ];
        uint[] pointOutcodes = new uint[4];

        uint result = EvaluateWoWSelectorObjectRasterPrepassComposition(
            prefix,
            payload,
            pointGrid,
            pointGrid.Length,
            pointOutcodes,
            pointOutcodes.Length,
            out SelectorObjectRasterPrepassCompositionTrace trace);

        Assert.Equal(4u, result);
        Assert.Equal(4u, trace.Prepass.PointWriteCount);
        Assert.Equal(4u, trace.Prepass.OutputWriteCount);
        Assert.Equal(0u, trace.Prepass.PointIndexOutOfRangeCount);
        Assert.Equal(prefix.TranslatedAnchorPoint0.ToString(), trace.TranslatedAnchorPoint0.ToString());
        Assert.Equal(prefix.TranslatedAnchorPoint1.ToString(), trace.TranslatedAnchorPoint1.ToString());
        Assert.Equal(prefix.TranslatedFirstPlaneDistance, trace.TranslatedFirstPlaneDistance, 6);
        Assert.Equal(new uint[] { 0u, 0u, 0u, 0u }, pointOutcodes);
    }

    [Fact]
    public void EvaluateWoWSelectorObjectRasterPrepassComposition_MatchesInnerPrepassLoopWhenPointIndexFallsPastGrid()
    {
        SelectorObjectRasterPrefixTrace prefix = BuildAcceptedPrefix();
        SelectorObjectRasterPayload payload = CreatePayload();
        Vector3[] pointGrid =
        [
            new(0.0f, 0.0f, 0.0f), new(1.0f, 0.0f, 0.0f), new(2.0f, 0.0f, 0.0f),
            new(0.0f, 1.0f, 0.0f),
        ];
        uint[] compositionOutcodes = new uint[4];
        uint[] directOutcodes = new uint[4];

        uint composed = EvaluateWoWSelectorObjectRasterPrepassComposition(
            prefix,
            payload,
            pointGrid,
            pointGrid.Length,
            compositionOutcodes,
            compositionOutcodes.Length,
            out SelectorObjectRasterPrepassCompositionTrace compositionTrace);
        uint direct = EvaluateWoWSelectorObjectRasterPrepassOutcodeLoop(
            prefix,
            pointGridRowStride: 3,
            objectTranslation: new Vector3(1.0f, 1.0f, 0.0f),
            sourcePayload: payload,
            pointGrid,
            pointGrid.Length,
            directOutcodes,
            directOutcodes.Length,
            out SelectorObjectRasterPrepassTrace directTrace);

        Assert.Equal(direct, composed);
        Assert.Equal(directTrace.PointWriteCount, compositionTrace.Prepass.PointWriteCount);
        Assert.Equal(directTrace.OutputWriteCount, compositionTrace.Prepass.OutputWriteCount);
        Assert.Equal(directTrace.PointIndexOutOfRangeCount, compositionTrace.Prepass.PointIndexOutOfRangeCount);
        Assert.Equal(directOutcodes, compositionOutcodes);
    }

    [Fact]
    public void EvaluateWoWSelectorObjectRasterPrepassComposition_NullPointGridReturnsEmptyTrace()
    {
        SelectorObjectRasterPrefixTrace prefix = BuildAcceptedPrefix();

        uint result = EvaluateWoWSelectorObjectRasterPrepassComposition(
            prefix,
            CreatePayload(),
            pointGrid: null,
            pointGridPointCount: 0,
            pointOutcodes: null,
            pointOutcodeCapacity: 0,
            out SelectorObjectRasterPrepassCompositionTrace trace);

        Assert.Equal(0u, result);
        Assert.Equal(0u, trace.Prepass.PointWriteCount);
        Assert.Equal(0u, trace.TranslatedFirstPlaneDistance, 6);
    }

    private static SelectorObjectRasterPrefixTrace BuildAcceptedPrefix()
    {
        uint result = EvaluateWoWSelectorObjectRasterConsumerPrefix(
            modeWord: 0x00001000u,
            rasterRowCount: 4,
            rasterColumnCount: 4,
            rasterRowStride: 3,
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
