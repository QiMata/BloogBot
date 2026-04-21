using GameData.Core.Enums;
using Moq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using WoWSharpClient.Client;

namespace WoWSharpClient.Tests.Client;

public sealed class WoWClientTests
{
    [Fact]
    public async Task SendMovementOpcodeAsync_FiresMovementOpcodeSent()
    {
        var client = new WoWClient();
        var worldClient = new Mock<IWorldClient>();
        worldClient
            .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        SetPrivateField(client, "_worldClient", worldClient.Object);

        Opcode? observedOpcode = null;
        int observedSize = 0;
        client.MovementOpcodeSent += (opcode, size) =>
        {
            observedOpcode = opcode;
            observedSize = size;
        };

        var payload = new byte[] { 1, 2, 3, 4 };
        await client.SendMovementOpcodeAsync(Opcode.MSG_MOVE_START_FORWARD, payload);

        Assert.Equal(Opcode.MSG_MOVE_START_FORWARD, observedOpcode);
        Assert.Equal(payload.Length, observedSize);
    }

    [Fact]
    public async Task SendAreaTriggerAsync_SendsCmsgAreaTriggerPayload()
    {
        var client = new WoWClient();
        var worldClient = new Mock<IWorldClient>();
        Opcode? observedOpcode = null;
        byte[]? observedPayload = null;

        worldClient
            .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback<Opcode, byte[], CancellationToken>((opcode, payload, _) =>
            {
                observedOpcode = opcode;
                observedPayload = payload;
            })
            .Returns(Task.CompletedTask);

        SetPrivateField(client, "_worldClient", worldClient.Object);

        await client.SendAreaTriggerAsync(3647u);

        Assert.Equal(Opcode.CMSG_AREATRIGGER, observedOpcode);
        Assert.Equal(BitConverter.GetBytes(3647u), observedPayload);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(target, value);
    }
}
