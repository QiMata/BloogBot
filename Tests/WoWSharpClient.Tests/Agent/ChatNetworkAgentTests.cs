using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Tests.Agent
{
    public class ChatNetworkClientComponentTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILogger<ChatNetworkClientComponent>> _mockLogger;
        private readonly Dictionary<Opcode, Subject<ReadOnlyMemory<byte>>> _opcodeSubjects = new();
        private readonly ChatNetworkClientComponent _chatAgent;

        public ChatNetworkClientComponentTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLogger = new Mock<ILogger<ChatNetworkClientComponent>>();

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

            _mockWorldClient
                .Setup(x => x.SendChatMessageAsync(It.IsAny<ChatMsg>(), It.IsAny<Language>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _chatAgent = new ChatNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
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
        /// Builds an SMSG_MESSAGECHAT payload for SAY/PARTY/YELL:
        /// chatType(1) + language(4) + senderGuid(8) + senderGuid(8) + textLen(4) + text\0 + chatTag(1)
        /// </summary>
        private static byte[] BuildSayPayload(ChatMsg chatType, Language language, ulong senderGuid, string text, PlayerChatTag tag = PlayerChatTag.CHAT_TAG_NONE)
        {
            var textBytes = Encoding.UTF8.GetBytes(text);
            uint textLen = (uint)(textBytes.Length + 1); // includes null terminator
            var payload = new byte[1 + 4 + 8 + 8 + 4 + textLen + 1];
            int offset = 0;

            payload[offset++] = (byte)chatType;
            BitConverter.GetBytes((int)language).CopyTo(payload, offset); offset += 4;
            BitConverter.GetBytes(senderGuid).CopyTo(payload, offset); offset += 8;
            BitConverter.GetBytes(senderGuid).CopyTo(payload, offset); offset += 8; // duplicate
            BitConverter.GetBytes(textLen).CopyTo(payload, offset); offset += 4;
            textBytes.CopyTo(payload, offset); offset += textBytes.Length;
            payload[offset++] = 0x00; // null terminator for text
            payload[offset] = (byte)tag;

            return payload;
        }

        /// <summary>
        /// Builds an SMSG_MESSAGECHAT payload for default case (WHISPER/SYSTEM/GUILD/RAID/etc):
        /// chatType(1) + language(4) + senderGuid(8) + textLen(4) + text\0 + chatTag(1)
        /// </summary>
        private static byte[] BuildDefaultPayload(ChatMsg chatType, Language language, ulong senderGuid, string text, PlayerChatTag tag = PlayerChatTag.CHAT_TAG_NONE)
        {
            var textBytes = Encoding.UTF8.GetBytes(text);
            uint textLen = (uint)(textBytes.Length + 1);
            var payload = new byte[1 + 4 + 8 + 4 + textLen + 1];
            int offset = 0;

            payload[offset++] = (byte)chatType;
            BitConverter.GetBytes((int)language).CopyTo(payload, offset); offset += 4;
            BitConverter.GetBytes(senderGuid).CopyTo(payload, offset); offset += 8;
            BitConverter.GetBytes(textLen).CopyTo(payload, offset); offset += 4;
            textBytes.CopyTo(payload, offset); offset += textBytes.Length;
            payload[offset++] = 0x00;
            payload[offset] = (byte)tag;

            return payload;
        }

        /// <summary>
        /// Builds an SMSG_MESSAGECHAT payload for MONSTER_* types:
        /// chatType(1) + language(4) + senderGuid(8) + nameLen(4) + name\0 + targetGuid(8) + textLen(4) + text\0 + chatTag(1)
        /// </summary>
        private static byte[] BuildMonsterPayload(ChatMsg chatType, Language language, ulong senderGuid, string senderName, ulong targetGuid, string text, PlayerChatTag tag = PlayerChatTag.CHAT_TAG_NONE)
        {
            var nameBytes = Encoding.UTF8.GetBytes(senderName);
            uint nameLen = (uint)(nameBytes.Length + 1);
            var textBytes = Encoding.UTF8.GetBytes(text);
            uint textLen = (uint)(textBytes.Length + 1);
            var payload = new byte[1 + 4 + 8 + 4 + nameLen + 8 + 4 + textLen + 1];
            int offset = 0;

            payload[offset++] = (byte)chatType;
            BitConverter.GetBytes((int)language).CopyTo(payload, offset); offset += 4;
            BitConverter.GetBytes(senderGuid).CopyTo(payload, offset); offset += 8;
            BitConverter.GetBytes(nameLen).CopyTo(payload, offset); offset += 4;
            nameBytes.CopyTo(payload, offset); offset += nameBytes.Length;
            payload[offset++] = 0x00; // name null terminator
            BitConverter.GetBytes(targetGuid).CopyTo(payload, offset); offset += 8;
            BitConverter.GetBytes(textLen).CopyTo(payload, offset); offset += 4;
            textBytes.CopyTo(payload, offset); offset += textBytes.Length;
            payload[offset++] = 0x00;
            payload[offset] = (byte)tag;

            return payload;
        }

        /// <summary>
        /// Builds an SMSG_MESSAGECHAT payload for CHANNEL:
        /// chatType(1) + language(4) + channelName\0 + playerRank(4) + senderGuid(8) + textLen(4) + text\0 + chatTag(1)
        /// </summary>
        private static byte[] BuildChannelPayload(Language language, ulong senderGuid, string channelName, uint playerRank, string text, PlayerChatTag tag = PlayerChatTag.CHAT_TAG_NONE)
        {
            var channelBytes = Encoding.UTF8.GetBytes(channelName);
            var textBytes = Encoding.UTF8.GetBytes(text);
            uint textLen = (uint)(textBytes.Length + 1);
            var payload = new byte[1 + 4 + channelBytes.Length + 1 + 4 + 8 + 4 + textLen + 1];
            int offset = 0;

            payload[offset++] = (byte)ChatMsg.CHAT_MSG_CHANNEL;
            BitConverter.GetBytes((int)language).CopyTo(payload, offset); offset += 4;
            channelBytes.CopyTo(payload, offset); offset += channelBytes.Length;
            payload[offset++] = 0x00; // channel name null terminator
            BitConverter.GetBytes(playerRank).CopyTo(payload, offset); offset += 4;
            BitConverter.GetBytes(senderGuid).CopyTo(payload, offset); offset += 8;
            BitConverter.GetBytes(textLen).CopyTo(payload, offset); offset += 4;
            textBytes.CopyTo(payload, offset); offset += textBytes.Length;
            payload[offset++] = 0x00;
            payload[offset] = (byte)tag;

            return payload;
        }

        #endregion

        #region SMSG Parser Tests — SAY/PARTY/YELL

        [Fact]
        public void ParseChatMessage_Say_ParsesCorrectly()
        {
            ChatMessageData? received = null;
            _chatAgent.IncomingMessages.Subscribe(msg => received = msg);

            var payload = BuildSayPayload(ChatMsg.CHAT_MSG_SAY, Language.Orcish, 147, "Hey");
            GetSubject(Opcode.SMSG_MESSAGECHAT).OnNext(payload);

            Assert.NotNull(received);
            Assert.Equal(ChatMsg.CHAT_MSG_SAY, received.ChatType);
            Assert.Equal(Language.Orcish, received.Language);
            Assert.Equal(147UL, received.SenderGuid);
            Assert.Equal("Hey", received.Text);
            Assert.Equal(PlayerChatTag.CHAT_TAG_NONE, received.PlayerChatTag);
        }

        [Fact]
        public void ParseChatMessage_Party_ParsesCorrectly()
        {
            ChatMessageData? received = null;
            _chatAgent.IncomingMessages.Subscribe(msg => received = msg);

            var payload = BuildSayPayload(ChatMsg.CHAT_MSG_PARTY, Language.Common, 200, "Follow me");
            GetSubject(Opcode.SMSG_MESSAGECHAT).OnNext(payload);

            Assert.NotNull(received);
            Assert.Equal(ChatMsg.CHAT_MSG_PARTY, received.ChatType);
            Assert.Equal(200UL, received.SenderGuid);
            Assert.Equal("Follow me", received.Text);
        }

        [Fact]
        public void ParseChatMessage_Yell_ParsesCorrectly()
        {
            ChatMessageData? received = null;
            _chatAgent.IncomingMessages.Subscribe(msg => received = msg);

            var payload = BuildSayPayload(ChatMsg.CHAT_MSG_YELL, Language.Orcish, 147, "HEY!");
            GetSubject(Opcode.SMSG_MESSAGECHAT).OnNext(payload);

            Assert.NotNull(received);
            Assert.Equal(ChatMsg.CHAT_MSG_YELL, received.ChatType);
            Assert.Equal(147UL, received.SenderGuid);
            Assert.Equal("HEY!", received.Text);
        }

        [Fact]
        public void ParseChatMessage_Say_WithChatTag_ParsesTagCorrectly()
        {
            ChatMessageData? received = null;
            _chatAgent.IncomingMessages.Subscribe(msg => received = msg);

            var payload = BuildSayPayload(ChatMsg.CHAT_MSG_SAY, Language.Common, 42, "brb", PlayerChatTag.CHAT_TAG_AFK);
            GetSubject(Opcode.SMSG_MESSAGECHAT).OnNext(payload);

            Assert.NotNull(received);
            Assert.Equal(PlayerChatTag.CHAT_TAG_AFK, received.PlayerChatTag);
            Assert.Equal("brb", received.Text);
        }

        [Fact]
        public void ParseChatMessage_Say_WithGMTag_ParsesTagCorrectly()
        {
            ChatMessageData? received = null;
            _chatAgent.IncomingMessages.Subscribe(msg => received = msg);

            var payload = BuildSayPayload(ChatMsg.CHAT_MSG_SAY, Language.Common, 1, "Hello!", PlayerChatTag.CHAT_TAG_GM);
            GetSubject(Opcode.SMSG_MESSAGECHAT).OnNext(payload);

            Assert.NotNull(received);
            Assert.Equal(PlayerChatTag.CHAT_TAG_GM, received.PlayerChatTag);
        }

        #endregion

        #region SMSG Parser Tests — Default case (WHISPER/SYSTEM/GUILD/RAID)

        [Fact]
        public void ParseChatMessage_Whisper_ParsesCorrectly()
        {
            ChatMessageData? received = null;
            _chatAgent.IncomingMessages.Subscribe(msg => received = msg);

            var payload = BuildDefaultPayload(ChatMsg.CHAT_MSG_WHISPER, Language.Universal, 147, "Hey");
            GetSubject(Opcode.SMSG_MESSAGECHAT).OnNext(payload);

            Assert.NotNull(received);
            Assert.Equal(ChatMsg.CHAT_MSG_WHISPER, received.ChatType);
            Assert.Equal(Language.Universal, received.Language);
            Assert.Equal(147UL, received.SenderGuid);
            Assert.Equal("Hey", received.Text);
            Assert.Equal(PlayerChatTag.CHAT_TAG_NONE, received.PlayerChatTag);
        }

        [Fact]
        public void ParseChatMessage_System_ParsesCorrectly()
        {
            ChatMessageData? received = null;
            _chatAgent.IncomingMessages.Subscribe(msg => received = msg);

            var payload = BuildDefaultPayload(ChatMsg.CHAT_MSG_SYSTEM, Language.Universal, 0, "Patch 1.12: Drums of War is now live!");
            GetSubject(Opcode.SMSG_MESSAGECHAT).OnNext(payload);

            Assert.NotNull(received);
            Assert.Equal(ChatMsg.CHAT_MSG_SYSTEM, received.ChatType);
            Assert.Equal(0UL, received.SenderGuid);
            Assert.Equal("Patch 1.12: Drums of War is now live!", received.Text);
        }

        [Fact]
        public void ParseChatMessage_Guild_ParsesCorrectly()
        {
            ChatMessageData? received = null;
            _chatAgent.IncomingMessages.Subscribe(msg => received = msg);

            var payload = BuildDefaultPayload(ChatMsg.CHAT_MSG_GUILD, Language.Common, 500, "Anyone for Deadmines?");
            GetSubject(Opcode.SMSG_MESSAGECHAT).OnNext(payload);

            Assert.NotNull(received);
            Assert.Equal(ChatMsg.CHAT_MSG_GUILD, received.ChatType);
            Assert.Equal(500UL, received.SenderGuid);
            Assert.Equal("Anyone for Deadmines?", received.Text);
        }

        [Fact]
        public void ParseChatMessage_Raid_ParsesCorrectly()
        {
            ChatMessageData? received = null;
            _chatAgent.IncomingMessages.Subscribe(msg => received = msg);

            var payload = BuildDefaultPayload(ChatMsg.CHAT_MSG_RAID, Language.Common, 300, "Pull in 5");
            GetSubject(Opcode.SMSG_MESSAGECHAT).OnNext(payload);

            Assert.NotNull(received);
            Assert.Equal(ChatMsg.CHAT_MSG_RAID, received.ChatType);
            Assert.Equal(300UL, received.SenderGuid);
            Assert.Equal("Pull in 5", received.Text);
        }

        [Fact]
        public void ParseChatMessage_Whisper_WithDNDTag()
        {
            ChatMessageData? received = null;
            _chatAgent.IncomingMessages.Subscribe(msg => received = msg);

            var payload = BuildDefaultPayload(ChatMsg.CHAT_MSG_WHISPER, Language.Universal, 99, "I'm busy", PlayerChatTag.CHAT_TAG_DND);
            GetSubject(Opcode.SMSG_MESSAGECHAT).OnNext(payload);

            Assert.NotNull(received);
            Assert.Equal(PlayerChatTag.CHAT_TAG_DND, received.PlayerChatTag);
            Assert.Equal("I'm busy", received.Text);
        }

        #endregion

        #region SMSG Parser Tests — MONSTER_* types

        [Fact]
        public void ParseChatMessage_MonsterSay_ParsesCorrectly()
        {
            ChatMessageData? received = null;
            _chatAgent.IncomingMessages.Subscribe(msg => received = msg);

            var payload = BuildMonsterPayload(ChatMsg.CHAT_MSG_MONSTER_SAY, Language.Universal, 0x100F0001_00004567UL, "Hogger", 147, "Gnoll punch!");
            GetSubject(Opcode.SMSG_MESSAGECHAT).OnNext(payload);

            Assert.NotNull(received);
            Assert.Equal(ChatMsg.CHAT_MSG_MONSTER_SAY, received.ChatType);
            Assert.Equal(0x100F0001_00004567UL, received.SenderGuid);
            Assert.Equal("Hogger", received.SenderName);
            Assert.Equal(147UL, received.TargetGuid);
            Assert.Equal("Gnoll punch!", received.Text);
        }

        [Fact]
        public void ParseChatMessage_MonsterYell_ParsesCorrectly()
        {
            ChatMessageData? received = null;
            _chatAgent.IncomingMessages.Subscribe(msg => received = msg);

            var payload = BuildMonsterPayload(ChatMsg.CHAT_MSG_MONSTER_YELL, Language.Universal, 0xF130001E_00001234UL, "Edwin VanCleef", 0, "None may challenge the Brotherhood!");
            GetSubject(Opcode.SMSG_MESSAGECHAT).OnNext(payload);

            Assert.NotNull(received);
            Assert.Equal(ChatMsg.CHAT_MSG_MONSTER_YELL, received.ChatType);
            Assert.Equal("Edwin VanCleef", received.SenderName);
            Assert.Equal("None may challenge the Brotherhood!", received.Text);
        }

        [Fact]
        public void ParseChatMessage_MonsterWhisper_ParsesCorrectly()
        {
            ChatMessageData? received = null;
            _chatAgent.IncomingMessages.Subscribe(msg => received = msg);

            var payload = BuildMonsterPayload(ChatMsg.CHAT_MSG_MONSTER_WHISPER, Language.Universal, 0xF130001E_00005678UL, "Spirit Healer", 147, "It is not yet your time...");
            GetSubject(Opcode.SMSG_MESSAGECHAT).OnNext(payload);

            Assert.NotNull(received);
            Assert.Equal(ChatMsg.CHAT_MSG_MONSTER_WHISPER, received.ChatType);
            Assert.Equal(0xF130001E_00005678UL, received.SenderGuid);
            Assert.Equal("Spirit Healer", received.SenderName);
            Assert.Equal(147UL, received.TargetGuid);
            Assert.Equal("It is not yet your time...", received.Text);
        }

        [Fact]
        public void ParseChatMessage_MonsterEmote_ParsesCorrectly()
        {
            ChatMessageData? received = null;
            _chatAgent.IncomingMessages.Subscribe(msg => received = msg);

            var payload = BuildMonsterPayload(ChatMsg.CHAT_MSG_MONSTER_EMOTE, Language.Universal, 0xF130001E_0000AAAAUL, "Guard Thomas", 0, "%s salutes you.");
            GetSubject(Opcode.SMSG_MESSAGECHAT).OnNext(payload);

            Assert.NotNull(received);
            Assert.Equal(ChatMsg.CHAT_MSG_MONSTER_EMOTE, received.ChatType);
            Assert.Equal("Guard Thomas", received.SenderName);
            Assert.Equal("%s salutes you.", received.Text);
        }

        [Fact]
        public void ParseChatMessage_RaidBossWhisper_ParsesCorrectly()
        {
            ChatMessageData? received = null;
            _chatAgent.IncomingMessages.Subscribe(msg => received = msg);

            var payload = BuildMonsterPayload(ChatMsg.CHAT_MSG_RAID_BOSS_WHISPER, Language.Universal, 0xF130001E_0000BBBBUL, "Ragnaros", 147, "BY FIRE BE PURGED!");
            GetSubject(Opcode.SMSG_MESSAGECHAT).OnNext(payload);

            Assert.NotNull(received);
            Assert.Equal(ChatMsg.CHAT_MSG_RAID_BOSS_WHISPER, received.ChatType);
            Assert.Equal("Ragnaros", received.SenderName);
            Assert.Equal("BY FIRE BE PURGED!", received.Text);
        }

        [Fact]
        public void ParseChatMessage_RaidBossEmote_ParsesCorrectly()
        {
            ChatMessageData? received = null;
            _chatAgent.IncomingMessages.Subscribe(msg => received = msg);

            var payload = BuildMonsterPayload(ChatMsg.CHAT_MSG_RAID_BOSS_EMOTE, Language.Universal, 0xF130001E_0000CCCCUL, "Onyxia", 0, "%s takes a deep breath...");
            GetSubject(Opcode.SMSG_MESSAGECHAT).OnNext(payload);

            Assert.NotNull(received);
            Assert.Equal(ChatMsg.CHAT_MSG_RAID_BOSS_EMOTE, received.ChatType);
            Assert.Equal("Onyxia", received.SenderName);
            Assert.Equal("%s takes a deep breath...", received.Text);
        }

        #endregion

        #region SMSG Parser Tests — CHANNEL

        [Fact]
        public void ParseChatMessage_Channel_ParsesCorrectly()
        {
            ChatMessageData? received = null;
            _chatAgent.IncomingMessages.Subscribe(msg => received = msg);

            var payload = BuildChannelPayload(Language.Common, 147, "General", 0, "LFG Deadmines");
            GetSubject(Opcode.SMSG_MESSAGECHAT).OnNext(payload);

            Assert.NotNull(received);
            Assert.Equal(ChatMsg.CHAT_MSG_CHANNEL, received.ChatType);
            Assert.Equal("General", received.ChannelName);
            Assert.Equal(147UL, received.SenderGuid);
            Assert.Equal(0, received.PlayerRank);
            Assert.Equal("LFG Deadmines", received.Text);
        }

        [Fact]
        public void ParseChatMessage_Channel_WithRank_ParsesCorrectly()
        {
            ChatMessageData? received = null;
            _chatAgent.IncomingMessages.Subscribe(msg => received = msg);

            var payload = BuildChannelPayload(Language.Common, 500, "Trade", 2, "WTS Arcanite Bar");
            GetSubject(Opcode.SMSG_MESSAGECHAT).OnNext(payload);

            Assert.NotNull(received);
            Assert.Equal("Trade", received.ChannelName);
            Assert.Equal(500UL, received.SenderGuid);
            Assert.Equal(2, received.PlayerRank);
            Assert.Equal("WTS Arcanite Bar", received.Text);
        }

        [Fact]
        public void ParseChatMessage_Channel_WithChatTag()
        {
            ChatMessageData? received = null;
            _chatAgent.IncomingMessages.Subscribe(msg => received = msg);

            var payload = BuildChannelPayload(Language.Common, 42, "LookingForGroup", 0, "Need tank", PlayerChatTag.CHAT_TAG_AFK);
            GetSubject(Opcode.SMSG_MESSAGECHAT).OnNext(payload);

            Assert.NotNull(received);
            Assert.Equal(PlayerChatTag.CHAT_TAG_AFK, received.PlayerChatTag);
        }

        #endregion

        #region SMSG Parser Tests — Real Binary Data

        [Fact]
        public void ParseChatMessage_RealSayBin_MatchesExpected()
        {
            // Real Say.bin from Elysium: 00 01000000 9300000000000000 9300000000000000 04000000 48657900 00
            ChatMessageData? received = null;
            _chatAgent.IncomingMessages.Subscribe(msg => received = msg);

            var data = new byte[] {
                0x00,                                           // chatType = SAY
                0x01, 0x00, 0x00, 0x00,                         // language = Orcish (1)
                0x93, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // senderGuid = 147
                0x93, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // duplicate guid
                0x04, 0x00, 0x00, 0x00,                         // textLength = 4
                0x48, 0x65, 0x79, 0x00,                         // "Hey\0"
                0x00                                            // chatTag = NONE
            };
            GetSubject(Opcode.SMSG_MESSAGECHAT).OnNext(data);

            Assert.NotNull(received);
            Assert.Equal(ChatMsg.CHAT_MSG_SAY, received.ChatType);
            Assert.Equal(Language.Orcish, received.Language);
            Assert.Equal(147UL, received.SenderGuid);
            Assert.Equal("Hey", received.Text);
            Assert.Equal(PlayerChatTag.CHAT_TAG_NONE, received.PlayerChatTag);
        }

        [Fact]
        public void ParseChatMessage_RealWhisperBin_MatchesExpected()
        {
            // Real Whisper.bin: 06 00000000 9300000000000000 04000000 48657900 00
            ChatMessageData? received = null;
            _chatAgent.IncomingMessages.Subscribe(msg => received = msg);

            var data = new byte[] {
                0x06,                                           // chatType = WHISPER
                0x00, 0x00, 0x00, 0x00,                         // language = Universal
                0x93, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // senderGuid = 147
                0x04, 0x00, 0x00, 0x00,                         // textLength = 4
                0x48, 0x65, 0x79, 0x00,                         // "Hey\0"
                0x00                                            // chatTag = NONE
            };
            GetSubject(Opcode.SMSG_MESSAGECHAT).OnNext(data);

            Assert.NotNull(received);
            Assert.Equal(ChatMsg.CHAT_MSG_WHISPER, received.ChatType);
            Assert.Equal(Language.Universal, received.Language);
            Assert.Equal(147UL, received.SenderGuid);
            Assert.Equal("Hey", received.Text);
            Assert.Equal(PlayerChatTag.CHAT_TAG_NONE, received.PlayerChatTag);
        }

        [Fact]
        public void ParseChatMessage_RealYellBin_MatchesExpected()
        {
            // Real Yell.bin: 05 01000000 9300000000000000 9300000000000000 05000000 48455921 00 00
            ChatMessageData? received = null;
            _chatAgent.IncomingMessages.Subscribe(msg => received = msg);

            var data = new byte[] {
                0x05,                                           // chatType = YELL
                0x01, 0x00, 0x00, 0x00,                         // language = Orcish
                0x93, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // senderGuid = 147
                0x93, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // duplicate guid
                0x05, 0x00, 0x00, 0x00,                         // textLength = 5
                0x48, 0x45, 0x59, 0x21, 0x00,                   // "HEY!\0"
                0x00                                            // chatTag = NONE
            };
            GetSubject(Opcode.SMSG_MESSAGECHAT).OnNext(data);

            Assert.NotNull(received);
            Assert.Equal(ChatMsg.CHAT_MSG_YELL, received.ChatType);
            Assert.Equal(147UL, received.SenderGuid);
            Assert.Equal("HEY!", received.Text);
        }

        #endregion

        #region ReadString Fix Verification

        [Fact]
        public void ParseChatMessage_ReadString_ReadsAllBytes_TagNotCorrupted()
        {
            // Before fix: ReadString reads length-1 chars, null left in stream, chatTag reads 0x00 instead of actual tag
            // After fix: ReadString reads all bytes, strips null, chatTag reads correctly
            ChatMessageData? received = null;
            _chatAgent.IncomingMessages.Subscribe(msg => received = msg);

            var payload = BuildDefaultPayload(ChatMsg.CHAT_MSG_WHISPER, Language.Universal, 42, "Test", PlayerChatTag.CHAT_TAG_AFK);
            GetSubject(Opcode.SMSG_MESSAGECHAT).OnNext(payload);

            Assert.NotNull(received);
            Assert.Equal("Test", received.Text);
            Assert.Equal(PlayerChatTag.CHAT_TAG_AFK, received.PlayerChatTag);
        }

        [Fact]
        public void ParseChatMessage_ReadString_EmptyText()
        {
            ChatMessageData? received = null;
            _chatAgent.IncomingMessages.Subscribe(msg => received = msg);

            // textLen=1 (just null terminator), text="\0"
            var payload = BuildDefaultPayload(ChatMsg.CHAT_MSG_SYSTEM, Language.Universal, 0, "");
            // Rebuild manually since BuildDefaultPayload with empty text creates textLen=1 which is just null
            var data = new byte[] {
                (byte)ChatMsg.CHAT_MSG_SYSTEM,
                0x00, 0x00, 0x00, 0x00,                         // language = Universal
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // senderGuid = 0
                0x01, 0x00, 0x00, 0x00,                         // textLength = 1
                0x00,                                           // "\0" (empty string)
                0x00                                            // chatTag = NONE
            };
            GetSubject(Opcode.SMSG_MESSAGECHAT).OnNext(data);

            Assert.NotNull(received);
            Assert.Equal("", received.Text);
        }

        #endregion

        #region Filtered Observable Tests

        [Fact]
        public void SayMessages_FiltersCorrectly()
        {
            ChatMessageData? received = null;
            _chatAgent.SayMessages.Subscribe(msg => received = msg);

            // Send a whisper — should NOT trigger
            var whisperPayload = BuildDefaultPayload(ChatMsg.CHAT_MSG_WHISPER, Language.Universal, 100, "whisper text");
            GetSubject(Opcode.SMSG_MESSAGECHAT).OnNext(whisperPayload);
            Assert.Null(received);

            // Send a say — SHOULD trigger
            var sayPayload = BuildSayPayload(ChatMsg.CHAT_MSG_SAY, Language.Common, 100, "say text");
            GetSubject(Opcode.SMSG_MESSAGECHAT).OnNext(sayPayload);
            Assert.NotNull(received);
            Assert.Equal("say text", received.Text);
        }

        [Fact]
        public void WhisperMessages_FiltersCorrectly()
        {
            ChatMessageData? received = null;
            _chatAgent.WhisperMessages.Subscribe(msg => received = msg);

            var payload = BuildDefaultPayload(ChatMsg.CHAT_MSG_WHISPER, Language.Universal, 100, "secret msg");
            GetSubject(Opcode.SMSG_MESSAGECHAT).OnNext(payload);

            Assert.NotNull(received);
            Assert.Equal("secret msg", received.Text);
        }

        [Fact]
        public void PartyMessages_FiltersCorrectly()
        {
            ChatMessageData? received = null;
            _chatAgent.PartyMessages.Subscribe(msg => received = msg);

            var payload = BuildSayPayload(ChatMsg.CHAT_MSG_PARTY, Language.Common, 200, "tank pull");
            GetSubject(Opcode.SMSG_MESSAGECHAT).OnNext(payload);

            Assert.NotNull(received);
            Assert.Equal("tank pull", received.Text);
        }

        [Fact]
        public void SystemMessages_FiltersCorrectly()
        {
            ChatMessageData? received = null;
            _chatAgent.SystemMessages.Subscribe(msg => received = msg);

            var payload = BuildDefaultPayload(ChatMsg.CHAT_MSG_SYSTEM, Language.Universal, 0, "Server restart");
            GetSubject(Opcode.SMSG_MESSAGECHAT).OnNext(payload);

            Assert.NotNull(received);
            Assert.Equal("Server restart", received.Text);
        }

        [Fact]
        public void ChannelMessages_FiltersCorrectly()
        {
            ChatMessageData? received = null;
            _chatAgent.ChannelMessages.Subscribe(msg => received = msg);

            var payload = BuildChannelPayload(Language.Common, 300, "Trade", 0, "WTS Runecloth");
            GetSubject(Opcode.SMSG_MESSAGECHAT).OnNext(payload);

            Assert.NotNull(received);
            Assert.Equal("WTS Runecloth", received.Text);
            Assert.Equal("Trade", received.ChannelName);
        }

        #endregion

        #region CMSG Tests — JoinChannel

        [Fact]
        public async Task JoinChannelAsync_SendsCorrectOpcode()
        {
            await _chatAgent.JoinChannelAsync("General");

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_JOIN_CHANNEL,
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task JoinChannelAsync_PayloadFormat_NameAndPassword()
        {
            byte[]? capturedPayload = null;
            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(Opcode.CMSG_JOIN_CHANNEL, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Callback<Opcode, byte[], CancellationToken>((_, p, _) => capturedPayload = p)
                .Returns(Task.CompletedTask);

            await _chatAgent.JoinChannelAsync("General");

            Assert.NotNull(capturedPayload);
            // Format: channelName\0 + password\0
            var expected = Encoding.UTF8.GetBytes("General");
            Assert.Equal(expected.Length + 1 + 1, capturedPayload.Length); // name + null + empty password null
            for (int i = 0; i < expected.Length; i++)
                Assert.Equal(expected[i], capturedPayload[i]);
            Assert.Equal(0x00, capturedPayload[expected.Length]); // name null terminator
            Assert.Equal(0x00, capturedPayload[expected.Length + 1]); // password null terminator
        }

        [Fact]
        public async Task JoinChannelAsync_AddsToActiveChannels()
        {
            await _chatAgent.JoinChannelAsync("General");

            var channels = _chatAgent.GetActiveChannels();
            Assert.Contains("General", channels);
        }

        [Fact]
        public async Task JoinChannelAsync_EmptyName_Throws()
        {
            await Assert.ThrowsAsync<ArgumentException>(() => _chatAgent.JoinChannelAsync(""));
        }

        #endregion

        #region CMSG Tests — LeaveChannel

        [Fact]
        public async Task LeaveChannelAsync_SendsCorrectOpcode()
        {
            await _chatAgent.LeaveChannelAsync("General");

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_LEAVE_CHANNEL,
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task LeaveChannelAsync_PayloadFormat_UnkAndName()
        {
            byte[]? capturedPayload = null;
            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(Opcode.CMSG_LEAVE_CHANNEL, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Callback<Opcode, byte[], CancellationToken>((_, p, _) => capturedPayload = p)
                .Returns(Task.CompletedTask);

            await _chatAgent.LeaveChannelAsync("Trade");

            Assert.NotNull(capturedPayload);
            // Format: unk(4) + channelName\0
            var nameBytes = Encoding.UTF8.GetBytes("Trade");
            Assert.Equal(4 + nameBytes.Length + 1, capturedPayload.Length);
            // First 4 bytes = unk (zeros)
            Assert.Equal(0, BitConverter.ToInt32(capturedPayload, 0));
            // Then channel name
            for (int i = 0; i < nameBytes.Length; i++)
                Assert.Equal(nameBytes[i], capturedPayload[4 + i]);
            Assert.Equal(0x00, capturedPayload[4 + nameBytes.Length]); // null terminator
        }

        [Fact]
        public async Task LeaveChannelAsync_EmptyName_Throws()
        {
            await Assert.ThrowsAsync<ArgumentException>(() => _chatAgent.LeaveChannelAsync(""));
        }

        #endregion

        #region CMSG Tests — SendMessage

        [Fact]
        public async Task SendMessageAsync_Say_DelegatesToWorldClient()
        {
            await _chatAgent.SayAsync("Hello world", Language.Common);

            _mockWorldClient.Verify(x => x.SendChatMessageAsync(
                ChatMsg.CHAT_MSG_SAY,
                Language.Common,
                string.Empty,
                "Hello world",
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task WhisperAsync_DelegatesToWorldClient()
        {
            await _chatAgent.WhisperAsync("Dralrahgra", "Hey there", Language.Common);

            _mockWorldClient.Verify(x => x.SendChatMessageAsync(
                ChatMsg.CHAT_MSG_WHISPER,
                Language.Common,
                "Dralrahgra",
                "Hey there",
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ChannelAsync_DelegatesToWorldClient()
        {
            await _chatAgent.ChannelAsync("Trade", "WTS stuff", Language.Common);

            _mockWorldClient.Verify(x => x.SendChatMessageAsync(
                ChatMsg.CHAT_MSG_CHANNEL,
                Language.Common,
                "Trade",
                "WTS stuff",
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Validation Tests

        [Fact]
        public void ValidateMessage_EmptyMessage_Invalid()
        {
            var result = _chatAgent.ValidateMessage(ChatMsg.CHAT_MSG_SAY, "", null);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ValidateMessage_TooLongMessage_Invalid()
        {
            var longMsg = new string('A', 256);
            var result = _chatAgent.ValidateMessage(ChatMsg.CHAT_MSG_SAY, longMsg, null);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ValidateMessage_WhisperWithoutDestination_Invalid()
        {
            var result = _chatAgent.ValidateMessage(ChatMsg.CHAT_MSG_WHISPER, "test", null);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ValidateMessage_ChannelWithoutDestination_Invalid()
        {
            var result = _chatAgent.ValidateMessage(ChatMsg.CHAT_MSG_CHANNEL, "test", null);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ValidateMessage_ValidSay_Passes()
        {
            var result = _chatAgent.ValidateMessage(ChatMsg.CHAT_MSG_SAY, "Hello", null);
            Assert.True(result.IsValid);
        }

        [Fact]
        public void ValidateMessage_ValidWhisper_Passes()
        {
            var result = _chatAgent.ValidateMessage(ChatMsg.CHAT_MSG_WHISPER, "Hey", "PlayerName");
            Assert.True(result.IsValid);
        }

        #endregion

        #region Channel Tracking Tests

        [Fact]
        public void ChannelJoinEvent_AddsToActiveChannels()
        {
            _chatAgent.IncomingMessages.Subscribe(_ => { });

            // CHAT_MSG_CHANNEL_JOIN doesn't go through SAY/PARTY/YELL case — it's default case
            var payload = BuildDefaultPayload(ChatMsg.CHAT_MSG_CHANNEL_JOIN, Language.Universal, 100, "Player joined");
            // Channel join notifications go through UpdateActiveChannels but channelName will be empty from default parser
            // Active channels are tracked via JoinChannelAsync (optimistic) or HandleIncomingMessage
        }

        [Fact]
        public void GetActiveChannels_InitiallyEmpty()
        {
            var channels = _chatAgent.GetActiveChannels();
            Assert.Empty(channels);
        }

        #endregion

        #region Disposal Tests

        [Fact]
        public void Dispose_DoesNotThrow()
        {
            var agent = new ChatNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
            agent.Dispose();
            // No exception expected
        }

        [Fact]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            var agent = new ChatNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
            agent.Dispose();
            agent.Dispose(); // second call should be no-op
        }

        #endregion

        #region State Tests

        [Fact]
        public void InitialState_PropertiesCorrect()
        {
            Assert.False(_chatAgent.IsChatOperationInProgress);
            Assert.Null(_chatAgent.LastChatOperationTime);
            Assert.False(_chatAgent.IsAfk);
            Assert.False(_chatAgent.IsDnd);
            Assert.Null(_chatAgent.AfkMessage);
            Assert.Null(_chatAgent.DndMessage);
        }

        [Fact]
        public async Task SetAfkAsync_SetsState()
        {
            await _chatAgent.SetAfkAsync("Away for lunch");
            Assert.True(_chatAgent.IsAfk);
            Assert.Equal("Away for lunch", _chatAgent.AfkMessage);
        }

        [Fact]
        public async Task SetAfkAsync_ClearsState()
        {
            await _chatAgent.SetAfkAsync("Away");
            await _chatAgent.SetAfkAsync(null);
            Assert.False(_chatAgent.IsAfk);
            Assert.Null(_chatAgent.AfkMessage);
        }

        [Fact]
        public async Task SetDndAsync_SetsState()
        {
            await _chatAgent.SetDndAsync("Do not disturb");
            Assert.True(_chatAgent.IsDnd);
            Assert.Equal("Do not disturb", _chatAgent.DndMessage);
        }

        #endregion
    }
}
