using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WoWSharpClient.Client;
using WoWSharpClient.Tests.Handlers;
using WoWSharpClient.Utils;

namespace WoWSharpClient.Tests;

[Collection("Sequential ObjectManager tests")]
public class WoWSharpObjectManagerCombatTests
{
    private readonly ObjectManagerFixture _fixture;

    public WoWSharpObjectManagerCombatTests(ObjectManagerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CastSpell_FishingSpell_IgnoresSelectedTargetAndSendsZeroTargetFlags()
    {
        var objectManager = WoWSharpObjectManager.Instance;
        ResetObjectManager();
        SetPrivateField(_fixture._woWClient.Object, "_worldClient", CreateWorldClientRecorder(out var sentPackets).Object);
        SetPrivateField(objectManager, "_currentTargetGuid", 0x1122334455667788ul);

        objectManager.CastSpell(18248);
        await Task.Delay(25);

        var packet = Assert.Single(sentPackets);
        Assert.Equal(Opcode.CMSG_CAST_SPELL, packet.opcode);
        Assert.Equal(6, packet.payload.Length);
        Assert.Equal(18248u, BitConverter.ToUInt32(packet.payload, 0));
        Assert.Equal(0u, BitConverter.ToUInt16(packet.payload, 4));
    }

    [Fact]
    public async Task CastSpell_NonFishingSpell_WithSelectedTarget_SendsUnitTargetPayload()
    {
        const ulong targetGuid = 0x1122334455667788ul;

        var objectManager = WoWSharpObjectManager.Instance;
        ResetObjectManager();
        SetPrivateField(_fixture._woWClient.Object, "_worldClient", CreateWorldClientRecorder(out var sentPackets).Object);
        SetPrivateField(objectManager, "_currentTargetGuid", targetGuid);

        objectManager.CastSpell(133);
        await Task.Delay(25);

        var packet = Assert.Single(sentPackets);
        Assert.Equal(Opcode.CMSG_CAST_SPELL, packet.opcode);

        using var expectedMs = new MemoryStream();
        using var expectedWriter = new BinaryWriter(expectedMs);
        expectedWriter.Write(133u);
        expectedWriter.Write((ushort)0x0002);
        ReaderUtils.WritePackedGuid(expectedWriter, targetGuid);

        Assert.Equal(expectedMs.ToArray(), packet.payload);
    }

    private void ResetObjectManager()
    {
        WoWSharpObjectManager.Instance.Initialize(
            _fixture._woWClient.Object,
            _fixture._pathfindingClient.Object,
            NullLogger<WoWSharpObjectManager>.Instance);
    }

    private static Mock<IWorldClient> CreateWorldClientRecorder(out List<(Opcode opcode, byte[] payload)> sentPackets)
    {
        var packets = new List<(Opcode opcode, byte[] payload)>();
        var mockWorldClient = new Mock<IWorldClient>();
        mockWorldClient
            .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback<Opcode, byte[], CancellationToken>((opcode, payload, _) => packets.Add((opcode, (byte[])payload.Clone())))
            .Returns(Task.CompletedTask);
        sentPackets = packets;
        return mockWorldClient;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var currentType = target.GetType();
        while (currentType != null)
        {
            var field = currentType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }

            currentType = currentType.BaseType;
        }

        throw new MissingFieldException(target.GetType().FullName, fieldName);
    }
}
