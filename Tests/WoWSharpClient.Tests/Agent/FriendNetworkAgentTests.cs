using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents;
using Xunit;

namespace WoWSharpClient.Tests.Agent
{
    public class FriendNetworkClientComponentTests
    {
        private static ILogger<T> CreateLogger<T>() => Mock.Of<ILogger<T>>();

        [Fact]
        public async Task FriendAgent_RequestAddRemove_SendsOpcodes()
        {
            var mockWorld = new Mock<IWorldClient>();
            mockWorld.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                     .Returns(Task.CompletedTask);

            var agent = new FriendNetworkClientComponent(mockWorld.Object, CreateLogger<FriendNetworkClientComponent>());

            await agent.RequestFriendListAsync();
            await agent.AddFriendAsync("Test");
            await agent.RemoveFriendAsync("Test");

            mockWorld.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_FRIEND_LIST,
                It.Is<byte[]>(p => p.Length == 0), It.IsAny<CancellationToken>()), Times.Once);

            mockWorld.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_ADD_FRIEND,
                It.Is<byte[]>(p => p.Last() == 0), It.IsAny<CancellationToken>()), Times.Once);

            mockWorld.Verify(x => x.SendOpcodeAsync(Opcode.CMSG_DEL_FRIEND,
                It.Is<byte[]>(p => p.Last() == 0), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void FriendAgent_ParsesFriendListAndStatus()
        {
            var mockWorld = new Mock<IWorldClient>();
            var agent = new FriendNetworkClientComponent(mockWorld.Object, CreateLogger<FriendNetworkClientComponent>());

            // Build a fake list: count=1, name="Alice", status=1 (online), level=10, class=Warrior, area="Elwynn\0"
            byte[] payload;
            {
                var name = System.Text.Encoding.UTF8.GetBytes("Alice");
                var area = System.Text.Encoding.UTF8.GetBytes("Elwynn");
                var buffer = new System.IO.MemoryStream();
                using var bw = new System.IO.BinaryWriter(buffer);
                bw.Write(1u); // count
                bw.Write(name); bw.Write((byte)0);
                bw.Write((byte)1); // online
                bw.Write((byte)10); // level
                bw.Write((byte)Class.Warrior);
                bw.Write(area); bw.Write((byte)0);
                payload = buffer.ToArray();
            }

            agent.HandleServerResponse(Opcode.SMSG_FRIEND_LIST, payload);
            Assert.True(agent.IsFriendListInitialized);
            Assert.Single(agent.Friends);
            var f = agent.Friends[0];
            Assert.Equal("Alice", f.Name);
            Assert.True(f.IsOnline);
            Assert.Equal((uint)10, f.Level);
            Assert.Equal(Class.Warrior, f.Class);
            Assert.Equal("Elwynn", f.Area);

            // Status update: Online for Alice with new area
            {
                var name = System.Text.Encoding.UTF8.GetBytes("Alice");
                var area = System.Text.Encoding.UTF8.GetBytes("Stormwind");
                var buffer = new System.IO.MemoryStream();
                using var bw = new System.IO.BinaryWriter(buffer);
                bw.Write((byte)3); // Online code
                bw.Write(name); bw.Write((byte)0);
                bw.Write((byte)11); // level
                bw.Write((byte)Class.Warrior);
                bw.Write(area); bw.Write((byte)0);
                agent.HandleServerResponse(Opcode.SMSG_FRIEND_STATUS, buffer.ToArray());
            }

            f = agent.Friends[0];
            Assert.True(f.IsOnline);
            Assert.Equal((uint)11, f.Level);
            Assert.Equal("Stormwind", f.Area);
        }
    }
}
