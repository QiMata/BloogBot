using System;
using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowAabbBoundarySelectorRecordTests
{
    [Fact]
    public void BuildAabbBoundarySelectorCandidateRecords_EmitsTwoTrianglesPerCrossedBoundaryFace()
    {
        var boundaryMin = new Vector3(10f, 20f, 30f);
        var boundaryMax = new Vector3(40f, 60f, 999f);
        var queryBoundsMin = new Vector3(5f, 15f, 0f);
        var queryBoundsMax = new Vector3(45f, 65f, 1f);

        SelectorCandidateRecord[] records = new SelectorCandidateRecord[8];
        int count = BuildWoWAabbBoundarySelectorCandidateRecords(
            boundaryMin,
            boundaryMax,
            queryBoundsMin,
            queryBoundsMax,
            records,
            records.Length);

        Assert.Equal(8, count);

        AssertRecord(
            records[0],
            new Vector3(0f, -1f, 0f),
            20f,
            new Vector3(10f, 20f, 30f),
            new Vector3(40f, 20f, 30f),
            new Vector3(40f, 20f, 32030f));
        AssertRecord(
            records[1],
            new Vector3(0f, -1f, 0f),
            20f,
            new Vector3(10f, 20f, 30f),
            new Vector3(10f, 20f, 32030f),
            new Vector3(40f, 20f, 32030f));

        AssertRecord(
            records[2],
            new Vector3(0f, 1f, 0f),
            -60f,
            new Vector3(40f, 60f, 30f),
            new Vector3(10f, 60f, 30f),
            new Vector3(10f, 60f, 32030f));
        AssertRecord(
            records[3],
            new Vector3(0f, 1f, 0f),
            -60f,
            new Vector3(40f, 60f, 30f),
            new Vector3(40f, 60f, 32030f),
            new Vector3(10f, 60f, 32030f));

        AssertRecord(
            records[4],
            new Vector3(-1f, 0f, 0f),
            10f,
            new Vector3(10f, 60f, 30f),
            new Vector3(10f, 20f, 30f),
            new Vector3(10f, 20f, 32030f));
        AssertRecord(
            records[5],
            new Vector3(-1f, 0f, 0f),
            10f,
            new Vector3(10f, 60f, 30f),
            new Vector3(10f, 60f, 32030f),
            new Vector3(10f, 20f, 32030f));

        AssertRecord(
            records[6],
            new Vector3(1f, 0f, 0f),
            -40f,
            new Vector3(40f, 20f, 30f),
            new Vector3(40f, 60f, 30f),
            new Vector3(40f, 60f, 32030f));
        AssertRecord(
            records[7],
            new Vector3(1f, 0f, 0f),
            -40f,
            new Vector3(40f, 20f, 30f),
            new Vector3(40f, 20f, 32030f),
            new Vector3(40f, 60f, 32030f));
    }

    [Fact]
    public void BuildAabbBoundarySelectorCandidateRecords_UsesInclusiveFaceComparisons()
    {
        var boundaryMin = new Vector3(10f, 20f, 30f);
        var boundaryMax = new Vector3(40f, 60f, 90f);
        var queryBoundsMin = new Vector3(12f, 20f, -5f);
        var queryBoundsMax = new Vector3(38f, 59f, 5f);

        SelectorCandidateRecord[] records = new SelectorCandidateRecord[8];
        int count = BuildWoWAabbBoundarySelectorCandidateRecords(
            boundaryMin,
            boundaryMax,
            queryBoundsMin,
            queryBoundsMax,
            records,
            records.Length);

        Assert.Equal(2, count);
        AssertRecord(
            records[0],
            new Vector3(0f, -1f, 0f),
            20f,
            new Vector3(10f, 20f, 30f),
            new Vector3(40f, 20f, 30f),
            new Vector3(40f, 20f, 32030f));
        AssertRecord(
            records[1],
            new Vector3(0f, -1f, 0f),
            20f,
            new Vector3(10f, 20f, 30f),
            new Vector3(10f, 20f, 32030f),
            new Vector3(40f, 20f, 32030f));
    }

    [Fact]
    public void BuildAabbBoundarySelectorCandidateRecords_WhenQueryFootprintStaysInside_ProducesNoRecords()
    {
        var boundaryMin = new Vector3(10f, 20f, 30f);
        var boundaryMax = new Vector3(40f, 60f, 90f);
        var queryBoundsMin = new Vector3(12f, 22f, -5f);
        var queryBoundsMax = new Vector3(38f, 58f, 5f);

        SelectorCandidateRecord[] records = new SelectorCandidateRecord[8];
        int count = BuildWoWAabbBoundarySelectorCandidateRecords(
            boundaryMin,
            boundaryMax,
            queryBoundsMin,
            queryBoundsMax,
            records,
            records.Length);

        Assert.Equal(0, count);
    }

    private static void AssertRecord(
        SelectorCandidateRecord record,
        Vector3 expectedNormal,
        float expectedPlaneDistance,
        Vector3 expectedPoint0,
        Vector3 expectedPoint1,
        Vector3 expectedPoint2)
    {
        AssertVector(expectedNormal, record.FilterPlane.Normal);
        Assert.Equal(expectedPlaneDistance, record.FilterPlane.PlaneDistance, 6);
        AssertVector(expectedPoint0, record.Point0);
        AssertVector(expectedPoint1, record.Point1);
        AssertVector(expectedPoint2, record.Point2);

        Assert.True(MathF.Abs(EvaluatePlane(record.FilterPlane, record.Point0)) <= 1e-5f);
        Assert.True(MathF.Abs(EvaluatePlane(record.FilterPlane, record.Point1)) <= 1e-5f);
        Assert.True(MathF.Abs(EvaluatePlane(record.FilterPlane, record.Point2)) <= 1e-5f);
    }

    private static float EvaluatePlane(SelectorSupportPlane plane, Vector3 point) =>
        Vector3.Dot(plane.Normal, point) + plane.PlaneDistance;

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, 6);
        Assert.Equal(expected.Y, actual.Y, 6);
        Assert.Equal(expected.Z, actual.Z, 6);
    }
}
