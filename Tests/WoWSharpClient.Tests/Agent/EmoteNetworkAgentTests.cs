using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents;
using WoWSharpClient.Networking.ClientComponents.Models;
using Xunit;

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

            // Wire opcode streams before creating the agent so it subscribes to them
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
            // Arrange & Act
            var agent = new EmoteNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.False(agent.IsEmoting);
            Assert.Null(agent.LastEmote);
            Assert.Null(agent.LastTextEmote);
            Assert.Null(agent.LastEmoteTime);
        }

        [Fact]
        public void Constructor_NullWorldClient_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new EmoteNetworkClientComponent(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new EmoteNetworkClientComponent(_mockWorldClient.Object, null!));
        }

        #endregion

        #region Emote Operation Tests

        [Fact]
        public async Task PerformEmoteAsync_ValidEmote_SendsPacket()
        {
            // Arrange
            var emote = Emote.EMOTE_ONESHOT_WAVE;
            byte[]? capturedPacket = null;
            Opcode capturedOpcode = default;

            _mockWorldClient.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Callback<Opcode, byte[], CancellationToken>((opcode, packet, _) =>
                {
                    capturedOpcode = opcode;
                    capturedPacket = packet;
                })
                .Returns(Task.CompletedTask);

            // Act
            await _agent.PerformEmoteAsync(emote);

            // Assert
            Assert.Equal(Opcode.CMSG_EMOTE, capturedOpcode);
            Assert.NotNull(capturedPacket);
            Assert.Equal(4, capturedPacket.Length);
            Assert.Equal((uint)emote, BitConverter.ToUInt32(capturedPacket, 0));
            Assert.Equal(emote, _agent.LastEmote);
            Assert.NotNull(_agent.LastEmoteTime);
            Assert.False(_agent.IsEmoting);
        }

        [Fact]
        public async Task PerformEmoteAsync_InvalidEmote_DoesNotSendPacket()
        {
            // Arrange
            var invalidEmote = Emote.EMOTE_ONESHOT_NONE;

            // Act
            await _agent.PerformEmoteAsync(invalidEmote);

            // Assert
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
            Assert.Null(_agent.LastEmote);
        }

        [Fact]
        public async Task PerformTextEmoteAsync_ValidTextEmote_SendsPacket()
        {
            // Arrange
            var textEmote = TextEmote.TEXTEMOTE_HELLO;
            ulong? targetGuid = 0x12345678;
            byte[]? capturedPacket = null;
            Opcode capturedOpcode = default;

            _mockWorldClient.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Callback<Opcode, byte[], CancellationToken>((opcode, packet, _) =>
                {
                    capturedOpcode = opcode;
                    capturedPacket = packet;
                })
                .Returns(Task.CompletedTask);

            // Act
            await _agent.PerformTextEmoteAsync(textEmote, targetGuid);

            // Assert
            Assert.Equal(Opcode.CMSG_TEXT_EMOTE, capturedOpcode);
            Assert.NotNull(capturedPacket);
            Assert.Equal(12, capturedPacket.Length);
            Assert.Equal((uint)textEmote, BitConverter.ToUInt32(capturedPacket, 0));
            Assert.Equal(targetGuid.Value, BitConverter.ToUInt64(capturedPacket, 4));
            Assert.Equal(textEmote, _agent.LastTextEmote);
            Assert.NotNull(_agent.LastEmoteTime);
            Assert.False(_agent.IsEmoting);
        }

        [Fact]
        public async Task PerformTextEmoteAsync_NoTarget_SendsPacketWithZeroGuid()
        {
            // Arrange
            var textEmote = TextEmote.TEXTEMOTE_HELLO;
            byte[]? capturedPacket = null;

            _mockWorldClient.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Callback<Opcode, byte[], CancellationToken>((opcode, packet, _) =>
                {
                    capturedPacket = packet;
                })
                .Returns(Task.CompletedTask);

            // Act
            await _agent.PerformTextEmoteAsync(textEmote);

            // Assert
            Assert.NotNull(capturedPacket);
            Assert.Equal(12, capturedPacket.Length);
            Assert.Equal((uint)textEmote, BitConverter.ToUInt32(capturedPacket, 0));
            Assert.Equal(0UL, BitConverter.ToUInt64(capturedPacket, 4));
        }

        #endregion

        #region Convenience Method Tests

        [Fact]
        public async Task WaveAsync_CallsPerformEmoteWithWave()
        {
            // Arrange
            _mockWorldClient.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _agent.WaveAsync();

            // Assert
            Assert.Equal(Emote.EMOTE_ONESHOT_WAVE, _agent.LastEmote);
        }

        [Fact]
        public async Task DanceAsync_CallsPerformEmoteWithDance()
        {
            // Arrange
            _mockWorldClient.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _agent.DanceAsync();

            // Assert
            Assert.Equal(Emote.EMOTE_STATE_DANCE, _agent.LastEmote);
        }

        [Fact]
        public async Task BowAsync_CallsPerformEmoteWithBow()
        {
            // Arrange
            _mockWorldClient.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _agent.BowAsync();

            // Assert
            Assert.Equal(Emote.EMOTE_ONESHOT_BOW, _agent.LastEmote);
        }

        [Fact]
        public async Task HelloAsync_CallsPerformTextEmoteWithHello()
        {
            // Arrange
            _mockWorldClient.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _agent.HelloAsync();

            // Assert
            Assert.Equal(TextEmote.TEXTEMOTE_HELLO, _agent.LastTextEmote);
        }

        [Fact]
        public async Task SitAsync_CallsPerformEmoteWithSit()
        {
            // Arrange
            _mockWorldClient.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _agent.SitAsync();

            // Assert
            Assert.Equal(Emote.EMOTE_STATE_SIT, _agent.LastEmote);
        }

        [Fact]
        public async Task StandAsync_CallsPerformEmoteWithStand()
        {
            // Arrange
            _mockWorldClient.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _agent.StandAsync();

            // Assert
            Assert.Equal(Emote.EMOTE_STATE_STAND, _agent.LastEmote);
        }

        #endregion

        #region Utility Method Tests

        [Theory]
        [InlineData(Emote.EMOTE_ONESHOT_WAVE, true)]
        [InlineData(Emote.EMOTE_STATE_DANCE, true)]
        [InlineData(Emote.EMOTE_ONESHOT_BOW, true)]
        [InlineData(Emote.EMOTE_ONESHOT_NONE, false)]
        public void IsValidEmote_VariousEmotes_ReturnsExpectedResult(Emote emote, bool expected)
        {
            // Act
            var result = _agent.IsValidEmote(emote);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(TextEmote.TEXTEMOTE_HELLO, true)]
        [InlineData(TextEmote.TEXTEMOTE_DANCE, true)]
        [InlineData(TextEmote.TEXTEMOTE_BYE, true)]
        public void IsValidTextEmote_VariousTextEmotes_ReturnsTrue(TextEmote textEmote, bool expected)
        {
            // Act
            var result = _agent.IsValidTextEmote(textEmote);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(Emote.EMOTE_ONESHOT_WAVE, "Wave")]
        [InlineData(Emote.EMOTE_STATE_DANCE, "Dance")]
        [InlineData(Emote.EMOTE_ONESHOT_BOW, "Bow")]
        [InlineData(Emote.EMOTE_ONESHOT_LAUGH, "Laugh")]
        public void GetEmoteName_KnownEmotes_ReturnsCorrectName(Emote emote, string expectedName)
        {
            // Act
            var result = _agent.GetEmoteName(emote);

            // Assert
            Assert.Equal(expectedName, result);
        }

        [Theory]
        [InlineData(TextEmote.TEXTEMOTE_HELLO, "Hello")]
        [InlineData(TextEmote.TEXTEMOTE_BYE, "Bye")]
        [InlineData(TextEmote.TEXTEMOTE_DANCE, "Dance")]
        [InlineData(TextEmote.TEXTEMOTE_CONGRATULATE, "Thank")]
        public void GetTextEmoteName_KnownTextEmotes_ReturnsCorrectName(TextEmote textEmote, string expectedName)
        {
            // Act
            var result = _agent.GetTextEmoteName(textEmote);

            // Assert
            Assert.Equal(expectedName, result);
        }

        #endregion

        #region Observable Tests

        [Fact]
        public void AnimatedEmotes_Emits_OnServerSendsSmsgEmote()
        {
            // Arrange
            WoWSharpClient.Networking.ClientComponents.Models.EmoteData? received = null;
            using var sub = _agent.AnimatedEmotes.Subscribe(e => received = e);

            // Simulate server SMSG_EMOTE payload: [uint32 emoteId]
            var payload = new byte[4];
            BitConverter.GetBytes((uint)Emote.EMOTE_ONESHOT_WAVE).CopyTo(payload, 0);
            _smsgEmoteStream.OnNext(payload);

            // Assert
            Assert.NotNull(received);
            Assert.Equal((uint)Emote.EMOTE_ONESHOT_WAVE, received!.EmoteId);
            Assert.Equal("Wave", received.EmoteName);
        }

        [Fact]
        public void TextEmotes_Emits_OnServerSendsSmsgTextEmote()
        {
            // Arrange
            WoWSharpClient.Networking.ClientComponents.Models.EmoteData? received = null;
            using var sub = _agent.TextEmotes.Subscribe(e => received = e);

            // Simulate server SMSG_TEXT_EMOTE payload: [uint64 sourceGuid][uint32 textEmote]
            var payload = new byte[12];
            BitConverter.GetBytes(0x1111UL).CopyTo(payload, 0);
            BitConverter.GetBytes((uint)TextEmote.TEXTEMOTE_HELLO).CopyTo(payload, 8);
            _smsgTextEmoteStream.OnNext(payload);

            // Assert
            Assert.NotNull(received);
            Assert.Equal((uint)TextEmote.TEXTEMOTE_HELLO, received!.EmoteId);
            Assert.Equal("Hello", received.EmoteName);
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task PerformEmoteAsync_WorldClientThrowsException_RethrowsAndResetsState()
        {
            // Arrange
            var emote = Emote.EMOTE_ONESHOT_WAVE;
            var testException = new InvalidOperationException("Test exception");

            _mockWorldClient.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(testException);

            // Act & Assert
            var thrownException = await Assert.ThrowsAsync<InvalidOperationException>(() => _agent.PerformEmoteAsync(emote));
            Assert.Same(testException, thrownException);
            Assert.False(_agent.IsEmoting);
        }

        [Fact]
        public async Task PerformTextEmoteAsync_WorldClientThrowsException_RethrowsAndResetsState()
        {
            // Arrange
            var textEmote = TextEmote.TEXTEMOTE_HELLO;
            var testException = new InvalidOperationException("Test exception");

            _mockWorldClient.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(testException);

            // Act & Assert
            var thrownException = await Assert.ThrowsAsync<InvalidOperationException>(() => _agent.PerformTextEmoteAsync(textEmote));
            Assert.Same(testException, thrownException);
            Assert.False(_agent.IsEmoting);
        }

        #endregion

        #region Cancellation Tests

        [Fact]
        public async Task PerformEmoteAsync_WithCancellationToken_PassesToWorldClient()
        {
            // Arrange
            var emote = Emote.EMOTE_ONESHOT_WAVE;
            var cancellationToken = new CancellationToken();
            CancellationToken capturedToken = default;

            _mockWorldClient.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Callback<Opcode, byte[], CancellationToken>((_, _, token) => capturedToken = token)
                .Returns(Task.CompletedTask);

            // Act
            await _agent.PerformEmoteAsync(emote, cancellationToken);

            // Assert
            Assert.Equal(cancellationToken, capturedToken);
        }

        [Fact]
        public async Task PerformTextEmoteAsync_WithCancellationToken_PassesToWorldClient()
        {
            // Arrange
            var textEmote = TextEmote.TEXTEMOTE_HELLO;
            var cancellationToken = new CancellationToken();
            CancellationToken capturedToken = default;

            _mockWorldClient.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Callback<Opcode, byte[], CancellationToken>((_, _, token) => capturedToken = token)
                .Returns(Task.CompletedTask);

            // Act
            await _agent.PerformTextEmoteAsync(textEmote, null, cancellationToken);

            // Assert
            Assert.Equal(cancellationToken, capturedToken);
        }

        #endregion
    }
}