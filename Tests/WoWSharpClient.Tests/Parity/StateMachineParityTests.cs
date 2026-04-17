using System.IO;
using System.Linq;
using GameData.Core.Enums;
using GameData.Core.Models;
using Xunit;

namespace WoWSharpClient.Tests.Parity;

[Collection("Sequential ObjectManager tests")]
public sealed class StateMachineParityTests
{
    [Fact]
    [Trait("Category", "StateMachineParity")]
    public void LoginVerifyWorld_DoesNotSendWorldportAck()
    {
        using var trace = new PacketFlowTraceFixture();
        trace.SeedLocalPlayer(0x300ul, mapId: 0);

        trace.Dispatch(
            Opcode.SMSG_LOGIN_VERIFY_WORLD,
            BuildWorldInfoPacket(mapId: 1u, position: new Position(10f, 20f, 30f), facing: 1.25f));

        Assert.Equal((uint)1, trace.ObjectManager.Player.MapId);
        Assert.Empty(trace.Events.Where(e => e.Kind == "outbound" && e.Opcode == Opcode.MSG_MOVE_WORLDPORT_ACK));
    }

    [Fact]
    [Trait("Category", "StateMachineParity")]
    public void NewWorld_SendsExactlyOneWorldportAck_AfterWorldInfoUpdate()
    {
        using var trace = new PacketFlowTraceFixture();
        trace.SeedLocalPlayer(0x301ul, mapId: 0);

        trace.Dispatch(
            Opcode.SMSG_NEW_WORLD,
            BuildWorldInfoPacket(mapId: 530u, position: new Position(-50f, 75f, 12f), facing: 2.5f));

        var worldInfoIndex = trace.Events.FindIndex(e => e.Kind == "event" && e.Label == "OnLoginVerifyWorld");
        var outboundIndex = trace.Events.FindIndex(e => e.Kind == "outbound" && e.Opcode == Opcode.MSG_MOVE_WORLDPORT_ACK);

        Assert.Equal((uint)530, trace.ObjectManager.Player.MapId);
        Assert.True(worldInfoIndex >= 0);
        Assert.True(outboundIndex > worldInfoIndex);
        Assert.Single(trace.Events.Where(e => e.Kind == "outbound" && e.Opcode == Opcode.MSG_MOVE_WORLDPORT_ACK));
    }

    private static byte[] BuildWorldInfoPacket(uint mapId, Position position, float facing)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(mapId);
        writer.Write(position.X);
        writer.Write(position.Y);
        writer.Write(position.Z);
        writer.Write(facing);
        return ms.ToArray();
    }
}
