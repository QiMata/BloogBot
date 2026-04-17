using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameData.Core.Enums;
using GameData.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using WoWSharpClient.Handlers;
using WoWSharpClient.Models;
using WoWSharpClient.Parsers;
using WoWSharpClient.Tests.Handlers;
using WoWSharpClient.Tests.Util;
using WoWSharpClient.Utils;
using static GameData.Core.Enums.UpdateFields;

namespace WoWSharpClient.Tests.Parity;

[Collection("Sequential ObjectManager tests")]
public class ObjectUpdateMutationOrderTests(ObjectManagerFixture fixture) : IClassFixture<ObjectManagerFixture>
{
    private readonly ObjectManagerFixture _fixture = fixture;
    private static readonly HandlerContext ctx = new(WoWSharpObjectManager.Instance, WoWSharpEventEmitter.Instance);

    [Fact]
    public void LocalPlayerCreateBlock_UsesSeededCachedCreateOrder()
    {
        ResetObjectManager();

        const ulong playerGuid = 0x120ul;
        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);

        var events = ReplayAndCapture(
            BuildCreateObjectPacket(
                playerGuid,
                WoWObjectType.Player,
                movementPosition: new Position(10f, 20f, 30f),
                movementFacing: 0.75f,
                fields: new SortedDictionary<uint, object?>
                {
                    [(uint)EObjectFields.OBJECT_FIELD_ENTRY] = 1u,
                    [(uint)EObjectFields.OBJECT_FIELD_SCALE_X] = 1.0f,
                    [(uint)EUnitFields.UNIT_FIELD_HEALTH] = 450u,
                    [(uint)EUnitFields.UNIT_FIELD_MAXHEALTH] = 900u,
                }));

        AssertStageSequence(
            events,
            playerGuid,
            (WoWSharpObjectManager.TestMutationStage.MovementApplied, "cached-create"),
            (WoWSharpObjectManager.TestMutationStage.FieldsApplied, "cached-create"));

