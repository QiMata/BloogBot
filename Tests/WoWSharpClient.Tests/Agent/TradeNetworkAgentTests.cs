using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents;

namespace WoWSharpClient.Tests.Agent
{
    public class TradeNetworkClientComponentTests
    {
        private static ILogger<T> CreateLogger<T>() => Mock.Of<ILogger<T>>();

        [Fact]
        public async Task TradeAgent_SendsOpcodes_AndParsesTradeStatus()
        {
            var mockWorld = new Mock<IWorldClient>();
            mockWorld.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                     .Returns(Task.CompletedTask);

            var agent = new TradeNetworkClientComponent(mockWorld.Object, CreateLogger<TradeNetworkClientComponent>());

            await agent.InitiateTradeAsync(0x1122334455667788UL);
            await agent.AcceptTradeAsync();
            await agent.UnacceptTradeAsync();
            await agent.CancelTradeAsync();
            await agent.OfferMoneyAsync(1234);
            await agent.OfferItemAsync(1, 2, 3, 4);
            await agent.ClearOfferedItemAsync(4);

            mockWorld.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_INITIATE_TRADE,
                It.Is<byte[]>(p => p.Length == 8), It.IsAny<CancellationToken>()), Times.Once);
            mockWorld.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_ACCEPT_TRADE,
                It.Is<byte[]>(p => p.Length == 0), It.IsAny<CancellationToken>()), Times.Once);
            mockWorld.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_UNACCEPT_TRADE,
                It.Is<byte[]>(p => p.Length == 0), It.IsAny<CancellationToken>()), Times.Once);
            mockWorld.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_CANCEL_TRADE,
                It.Is<byte[]>(p => p.Length == 0), It.IsAny<CancellationToken>()), Times.Once);
            mockWorld.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_SET_TRADE_GOLD,
                It.Is<byte[]>(p => p.Length == 4 && BitConverter.ToUInt32(p,0)==1234), It.IsAny<CancellationToken>()), Times.Once);
            mockWorld.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_SET_TRADE_ITEM,
                It.Is<byte[]>(p => p.Length == 4 && p[0]==1 && p[1]==2 && p[2]==4 && p[3]==3), It.IsAny<CancellationToken>()), Times.Once);
            mockWorld.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_CLEAR_TRADE_ITEM,
                It.Is<byte[]>(p => p.Length == 1 && p[0]==4), It.IsAny<CancellationToken>()), Times.Once);

            bool opened = false, closed = false; uint money = 0;
            agent.TradeOpened += () => opened = true;
            agent.TradeClosed += () => closed = true;
            agent.MoneyOfferedChanged += v => money = v;

            // Simulate Open
            agent.HandleServerResponse(Opcode.SMSG_TRADE_STATUS, new byte[]{1});
            Assert.True(opened);

            // Simulate Money change
            {
                var buffer = new System.IO.MemoryStream();
                using var bw = new System.IO.BinaryWriter(buffer);
                bw.Write((byte)3); // money-changed code (heuristic)
                bw.Write(777u);
                agent.HandleServerResponse(Opcode.SMSG_TRADE_STATUS, buffer.ToArray());
            }
            Assert.Equal((uint)777, money);

            // Simulate Close
            agent.HandleServerResponse(Opcode.SMSG_TRADE_STATUS, new byte[]{2});
            Assert.True(closed);
        }
    }
}
