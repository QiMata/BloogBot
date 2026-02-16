using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents;

namespace WoWSharpClient.Tests.Agent
{
    public class SpellCastingNetworkClientComponentTests
    {
        private readonly Mock<IWorldClient> _mockWorld;
        private readonly SpellCastingNetworkClientComponent _agent;

        public SpellCastingNetworkClientComponentTests()
        {
            _mockWorld = new Mock<IWorldClient>();
            _mockWorld.Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                      .Returns(Task.CompletedTask);
            _agent = new SpellCastingNetworkClientComponent(_mockWorld.Object, Mock.Of<ILogger<SpellCastingNetworkClientComponent>>());
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullWorldClient_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new SpellCastingNetworkClientComponent(null!, Mock.Of<ILogger<SpellCastingNetworkClientComponent>>()));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new SpellCastingNetworkClientComponent(_mockWorld.Object, null!));
        }

        #endregion

        #region CMSG_CAST_SPELL Tests

        [Fact]
        public async Task CastSpellAsync_SelfCast_SendsSpellIdPlusTargetFlagSelf()
        {
            const uint spellId = 133; // Fireball
            await _agent.CastSpellAsync(spellId);

            _mockWorld.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_CAST_SPELL,
                It.Is<byte[]>(p =>
                    p.Length == 6 && // spellId(4) + targetMask(2) = 6 bytes, NO castCount
                    BitConverter.ToUInt32(p, 0) == spellId &&
                    BitConverter.ToUInt16(p, 4) == 0x0000), // TARGET_FLAG_SELF
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CastSpellOnTargetAsync_SendsSpellIdPlusTargetFlagUnit()
        {
            const uint spellId = 585; // Smite
            const ulong targetGuid = 0x123456789ABCDEF0;

            await _agent.CastSpellOnTargetAsync(spellId, targetGuid);

            _mockWorld.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_CAST_SPELL,
                It.Is<byte[]>(p =>
                    p.Length >= 6 && // at least spellId(4) + targetMask(2) + packed guid
                    BitConverter.ToUInt32(p, 0) == spellId &&
                    BitConverter.ToUInt16(p, 4) == 0x0002), // TARGET_FLAG_UNIT
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CastSpellAtLocationAsync_SendsTargetFlagDestLocation()
        {
            const uint spellId = 2120; // Flamestrike
            float x = 100f, y = 200f, z = 50f;

            await _agent.CastSpellAtLocationAsync(spellId, x, y, z);

            _mockWorld.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_CAST_SPELL,
                It.Is<byte[]>(p =>
                    p.Length == 18 && // spellId(4) + targetMask(2) + x(4) + y(4) + z(4) = 18
                    BitConverter.ToUInt32(p, 0) == spellId &&
                    BitConverter.ToUInt16(p, 4) == 0x0040 && // TARGET_FLAG_DEST_LOCATION
                    Math.Abs(BitConverter.ToSingle(p, 6) - 100f) < 0.001f &&
                    Math.Abs(BitConverter.ToSingle(p, 10) - 200f) < 0.001f &&
                    Math.Abs(BitConverter.ToSingle(p, 14) - 50f) < 0.001f),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region CMSG_CANCEL_CAST Tests

        [Fact]
        public async Task InterruptCastAsync_SendsCurrentSpellId()
        {
            // Set a current spell via state update
            _agent.UpdateSpellCastStarted(133, 3000);

            await _agent.InterruptCastAsync();

            _mockWorld.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_CANCEL_CAST,
                It.Is<byte[]>(p =>
                    p.Length == 4 &&
                    BitConverter.ToUInt32(p, 0) == 133), // sends the actual spellId
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InterruptCastAsync_NoCurrentSpell_SendsZero()
        {
            await _agent.InterruptCastAsync();

            _mockWorld.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_CANCEL_CAST,
                It.Is<byte[]>(p =>
                    p.Length == 4 &&
                    BitConverter.ToUInt32(p, 0) == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region CMSG_CANCEL_CHANNELLING Tests

        [Fact]
        public async Task StopChannelingAsync_SendsCurrentSpellId()
        {
            _agent.UpdateChannelingStarted(740, 5000); // Tranquility

            await _agent.StopChannelingAsync();

            _mockWorld.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_CANCEL_CHANNELLING,
                It.Is<byte[]>(p =>
                    p.Length == 4 &&
                    BitConverter.ToUInt32(p, 0) == 740),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region CMSG_CANCEL_AUTO_REPEAT_SPELL Tests

        [Fact]
        public async Task StopAutoRepeatSpellAsync_SendsEmptyPayload()
        {
            await _agent.StopAutoRepeatSpellAsync();

            _mockWorld.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_CANCEL_AUTO_REPEAT_SPELL,
                It.Is<byte[]>(p => p.Length == 0), // empty packet!
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region State Tracking Tests

        [Fact]
        public void UpdateSpellCastStarted_SetsState()
        {
            _agent.UpdateSpellCastStarted(100, 2000, 0xBEEF);

            Assert.True(_agent.IsCasting);
            Assert.Equal(100u, _agent.CurrentSpellId);
            Assert.Equal(0xBEEFUL, _agent.CurrentSpellTarget);
            Assert.Equal(2000u, _agent.RemainingCastTime);
        }

        [Fact]
        public void UpdateSpellCastCompleted_ClearsState()
        {
            _agent.UpdateSpellCastStarted(100, 2000);
            _agent.UpdateSpellCastCompleted(100);

            Assert.False(_agent.IsCasting);
            Assert.Null(_agent.CurrentSpellId);
            Assert.Null(_agent.CurrentSpellTarget);
            Assert.Equal(0u, _agent.RemainingCastTime);
        }

        [Fact]
        public void CanCastSpell_WhileCasting_ReturnsFalse()
        {
            _agent.UpdateSpellCastStarted(100, 2000);
            Assert.False(_agent.CanCastSpell(200));
        }

        [Fact]
        public void CanCastSpell_NotCasting_ReturnsTrue()
        {
            Assert.True(_agent.CanCastSpell(200));
        }

        #endregion
    }
}