        var player = Assert.IsType<WoWLocalPlayer>(objectManager.Player);
        Assert.Equal(450u, player.Health);
    }

    [Fact]
    public void RemoteUnitCreateBlock_AppliesFieldsBeforeMovement()
    {
        ResetObjectManager();

        const ulong playerGuid = 0x121ul;
        const ulong remoteGuid = 0xF130000000001001ul;
        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);

        var events = ReplayAndCapture(
            BuildCreateObjectPacket(
                remoteGuid,
                WoWObjectType.Unit,
                movementPosition: new Position(40f, 50f, 60f),
                movementFacing: 1.5f,
                fields: new SortedDictionary<uint, object?>
                {
                    [(uint)EObjectFields.OBJECT_FIELD_ENTRY] = 88u,
                    [(uint)EObjectFields.OBJECT_FIELD_SCALE_X] = 1.0f,
                    [(uint)EUnitFields.UNIT_FIELD_HEALTH] = 777u,
                    [(uint)EUnitFields.UNIT_FIELD_MAXHEALTH] = 1000u,
                    [(uint)EUnitFields.UNIT_FIELD_LEVEL] = 12u,
                }));

        AssertStageSequence(
            events,
            remoteGuid,
            (WoWSharpObjectManager.TestMutationStage.FieldsApplied, "create"),
            (WoWSharpObjectManager.TestMutationStage.MovementApplied, "create"));

        var unit = Assert.IsType<WoWUnit>(objectManager.GetObjectByGuid(remoteGuid));
        Assert.Equal(777u, unit.Health);
        Assert.Equal(12u, unit.Level);
        Assert.Equal(40f, unit.Position.X, 3);
        Assert.Equal(50f, unit.Position.Y, 3);
        Assert.Equal(60f, unit.Position.Z, 3);
        Assert.Equal(1.5f, unit.Facing, 3);
    }

    [Fact]
    public void LocalPlayerPartialThenMovementPacket_AppliesFieldsBeforeMovement()
    {
        ResetObjectManager();

        const ulong playerGuid = 0x122ul;
        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);

        var player = Assert.IsType<WoWLocalPlayer>(objectManager.Player);
        player.Position = new Position(1f, 2f, 3f);
        player.Facing = 0.1f;
        player.Health = 10u;

        var events = ReplayAndCapture(
            BuildPartialThenMovementPacket(
                playerGuid,
                new SortedDictionary<uint, object?>
                {
                    [(uint)EUnitFields.UNIT_FIELD_HEALTH] = 600u,
                    [(uint)EUnitFields.UNIT_FIELD_MAXHEALTH] = 1200u,
                },
                new Position(70f, 80f, 90f),
                2.25f));

        AssertStageSequence(
            events,
            playerGuid,
            (WoWSharpObjectManager.TestMutationStage.FieldsApplied, "update"),
            (WoWSharpObjectManager.TestMutationStage.MovementApplied, "update"));

        Assert.Equal(600u, player.Health);
    }

    [Fact]
    public void DuplicateCreateObjectBlock_WithFallbackGameObjectType_MutatesCachedGameObjectInPlace_AndDescriptorFieldsWinAfterMovementPrepass()
    {
        ResetObjectManager();

        const ulong guid = 0xF11000000000D00Dul;
        var objectManager = WoWSharpObjectManager.Instance;

        ReplayAndCapture(
            BuildCreateGameObjectPacket(
                guid,
                packetObjectType: WoWObjectType.None,
                movementPosition: new Position(5f, 6f, 7f),
                movementFacing: 0.25f,
                descriptorPosition: new Position(5f, 6f, 7f),
                descriptorFacing: 0.25f,
                displayId: 111u));

        var initial = Assert.IsType<WoWGameObject>(objectManager.GetObjectByGuid(guid));
        Assert.Equal(111u, initial.DisplayId);

        var movementPosition = new Position(10f, 20f, 30f);
        var descriptorPosition = new Position(40f, 50f, 60f);
        var events = ReplayAndCapture(
            BuildCreateGameObjectPacket(
                guid,
                packetObjectType: WoWObjectType.None,
                movementPosition,
                movementFacing: 0.75f,
                descriptorPosition,
                descriptorFacing: 1.25f,
                displayId: 222u));

        AssertStageSequence(
            events,
            guid,
            (WoWSharpObjectManager.TestMutationStage.MovementApplied, "cached-create"),
            (WoWSharpObjectManager.TestMutationStage.FieldsApplied, "cached-create"));

        var updated = Assert.IsType<WoWGameObject>(objectManager.GetObjectByGuid(guid));

        Assert.Same(initial, updated);
        Assert.Equal(222u, updated.DisplayId);
        Assert.Equal(descriptorPosition.X, updated.Position.X, 3);
        Assert.Equal(descriptorPosition.Y, updated.Position.Y, 3);
        Assert.Equal(descriptorPosition.Z, updated.Position.Z, 3);
        Assert.Equal(1.25f, updated.Facing, 3);
    }

    private void ResetObjectManager()
    {
        WoWSharpObjectManager.Instance.Initialize(
            _fixture._woWClient.Object,
            _fixture._pathfindingClient.Object,
            NullLogger<WoWSharpObjectManager>.Instance,
            WoWSharpEventEmitter.Instance);
        WoWSharpObjectManager.Instance.TestMutationObserver = null;
    }

    private static List<WoWSharpObjectManager.TestMutationTrace> ReplayAndCapture(byte[] packet)
    {
        var events = new List<WoWSharpObjectManager.TestMutationTrace>();
        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.TestMutationObserver = trace => events.Add(trace);
        try
        {
            ObjectUpdateHandler.HandleUpdateObject(Opcode.SMSG_UPDATE_OBJECT, packet, ctx);
            UpdateProcessingHelper.DrainPendingUpdates();
            return events;
        }
        finally
        {
            objectManager.TestMutationObserver = null;
        }
    }

    private static void AssertStageSequence(
        IEnumerable<WoWSharpObjectManager.TestMutationTrace> events,
        ulong guid,
        params (WoWSharpObjectManager.TestMutationStage stage, string context)[] expected)
    {
        var actual = events
            .Where(trace => trace.Guid == guid)
            .Select(trace => (trace.Stage, trace.Context))
            .ToArray();

        Assert.Equal(expected, actual);
    }

    private static byte[] BuildCreateObjectPacket(
        ulong guid,
        WoWObjectType objectType,
        Position movementPosition,
        float movementFacing,
        SortedDictionary<uint, object?> fields)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write(1u); // blockCount
        writer.Write((byte)0); // leading flag byte consumed by WoW.exe 0x4651A0 and our parser
        writer.Write((byte)ObjectUpdateType.CREATE_OBJECT);
        ReaderUtils.WritePackedGuid(writer, guid);
        writer.Write((byte)objectType);
        writer.Write((byte)ObjectUpdateFlags.UPDATEFLAG_HAS_POSITION);
        writer.Write(movementPosition.X);
        writer.Write(movementPosition.Y);
        writer.Write(movementPosition.Z);
        writer.Write(movementFacing);
        WriteValuesUpdateBlock(writer, fields);
        return ms.ToArray();
    }

    private static byte[] BuildCreateGameObjectPacket(
        ulong guid,
        WoWObjectType packetObjectType,
        Position movementPosition,
        float movementFacing,
        Position descriptorPosition,
        float descriptorFacing,
        uint displayId)
        => BuildCreateObjectPacket(
            guid,
            packetObjectType,
            movementPosition,
            movementFacing,
            new SortedDictionary<uint, object?>
            {
                [(uint)EObjectFields.OBJECT_FIELD_ENTRY] = 9001u,
                [(uint)EObjectFields.OBJECT_FIELD_SCALE_X] = 1.0f,
                [(uint)EGameObjectFields.GAMEOBJECT_DISPLAYID] = displayId,
                [(uint)EGameObjectFields.GAMEOBJECT_POS_X] = descriptorPosition.X,
                [(uint)EGameObjectFields.GAMEOBJECT_POS_Y] = descriptorPosition.Y,
                [(uint)EGameObjectFields.GAMEOBJECT_POS_Z] = descriptorPosition.Z,
                [(uint)EGameObjectFields.GAMEOBJECT_FACING] = descriptorFacing,
                [(uint)EGameObjectFields.GAMEOBJECT_TYPE_ID] = 5u,
            });

    private static byte[] BuildPartialThenMovementPacket(
        ulong guid,
        SortedDictionary<uint, object?> fields,
        Position movementPosition,
        float movementFacing)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write(2u); // blockCount
        writer.Write((byte)0); // leading flag byte consumed by WoW.exe 0x4651A0 and our parser

        writer.Write((byte)ObjectUpdateType.PARTIAL);
        ReaderUtils.WritePackedGuid(writer, guid);
        WriteValuesUpdateBlock(writer, fields);

        writer.Write((byte)ObjectUpdateType.MOVEMENT);
        ReaderUtils.WritePackedGuid(writer, guid);
        writer.Write(BuildMovementInfoPayload(guid, MovementFlags.MOVEFLAG_FORWARD, movementPosition, movementFacing));

        return ms.ToArray();
    }

    private static byte[] BuildMovementInfoPayload(
        ulong guid,
        MovementFlags movementFlags,
        Position position,
        float facing)
    {
        var player = new WoWLocalPlayer(new HighGuid(guid))
        {
            MovementFlags = movementFlags,
            Position = position,
            Facing = facing,
            FallTime = 0,
        };

        return MovementPacketHandler.BuildMovementInfoBuffer(
            player,
            clientTimeMs: 1234u,
            fallTimeMs: 0u);
    }

    private static void WriteValuesUpdateBlock(BinaryWriter writer, SortedDictionary<uint, object?> fields)
    {
        var maxIndex = fields.Keys.Max();
        var blockCount = checked((byte)(maxIndex / 32 + 1));
        var mask = new byte[blockCount * 4];
        foreach (var fieldIndex in fields.Keys)
        {
            mask[fieldIndex / 8] |= (byte)(1 << (int)(fieldIndex % 8));
        }

        writer.Write(blockCount);
        writer.Write(mask);

        foreach (var value in fields.Values)
        {
            switch (value)
            {
                case uint u:
                    writer.Write(u);
                    break;
                case int i:
                    writer.Write(i);
                    break;
                case float f:
                    writer.Write(f);
                    break;
                case byte[] bytes when bytes.Length == 4:
                    writer.Write(bytes);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported update-field value type: {value?.GetType().FullName ?? "null"}");
            }
        }
    }
}
