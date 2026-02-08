using System.Reactive.Linq;
using System.Reactive.Subjects;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents;

namespace WoWSharpClient.Tests.Agent
{
    /// <summary>
    /// Tests for the EmoteNetworkClientComponent class.
    /// </summary>
    public class EmoteNetworkClientComponentTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILogger<EmoteNetworkClientComponent>> _mockLogger;
        private readonly Subject<ReadOnlyMemory<byte>> _smsgEmoteStream = new();
        private readonly Subject<ReadOnlyMemory<byte>> _smsgTextEmoteStream = new();
        private readonly EmoteNetworkClientComponent _agent;

        public EmoteNetworkClientComponentTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLogger = new Mock<ILogger<EmoteNetworkClientComponent>>();

            _mockWorldClient
                .Setup(x => x.RegisterOpcodeHandler(Opcode.SMSG_EMOTE))
                .Returns(_smsgEmoteStream.AsObservable());
            _mockWorldClient
                .Setup(x => x.RegisterOpcodeHandler(Opcode.SMSG_TEXT_EMOTE))
                .Returns(_smsgTextEmoteStream.AsObservable());

            _agent = new EmoteNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_ValidParameters_InitializesCorrectly()
        {
            var agent = new EmoteNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
            Assert.NotNull(agent);
            Assert.False(agent.IsEmoting);
            Assert.Null(agent.LastEmote);
            Assert.Null(agent.LastTextEmote);
            Assert.Null(agent.LastEmoteTime);
        }

