using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameData.Core.Enums;
using GameData.Core.Models;
using WoWSharpClient.Handlers;
using WoWSharpClient.Models;
using WoWSharpClient.Tests.Util;
using WoWSharpClient.Utils;
using static GameData.Core.Enums.UpdateFields;

namespace WoWSharpClient.Tests.Handlers;

[Collection("Sequential ObjectManager tests")]
public class ObjectUpdateMutationOrderTests(ObjectManagerFixture _) : IClassFixture<ObjectManagerFixture>
{
    private static readonly HandlerContext ctx = new(WoWSharpObjectManager.Instance, WoWSharpEventEmitter.Instance);

    [Fact]
    public void DuplicateCreateObjectBlock_MutatesCachedGameObjectInPlace_AndDescriptorFieldsWinAfterMovementPrepass()
    {
        const ulong guid = 0xF11000000000D00Dul;
        var objectManager = WoWSharpObjectManager.Instance;

        ObjectUpdateHandler.HandleUpdateObject(
            Opcode.SMSG_UPDATE_OBJECT,
            BuildCreateGameObjectPacket(
                guid,
                movementPosition: new Position(5f, 6f, 7f),
                movementFacing: 0.25f,
                descriptorPosition: new Position(5f, 6f, 7f),
                descriptorFacing: 0.25f,
                displayId: 111u),
            ctx);
        UpdateProcessingHelper.DrainPendingUpdates();

        var initial = Assert.IsType<WoWGameObject>(objectManager.GetObjectByGuid(guid));
        Assert.Equal(111u, initial.DisplayId);

        var movementPosition = new Position(10f, 20f, 30f);
        var descriptorPosition = new Position(40f, 50f, 60f);
        ObjectUpdateHandler.HandleUpdateObject(
            Opcode.SMSG_UPDATE_OBJECT,
            BuildCreateGameObjectPacket(
                guid,
                movementPosition,
                movementFacing: 0.75f,
                descriptorPosition,
                descriptorFacing: 1.25f,
                displayId: 222u),
            ctx);
        UpdateProcessingHelper.DrainPendingUpdates();

        var updated = Assert.IsType<WoWGameObject>(objectManager.GetObjectByGuid(guid));

        Assert.Same(initial, updated);
        Assert.Equal(222u, updated.DisplayId);
        Assert.Equal(descriptorPosition.X, updated.Position.X, 3);
        Assert.Equal(descriptorPosition.Y, updated.Position.Y, 3);
        Assert.Equal(descriptorPosition.Z, updated.Position.Z, 3);
        Assert.Equal(1.25f, updated.Facing, 3);
    }

    private static byte[] BuildCreateGameObjectPacket(
        ulong guid,
        Position movementPosition,
        float movementFacing,
        Position descriptorPosition,
        float descriptorFacing,
        uint displayId)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write(1u); // blockCount
        writer.Write((byte)0); // leading flag byte consumed by WoW.exe 0x4651A0 and our parser
        writer.Write((byte)ObjectUpdateType.CREATE_OBJECT);
        ReaderUtils.WritePackedGuid(writer, guid);
        writer.Write((byte)WoWObjectType.GameObj);
        writer.Write((byte)ObjectUpdateFlags.UPDATEFLAG_HAS_POSITION);
        writer.Write(movementPosition.X);
        writer.Write(movementPosition.Y);
        writer.Write(movementPosition.Z);
        writer.Write(movementFacing);

        var fields = new SortedDictionary<uint, object?>
        {
            [(uint)EObjectFields.OBJECT_FIELD_ENTRY] = 9001u,
            [(uint)EObjectFields.OBJECT_FIELD_SCALE_X] = 1.0f,
            [(uint)EGameObjectFields.GAMEOBJECT_DISPLAYID] = displayId,
            [(uint)EGameObjectFields.GAMEOBJECT_POS_X] = descriptorPosition.X,
            [(uint)EGameObjectFields.GAMEOBJECT_POS_Y] = descriptorPosition.Y,
            [(uint)EGameObjectFields.GAMEOBJECT_POS_Z] = descriptorPosition.Z,
            [(uint)EGameObjectFields.GAMEOBJECT_FACING] = descriptorFacing,
            [(uint)EGameObjectFields.GAMEOBJECT_TYPE_ID] = 5u,
        };

        WriteValuesUpdateBlock(writer, fields);
        return ms.ToArray();
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
