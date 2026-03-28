using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorObjectRasterConsumerPrefixTests
{
    [Fact]
    public void EvaluateWoWSelectorObjectRasterConsumerPrefix_ModeGateRejectsWithoutRasterBit()
    {
        SelectorObjectRasterPayload payload = CreatePayload();

        uint result = EvaluateWoWSelectorObjectRasterConsumerPrefix(
            modeWord: 0u,
            rasterRowCount: 32,
            rasterColumnCount: 32,
            rasterRowStride: 32,
            quantizeScale: -1.0f,
            objectTranslation: new Vector3(10.0f, 20.0f, 30.0f),
            sourcePayload: payload,
            out SelectorObjectRasterPrefixTrace trace);

        Assert.Equal(0u, result);
        Assert.Equal(0u, trace.ModeGateAccepted);
        Assert.Equal(0u, trace.QuantizedWindowAccepted);
        Assert.Equal(0u, trace.ScratchAllocationRequired);
        Assert.Equal(0u, trace.EnteredPrepassPointLoops);
        Assert.Equal(0u, trace.EnteredRasterCellLoops);
    }

    [Fact]
    public void EvaluateWoWSelectorObjectRasterConsumerPrefix_TranslatesPayloadAndBuildsAcceptedWindow()
    {
        SelectorObjectRasterPayload payload = CreatePayload(
            planeDistance0: 5.0f,
            anchorPoint0: new Vector3(3.0f, 4.0f, 5.0f),
            anchorPoint1: new Vector3(6.0f, 7.0f, 8.0f));

        uint result = EvaluateWoWSelectorObjectRasterConsumerPrefix(
            modeWord: 0x00001000u,
            rasterRowCount: 16,
            rasterColumnCount: 32,
            rasterRowStride: 20,
            quantizeScale: -1.0f,
            objectTranslation: new Vector3(10.0f, 20.0f, 30.0f),
            sourcePayload: payload,
            out SelectorObjectRasterPrefixTrace trace);

        Assert.Equal(1u, result);
        Assert.Equal(1u, trace.ModeGateAccepted);
        Assert.Equal(1u, trace.QuantizedWindowAccepted);
        Assert.Equal(new Vector3(-10.0f, -20.0f, -30.0f).ToString(), trace.AppliedTranslation.ToString());
        Assert.Equal(new Vector3(-9.0f, -18.0f, -30.0f).ToString(), trace.TranslatedSupportPointMin.ToString());
        Assert.Equal(new Vector3(-7.0f, -16.0f, -30.0f).ToString(), trace.TranslatedSupportPointMax.ToString());
        Assert.Equal(new Vector3(-7.0f, -16.0f, -25.0f).ToString(), trace.TranslatedAnchorPoint0.ToString());
        Assert.Equal(new Vector3(-4.0f, -13.0f, -22.0f).ToString(), trace.TranslatedAnchorPoint1.ToString());
        Assert.Equal(15.0f, trace.TranslatedFirstPlaneDistance, 5);
        Assert.Equal(7, trace.RawWindow.RowMin);
        Assert.Equal(16, trace.RawWindow.ColumnMin);
        Assert.Equal(9, trace.RawWindow.RowMax);
        Assert.Equal(18, trace.RawWindow.ColumnMax);
        Assert.Equal(64u, trace.ScratchByteCount);
        Assert.Equal(4u, trace.PrepassPointCountX);
        Assert.Equal(4u, trace.PrepassPointCountY);
        Assert.Equal(3u, trace.RasterCellCountX);
        Assert.Equal(3u, trace.RasterCellCountY);
        Assert.Equal(156, trace.PointStartIndex);
        Assert.Equal(16, trace.PointRowAdvance);
        Assert.Equal(1u, trace.EnteredPrepassPointLoops);
        Assert.Equal(1u, trace.EnteredRasterCellLoops);
    }

    [Fact]
    public void EvaluateWoWSelectorObjectRasterConsumerPrefix_RejectsWindowOutsideRasterBounds()
    {
        SelectorObjectRasterPayload payload = CreatePayload();

        uint result = EvaluateWoWSelectorObjectRasterConsumerPrefix(
            modeWord: 0x00001000u,
            rasterRowCount: 8,
            rasterColumnCount: 18,
            rasterRowStride: 18,
            quantizeScale: -1.0f,
            objectTranslation: new Vector3(10.0f, 20.0f, 30.0f),
            sourcePayload: payload,
            out SelectorObjectRasterPrefixTrace trace);

        Assert.Equal(0u, result);
        Assert.Equal(1u, trace.ModeGateAccepted);
        Assert.Equal(0u, trace.QuantizedWindowAccepted);
        Assert.Equal(0u, trace.ScratchAllocationRequired);
        Assert.Equal(0u, trace.ScratchByteCount);
        Assert.Equal(0u, trace.EnteredPrepassPointLoops);
        Assert.Equal(0u, trace.EnteredRasterCellLoops);
        Assert.Equal(9, trace.RawWindow.RowMax);
        Assert.Equal(18, trace.RawWindow.ColumnMax);
    }

    [Fact]
    public void EvaluateWoWSelectorObjectRasterConsumerPrefix_SingleCellWindowUsesTwoByTwoScratchGrid()
    {
        SelectorObjectRasterPayload payload = CreatePayload(
            supportPoints:
            [
                new Vector3(1.0f, 2.0f, 0.0f),
                new Vector3(1.0f, 2.0f, 0.0f),
                new Vector3(1.0f, 2.0f, 0.0f),
                new Vector3(1.0f, 2.0f, 0.0f),
                new Vector3(1.0f, 2.0f, 0.0f),
                new Vector3(1.0f, 2.0f, 0.0f),
                new Vector3(1.0f, 2.0f, 0.0f),
                new Vector3(1.0f, 2.0f, 0.0f),
            ]);

        uint result = EvaluateWoWSelectorObjectRasterConsumerPrefix(
            modeWord: 0x00001000u,
            rasterRowCount: 8,
            rasterColumnCount: 8,
            rasterRowStride: 8,
            quantizeScale: -1.0f,
            objectTranslation: new Vector3(3.0f, 4.0f, 0.0f),
            sourcePayload: payload,
            out SelectorObjectRasterPrefixTrace trace);

        Assert.Equal(1u, result);
        Assert.Equal(2u, trace.PrepassPointCountX);
        Assert.Equal(2u, trace.PrepassPointCountY);
        Assert.Equal(1u, trace.RasterCellCountX);
        Assert.Equal(1u, trace.RasterCellCountY);
        Assert.Equal(16u, trace.ScratchByteCount);
        Assert.Equal(1u, trace.EnteredPrepassPointLoops);
        Assert.Equal(1u, trace.EnteredRasterCellLoops);
    }

    private static SelectorObjectRasterPayload CreatePayload(
        float planeDistance0 = 5.0f,
        Vector3? anchorPoint0 = null,
        Vector3? anchorPoint1 = null,
        Vector3[]? supportPoints = null)
    {
        return new SelectorObjectRasterPayload
        {
            Planes =
            [
                new SelectorSupportPlane { Normal = new Vector3(1.0f, 0.0f, 0.0f), PlaneDistance = planeDistance0 },
                new SelectorSupportPlane { Normal = new Vector3(0.0f, 1.0f, 0.0f), PlaneDistance = 11.0f },
                new SelectorSupportPlane { Normal = new Vector3(0.0f, 0.0f, 1.0f), PlaneDistance = 13.0f },
                new SelectorSupportPlane { Normal = new Vector3(-1.0f, 0.0f, 0.0f), PlaneDistance = 17.0f },
                new SelectorSupportPlane { Normal = new Vector3(0.0f, -1.0f, 0.0f), PlaneDistance = 19.0f },
                new SelectorSupportPlane { Normal = new Vector3(0.0f, 0.0f, -1.0f), PlaneDistance = 23.0f },
            ],
            SupportPoints = supportPoints ??
            [
                new Vector3(1.0f, 2.0f, 0.0f),
                new Vector3(1.0f, 4.0f, 0.0f),
                new Vector3(2.0f, 2.0f, 0.0f),
                new Vector3(2.0f, 4.0f, 0.0f),
                new Vector3(3.0f, 2.0f, 0.0f),
                new Vector3(3.0f, 4.0f, 0.0f),
                new Vector3(2.0f, 3.0f, 0.0f),
                new Vector3(1.5f, 2.5f, 0.0f),
            ],
            AnchorPoint0 = anchorPoint0 ?? new Vector3(3.0f, 4.0f, 5.0f),
            AnchorPoint1 = anchorPoint1 ?? new Vector3(6.0f, 7.0f, 8.0f),
        };
    }
}
