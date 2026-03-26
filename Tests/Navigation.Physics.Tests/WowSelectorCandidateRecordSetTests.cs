using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorCandidateRecordSetTests
{
    [Fact]
    public void ClipSelectorPointStripAgainstPlanePrefix_StopsWhenPlaneSetEmptiesStrip()
    {
        SelectorSupportPlane[] planes =
        [
            new SelectorSupportPlane
            {
                Normal = new Vector3(0f, 0f, 1f),
                PlaneDistance = 1f
            }
        ];

        Vector3[] points = new Vector3[15];
        uint[] sourceIndices = new uint[15];
        points[0] = new Vector3(0f, 0f, 0f);
        points[1] = new Vector3(1f, 0f, 0f);
        points[2] = new Vector3(0f, 1f, 0f);
        sourceIndices[0] = 1u;
        sourceIndices[1] = 2u;
        sourceIndices[2] = 3u;
        int count = 3;

        bool ok = ClipWoWSelectorPointStripAgainstPlanePrefix(
            planes,
            planes.Length,
            points,
            sourceIndices,
            points.Length,
            ref count);

        Assert.False(ok);
        Assert.Equal(0, count);
    }

    [Fact]
    public void EvaluateSelectorCandidateRecordSet_DotRejectedRecordLeavesBestRatioUnchanged()
    {
        SelectorCandidateRecord[] records =
        [
            CreateRecord(
                filterNormal: new Vector3(1f, 0f, 0f),
                point0: new Vector3(0.2f, 0f, 0f),
                point1: new Vector3(0.4f, 1f, 0f),
                point2: new Vector3(0.6f, -1f, 0f))
        ];

        SelectorSupportPlane[] clipPlanes = BuildPermissiveClipPlanes();
        SelectorSupportPlane[] validationPlanes = BuildValidationPlanes();
        float bestRatio = 1f;
        int bestRecordIndex = -1;

        bool accepted = EvaluateWoWSelectorCandidateRecordSet(
            records,
            records.Length,
            testPoint: new Vector3(1f, 0f, 0f),
            clipPlanes,
            clipPlanes.Length,
            validationPlanes,
            validationPlanes.Length,
            validationPlaneIndex: 0,
            inOutBestRatio: ref bestRatio,
            inOutBestRecordIndex: ref bestRecordIndex,
            trace: out SelectorRecordEvaluationTrace trace);

        Assert.False(accepted);
        Assert.Equal(1f, bestRatio, 6);
        Assert.Equal(-1, bestRecordIndex);
        Assert.Equal(1u, trace.RecordCount);
        Assert.Equal(1u, trace.DotRejectedCount);
        Assert.Equal(0u, trace.ClipRejectedCount);
        Assert.Equal(0u, trace.ValidationAcceptedCount);
        Assert.Equal(uint.MaxValue, trace.SelectedRecordIndex);
    }

    [Fact]
    public void EvaluateSelectorCandidateRecordSet_ClipRejectedRecordLeavesBestRatioUnchanged()
    {
        SelectorCandidateRecord[] records =
        [
            CreateRecord(
                filterNormal: new Vector3(-1f, 0f, 0f),
                point0: new Vector3(0.2f, 0f, 0f),
                point1: new Vector3(0.4f, 1f, 0f),
                point2: new Vector3(0.6f, -1f, 0f))
        ];

        SelectorSupportPlane[] clipPlanes =
        [
            new SelectorSupportPlane
            {
                Normal = new Vector3(0f, 0f, 1f),
                PlaneDistance = 1f
            }
        ];

        SelectorSupportPlane[] validationPlanes = BuildValidationPlanes();
        float bestRatio = 1f;
        int bestRecordIndex = -1;

        bool accepted = EvaluateWoWSelectorCandidateRecordSet(
            records,
            records.Length,
            testPoint: new Vector3(1f, 0f, 0f),
            clipPlanes,
            clipPlanes.Length,
            validationPlanes,
            validationPlanes.Length,
            validationPlaneIndex: 0,
            inOutBestRatio: ref bestRatio,
            inOutBestRecordIndex: ref bestRecordIndex,
            trace: out SelectorRecordEvaluationTrace trace);

        Assert.False(accepted);
        Assert.Equal(1f, bestRatio, 6);
        Assert.Equal(-1, bestRecordIndex);
        Assert.Equal(0u, trace.DotRejectedCount);
        Assert.Equal(1u, trace.ClipRejectedCount);
        Assert.Equal(0u, trace.ValidationAcceptedCount);
        Assert.Equal(uint.MaxValue, trace.SelectedRecordIndex);
    }

    [Fact]
    public void EvaluateSelectorCandidateRecordSet_ChoosesLowestImprovingRatioAndUpdatesIndex()
    {
        SelectorCandidateRecord[] records =
        [
            CreateRecord(
                filterNormal: new Vector3(-1f, 0f, 0f),
                point0: new Vector3(0.6f, 0f, 0f),
                point1: new Vector3(0.8f, 1f, 0f),
                point2: new Vector3(0.7f, -1f, 0f)),
            CreateRecord(
                filterNormal: new Vector3(-1f, 0f, 0f),
                point0: new Vector3(0.2f, 0f, 0f),
                point1: new Vector3(0.5f, 1f, 0f),
                point2: new Vector3(0.9f, -1f, 0f))
        ];

        SelectorSupportPlane[] clipPlanes = BuildPermissiveClipPlanes();
        SelectorSupportPlane[] validationPlanes = BuildValidationPlanes();
        float bestRatio = 1f;
        int bestRecordIndex = -1;

        bool accepted = EvaluateWoWSelectorCandidateRecordSet(
            records,
            records.Length,
            testPoint: new Vector3(1f, 0f, 0f),
            clipPlanes,
            clipPlanes.Length,
            validationPlanes,
            validationPlanes.Length,
            validationPlaneIndex: 0,
            inOutBestRatio: ref bestRatio,
            inOutBestRecordIndex: ref bestRecordIndex,
            trace: out SelectorRecordEvaluationTrace trace);

        Assert.True(accepted);
        Assert.Equal(0.2f, bestRatio, 6);
        Assert.Equal(1, bestRecordIndex);
        Assert.Equal(2u, trace.RecordCount);
        Assert.Equal(0u, trace.DotRejectedCount);
        Assert.Equal(0u, trace.ClipRejectedCount);
        Assert.Equal(0u, trace.ValidationRejectedCount);
        Assert.Equal(2u, trace.ValidationAcceptedCount);
        Assert.Equal(1u, trace.UpdatedBestRatio);
        Assert.Equal(1u, trace.SelectedRecordIndex);
        Assert.Equal(0.2f, trace.SelectedBestRatio, 6);
        Assert.Equal(3u, trace.SelectedStripCount);
    }

    private static SelectorCandidateRecord CreateRecord(
        Vector3 filterNormal,
        Vector3 point0,
        Vector3 point1,
        Vector3 point2) =>
        new()
        {
            FilterPlane = new SelectorSupportPlane
            {
                Normal = filterNormal,
                PlaneDistance = 0f
            },
            Point0 = point0,
            Point1 = point1,
            Point2 = point2
        };

    private static SelectorSupportPlane[] BuildPermissiveClipPlanes() =>
    [
        new SelectorSupportPlane
        {
            Normal = new Vector3(0f, 0f, 1f),
            PlaneDistance = -10f
        },
        new SelectorSupportPlane
        {
            Normal = new Vector3(0f, 0f, 1f),
            PlaneDistance = -10f
        },
        new SelectorSupportPlane
        {
            Normal = new Vector3(0f, 0f, 1f),
            PlaneDistance = -10f
        }
    ];

    private static SelectorSupportPlane[] BuildValidationPlanes()
    {
        SelectorSupportPlane[] planes = new SelectorSupportPlane[9];
        for (int i = 0; i < planes.Length; ++i)
        {
            planes[i] = new SelectorSupportPlane
            {
                Normal = new Vector3(0f, 0f, 1f),
                PlaneDistance = -1000f
            };
        }

        planes[0] = new SelectorSupportPlane
        {
            Normal = new Vector3(1f, 0f, 0f),
            PlaneDistance = 0f
        };

        return planes;
    }
}
