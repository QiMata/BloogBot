using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents;
using WoWSharpClient.Networking.ClientComponents.I;

namespace WoWSharpClient.Tests.Agent
{
    public class FriendNetworkClientComponentTests
    {
        private readonly Mock<IWorldClient> _mockWorld;
        private readonly FriendNetworkClientComponent _agent;

        public FriendNetworkClientComponentTests()
        {
            _mockWorld = new Mock<IWorldClient>();
            _mockWorld.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                      .Returns(Task.CompletedTask);
            _agent = new FriendNetworkClientComponent(_mockWorld.Object, Mock.Of<ILogger<FriendNetworkClientComponent>>());
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullWorldClient_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new FriendNetworkClientComponent(null!, Mock.Of<ILogger<FriendNetworkClientComponent>>()));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new FriendNetworkClientComponent(_mockWorld.Object, null!));
        }

        #endregion

        #region CMSG Payload Tests

        [Fact]
        public async Task RequestFriendListAsync_SendsEmptyPayload()
        {
            await _agent.RequestFriendListAsync();

            _mockWorld.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_FRIEND_LIST,
                It.Is<byte[]>(p => p.Length == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AddFriendAsync_SendsNameAsCString()
        {
            await _agent.AddFriendAsync("Alice");

            _mockWorld.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_ADD_FRIEND,
                It.Is<byte[]>(p =>
                    p.Length == 6 && // "Alice" + null
                    p[0] == (byte)'A' &&
                    p[4] == (byte)'e' &&
                    p[5] == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RemoveFriendAsync_SendsGuidAs8Bytes()
        {
            const ulong guid = 0x123456789ABCDEF0;
            await _agent.RemoveFriendAsync(guid);

            _mockWorld.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_DEL_FRIEND,
                It.Is<byte[]>(p =>
                    p.Length == 8 &&
                    BitConverter.ToUInt64(p, 0) == guid),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region ParseFriendList Tests

        [Fact]
        public void ParseFriendList_SingleOnlineFriend_ParsesAllFields()
        {
            // uint8 count=1 + guid(8) + status(1) + area(4) + level(4) + class(4) = 22 bytes
            var payload = new byte[22];
            int offset = 0;
            payload[offset++] = 1; // count
            BitConverter.GetBytes(0xDEADBEEFUL).CopyTo(payload, offset); offset += 8;
            payload[offset++] = 1; // status = online
            BitConverter.GetBytes(12u).CopyTo(payload, offset); offset += 4; // areaId
            BitConverter.GetBytes(60u).CopyTo(payload, offset); offset += 4; // level
            BitConverter.GetBytes((uint)Class.Mage).CopyTo(payload, offset); offset += 4; // class

            var list = FriendNetworkClientComponent.ParseFriendList(payload);

            Assert.Single(list);
            var f = list[0];
            Assert.Equal(0xDEADBEEFUL, f.Guid);
            Assert.True(f.IsOnline);
            Assert.Equal((byte)1, f.Status);
            Assert.Equal(12u, f.AreaId);
            Assert.Equal(60u, f.Level);
            Assert.Equal(Class.Mage, f.Class);
        }

        [Fact]
        public void ParseFriendList_OfflineFriend_NoAreaLevelClass()
        {
            // uint8 count=1 + guid(8) + status(1) = 10 bytes (no area/level/class when offline)
            var payload = new byte[10];
            int offset = 0;
            payload[offset++] = 1;
            BitConverter.GetBytes(0x1234UL).CopyTo(payload, offset); offset += 8;
            payload[offset++] = 0; // offline

            var list = FriendNetworkClientComponent.ParseFriendList(payload);

            Assert.Single(list);
            var f = list[0];
            Assert.Equal(0x1234UL, f.Guid);
            Assert.False(f.IsOnline);
            Assert.Equal((byte)0, f.Status);
            Assert.Equal(0u, f.AreaId);
            Assert.Equal(0u, f.Level);
        }

        [Fact]
        public void ParseFriendList_AfkFriend_StatusIs2()
        {
            var payload = new byte[22];
            int offset = 0;
            payload[offset++] = 1;
            BitConverter.GetBytes(0xAAAAUL).CopyTo(payload, offset); offset += 8;
            payload[offset++] = 2; // AFK
            BitConverter.GetBytes(100u).CopyTo(payload, offset); offset += 4;
            BitConverter.GetBytes(30u).CopyTo(payload, offset); offset += 4;
            BitConverter.GetBytes((uint)Class.Warrior).CopyTo(payload, offset); offset += 4;

            var list = FriendNetworkClientComponent.ParseFriendList(payload);

            Assert.Single(list);
            Assert.True(list[0].IsOnline); // status != 0 means online
            Assert.Equal((byte)2, list[0].Status);
        }

        [Fact]
        public void ParseFriendList_MultipleFriends_MixedStatus()
        {
            // 2 friends: one online, one offline
            // online: count(1) + guid(8) + status(1) + area(4) + level(4) + class(4) = 22
            // offline: guid(8) + status(1) = 9
            // total = 1 + 21 + 9 = 31
            var payload = new byte[31];
            int offset = 0;
            payload[offset++] = 2; // count

            // Friend 1: online
            BitConverter.GetBytes(0x1111UL).CopyTo(payload, offset); offset += 8;
            payload[offset++] = 1;
            BitConverter.GetBytes(5u).CopyTo(payload, offset); offset += 4;
            BitConverter.GetBytes(40u).CopyTo(payload, offset); offset += 4;
            BitConverter.GetBytes((uint)Class.Priest).CopyTo(payload, offset); offset += 4;

            // Friend 2: offline
            BitConverter.GetBytes(0x2222UL).CopyTo(payload, offset); offset += 8;
            payload[offset++] = 0;

            var list = FriendNetworkClientComponent.ParseFriendList(payload);

            Assert.Equal(2, list.Count);
            Assert.True(list[0].IsOnline);
            Assert.Equal(0x1111UL, list[0].Guid);
            Assert.False(list[1].IsOnline);
            Assert.Equal(0x2222UL, list[1].Guid);
        }

        [Fact]
        public void ParseFriendList_EmptyPayload_ReturnsEmpty()
        {
            var list = FriendNetworkClientComponent.ParseFriendList(Array.Empty<byte>());
            Assert.Empty(list);
        }

        [Fact]
        public void ParseFriendList_ZeroCount_ReturnsEmpty()
        {
            var list = FriendNetworkClientComponent.ParseFriendList(new byte[] { 0 });
            Assert.Empty(list);
        }

        #endregion

        #region ParseFriendStatus Tests

        [Fact]
        public void ParseFriendStatus_AddedOnline_ParsesAllFields()
        {
            // result(1) + guid(8) + status(1) + area(4) + level(4) + class(4) = 22
            var payload = new byte[22];
            int offset = 0;
            payload[offset++] = (byte)FriendsResult.AddedOnline;
            BitConverter.GetBytes(0xABCDUL).CopyTo(payload, offset); offset += 8;
            payload[offset++] = 1; // online sub-status
            BitConverter.GetBytes(15u).CopyTo(payload, offset); offset += 4; // area
            BitConverter.GetBytes(20u).CopyTo(payload, offset); offset += 4; // level
            BitConverter.GetBytes((uint)Class.Rogue).CopyTo(payload, offset); offset += 4;

            var (result, entry) = FriendNetworkClientComponent.ParseFriendStatus(payload);

            Assert.Equal(FriendsResult.AddedOnline, result);
            Assert.Equal(0xABCDUL, entry.Guid);
            Assert.True(entry.IsOnline);
            Assert.Equal(15u, entry.AreaId);
            Assert.Equal(20u, entry.Level);
            Assert.Equal(Class.Rogue, entry.Class);
        }

        [Fact]
        public void ParseFriendStatus_Online_ParsesAllFields()
        {
            var payload = new byte[22];
            int offset = 0;
            payload[offset++] = (byte)FriendsResult.Online;
            BitConverter.GetBytes(0x5678UL).CopyTo(payload, offset); offset += 8;
            payload[offset++] = 2; // AFK
            BitConverter.GetBytes(50u).CopyTo(payload, offset); offset += 4;
            BitConverter.GetBytes(55u).CopyTo(payload, offset); offset += 4;
            BitConverter.GetBytes((uint)Class.Druid).CopyTo(payload, offset); offset += 4;

            var (result, entry) = FriendNetworkClientComponent.ParseFriendStatus(payload);

            Assert.Equal(FriendsResult.Online, result);
            Assert.Equal(0x5678UL, entry.Guid);
            Assert.True(entry.IsOnline);
            Assert.Equal((byte)2, entry.Status);
            Assert.Equal(55u, entry.Level);
        }

        [Fact]
        public void ParseFriendStatus_Offline_NoExtraFields()
        {
            // result(1) + guid(8) = 9 bytes only
            var payload = new byte[9];
            payload[0] = (byte)FriendsResult.Offline;
            BitConverter.GetBytes(0x9999UL).CopyTo(payload, 1);

            var (result, entry) = FriendNetworkClientComponent.ParseFriendStatus(payload);

            Assert.Equal(FriendsResult.Offline, result);
            Assert.Equal(0x9999UL, entry.Guid);
            Assert.False(entry.IsOnline);
        }

        [Fact]
        public void ParseFriendStatus_Removed_NoExtraFields()
        {
            var payload = new byte[9];
            payload[0] = (byte)FriendsResult.Removed;
            BitConverter.GetBytes(0x7777UL).CopyTo(payload, 1);

            var (result, entry) = FriendNetworkClientComponent.ParseFriendStatus(payload);

            Assert.Equal(FriendsResult.Removed, result);
            Assert.Equal(0x7777UL, entry.Guid);
        }

        [Fact]
        public void ParseFriendStatus_AddedOffline_NoExtraFields()
        {
            var payload = new byte[9];
            payload[0] = (byte)FriendsResult.AddedOffline;
            BitConverter.GetBytes(0x3333UL).CopyTo(payload, 1);

            var (result, entry) = FriendNetworkClientComponent.ParseFriendStatus(payload);

            Assert.Equal(FriendsResult.AddedOffline, result);
            Assert.Equal(0x3333UL, entry.Guid);
            Assert.False(entry.IsOnline);
        }

        [Fact]
        public void ParseFriendStatus_TooShort_ReturnsDefaults()
        {
            var (result, entry) = FriendNetworkClientComponent.ParseFriendStatus(new byte[4]);

            Assert.Equal(FriendsResult.DbError, result);
            Assert.Equal(0UL, entry.Guid);
        }

        #endregion

        #region HandleServerResponse Integration Tests

        [Fact]
        public void HandleFriendList_UpdatesFriendsCollectionAndFlag()
        {
            var payload = new byte[22];
            int offset = 0;
            payload[offset++] = 1;
            BitConverter.GetBytes(0xBEEFUL).CopyTo(payload, offset); offset += 8;
            payload[offset++] = 1; // online
            BitConverter.GetBytes(1u).CopyTo(payload, offset); offset += 4;
            BitConverter.GetBytes(10u).CopyTo(payload, offset); offset += 4;
            BitConverter.GetBytes((uint)Class.Warrior).CopyTo(payload, offset);

            _agent.HandleServerResponse(Opcode.SMSG_FRIEND_LIST, payload);

            Assert.True(_agent.IsFriendListInitialized);
            Assert.Single(_agent.Friends);
            Assert.Equal(0xBEEFUL, _agent.Friends[0].Guid);
            Assert.True(_agent.Friends[0].IsOnline);
            Assert.Equal(10u, _agent.Friends[0].Level);
        }

        [Fact]
        public void HandleFriendStatus_AddedOnline_AddsFriendToList()
        {
            var payload = new byte[22];
            int offset = 0;
            payload[offset++] = (byte)FriendsResult.AddedOnline;
            BitConverter.GetBytes(0xAABBUL).CopyTo(payload, offset); offset += 8;
            payload[offset++] = 1;
            BitConverter.GetBytes(5u).CopyTo(payload, offset); offset += 4;
            BitConverter.GetBytes(25u).CopyTo(payload, offset); offset += 4;
            BitConverter.GetBytes((uint)Class.Paladin).CopyTo(payload, offset);

            _agent.HandleServerResponse(Opcode.SMSG_FRIEND_STATUS, payload);

            Assert.Single(_agent.Friends);
            Assert.Equal(0xAABBUL, _agent.Friends[0].Guid);
        }

        [Fact]
        public void HandleFriendStatus_Removed_RemovesFriendFromList()
        {
            // First add a friend via list
            var listPayload = new byte[10];
            listPayload[0] = 1;
            BitConverter.GetBytes(0xDEADUL).CopyTo(listPayload, 1);
            listPayload[9] = 0; // offline
            _agent.HandleServerResponse(Opcode.SMSG_FRIEND_LIST, listPayload);
            Assert.Single(_agent.Friends);

            // Now remove via status
            var statusPayload = new byte[9];
            statusPayload[0] = (byte)FriendsResult.Removed;
            BitConverter.GetBytes(0xDEADUL).CopyTo(statusPayload, 1);
            _agent.HandleServerResponse(Opcode.SMSG_FRIEND_STATUS, statusPayload);

            Assert.Empty(_agent.Friends);
        }

        [Fact]
        public void HandleFriendStatus_Online_UpdatesExistingEntry()
        {
            // Add friend via list (offline)
            var listPayload = new byte[10];
            listPayload[0] = 1;
            BitConverter.GetBytes(0x5555UL).CopyTo(listPayload, 1);
            listPayload[9] = 0;
            _agent.HandleServerResponse(Opcode.SMSG_FRIEND_LIST, listPayload);
            Assert.False(_agent.Friends[0].IsOnline);

            // Now online update
            var statusPayload = new byte[22];
            int offset = 0;
            statusPayload[offset++] = (byte)FriendsResult.Online;
            BitConverter.GetBytes(0x5555UL).CopyTo(statusPayload, offset); offset += 8;
            statusPayload[offset++] = 1;
            BitConverter.GetBytes(99u).CopyTo(statusPayload, offset); offset += 4;
            BitConverter.GetBytes(45u).CopyTo(statusPayload, offset); offset += 4;
            BitConverter.GetBytes((uint)Class.Hunter).CopyTo(statusPayload, offset);

            _agent.HandleServerResponse(Opcode.SMSG_FRIEND_STATUS, statusPayload);

            Assert.Single(_agent.Friends);
            Assert.True(_agent.Friends[0].IsOnline);
            Assert.Equal(45u, _agent.Friends[0].Level);
            Assert.Equal(Class.Hunter, _agent.Friends[0].Class);
        }

        #endregion

        #region FriendsResult Enum Tests

        [Fact]
        public void FriendsResult_EnumValues_MatchMaNGOS()
        {
            Assert.Equal(0x00, (byte)FriendsResult.DbError);
            Assert.Equal(0x01, (byte)FriendsResult.ListFull);
            Assert.Equal(0x02, (byte)FriendsResult.Online);
            Assert.Equal(0x03, (byte)FriendsResult.Offline);
            Assert.Equal(0x04, (byte)FriendsResult.NotFound);
            Assert.Equal(0x05, (byte)FriendsResult.Removed);
            Assert.Equal(0x06, (byte)FriendsResult.AddedOnline);
            Assert.Equal(0x07, (byte)FriendsResult.AddedOffline);
            Assert.Equal(0x08, (byte)FriendsResult.Already);
            Assert.Equal(0x09, (byte)FriendsResult.Self);
            Assert.Equal(0x0A, (byte)FriendsResult.Enemy);
            Assert.Equal(0x0F, (byte)FriendsResult.IgnoreAdded);
            Assert.Equal(0x10, (byte)FriendsResult.IgnoreRemoved);
        }

        #endregion
    }
}