        [Fact]
        public void Constructor_NullWorldClient_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new EmoteNetworkClientComponent(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new EmoteNetworkClientComponent(_mockWorldClient.Object, null!));
        }

        #endregion

        #region CMSG_EMOTE Tests

        [Fact]
        public async Task PerformEmoteAsync_ValidEmote_Sends4BytePacket()
        {
            var emote = Emote.EMOTE_ONESHOT_WAVE;
            byte[]? capturedPacket = null;

            _mockWorldClient.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Callback<Opcode, byte[], CancellationToken>((_, packet, _) => capturedPacket = packet)
                .Returns(Task.CompletedTask);

            await _agent.PerformEmoteAsync(emote);

            Assert.NotNull(capturedPacket);
            Assert.Equal(4, capturedPacket.Length);
            Assert.Equal((uint)emote, BitConverter.ToUInt32(capturedPacket, 0));
            Assert.Equal(emote, _agent.LastEmote);
        }

        [Fact]
        public async Task PerformEmoteAsync_InvalidEmote_DoesNotSendPacket()
        {
            await _agent.PerformEmoteAsync(Emote.EMOTE_ONESHOT_NONE);
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
            Assert.Null(_agent.LastEmote);
        }

        #endregion

        #region CMSG_TEXT_EMOTE Tests (Fixed: emoteNum field)

        [Fact]
        public async Task PerformTextEmoteAsync_ValidEmote_Sends16BytePacket()
        {
            // MaNGOS expects: uint32 textEmoteId + uint32 emoteNum + uint64 targetGuid = 16 bytes
            var textEmote = TextEmote.TEXTEMOTE_HELLO;
            ulong? targetGuid = 0x12345678;
            byte[]? capturedPacket = null;

            _mockWorldClient.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Callback<Opcode, byte[], CancellationToken>((_, packet, _) => capturedPacket = packet)
                .Returns(Task.CompletedTask);

            await _agent.PerformTextEmoteAsync(textEmote, targetGuid);

            Assert.NotNull(capturedPacket);
            Assert.Equal(16, capturedPacket.Length); // Was 12 before fix
            Assert.Equal((uint)textEmote, BitConverter.ToUInt32(capturedPacket, 0));  // textEmoteId
            Assert.Equal(0u, BitConverter.ToUInt32(capturedPacket, 4));               // emoteNum (new field)
            Assert.Equal(targetGuid.Value, BitConverter.ToUInt64(capturedPacket, 8)); // targetGuid (shifted from offset 4 to 8)
        }

        [Fact]
        public async Task PerformTextEmoteAsync_NoTarget_Sends16BytesWithZeroGuid()
        {
            var textEmote = TextEmote.TEXTEMOTE_BYE;
            byte[]? capturedPacket = null;

            _mockWorldClient.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Callback<Opcode, byte[], CancellationToken>((_, packet, _) => capturedPacket = packet)
                .Returns(Task.CompletedTask);

            await _agent.PerformTextEmoteAsync(textEmote);

            Assert.NotNull(capturedPacket);
            Assert.Equal(16, capturedPacket.Length);
            Assert.Equal((uint)textEmote, BitConverter.ToUInt32(capturedPacket, 0));
            Assert.Equal(0u, BitConverter.ToUInt32(capturedPacket, 4)); // emoteNum
            Assert.Equal(0UL, BitConverter.ToUInt64(capturedPacket, 8)); // no target
        }

        [Fact]
        public async Task PerformTextEmoteAsync_InvalidEmote_DoesNotSend()
        {
            await _agent.PerformTextEmoteAsync((TextEmote)999999);
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        #endregion

        #region SMSG_EMOTE Parser Tests (Fixed: source GUID)

        [Fact]
        public void ParseSmsgEmote_FullPayload_ExtractsSourceGuid()
        {
            // MaNGOS format: uint32 emoteId + ObjectGuid(8) = 12 bytes
            WoWSharpClient.Networking.ClientComponents.Models.EmoteData? received = null;
            using var sub = _agent.AnimatedEmotes.Subscribe(e => received = e);

            var payload = new byte[12];
            BitConverter.GetBytes((uint)Emote.EMOTE_ONESHOT_WAVE).CopyTo(payload, 0);
            BitConverter.GetBytes(0xABCD1234UL).CopyTo(payload, 4); // source GUID

            _smsgEmoteStream.OnNext(payload);

            Assert.NotNull(received);
            Assert.Equal((uint)Emote.EMOTE_ONESHOT_WAVE, received!.EmoteId);
            Assert.Equal("Wave", received.EmoteName);
            Assert.Equal(0xABCD1234UL, received.TargetGuid); // now populated from source GUID
        }

        [Fact]
        public void ParseSmsgEmote_4BytePayload_StillParsesEmoteId()
        {
            WoWSharpClient.Networking.ClientComponents.Models.EmoteData? received = null;
            using var sub = _agent.AnimatedEmotes.Subscribe(e => received = e);

            var payload = new byte[4];
            BitConverter.GetBytes((uint)Emote.EMOTE_ONESHOT_BOW).CopyTo(payload, 0);

            _smsgEmoteStream.OnNext(payload);

            Assert.NotNull(received);
            Assert.Equal((uint)Emote.EMOTE_ONESHOT_BOW, received!.EmoteId);
            Assert.Null(received.TargetGuid); // no GUID in short payload
        }

        #endregion

        #region SMSG_TEXT_EMOTE Parser Tests (Fixed: full format)

        [Fact]
        public void ParseSmsgTextEmote_FullPayload_ExtractsAllFields()
        {
            // MaNGOS format: ObjectGuid(8) + uint32 textEmoteId + uint32 emoteNum + uint32 nameLen + name[]
            WoWSharpClient.Networking.ClientComponents.Models.EmoteData? received = null;
            using var sub = _agent.TextEmotes.Subscribe(e => received = e);

            var name = "Targetname"u8;
            var payload = new byte[20 + name.Length];
            BitConverter.GetBytes(0x1111UL).CopyTo(payload, 0);                        // source GUID
            BitConverter.GetBytes((uint)TextEmote.TEXTEMOTE_HELLO).CopyTo(payload, 8);  // textEmoteId
            BitConverter.GetBytes(22u).CopyTo(payload, 12);                             // emoteNum (animation)
            BitConverter.GetBytes((uint)name.Length).CopyTo(payload, 16);               // nameLen
            name.CopyTo(payload.AsSpan(20));                                            // name

            _smsgTextEmoteStream.OnNext(payload);

            Assert.NotNull(received);
            Assert.Equal((uint)TextEmote.TEXTEMOTE_HELLO, received!.EmoteId);
            Assert.Equal("Hello", received.EmoteName);
            Assert.Equal(0x1111UL, received.TargetGuid); // source GUID
            Assert.Equal("Targetname", received.TargetName);
        }

        [Fact]
        public void ParseSmsgTextEmote_NoName_StillParsesEmote()
        {
            WoWSharpClient.Networking.ClientComponents.Models.EmoteData? received = null;
            using var sub = _agent.TextEmotes.Subscribe(e => received = e);

            var payload = new byte[20]; // guid(8)+textEmote(4)+emoteNum(4)+nameLen(4)
            BitConverter.GetBytes(0x2222UL).CopyTo(payload, 0);
            BitConverter.GetBytes((uint)TextEmote.TEXTEMOTE_BYE).CopyTo(payload, 8);
            BitConverter.GetBytes(0u).CopyTo(payload, 12); // emoteNum
            BitConverter.GetBytes(0u).CopyTo(payload, 16); // nameLen = 0

            _smsgTextEmoteStream.OnNext(payload);

            Assert.NotNull(received);
            Assert.Equal((uint)TextEmote.TEXTEMOTE_BYE, received!.EmoteId);
            Assert.Null(received.TargetName);
        }

        [Fact]
        public void ParseSmsgTextEmote_12BytePayload_ParsesGuidAndEmote()
        {
            WoWSharpClient.Networking.ClientComponents.Models.EmoteData? received = null;
            using var sub = _agent.TextEmotes.Subscribe(e => received = e);

            var payload = new byte[12]; // just guid(8) + textEmote(4)
            BitConverter.GetBytes(0x3333UL).CopyTo(payload, 0);
            BitConverter.GetBytes((uint)TextEmote.TEXTEMOTE_WAVE).CopyTo(payload, 8);

            _smsgTextEmoteStream.OnNext(payload);

            Assert.NotNull(received);
            Assert.Equal((uint)TextEmote.TEXTEMOTE_WAVE, received!.EmoteId);
            Assert.Equal("Wave", received.EmoteName);
        }

        #endregion

        #region Convenience Method Tests

        [Fact]
        public async Task WaveAsync_CallsPerformEmoteWithWave()
        {
            _mockWorldClient.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            await _agent.WaveAsync();
            Assert.Equal(Emote.EMOTE_ONESHOT_WAVE, _agent.LastEmote);
        }

        [Fact]
        public async Task DanceAsync_CallsPerformEmoteWithDance()
        {
            _mockWorldClient.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            await _agent.DanceAsync();
            Assert.Equal(Emote.EMOTE_STATE_DANCE, _agent.LastEmote);
        }

        [Fact]
        public async Task HelloAsync_CallsPerformTextEmoteWithHello()
        {
            _mockWorldClient.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            await _agent.HelloAsync();
            Assert.Equal(TextEmote.TEXTEMOTE_HELLO, _agent.LastTextEmote);
        }

        #endregion

        #region Utility Tests

        [Theory]
        [InlineData(Emote.EMOTE_ONESHOT_WAVE, true)]
        [InlineData(Emote.EMOTE_STATE_DANCE, true)]
        [InlineData(Emote.EMOTE_ONESHOT_NONE, false)]
        public void IsValidEmote_VariousEmotes_ReturnsExpectedResult(Emote emote, bool expected)
        {
            Assert.Equal(expected, _agent.IsValidEmote(emote));
        }

        [Theory]
        [InlineData(TextEmote.TEXTEMOTE_HELLO, true)]
        [InlineData(TextEmote.TEXTEMOTE_DANCE, true)]
        public void IsValidTextEmote_VariousTextEmotes_ReturnsTrue(TextEmote textEmote, bool expected)
        {
            Assert.Equal(expected, _agent.IsValidTextEmote(textEmote));
        }

        [Theory]
        [InlineData(Emote.EMOTE_ONESHOT_WAVE, "Wave")]
        [InlineData(Emote.EMOTE_STATE_DANCE, "Dance")]
        [InlineData(Emote.EMOTE_ONESHOT_BOW, "Bow")]
        public void GetEmoteName_KnownEmotes_ReturnsCorrectName(Emote emote, string expectedName)
        {
            Assert.Equal(expectedName, _agent.GetEmoteName(emote));
        }

        [Theory]
        [InlineData(TextEmote.TEXTEMOTE_HELLO, "Hello")]
        [InlineData(TextEmote.TEXTEMOTE_BYE, "Bye")]
        [InlineData(TextEmote.TEXTEMOTE_CONGRATULATE, "Thank")]
        public void GetTextEmoteName_KnownTextEmotes_ReturnsCorrectName(TextEmote textEmote, string expectedName)
        {
            Assert.Equal(expectedName, _agent.GetTextEmoteName(textEmote));
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task PerformEmoteAsync_WorldClientThrows_RethrowsAndResetsState()
        {
            var ex = new InvalidOperationException("Test");
            _mockWorldClient.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(ex);

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => _agent.PerformEmoteAsync(Emote.EMOTE_ONESHOT_WAVE));
            Assert.Same(ex, thrown);
            Assert.False(_agent.IsEmoting);
        }

        [Fact]
        public async Task PerformTextEmoteAsync_WorldClientThrows_RethrowsAndResetsState()
        {
            var ex = new InvalidOperationException("Test");
            _mockWorldClient.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(ex);

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => _agent.PerformTextEmoteAsync(TextEmote.TEXTEMOTE_HELLO));
            Assert.Same(ex, thrown);
            Assert.False(_agent.IsEmoting);
        }

        #endregion

        #region Cancellation Tests

        [Fact]
        public async Task PerformEmoteAsync_PassesCancellationToken()
        {
            var ct = new CancellationToken();
            CancellationToken captured = default;

            _mockWorldClient.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Callback<Opcode, byte[], CancellationToken>((_, _, token) => captured = token)
                .Returns(Task.CompletedTask);

            await _agent.PerformEmoteAsync(Emote.EMOTE_ONESHOT_WAVE, ct);
            Assert.Equal(ct, captured);
        }

        [Fact]
        public async Task PerformTextEmoteAsync_PassesCancellationToken()
        {
            var ct = new CancellationToken();
            CancellationToken captured = default;

            _mockWorldClient.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Callback<Opcode, byte[], CancellationToken>((_, _, token) => captured = token)
                .Returns(Task.CompletedTask);

            await _agent.PerformTextEmoteAsync(TextEmote.TEXTEMOTE_HELLO, null, ct);
            Assert.Equal(ct, captured);
        }

        #endregion
    }
}
