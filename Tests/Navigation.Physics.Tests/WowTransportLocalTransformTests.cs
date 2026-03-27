using System;
using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowTransportLocalTransformTests
{
    [Fact]
    public void TransformWorldPointToTransportLocal_InvertsYawAndTranslation()
    {
        Vector3 transportPosition = new(10f, 20f, 5f);
        float transportOrientation = MathF.PI * 0.5f;
        Vector3 worldPoint = new(10f, 22f, 6f);

        bool transformed = TransformWoWWorldPointToTransportLocal(
            worldPoint,
            transportPosition,
            transportOrientation,
            out Vector3 localPoint);

        Assert.True(transformed);
        Assert.Equal(2f, localPoint.X, 5);
        Assert.Equal(0f, localPoint.Y, 5);
        Assert.Equal(1f, localPoint.Z, 5);
    }

    [Fact]
    public void BuildTransportLocalPlane_RotatesNormalAndRecomputesPlaneDistance()
    {
        Vector3 transportPosition = new(10f, 20f, 5f);
        float transportOrientation = MathF.PI * 0.5f;
        Vector3 worldNormal = new(0f, 1f, 0f);
        Vector3 worldPoint = new(10f, 22f, 6f);

        bool built = BuildWoWTransportLocalPlane(
            worldNormal,
            worldPoint,
            transportPosition,
            transportOrientation,
            out SelectorSupportPlane plane);

        Assert.True(built);
        Assert.Equal(1f, plane.Normal.X, 5);
        Assert.Equal(0f, plane.Normal.Y, 5);
        Assert.Equal(0f, plane.Normal.Z, 5);
        Assert.Equal(-2f, plane.PlaneDistance, 5);
    }

    [Fact]
    public void TransformSelectorCandidateRecordToTransportLocal_RewritesPointsAndPlane()
    {
        Vector3 transportPosition = new(10f, 20f, 5f);
        float transportOrientation = MathF.PI * 0.5f;
        SelectorCandidateRecord worldRecord = new()
        {
            FilterPlane = new SelectorSupportPlane
            {
                Normal = new Vector3(0f, 1f, 0f),
                PlaneDistance = -22f,
            },
            Point0 = new Vector3(10f, 22f, 6f),
            Point1 = new Vector3(10f, 22f, 7f),
            Point2 = new Vector3(9f, 22f, 6f),
        };

        bool transformed = TransformWoWSelectorCandidateRecordToTransportLocal(
            worldRecord,
            transportPosition,
            transportOrientation,
            out SelectorCandidateRecord localRecord);

        Assert.True(transformed);
        Assert.Equal(2f, localRecord.Point0.X, 5);
        Assert.Equal(0f, localRecord.Point0.Y, 5);
        Assert.Equal(1f, localRecord.Point0.Z, 5);
        Assert.Equal(2f, localRecord.Point1.X, 5);
        Assert.Equal(0f, localRecord.Point1.Y, 5);
        Assert.Equal(2f, localRecord.Point1.Z, 5);
        Assert.Equal(2f, localRecord.Point2.X, 5);
        Assert.Equal(1f, localRecord.Point2.Y, 5);
        Assert.Equal(1f, localRecord.Point2.Z, 5);
        Assert.Equal(1f, localRecord.FilterPlane.Normal.X, 5);
        Assert.Equal(0f, localRecord.FilterPlane.Normal.Y, 5);
        Assert.Equal(0f, localRecord.FilterPlane.Normal.Z, 5);
        Assert.Equal(-2f, localRecord.FilterPlane.PlaneDistance, 5);
    }

    [Fact]
    public void TransformSelectorCandidateRecordBufferToTransportLocal_NoTransportGuid_LeavesRecordsUnchanged()
    {
        Vector3 transportPosition = new(10f, 20f, 5f);
        float transportOrientation = MathF.PI * 0.5f;
        SelectorCandidateRecord[] records =
        [
            new SelectorCandidateRecord
            {
                FilterPlane = new SelectorSupportPlane
                {
                    Normal = new Vector3(0f, 1f, 0f),
                    PlaneDistance = -22f,
                },
                Point0 = new Vector3(10f, 22f, 6f),
                Point1 = new Vector3(10f, 22f, 7f),
                Point2 = new Vector3(9f, 22f, 6f),
            },
        ];

        bool transformed = TransformWoWSelectorCandidateRecordBufferToTransportLocal(
            0u,
            0u,
            transportPosition,
            transportOrientation,
            records,
            (uint)records.Length);

        Assert.True(transformed);
        Assert.Equal(10f, records[0].Point0.X, 5);
        Assert.Equal(22f, records[0].Point0.Y, 5);
        Assert.Equal(6f, records[0].Point0.Z, 5);
        Assert.Equal(0f, records[0].FilterPlane.Normal.X, 5);
        Assert.Equal(1f, records[0].FilterPlane.Normal.Y, 5);
        Assert.Equal(-22f, records[0].FilterPlane.PlaneDistance, 5);
    }

    [Fact]
    public void TransformSelectorCandidateRecordBufferToTransportLocal_WithTransportGuid_RewritesEveryRecord()
    {
        Vector3 transportPosition = new(10f, 20f, 5f);
        float transportOrientation = MathF.PI * 0.5f;
        SelectorCandidateRecord[] records =
        [
            new SelectorCandidateRecord
            {
                FilterPlane = new SelectorSupportPlane
                {
                    Normal = new Vector3(0f, 1f, 0f),
                    PlaneDistance = -22f,
                },
                Point0 = new Vector3(10f, 22f, 6f),
                Point1 = new Vector3(10f, 22f, 7f),
                Point2 = new Vector3(9f, 22f, 6f),
            },
            new SelectorCandidateRecord
            {
                FilterPlane = new SelectorSupportPlane
                {
                    Normal = new Vector3(1f, 0f, 0f),
                    PlaneDistance = -10f,
                },
                Point0 = new Vector3(10f, 20f, 5f),
                Point1 = new Vector3(10f, 21f, 5f),
                Point2 = new Vector3(10f, 20f, 6f),
            },
        ];

        bool transformed = TransformWoWSelectorCandidateRecordBufferToTransportLocal(
            0x12345678u,
            0u,
            transportPosition,
            transportOrientation,
            records,
            (uint)records.Length);

        Assert.True(transformed);
        Assert.Equal(2f, records[0].Point0.X, 5);
        Assert.Equal(0f, records[0].Point0.Y, 5);
        Assert.Equal(1f, records[0].Point0.Z, 5);
        Assert.Equal(1f, records[0].FilterPlane.Normal.X, 5);
        Assert.Equal(0f, records[0].FilterPlane.Normal.Y, 5);
        Assert.Equal(-2f, records[0].FilterPlane.PlaneDistance, 5);
        Assert.Equal(0f, records[1].Point0.X, 5);
        Assert.Equal(0f, records[1].Point0.Y, 5);
        Assert.Equal(0f, records[1].Point0.Z, 5);
        Assert.Equal(0f, records[1].FilterPlane.Normal.X, 5);
        Assert.Equal(-1f, records[1].FilterPlane.Normal.Y, 5);
        Assert.Equal(0f, records[1].FilterPlane.Normal.Z, 5);
        Assert.Equal(0f, records[1].FilterPlane.PlaneDistance, 5);
    }
}
