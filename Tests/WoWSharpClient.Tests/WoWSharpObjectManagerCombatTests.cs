using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using GameData.Core.Enums;
using GameData.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WoWSharpClient.Client;
using WoWSharpClient.Models;
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
    public async Task CastSpell_FishingSpell_IgnoresSelectedTargetAndSendsNoTargetPayload()
    {
        var objectManager = WoWSharpObjectManager.Instance;
        ResetObjectManager();
        SetPrivateField(_fixture._woWClient.Object, "_worldClient", CreateWorldClientRecorder(out var sentPackets).Object);
        objectManager.Player = new WoWLocalPlayer(new HighGuid(0x10))
        {
            Position = new Position(100f, 200f, 50f),
            Facing = 0f,
        };
        SetCurrentTargetGuid(objectManager, 0x1122334455667788ul);

        objectManager.CastSpell(18248);
        await Task.Delay(25);

        var packet = Assert.Single(sentPackets);
        Assert.Equal(Opcode.CMSG_CAST_SPELL, packet.opcode);
        Assert.Equal(6, packet.payload.Length);
        Assert.Equal(18248u, BitConverter.ToUInt32(packet.payload, 0));
        Assert.Equal(0x0000u, BitConverter.ToUInt16(packet.payload, 4));
    }

    [Fact]
    public async Task CastSpell_NonFishingSpell_WithSelectedTarget_SendsUnitTargetPayload()
    {
        const ulong targetGuid = 0x1122334455667788ul;

        var objectManager = WoWSharpObjectManager.Instance;
        ResetObjectManager();
        SetPrivateField(_fixture._woWClient.Object, "_worldClient", CreateWorldClientRecorder(out var sentPackets).Object);
        SetCurrentTargetGuid(objectManager, targetGuid);

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

    [Fact]
    public async Task StartWandAttack_WithSelectedTarget_SendsShootSpellAtUnit()
    {
        const ulong targetGuid = 0x1122334455667788ul;

        var objectManager = WoWSharpObjectManager.Instance;
        ResetObjectManager();
        SetPrivateField(_fixture._woWClient.Object, "_worldClient", CreateWorldClientRecorder(out var sentPackets).Object);
        SetCurrentTargetGuid(objectManager, targetGuid);

        objectManager.StartWandAttack();
        await Task.Delay(25);

        var packet = Assert.Single(sentPackets);
        Assert.Equal(Opcode.CMSG_CAST_SPELL, packet.opcode);

        using var expectedMs = new MemoryStream();
        using var expectedWriter = new BinaryWriter(expectedMs);
        expectedWriter.Write(5019u);
        expectedWriter.Write((ushort)0x0002);
        ReaderUtils.WritePackedGuid(expectedWriter, targetGuid);

        Assert.Equal(expectedMs.ToArray(), packet.payload);
    }

    [Fact]
    public async Task StartMeleeAttack_ConfirmedSameTarget_DoesNotResendAttackSwing()
    {
        const ulong playerGuid = 0x10;
        const ulong targetGuid = 0x1234;

        var objectManager = WoWSharpObjectManager.Instance;
        ResetObjectManager();
        SetPrivateField(_fixture._woWClient.Object, "_worldClient", CreateWorldClientRecorder(out var sentPackets).Object);

        var localPlayer = new WoWLocalPlayer(new HighGuid(playerGuid))
        {
            Position = new Position(1, 2, 3),
            Facing = 0.5f,
            IsAutoAttacking = true,
            TargetGuid = targetGuid,
        };

        objectManager.Player = localPlayer;
        objectManager.ClearTrackedMeleeAttackState();
        objectManager.ConfirmMeleeAttackStarted(targetGuid);
        SetCurrentTargetGuid(objectManager, targetGuid);

        objectManager.StartMeleeAttack();
        await Task.Delay(25);

        Assert.True(objectManager.HasConfirmedMeleeAttackStart(targetGuid));
        Assert.DoesNotContain(sentPackets, packet => packet.opcode == Opcode.CMSG_ATTACKSWING);
        Assert.DoesNotContain(sentPackets, packet => packet.opcode == Opcode.MSG_MOVE_HEARTBEAT);
    }

    [Fact]
    public async Task StartMeleeAttack_PendingExpiredWithoutConfirmation_RetriesAttackSwing()
    {
        const ulong playerGuid = 0x10;
        const ulong targetGuid = 0x1234;

        var objectManager = WoWSharpObjectManager.Instance;
        ResetObjectManager();
        SetPrivateField(_fixture._woWClient.Object, "_worldClient", CreateWorldClientRecorder(out var sentPackets).Object);

        var localPlayer = new WoWLocalPlayer(new HighGuid(playerGuid))
        {
            Position = new Position(10, 20, 30),
            Facing = 1.25f,
            IsAutoAttacking = true,
            TargetGuid = targetGuid,
        };

        objectManager.Player = localPlayer;
        objectManager.ClearTrackedMeleeAttackState();
        objectManager.NotePendingMeleeAttackStart(targetGuid);
        SetPrivateField(objectManager, "_worldTimeTracker", new WorldTimeTracker());
        SetCurrentTargetGuid(objectManager, targetGuid);
        SetPrivateField(GetPrivateField<object>(objectManager, "_spellcasting"), "_pendingMeleeAttackConfirmUntilTicks", DateTime.UtcNow.Ticks - 1L);

        objectManager.StartMeleeAttack();
        await Task.Delay(25);

        Assert.Single(sentPackets, packet => packet.opcode == Opcode.CMSG_ATTACKSWING);
        Assert.True(objectManager.HasPendingMeleeAttackStart(targetGuid));
        Assert.False(objectManager.HasConfirmedMeleeAttackStart(targetGuid));
    }

    [Fact]
    public async Task StartMeleeAttack_AfterStopAttack_CanSendAgain()
    {
        const ulong playerGuid = 0x10;
        const ulong targetGuid = 0x1234;

        var objectManager = WoWSharpObjectManager.Instance;
        ResetObjectManager();
        SetPrivateField(_fixture._woWClient.Object, "_worldClient", CreateWorldClientRecorder(out var sentPackets).Object);

        var localPlayer = new WoWLocalPlayer(new HighGuid(playerGuid))
        {
            Position = new Position(5, 6, 7),
            Facing = 0.25f,
            IsAutoAttacking = true,
            TargetGuid = targetGuid,
        };

        objectManager.Player = localPlayer;
        objectManager.ClearTrackedMeleeAttackState();
        objectManager.ConfirmMeleeAttackStarted(targetGuid);
        SetCurrentTargetGuid(objectManager, targetGuid);

        objectManager.StopAttack();
        objectManager.StartMeleeAttack();
        await Task.Delay(25);

        Assert.False(objectManager.HasConfirmedMeleeAttackStart(targetGuid));
        Assert.True(objectManager.HasPendingMeleeAttackStart(targetGuid));
        Assert.Single(sentPackets, packet => packet.opcode == Opcode.CMSG_ATTACKSTOP);
        Assert.Single(sentPackets, packet => packet.opcode == Opcode.CMSG_ATTACKSWING);
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

    private static void SetCurrentTargetGuid(WoWSharpObjectManager objectManager, ulong targetGuid)
    {
        var spellcasting = GetPrivateField<object>(objectManager, "_spellcasting");
        SetPrivateField(spellcasting, "CurrentTargetGuid", targetGuid);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        var currentType = target.GetType();
        while (currentType != null)
        {
            var field = currentType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null)
            {
                return (T)field.GetValue(target)!;
            }

            currentType = currentType.BaseType;
        }

        throw new MissingFieldException(target.GetType().FullName, fieldName);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var currentType = target.GetType();
        while (currentType != null)
        {
            var field = currentType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
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
