using System;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorSourceTriangleRecordTests
{
    [Fact]
    public void BuildWoWSelectorSourceTriangleCandidateRecords_SkipsRejectedTrianglesAndPreservesBinaryOrder()
    {
        SelectorSupportPlane[] planes =
        [
            new SelectorSupportPlane
            {
                Normal = new Vector3(1f, 0f, 0f),
                PlaneDistance = 0f,
            },
        ];

        Vector3[] points = new Vector3[19];
        points[0] = new Vector3(-0.03f, -1f, 0.25f);
        points[1] = new Vector3(0.5f, -0.25f, 0.75f);
        points[9] = new Vector3(-0.03f, 0.5f, 1.5f);
        points[17] = new Vector3(-0.03f, 1.25f, -0.75f);
        points[18] = new Vector3(0.75f, 1f, 0.5f);

        Vector3 translation = new(4f, 5f, 6f);
        SelectorCandidateRecord[] records = new SelectorCandidateRecord[4];

        int count = BuildWoWSelectorSourceTriangleCandidateRecords(
            planes,
            planes.Length,
            points,
            points.Length,
            translation,
            false,
            records,
            records.Length);

        Assert.Equal(3, count);
        AssertRecordMatchesFallbackPlane(records[0], points[9] + translation, points[1] + translation, points[0] + translation);
        AssertRecordMatchesFallbackPlane(records[1], points[9] + translation, points[17] + translation, points[18] + translation);
        AssertRecordMatchesFallbackPlane(records[2], points[9] + translation, points[18] + translation, points[1] + translation);
    }

    [Fact]
    public void BuildWoWSelectorSourceTriangleCandidateRecords_FastPlanePathUsesBinaryRsqrtNormalization()
    {
        Assert.True(Sse.IsSupported);

        Vector3[] points = new Vector3[19];
        points[0] = new Vector3(1.2f, -0.5f, 0.9f);
        points[1] = new Vector3(-0.8f, 0.6f, 0.3f);
        points[9] = new Vector3(-0.4f, 1.1f, 0.7f);
        points[17] = new Vector3(0.3f, 0.8f, -0.2f);
        points[18] = new Vector3(1.5f, -1.3f, 0.4f);

        Vector3 translation = new(-2f, 3f, 4f);
        SelectorCandidateRecord[] records = new SelectorCandidateRecord[4];

        int count = BuildWoWSelectorSourceTriangleCandidateRecords(
            Array.Empty<SelectorSupportPlane>(),
            0,
            points,
            points.Length,
            translation,
            true,
            records,
            records.Length);

        Assert.Equal(4, count);

        Vector3 expectedPoint0 = points[17] + translation;
        Vector3 expectedPoint1 = points[9] + translation;
        Vector3 expectedPoint2 = points[0] + translation;
        AssertRecordPoints(records[0], expectedPoint0, expectedPoint1, expectedPoint2);

        Vector3 rawNormal = Vector3.Cross(expectedPoint2 - expectedPoint0, expectedPoint1 - expectedPoint0);
        float inverseMagnitude = Sse.ReciprocalSqrtScalar(Vector128.Create(rawNormal.LengthSquared())).GetElement(0);
        Vector3 expectedNormal = rawNormal * inverseMagnitude;
        float expectedPlaneDistance = -Vector3.Dot(expectedNormal, expectedPoint0);

        AssertFloatBitsEqual(expectedNormal.X, records[0].FilterPlane.Normal.X);
        AssertFloatBitsEqual(expectedNormal.Y, records[0].FilterPlane.Normal.Y);
        AssertFloatBitsEqual(expectedNormal.Z, records[0].FilterPlane.Normal.Z);
        AssertFloatBitsEqual(expectedPlaneDistance, records[0].FilterPlane.PlaneDistance);
    }

    [Fact]
    public void BuildWoWSelectorSourceTriangleCandidateRecords_WhenAllTrianglesReject_ReturnsZero()
    {
        SelectorSupportPlane[] planes =
        [
            new SelectorSupportPlane
            {
                Normal = new Vector3(1f, 0f, 0f),
                PlaneDistance = 0f,
            },
        ];

        Vector3[] points = new Vector3[19];
        points[0] = new Vector3(-0.03f, -1f, 0.25f);
        points[1] = new Vector3(-0.03f, -0.25f, 0.75f);
        points[9] = new Vector3(-0.03f, 0.5f, 1.5f);
        points[17] = new Vector3(-0.03f, 1.25f, -0.75f);
        points[18] = new Vector3(-0.03f, 1f, 0.5f);

        SelectorCandidateRecord[] records = new SelectorCandidateRecord[4];

        int count = BuildWoWSelectorSourceTriangleCandidateRecords(
            planes,
            planes.Length,
            points,
            points.Length,
            new Vector3(0f, 0f, 0f),
            false,
            records,
            records.Length);

        Assert.Equal(0, count);
    }

    private static void AssertRecordMatchesFallbackPlane(SelectorCandidateRecord record, Vector3 expectedPoint0, Vector3 expectedPoint1, Vector3 expectedPoint2)
    {
        AssertRecordPoints(record, expectedPoint0, expectedPoint1, expectedPoint2);
        Assert.True(BuildWoWPlaneFromTrianglePoints(expectedPoint0, expectedPoint1, expectedPoint2, out SelectorSupportPlane plane));
        AssertPlaneBitsEqual(plane, record.FilterPlane);
    }

    private static void AssertRecordPoints(SelectorCandidateRecord record, Vector3 expectedPoint0, Vector3 expectedPoint1, Vector3 expectedPoint2)
    {
        Assert.Equal(expectedPoint0.X, record.Point0.X);
        Assert.Equal(expectedPoint0.Y, record.Point0.Y);
        Assert.Equal(expectedPoint0.Z, record.Point0.Z);
        Assert.Equal(expectedPoint1.X, record.Point1.X);
        Assert.Equal(expectedPoint1.Y, record.Point1.Y);
        Assert.Equal(expectedPoint1.Z, record.Point1.Z);
        Assert.Equal(expectedPoint2.X, record.Point2.X);
        Assert.Equal(expectedPoint2.Y, record.Point2.Y);
        Assert.Equal(expectedPoint2.Z, record.Point2.Z);
    }

    private static void AssertPlaneBitsEqual(SelectorSupportPlane expected, SelectorSupportPlane actual)
    {
        AssertFloatBitsEqual(expected.Normal.X, actual.Normal.X);
        AssertFloatBitsEqual(expected.Normal.Y, actual.Normal.Y);
        AssertFloatBitsEqual(expected.Normal.Z, actual.Normal.Z);
        AssertFloatBitsEqual(expected.PlaneDistance, actual.PlaneDistance);
    }

    private static void AssertFloatBitsEqual(float expected, float actual)
    {
        Assert.Equal(BitConverter.SingleToInt32Bits(expected), BitConverter.SingleToInt32Bits(actual));
    }
}
