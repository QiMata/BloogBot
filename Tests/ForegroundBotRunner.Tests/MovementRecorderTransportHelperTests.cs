using ForegroundBotRunner;
using Game;
using WorldPosition = GameData.Core.Models.Position;

namespace ForegroundBotRunner.Tests;

public sealed class MovementRecorderTransportHelperTests
{
    [Fact]
    public void TransformTransportLocalToWorld_RotatesAndTranslatesPosition()
    {
        var localPosition = new Position { X = 4f, Y = -2f, Z = 3f };
        var transportWorldPosition = new WorldPosition(100f, 50f, 10f);

        var worldPosition = MovementRecorder.TransformTransportLocalToWorld(
            localPosition,
            transportWorldPosition,
            MathF.PI / 2f);

        Assert.Equal(102f, worldPosition.X, 3);
        Assert.Equal(54f, worldPosition.Y, 3);
        Assert.Equal(13f, worldPosition.Z, 3);
    }

    [Fact]
    public void ApplyTransportState_CopiesLocalOffsetsAndRelativeFacing()
    {
        var frame = new MovementData
        {
            Position = new Position { X = 11.5f, Y = -22.75f, Z = 33.125f }
        };

        MovementRecorder.ApplyTransportState(
            frame,
            transportGuid: 123456789ul,
            localPosition: frame.Position,
            worldFacing: 0.1f,
            transportFacing: 6.1f);

        Assert.Equal(123456789ul, frame.TransportGuid);
        Assert.Equal(11.5f, frame.TransportOffsetX, 3);
        Assert.Equal(-22.75f, frame.TransportOffsetY, 3);
        Assert.Equal(33.125f, frame.TransportOffsetZ, 3);
        Assert.Equal(0.2831853f, frame.TransportOrientation, 3);
    }

    [Fact]
    public void ApplyTransportState_ZeroTransportGuid_ClearsTransportFields()
    {
        var frame = new MovementData
        {
            TransportGuid = 42ul,
            TransportOffsetX = 1f,
            TransportOffsetY = 2f,
            TransportOffsetZ = 3f,
            TransportOrientation = 4f,
            Position = new Position { X = 8f, Y = 9f, Z = 10f }
        };

        MovementRecorder.ApplyTransportState(
            frame,
            transportGuid: 0ul,
            localPosition: frame.Position,
            worldFacing: 1.5f,
            transportFacing: 0.25f);

        Assert.Equal(0ul, frame.TransportGuid);
        Assert.Equal(0f, frame.TransportOffsetX);
        Assert.Equal(0f, frame.TransportOffsetY);
        Assert.Equal(0f, frame.TransportOffsetZ);
        Assert.Equal(0f, frame.TransportOrientation);
    }

    [Fact]
    public void EnsureTransportSnapshot_AddsOnlyOneSnapshotPerGuid()
    {
        var frame = new MovementData();
        var snapshot = MovementRecorder.CreateGameObjectSnapshot(
            guid: 99ul,
            entry: 176495u,
            displayId: 3031u,
            gameObjectType: 15u,
            flags: 0u,
            goState: 1u,
            worldPosition: new WorldPosition(1320f, -4649f, 53f),
            facing: 1.25f,
            name: "Orgrimmar - Undercity",
            scale: 1f,
            animProgress: 255u,
            playerWorldPosition: new WorldPosition(1322f, -4645f, 53f));

        bool firstAdd = MovementRecorder.EnsureTransportSnapshot(frame, snapshot);
        bool secondAdd = MovementRecorder.EnsureTransportSnapshot(frame, snapshot);

        Assert.True(firstAdd);
        Assert.False(secondAdd);
        Assert.Single(frame.NearbyGameObjects);
        Assert.Equal(99ul, frame.NearbyGameObjects[0].Guid);
        Assert.Equal(4.472136f, frame.NearbyGameObjects[0].DistanceToPlayer, 3);
    }
}
