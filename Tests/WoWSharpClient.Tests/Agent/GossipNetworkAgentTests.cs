using System.Buffers.Binary;
using System.Reactive.Subjects;
using System.Text;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Tests.Agent
{
    public class GossipNetworkClientComponentTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILogger<GossipNetworkClientComponent>> _mockLogger;
        private readonly Dictionary<Opcode, Subject<ReadOnlyMemory<byte>>> _opcodeSubjects = new();
        private readonly GossipNetworkClientComponent _gossipAgent;

        public GossipNetworkClientComponentTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLogger = new Mock<ILogger<GossipNetworkClientComponent>>();

            _mockWorldClient
                .Setup(x => x.RegisterOpcodeHandler(It.IsAny<Opcode>()))
                .Returns((Opcode op) =>
                {
                    if (!_opcodeSubjects.TryGetValue(op, out var subj))
                    {
                        subj = new Subject<ReadOnlyMemory<byte>>();
                        _opcodeSubjects[op] = subj;
                    }
                    return subj;
                });

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _gossipAgent = new GossipNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
        }

        private Subject<ReadOnlyMemory<byte>> GetSubject(Opcode op)
        {
            if (!_opcodeSubjects.TryGetValue(op, out var subj))
            {
                subj = new Subject<ReadOnlyMemory<byte>>();
                _opcodeSubjects[op] = subj;
            }
            return subj;
        }

        #region Payload Builders

        /// <summary>
        /// Builds an SMSG_GOSSIP_MESSAGE payload in MaNGOS 1.12.1 format:
        /// npcGuid(8) + textId(4) + menuId(4) + gossipCount(4)
        /// + [optIndex(4) + icon(1) + coded(1) + boxMoney(4) + text\0 + boxText\0] * gossipCount
        /// + questCount(4)
        /// + [questId(4) + questIcon(4) + questLevel(4) + questTitle\0] * questCount
        /// </summary>
        private static byte[] BuildGossipMessagePayload(
            ulong npcGuid, uint textId, uint menuId,
            (uint OptIndex, byte Icon, byte Coded, uint BoxMoney, string Text, string BoxText)[]? gossipOptions = null,
            (uint QuestId, uint QuestIcon, uint QuestLevel, string QuestTitle)[]? questOptions = null)
        {
            gossipOptions ??= [];
            questOptions ??= [];

            using var ms = new MemoryStream();
            var buf = new byte[8];

            // npcGuid(8)
            BinaryPrimitives.WriteUInt64LittleEndian(buf, npcGuid);
            ms.Write(buf, 0, 8);
            // textId(4)
            BinaryPrimitives.WriteUInt32LittleEndian(buf, textId);
            ms.Write(buf, 0, 4);
            // menuId(4)
            BinaryPrimitives.WriteUInt32LittleEndian(buf, menuId);
            ms.Write(buf, 0, 4);
            // gossipCount(4)
            BinaryPrimitives.WriteUInt32LittleEndian(buf, (uint)gossipOptions.Length);
            ms.Write(buf, 0, 4);

            foreach (var opt in gossipOptions)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(buf, opt.OptIndex);
                ms.Write(buf, 0, 4);
                ms.WriteByte(opt.Icon);
                ms.WriteByte(opt.Coded);
                BinaryPrimitives.WriteUInt32LittleEndian(buf, opt.BoxMoney);
                ms.Write(buf, 0, 4);
                var textBytes = Encoding.UTF8.GetBytes(opt.Text);
                ms.Write(textBytes, 0, textBytes.Length);
                ms.WriteByte(0); // null terminator
                var boxTextBytes = Encoding.UTF8.GetBytes(opt.BoxText);
                ms.Write(boxTextBytes, 0, boxTextBytes.Length);
                ms.WriteByte(0); // null terminator
            }

            // questCount(4)
            BinaryPrimitives.WriteUInt32LittleEndian(buf, (uint)questOptions.Length);
            ms.Write(buf, 0, 4);

            foreach (var quest in questOptions)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(buf, quest.QuestId);
                ms.Write(buf, 0, 4);
                BinaryPrimitives.WriteUInt32LittleEndian(buf, quest.QuestIcon);
                ms.Write(buf, 0, 4);
                BinaryPrimitives.WriteUInt32LittleEndian(buf, quest.QuestLevel);
                ms.Write(buf, 0, 4);
                var titleBytes = Encoding.UTF8.GetBytes(quest.QuestTitle);
                ms.Write(titleBytes, 0, titleBytes.Length);
                ms.WriteByte(0); // null terminator
            }

            return ms.ToArray();
        }

        /// <summary>
        /// Builds an SMSG_NPC_TEXT_UPDATE payload in MaNGOS 1.12.1 format:
        /// textId(4) + 8 × [probability(float4) + text0\0 + text1\0 + lang(4) + 3×[emoteDelay(4)+emote(4)]]
        /// </summary>
        private static byte[] BuildNpcTextUpdatePayload(uint textId, string text0, string text1 = "")
        {
            using var ms = new MemoryStream();
            var buf = new byte[4];

            // textId(4)
            BinaryPrimitives.WriteUInt32LittleEndian(buf, textId);
            ms.Write(buf, 0, 4);

            for (int i = 0; i < 8; i++)
            {
                // probability(float4)
                float prob = i == 0 ? 1.0f : 0.0f;
                var probBytes = BitConverter.GetBytes(prob);
                ms.Write(probBytes, 0, 4);

                // text0\0
                var t0 = i == 0 ? text0 : "";
                var t0Bytes = Encoding.UTF8.GetBytes(t0);
                ms.Write(t0Bytes, 0, t0Bytes.Length);
                ms.WriteByte(0);

                // text1\0
                var t1 = i == 0 ? text1 : "";
                var t1Bytes = Encoding.UTF8.GetBytes(t1);
                ms.Write(t1Bytes, 0, t1Bytes.Length);
                ms.WriteByte(0);

                // lang(4)
                BinaryPrimitives.WriteUInt32LittleEndian(buf, 0);
                ms.Write(buf, 0, 4);

                // 3 emotes: emoteDelay(4) + emote(4)
                for (int j = 0; j < 3; j++)
                {
                    ms.Write(buf, 0, 4); // emoteDelay = 0
                    ms.Write(buf, 0, 4); // emote = 0
                }
            }

            return ms.ToArray();
        }

        #endregion

        #region SMSG_GOSSIP_MESSAGE Parser Tests

        [Fact]
        public void ParseGossipMenu_EmptyMenu_ParsesCorrectly()
        {
            GossipMenuData? received = null;
            _gossipAgent.GossipMenus.Subscribe(m => received = m);

            var payload = BuildGossipMessagePayload(0x1234UL, 100, 42);
            GetSubject(Opcode.SMSG_GOSSIP_MESSAGE).OnNext(payload);

            Assert.NotNull(received);
            Assert.Equal(0x1234UL, received.NpcGuid);
            Assert.Equal(100U, received.TextId);
            Assert.Equal(42U, received.MenuId);
            Assert.Empty(received.Options);
            Assert.Empty(received.QuestOptions);
        }

        [Fact]
        public void ParseGossipMenu_WithGossipOptions_ParsesCorrectly()
        {
            GossipMenuData? received = null;
            _gossipAgent.GossipMenus.Subscribe(m => received = m);

            var gossipOpts = new[]
            {
                (OptIndex: 0U, Icon: (byte)GossipTypes.Gossip, Coded: (byte)0, BoxMoney: 0U, Text: "I have heard tales of dark magic...", BoxText: ""),
                (OptIndex: 1U, Icon: (byte)GossipTypes.Vendor, Coded: (byte)0, BoxMoney: 0U, Text: "Show me your wares", BoxText: ""),
            };
            var payload = BuildGossipMessagePayload(0xAABBUL, 200, 10, gossipOpts);
            GetSubject(Opcode.SMSG_GOSSIP_MESSAGE).OnNext(payload);

            Assert.NotNull(received);
            Assert.Equal(2, received.Options.Count);
            Assert.Equal(0U, received.Options[0].Index);
            Assert.Equal("I have heard tales of dark magic...", received.Options[0].Text);
            Assert.Equal(GossipServiceType.Gossip, received.Options[0].ServiceType);
            Assert.Equal(1U, received.Options[1].Index);
            Assert.Equal("Show me your wares", received.Options[1].Text);
            Assert.Equal(GossipServiceType.Vendor, received.Options[1].ServiceType);
        }

        [Fact]
        public void ParseGossipMenu_WithPaidOption_ParsesCostCorrectly()
        {
            GossipMenuData? received = null;
            _gossipAgent.GossipMenus.Subscribe(m => received = m);

            var gossipOpts = new[]
            {
                (OptIndex: 0U, Icon: (byte)GossipTypes.Trainer, Coded: (byte)1, BoxMoney: 5000U, Text: "Train me", BoxText: "Are you sure? This costs 50 silver."),
            };
            var payload = BuildGossipMessagePayload(0xCCDDUL, 300, 5, gossipOpts);
            GetSubject(Opcode.SMSG_GOSSIP_MESSAGE).OnNext(payload);

            Assert.NotNull(received);
            Assert.Single(received.Options);
            Assert.True(received.Options[0].RequiresPayment);
            Assert.Equal(5000U, received.Options[0].Cost);
            Assert.Equal("Train me", received.Options[0].Text);
            Assert.Equal(GossipServiceType.Trainer, received.Options[0].ServiceType);
        }

        [Fact]
        public void ParseGossipMenu_WithQuests_ParsesCorrectly()
        {
            GossipMenuData? received = null;
            _gossipAgent.GossipMenus.Subscribe(m => received = m);

            var questOpts = new[]
            {
                (QuestId: 100U, QuestIcon: 0U, QuestLevel: 10U, QuestTitle: "A Threat Within"),
                (QuestId: 200U, QuestIcon: 4U, QuestLevel: 12U, QuestTitle: "The Defias Brotherhood"),
            };
            var payload = BuildGossipMessagePayload(0x1111UL, 400, 15, questOptions: questOpts);
            GetSubject(Opcode.SMSG_GOSSIP_MESSAGE).OnNext(payload);

            Assert.NotNull(received);
            Assert.Equal(2, received.QuestOptions.Count);
            Assert.Equal(100U, received.QuestOptions[0].QuestId);
            Assert.Equal("A Threat Within", received.QuestOptions[0].QuestTitle);
            Assert.Equal(10U, received.QuestOptions[0].QuestLevel);
            Assert.Equal(QuestGossipState.Available, received.QuestOptions[0].State);
            Assert.Equal(200U, received.QuestOptions[1].QuestId);
            Assert.Equal("The Defias Brotherhood", received.QuestOptions[1].QuestTitle);
            Assert.Equal(QuestGossipState.Completable, received.QuestOptions[1].State);
        }

        [Fact]
        public void ParseGossipMenu_WithOptionsAndQuests_ParsesBothCorrectly()
        {
            GossipMenuData? received = null;
            _gossipAgent.GossipMenus.Subscribe(m => received = m);

            var gossipOpts = new[]
            {
                (OptIndex: 0U, Icon: (byte)GossipTypes.Gossip, Coded: (byte)0, BoxMoney: 0U, Text: "Tell me more", BoxText: ""),
            };
            var questOpts = new[]
            {
                (QuestId: 500U, QuestIcon: 2U, QuestLevel: 20U, QuestTitle: "Report to Goldshire"),
            };
            var payload = BuildGossipMessagePayload(0x2222UL, 500, 25, gossipOpts, questOpts);
            GetSubject(Opcode.SMSG_GOSSIP_MESSAGE).OnNext(payload);

            Assert.NotNull(received);
            Assert.Equal(25U, received.MenuId);
            Assert.Single(received.Options);
            Assert.Equal("Tell me more", received.Options[0].Text);
            Assert.Single(received.QuestOptions);
            Assert.Equal("Report to Goldshire", received.QuestOptions[0].QuestTitle);
        }

        [Fact]
        public void ParseGossipMenu_MenuId_NotZero()
        {
            GossipMenuData? received = null;
            _gossipAgent.GossipMenus.Subscribe(m => received = m);

            var payload = BuildGossipMessagePayload(0x3333UL, 600, 999);
            GetSubject(Opcode.SMSG_GOSSIP_MESSAGE).OnNext(payload);

            Assert.NotNull(received);
            Assert.Equal(999U, received.MenuId);
        }

        [Fact]
        public void ParseGossipMenu_AllServiceTypes_MapCorrectly()
        {
            GossipMenuData? received = null;
            _gossipAgent.GossipMenus.Subscribe(m => received = m);

            var gossipOpts = new[]
            {
                (OptIndex: 0U, Icon: (byte)GossipTypes.Taxi, Coded: (byte)0, BoxMoney: 0U, Text: "Fly me", BoxText: ""),
                (OptIndex: 1U, Icon: (byte)GossipTypes.Healer, Coded: (byte)0, BoxMoney: 0U, Text: "Heal me", BoxText: ""),
                (OptIndex: 2U, Icon: (byte)GossipTypes.Banker, Coded: (byte)0, BoxMoney: 0U, Text: "My bank", BoxText: ""),
                (OptIndex: 3U, Icon: (byte)GossipTypes.Auctioneer, Coded: (byte)0, BoxMoney: 0U, Text: "Auction", BoxText: ""),
            };
            var payload = BuildGossipMessagePayload(0x4444UL, 700, 30, gossipOpts);
            GetSubject(Opcode.SMSG_GOSSIP_MESSAGE).OnNext(payload);

            Assert.NotNull(received);
            Assert.Equal(4, received.Options.Count);
            Assert.Equal(GossipServiceType.Taxi, received.Options[0].ServiceType);
            Assert.Equal(GossipServiceType.Healer, received.Options[1].ServiceType);
            Assert.Equal(GossipServiceType.Banker, received.Options[2].ServiceType);
            Assert.Equal(GossipServiceType.Auctioneer, received.Options[3].ServiceType);
        }

        #endregion

        #region SMSG_NPC_TEXT_UPDATE Parser Tests

        [Fact]
        public void ParseNpcTextUpdate_ParsesTextIdAndContent()
        {
            // First open a gossip menu to set _currentNpcGuid
            _gossipAgent.GossipMenus.Subscribe(_ => { });
            var gossipPayload = BuildGossipMessagePayload(0x5555UL, 800, 50);
            GetSubject(Opcode.SMSG_GOSSIP_MESSAGE).OnNext(gossipPayload);

            // Now send text update
            GossipMenuData? updated = null;
            _gossipAgent.GossipMenus.Subscribe(m => updated = m);

            var textPayload = BuildNpcTextUpdatePayload(800, "Greetings, traveler. What brings you here?");
            GetSubject(Opcode.SMSG_NPC_TEXT_UPDATE).OnNext(textPayload);

            Assert.NotNull(updated);
            Assert.Equal("Greetings, traveler. What brings you here?", updated.GossipText);
        }

        [Fact]
        public void ParseNpcTextUpdate_TextIdAtOffset0_NotOffset8()
        {
            // Verify the fix: textId should be at offset 0 (4 bytes), NOT offset 8
            _gossipAgent.GossipMenus.Subscribe(_ => { });
            var gossipPayload = BuildGossipMessagePayload(0x6666UL, 900, 60);
            GetSubject(Opcode.SMSG_GOSSIP_MESSAGE).OnNext(gossipPayload);

            // Build payload where textId=900 is at offset 0
            var payload = BuildNpcTextUpdatePayload(900, "Hello!");
            // Verify first 4 bytes ARE the textId
            Assert.Equal(900U, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0, 4)));

            GossipMenuData? updated = null;
            _gossipAgent.GossipMenus.Subscribe(m => updated = m);
            GetSubject(Opcode.SMSG_NPC_TEXT_UPDATE).OnNext(payload);

            Assert.NotNull(updated);
            Assert.Equal("Hello!", updated.GossipText);
        }

        #endregion

        #region CMSG Tests

        [Fact]
        public async Task GreetNpcAsync_SendsCorrectPayload()
        {
            byte[]? capturedPayload = null;
            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(Opcode.CMSG_GOSSIP_HELLO, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Callback<Opcode, byte[], CancellationToken>((_, p, _) => capturedPayload = p)
                .Returns(Task.CompletedTask);

            await _gossipAgent.GreetNpcAsync(0xABCD1234UL);

            Assert.NotNull(capturedPayload);
            Assert.Equal(8, capturedPayload.Length);
            Assert.Equal(0xABCD1234UL, BitConverter.ToUInt64(capturedPayload, 0));
        }

        [Fact]
        public async Task SelectGossipOptionAsync_SendsGuidAndOptionId()
        {
            // First open a gossip menu
            _gossipAgent.GossipMenus.Subscribe(_ => { });
            var gossipPayload = BuildGossipMessagePayload(0x7777UL, 100, 10,
                new[] { (0U, (byte)0, (byte)0, 0U, "Option", "") });
            GetSubject(Opcode.SMSG_GOSSIP_MESSAGE).OnNext(gossipPayload);

            byte[]? capturedPayload = null;
            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(Opcode.CMSG_GOSSIP_SELECT_OPTION, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Callback<Opcode, byte[], CancellationToken>((_, p, _) => capturedPayload = p)
                .Returns(Task.CompletedTask);

            await _gossipAgent.SelectGossipOptionAsync(0);

            Assert.NotNull(capturedPayload);
            Assert.Equal(12, capturedPayload.Length);
            Assert.Equal(0x7777UL, BitConverter.ToUInt64(capturedPayload, 0));
            Assert.Equal(0U, BitConverter.ToUInt32(capturedPayload, 8));
        }

        [Fact]
        public async Task QueryNpcTextAsync_SendsTextIdThenGuid()
        {
            // MaNGOS reads textId(4) then guid(8)
            byte[]? capturedPayload = null;
            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(Opcode.CMSG_NPC_TEXT_QUERY, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Callback<Opcode, byte[], CancellationToken>((_, p, _) => capturedPayload = p)
                .Returns(Task.CompletedTask);

            await _gossipAgent.QueryNpcTextAsync(42, 0x8888UL);

            Assert.NotNull(capturedPayload);
            Assert.Equal(12, capturedPayload.Length);
            Assert.Equal(42U, BitConverter.ToUInt32(capturedPayload, 0));
            Assert.Equal(0x8888UL, BitConverter.ToUInt64(capturedPayload, 4));
        }

        [Fact]
        public async Task SelectGossipOptionAsync_WithoutOpenMenu_Throws()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _gossipAgent.SelectGossipOptionAsync(0));
        }

        #endregion

        #region State Tests

        [Fact]
        public void InitialState_Correct()
        {
            Assert.False(_gossipAgent.IsGossipWindowOpen);
            Assert.Null(_gossipAgent.CurrentNpcGuid);
            Assert.Equal(GossipMenuState.Closed, _gossipAgent.MenuState);
            Assert.Null(_gossipAgent.GetCurrentGossipMenu());
        }

        [Fact]
        public void GossipMenuReceived_UpdatesState()
        {
            _gossipAgent.GossipMenus.Subscribe(_ => { });

            var payload = BuildGossipMessagePayload(0x9999UL, 100, 10);
            GetSubject(Opcode.SMSG_GOSSIP_MESSAGE).OnNext(payload);

            Assert.True(_gossipAgent.IsGossipWindowOpen);
            Assert.Equal(0x9999UL, _gossipAgent.CurrentNpcGuid);
            Assert.Equal(GossipMenuState.Open, _gossipAgent.MenuState);
            Assert.NotNull(_gossipAgent.GetCurrentGossipMenu());
        }

        [Fact]
        public void GossipComplete_ClearsState()
        {
            _gossipAgent.GossipMenus.Subscribe(_ => { });
            _gossipAgent.GossipMenuClosed.Subscribe(_ => { });

            // Open
            var payload = BuildGossipMessagePayload(0xAAAAUL, 100, 10);
            GetSubject(Opcode.SMSG_GOSSIP_MESSAGE).OnNext(payload);
            Assert.True(_gossipAgent.IsGossipWindowOpen);

            // Complete
            GetSubject(Opcode.SMSG_GOSSIP_COMPLETE).OnNext(ReadOnlyMemory<byte>.Empty);
            Assert.False(_gossipAgent.IsGossipWindowOpen);
            Assert.Null(_gossipAgent.CurrentNpcGuid);
            Assert.Equal(GossipMenuState.Closed, _gossipAgent.MenuState);
        }

        [Fact]
        public async Task CloseGossipAsync_ClearsState()
        {
            // Open
            _gossipAgent.GossipMenus.Subscribe(_ => { });
            var payload = BuildGossipMessagePayload(0xBBBBUL, 100, 10);
            GetSubject(Opcode.SMSG_GOSSIP_MESSAGE).OnNext(payload);

            await _gossipAgent.CloseGossipAsync();
            Assert.False(_gossipAgent.IsGossipWindowOpen);
            Assert.Null(_gossipAgent.CurrentNpcGuid);
        }

        #endregion

        #region Validation Tests

        [Fact]
        public void CanPerformGossipOperation_Greet_WhenClosed()
        {
            Assert.True(_gossipAgent.CanPerformGossipOperation(GossipOperationType.Greet));
        }

        [Fact]
        public void CanPerformGossipOperation_SelectOption_WhenOpen()
        {
            _gossipAgent.GossipMenus.Subscribe(_ => { });
            var payload = BuildGossipMessagePayload(0xCCCCUL, 100, 10);
            GetSubject(Opcode.SMSG_GOSSIP_MESSAGE).OnNext(payload);

            Assert.True(_gossipAgent.CanPerformGossipOperation(GossipOperationType.SelectOption));
        }

        [Fact]
        public void CanPerformGossipOperation_Greet_WhenOpen_ReturnsFalse()
        {
            _gossipAgent.GossipMenus.Subscribe(_ => { });
            var payload = BuildGossipMessagePayload(0xDDDDUL, 100, 10);
            GetSubject(Opcode.SMSG_GOSSIP_MESSAGE).OnNext(payload);

            Assert.False(_gossipAgent.CanPerformGossipOperation(GossipOperationType.Greet));
        }

        [Fact]
        public void IsServiceAvailable_WhenVendorPresent()
        {
            _gossipAgent.GossipMenus.Subscribe(_ => { });
            var gossipOpts = new[]
            {
                (OptIndex: 0U, Icon: (byte)GossipTypes.Vendor, Coded: (byte)0, BoxMoney: 0U, Text: "Buy", BoxText: ""),
            };
            var payload = BuildGossipMessagePayload(0xEEEEUL, 100, 10, gossipOpts);
            GetSubject(Opcode.SMSG_GOSSIP_MESSAGE).OnNext(payload);

            Assert.True(_gossipAgent.IsServiceAvailable(GossipServiceType.Vendor));
            Assert.False(_gossipAgent.IsServiceAvailable(GossipServiceType.Trainer));
        }

        #endregion

        #region Quest State Mapping

        [Theory]
        [InlineData(0U, QuestGossipState.Available)]
        [InlineData(1U, QuestGossipState.InProgress)]
        [InlineData(2U, QuestGossipState.Available)]
        [InlineData(3U, QuestGossipState.InProgress)]
        [InlineData(4U, QuestGossipState.Completable)]
        [InlineData(5U, QuestGossipState.Available)]
        public void QuestIcon_MapsToCorrectState(uint questIcon, QuestGossipState expectedState)
        {
            GossipMenuData? received = null;
            _gossipAgent.GossipMenus.Subscribe(m => received = m);

            var questOpts = new[] { (QuestId: 1U, QuestIcon: questIcon, QuestLevel: 10U, QuestTitle: "Test") };
            var payload = BuildGossipMessagePayload(0xFFFFUL, 100, 10, questOptions: questOpts);
            GetSubject(Opcode.SMSG_GOSSIP_MESSAGE).OnNext(payload);

            Assert.NotNull(received);
            Assert.Single(received.QuestOptions);
            Assert.Equal(expectedState, received.QuestOptions[0].State);
        }

        #endregion

        #region Disposal Tests

        [Fact]
        public void Dispose_DoesNotThrow()
        {
            var agent = new GossipNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
            agent.Dispose();
        }

        [Fact]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            var agent = new GossipNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
            agent.Dispose();
            agent.Dispose();
        }

        [Fact]
        public async Task AfterDispose_GreetNpc_Throws()
        {
            var agent = new GossipNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
            agent.Dispose();
            await Assert.ThrowsAsync<ObjectDisposedException>(() => agent.GreetNpcAsync(0x1111UL));
        }

        #endregion
    }
}
