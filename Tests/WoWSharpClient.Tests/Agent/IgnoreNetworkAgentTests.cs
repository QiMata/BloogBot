using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents;

namespace WoWSharpClient.Tests.Agent
{
    public class IgnoreNetworkClientComponentTests
    {
        private readonly Mock<IWorldClient> _mockWorld;
        private readonly IgnoreNetworkClientComponent _agent;

        public IgnoreNetworkClientComponentTests()
        {
            _mockWorld = new Mock<IWorldClient>();
            _mockWorld.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                      .Returns(Task.CompletedTask);
            _agent = new IgnoreNetworkClientComponent(_mockWorld.Object, Mock.Of<ILogger<IgnoreNetworkClientComponent>>());
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullWorldClient_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new IgnoreNetworkClientComponent(null!, Mock.Of<ILogger<IgnoreNetworkClientComponent>>()));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new IgnoreNetworkClientComponent(_mockWorld.Object, null!));
        }

        [Fact]
        public void Constructor_InitialState_EmptyAndNotInitialized()
        {
            Assert.Empty(_agent.IgnoredPlayers);
            Assert.False(_agent.IsIgnoreListInitialized);
        }

        #endregion

        #region CMSG Payload Tests

        [Fact]
        public async Task RequestIgnoreListAsync_SendsEmptyPayloadViaCMSG_FRIEND_LIST()
        {
            await _agent.RequestIgnoreListAsync();

            _mockWorld.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_FRIEND_LIST,
                It.Is<byte[]>(p => p.Length == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AddIgnoreAsync_SendsNameAsCString()
        {
            await _agent.AddIgnoreAsync("Troll");

            _mockWorld.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_ADD_IGNORE,
                It.Is<byte[]>(p =>
                    p.Length == 6 && // "Troll" + null
                    p[0] == (byte)'T' &&
                    p[4] == (byte)'l' &&
                    p[5] == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RemoveIgnoreAsync_SendsGuidAs8Bytes()
        {
            const ulong guid = 0xFEDCBA9876543210;
            await _agent.RemoveIgnoreAsync(guid);

            _mockWorld.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_DEL_IGNORE,
                It.Is<byte[]>(p =>
                    p.Length == 8 &&
                    BitConverter.ToUInt64(p, 0) == guid),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region ParseIgnoreList Tests

        [Fact]
        public void ParseIgnoreList_SingleGuid_ParsesCorrectly()
        {
            // uint8 count=1 + guid(8) = 9 bytes
            var payload = new byte[9];
            payload[0] = 1;
            BitConverter.GetBytes(0xDEADBEEFUL).CopyTo(payload, 1);

            var list = IgnoreNetworkClientComponent.ParseIgnoreList(payload);

            Assert.Single(list);
            Assert.Equal(0xDEADBEEFUL, list[0]);
        }

        [Fact]
        public void ParseIgnoreList_MultipleGuids_ParsesAll()
        {
            // uint8 count=3 + 3*guid(8) = 25 bytes
            var payload = new byte[25];
            payload[0] = 3;
            BitConverter.GetBytes(0x1111UL).CopyTo(payload, 1);
            BitConverter.GetBytes(0x2222UL).CopyTo(payload, 9);
            BitConverter.GetBytes(0x3333UL).CopyTo(payload, 17);

            var list = IgnoreNetworkClientComponent.ParseIgnoreList(payload);

            Assert.Equal(3, list.Count);
            Assert.Equal(0x1111UL, list[0]);
            Assert.Equal(0x2222UL, list[1]);
            Assert.Equal(0x3333UL, list[2]);
        }

        [Fact]
        public void ParseIgnoreList_ZeroCount_ReturnsEmpty()
        {
            var list = IgnoreNetworkClientComponent.ParseIgnoreList(new byte[] { 0 });
            Assert.Empty(list);
        }

        [Fact]
        public void ParseIgnoreList_EmptyPayload_ReturnsEmpty()
        {
            var list = IgnoreNetworkClientComponent.ParseIgnoreList(Array.Empty<byte>());
            Assert.Empty(list);
        }

        [Fact]
        public void ParseIgnoreList_TruncatedPayload_ParsesAvailableEntries()
        {
            // count=2 but only 1 full guid (12 bytes instead of 17)
            var payload = new byte[12];
            payload[0] = 2;
            BitConverter.GetBytes(0xAAAAUL).CopyTo(payload, 1);
            // Second guid truncated (only 3 bytes)

            var list = IgnoreNetworkClientComponent.ParseIgnoreList(payload);

            Assert.Single(list); // Only first entry parsed
            Assert.Equal(0xAAAAUL, list[0]);
        }

        [Fact]
        public void ParseIgnoreList_MaxIgnores_25Entries()
        {
            // MaNGOS limit: SOCIALMGR_IGNORE_LIMIT = 25
            int count = 25;
            var payload = new byte[1 + count * 8];
            payload[0] = (byte)count;
            for (int i = 0; i < count; i++)
            {
                BitConverter.GetBytes((ulong)(i + 100)).CopyTo(payload, 1 + i * 8);
            }

            var list = IgnoreNetworkClientComponent.ParseIgnoreList(payload);

            Assert.Equal(25, list.Count);
            Assert.Equal(100UL, list[0]);
            Assert.Equal(124UL, list[24]);
        }

        #endregion

        #region HandleServerResponse Integration Tests

        [Fact]
        public void HandleServerResponse_SMSG_IGNORE_LIST_UpdatesState()
        {
            var payload = new byte[17];
            payload[0] = 2;
            BitConverter.GetBytes(0xBBBBUL).CopyTo(payload, 1);
            BitConverter.GetBytes(0xCCCCUL).CopyTo(payload, 9);

            _agent.HandleServerResponse(Opcode.SMSG_IGNORE_LIST, payload);

            Assert.True(_agent.IsIgnoreListInitialized);
            Assert.Equal(2, _agent.IgnoredPlayers.Count);
            Assert.Equal(0xBBBBUL, _agent.IgnoredPlayers[0]);
            Assert.Equal(0xCCCCUL, _agent.IgnoredPlayers[1]);
        }

        [Fact]
        public void HandleServerResponse_ReplacesOldList()
        {
            // First list: 1 entry
            var payload1 = new byte[9];
            payload1[0] = 1;
            BitConverter.GetBytes(0x1111UL).CopyTo(payload1, 1);
            _agent.HandleServerResponse(Opcode.SMSG_IGNORE_LIST, payload1);
            Assert.Single(_agent.IgnoredPlayers);

            // Second list: 2 different entries
            var payload2 = new byte[17];
            payload2[0] = 2;
            BitConverter.GetBytes(0x4444UL).CopyTo(payload2, 1);
            BitConverter.GetBytes(0x5555UL).CopyTo(payload2, 9);
            _agent.HandleServerResponse(Opcode.SMSG_IGNORE_LIST, payload2);

            Assert.Equal(2, _agent.IgnoredPlayers.Count);
            Assert.Equal(0x4444UL, _agent.IgnoredPlayers[0]);
            Assert.Equal(0x5555UL, _agent.IgnoredPlayers[1]);
        }

        #endregion
    }
}
