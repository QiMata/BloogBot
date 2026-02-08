using System.Buffers.Binary;
using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using GameData.Core.Enums;
using WoWSharpClient.Networking.ClientComponents;
using WoWSharpClient.Networking.ClientComponents.I;
using System.Reactive.Linq;

namespace WoWSharpClient.Tests.Agent
{
    /// <summary>
    /// Unit tests for the ProfessionsNetworkClientComponent class.
    /// Tests CMSG payload formats and SMSG parser correctness against MaNGOS 1.12.1.
    /// </summary>
    public class ProfessionsNetworkClientComponentTests
    {
        private const ulong TestTrainerGuid = 0x0000DEAD0000BEEF;
        private const ulong TestNodeGuid = 0x0000CAFE0000BABE;

        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILogger<ProfessionsNetworkClientComponent>> _mockLogger;
        private readonly ProfessionsNetworkClientComponent _agent;

        public ProfessionsNetworkClientComponentTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLogger = new Mock<ILogger<ProfessionsNetworkClientComponent>>();
            _agent = new ProfessionsNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
        {
            Assert.NotNull(_agent);
            Assert.False(_agent.IsTrainerWindowOpen);
            Assert.False(_agent.IsCraftingWindowOpen);
            Assert.Null(_agent.CurrentTrainerGuid);
            Assert.Null(_agent.CurrentProfession);
        }

        [Fact]
        public void Constructor_WithNullWorldClient_ShouldThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ProfessionsNetworkClientComponent(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ProfessionsNetworkClientComponent(_mockWorldClient.Object, null!));
        }

        #endregion

        #region CMSG_GOSSIP_HELLO (OpenProfessionTrainer) — payload: trainerGuid(8)

        [Fact]
        public async Task OpenProfessionTrainerAsync_ShouldSend_GossipHello_WithGuid8()
        {
            await _agent.OpenProfessionTrainerAsync(TestTrainerGuid, ProfessionType.Alchemy);

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GOSSIP_HELLO,
                It.Is<byte[]>(p =>
                    p.Length == 8 &&
                    BitConverter.ToUInt64(p, 0) == TestTrainerGuid),
                It.IsAny<CancellationToken>()), Times.Once);

            Assert.True(_agent.IsTrainerWindowOpen);
            Assert.Equal(TestTrainerGuid, _agent.CurrentTrainerGuid);
            Assert.Equal(ProfessionType.Alchemy, _agent.CurrentProfession);
        }

        #endregion

        #region CMSG_TRAINER_LIST (RequestProfessionServices) — payload: trainerGuid(8)

        [Fact]
        public async Task RequestProfessionServicesAsync_ShouldSend_TrainerList_WithGuid8()
        {
            await _agent.RequestProfessionServicesAsync(TestTrainerGuid);

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_TRAINER_LIST,
                It.Is<byte[]>(p =>
                    p.Length == 8 &&
                    BitConverter.ToUInt64(p, 0) == TestTrainerGuid),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region CMSG_TRAINER_BUY_SPELL (LearnProfessionSkill) — payload: trainerGuid(8) + spellId(4)

        [Fact]
        public async Task LearnProfessionSkillAsync_ShouldSend_TrainerBuySpell_WithGuid8_SpellId4()
        {
            const uint spellId = 12345;

            await _agent.LearnProfessionSkillAsync(TestTrainerGuid, spellId);

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_TRAINER_BUY_SPELL,
                It.Is<byte[]>(p =>
                    p.Length == 12 &&
                    BitConverter.ToUInt64(p, 0) == TestTrainerGuid &&
                    BitConverter.ToUInt32(p, 8) == spellId),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task LearnMultipleProfessionSkillsAsync_ShouldSendOnePacketPerSpell()
        {
            uint[] spellIds = { 111, 222, 333 };

            await _agent.LearnMultipleProfessionSkillsAsync(TestTrainerGuid, spellIds);

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_TRAINER_BUY_SPELL,
                It.Is<byte[]>(p =>
                    p.Length == 12 &&
                    BitConverter.ToUInt64(p, 0) == TestTrainerGuid),
                It.IsAny<CancellationToken>()), Times.Exactly(3));
        }

        [Fact]
        public async Task LearnMultipleProfessionSkillsAsync_WithEmptyArray_ShouldNotSendPackets()
        {
            await _agent.LearnMultipleProfessionSkillsAsync(TestTrainerGuid, Array.Empty<uint>());

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_TRAINER_BUY_SPELL,
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        #endregion

        #region CloseProfessionTrainer — local state only, no packet sent

        [Fact]
        public async Task CloseProfessionTrainerAsync_ShouldNotSendAnyPacket()
        {
            await _agent.OpenProfessionTrainerAsync(TestTrainerGuid, ProfessionType.Alchemy);
            _mockWorldClient.Invocations.Clear();

            await _agent.CloseProfessionTrainerAsync();

            // Must NOT send SMSG_GOSSIP_COMPLETE (that's a server opcode) or any other packet
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                It.IsAny<Opcode>(),
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()), Times.Never);

            Assert.False(_agent.IsTrainerWindowOpen);
            Assert.Null(_agent.CurrentTrainerGuid);
            Assert.Null(_agent.CurrentProfession);
        }

        #endregion

        #region CMSG_CAST_SPELL — payload: spellId(4) + targetFlags(2) = 6 bytes (no castCount in vanilla 1.12.1)

        [Fact]
        public async Task OpenCraftingWindowAsync_ShouldSend_CastSpell_6Bytes()
        {
            await _agent.OpenCraftingWindowAsync(ProfessionType.Blacksmithing);

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_CAST_SPELL,
                It.Is<byte[]>(p =>
                    p.Length == 6 &&
                    BitConverter.ToUInt32(p, 0) == 2018u && // Blacksmithing spell ID
                    p[4] == 0 && p[5] == 0), // targetFlags = 0 (self-cast)
                It.IsAny<CancellationToken>()), Times.Once);

            Assert.True(_agent.IsCraftingWindowOpen);
            Assert.Equal(ProfessionType.Blacksmithing, _agent.CurrentProfession);
        }

        [Theory]
        [InlineData(ProfessionType.Alchemy, 2259u)]
        [InlineData(ProfessionType.Blacksmithing, 2018u)]
        [InlineData(ProfessionType.Enchanting, 7411u)]
        [InlineData(ProfessionType.Engineering, 4036u)]
        [InlineData(ProfessionType.Leatherworking, 2108u)]
        [InlineData(ProfessionType.Tailoring, 3908u)]
        [InlineData(ProfessionType.Cooking, 2550u)]
        [InlineData(ProfessionType.FirstAid, 3273u)]
        public async Task OpenCraftingWindowAsync_ShouldUseProfessionSpellId(ProfessionType profession, uint expectedSpellId)
        {
            await _agent.OpenCraftingWindowAsync(profession);

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_CAST_SPELL,
                It.Is<byte[]>(p =>
                    p.Length == 6 &&
                    BitConverter.ToUInt32(p, 0) == expectedSpellId),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CraftItemAsync_ShouldSend_CastSpell_WithRecipeSpellId()
        {
            const uint recipeId = 55555;

            await _agent.CraftItemAsync(recipeId, 1);

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_CAST_SPELL,
                It.Is<byte[]>(p =>
                    p.Length == 6 &&
                    BitConverter.ToUInt32(p, 0) == recipeId &&
                    p[4] == 0 && p[5] == 0), // self-cast
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CraftItemAsync_WithQuantity3_ShouldSend3Packets()
        {
            const uint recipeId = 55555;
            var capturedPayloads = new List<byte[]>();

            _mockWorldClient.Setup(x => x.SendOpcodeAsync(
                Opcode.CMSG_CAST_SPELL,
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()))
                .Callback<Opcode, byte[], CancellationToken>((_, p, _) => capturedPayloads.Add((byte[])p.Clone()))
                .Returns(Task.CompletedTask);

            await _agent.CraftItemAsync(recipeId, 3);

            Assert.Equal(3, capturedPayloads.Count);
            for (int i = 0; i < 3; i++)
            {
                Assert.Equal(6, capturedPayloads[i].Length);
                Assert.Equal(recipeId, BitConverter.ToUInt32(capturedPayloads[i], 0));
                // targetFlags = 0 (self-cast)
                Assert.Equal(0, BitConverter.ToUInt16(capturedPayloads[i], 4));
            }
        }

        [Fact]
        public async Task CraftItemAsync_WithZeroQuantity_ShouldNotSendPackets()
        {
            await _agent.CraftItemAsync(12345, 0);

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_CAST_SPELL,
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CraftMultipleItemsAsync_ShouldProcessInPriorityOrder()
        {
            var capturedSpellIds = new List<uint>();

            _mockWorldClient.Setup(x => x.SendOpcodeAsync(
                Opcode.CMSG_CAST_SPELL,
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()))
                .Callback<Opcode, byte[], CancellationToken>((_, p, _) =>
                    capturedSpellIds.Add(BitConverter.ToUInt32(p, 0)))
                .Returns(Task.CompletedTask);

            var queue = new CraftingRequest[]
            {
                new() { RecipeId = 111, Quantity = 1, Priority = 3 },
                new() { RecipeId = 222, Quantity = 2, Priority = 1 },
                new() { RecipeId = 333, Quantity = 1, Priority = 2 }
            };

            await _agent.CraftMultipleItemsAsync(queue);

            // Priority order: 222 (×2), 333 (×1), 111 (×1) = 4 total
            Assert.Equal(4, capturedSpellIds.Count);
            Assert.Equal(222u, capturedSpellIds[0]);
            Assert.Equal(222u, capturedSpellIds[1]);
            Assert.Equal(333u, capturedSpellIds[2]);
            Assert.Equal(111u, capturedSpellIds[3]);
        }

        [Fact]
        public async Task CraftMultipleItemsAsync_WithEmptyQueue_ShouldNotSendPackets()
        {
            await _agent.CraftMultipleItemsAsync(Array.Empty<CraftingRequest>());

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_CAST_SPELL,
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        #endregion

        #region CloseCraftingWindow — local state only

        [Fact]
        public async Task CloseCraftingWindowAsync_ShouldUpdateState()
        {
            await _agent.OpenCraftingWindowAsync(ProfessionType.Alchemy);
            Assert.True(_agent.IsCraftingWindowOpen);

            await _agent.CloseCraftingWindowAsync();

            Assert.False(_agent.IsCraftingWindowOpen);
            Assert.Null(_agent.CurrentProfession);
        }

        #endregion

        #region CMSG_GAMEOBJ_USE (GatherResource) — payload: nodeGuid(8)

        [Fact]
        public async Task GatherResourceAsync_ShouldSend_GameobjUse_WithGuid8()
        {
            await _agent.GatherResourceAsync(TestNodeGuid, GatheringType.Herbalism);

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GAMEOBJ_USE,
                It.Is<byte[]>(p =>
                    p.Length == 8 &&
                    BitConverter.ToUInt64(p, 0) == TestNodeGuid),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region QuickTrainProfessionSkills — full sequence

        [Fact]
        public async Task QuickTrainProfessionSkillsAsync_ShouldSendFullSequence()
        {
            uint[] skillIds = { 111, 222 };

            await _agent.QuickTrainProfessionSkillsAsync(TestTrainerGuid, ProfessionType.Engineering, skillIds);

            // CMSG_GOSSIP_HELLO (open trainer)
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GOSSIP_HELLO,
                It.Is<byte[]>(p => p.Length == 8),
                It.IsAny<CancellationToken>()), Times.Once);

            // CMSG_TRAINER_LIST (request services)
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_TRAINER_LIST,
                It.Is<byte[]>(p => p.Length == 8),
                It.IsAny<CancellationToken>()), Times.Once);

            // CMSG_TRAINER_BUY_SPELL (×2)
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_TRAINER_BUY_SPELL,
                It.Is<byte[]>(p => p.Length == 12),
                It.IsAny<CancellationToken>()), Times.Exactly(2));

            // No packet for close (local state only)
            Assert.False(_agent.IsTrainerWindowOpen);
        }

        #endregion

        #region SMSG_TRAINER_LIST parser (ParseTrainerServices)

        [Fact]
        public void ParseTrainerServices_WithSingleSpell_ShouldParseCorrectly()
        {
            var payload = BuildTrainerListPayload(TestTrainerGuid, trainerType: 2, entries: new[]
            {
                (spellId: 100u, state: (byte)0, cost: 5000u, reqLevel: (byte)10,
                 reqSkill: 164u, reqSkillValue: 50u, prereq1: 0u, prereq2: 0u)
            });

            var services = InvokeParseTrainerServices(payload);

            Assert.Single(services);
            Assert.Equal(100u, services[0].SpellId);
            Assert.Equal(5000u, services[0].Cost);
            Assert.Equal(10u, services[0].RequiredLevel);
            Assert.True(services[0].IsAvailable); // state=0 => green/can learn
            Assert.Empty(services[0].Prerequisites);
        }

        [Fact]
        public void ParseTrainerServices_WithMultipleSpells_ShouldParseAll()
        {
            var payload = BuildTrainerListPayload(TestTrainerGuid, trainerType: 2, entries: new[]
            {
                (spellId: 100u, state: (byte)0, cost: 5000u, reqLevel: (byte)10,
                 reqSkill: 0u, reqSkillValue: 0u, prereq1: 0u, prereq2: 0u),
                (spellId: 200u, state: (byte)1, cost: 10000u, reqLevel: (byte)20,
                 reqSkill: 164u, reqSkillValue: 100u, prereq1: 100u, prereq2: 0u),
                (spellId: 300u, state: (byte)2, cost: 0u, reqLevel: (byte)5,
                 reqSkill: 0u, reqSkillValue: 0u, prereq1: 100u, prereq2: 200u)
            });

            var services = InvokeParseTrainerServices(payload);

            Assert.Equal(3, services.Length);

            // First: available (state=0)
            Assert.Equal(100u, services[0].SpellId);
            Assert.True(services[0].IsAvailable);

            // Second: prerequisite not met (state=1)
            Assert.Equal(200u, services[1].SpellId);
            Assert.Equal(10000u, services[1].Cost);
            Assert.Equal(20u, services[1].RequiredLevel);
            Assert.False(services[1].IsAvailable);
            Assert.Single(services[1].Prerequisites);
            Assert.Equal(100u, services[1].Prerequisites[0]);

            // Third: already known (state=2)
            Assert.Equal(300u, services[2].SpellId);
            Assert.False(services[2].IsAvailable);
            Assert.Equal(2, services[2].Prerequisites.Length);
            Assert.Equal(100u, services[2].Prerequisites[0]);
            Assert.Equal(200u, services[2].Prerequisites[1]);
        }

        [Fact]
        public void ParseTrainerServices_WithEmptyList_ShouldReturnEmpty()
        {
            var payload = BuildTrainerListPayload(TestTrainerGuid, trainerType: 2,
                entries: Array.Empty<(uint, byte, uint, byte, uint, uint, uint, uint)>());

            var services = InvokeParseTrainerServices(payload);
            Assert.Empty(services);
        }

        [Fact]
        public void ParseTrainerServices_WithTooShortPayload_ShouldReturnEmpty()
        {
            var services = InvokeParseTrainerServices(new byte[10]); // < 16 bytes
            Assert.Empty(services);
        }

        [Fact]
        public void ParseTrainerServices_WithTruncatedSpellEntry_ShouldParseAvailableEntries()
        {
            // Build payload with 2 spells but truncate the second entry
            var fullPayload = BuildTrainerListPayload(TestTrainerGuid, trainerType: 2, entries: new[]
            {
                (spellId: 100u, state: (byte)0, cost: 5000u, reqLevel: (byte)10,
                 reqSkill: 0u, reqSkillValue: 0u, prereq1: 0u, prereq2: 0u),
                (spellId: 200u, state: (byte)0, cost: 10000u, reqLevel: (byte)20,
                 reqSkill: 0u, reqSkillValue: 0u, prereq1: 0u, prereq2: 0u)
            });

            // Truncate the second entry (header=16, first entry=38, leave only 10 bytes of second)
            var truncated = fullPayload[..(16 + 38 + 10)];

            var services = InvokeParseTrainerServices(truncated);
            Assert.Single(services); // Only the first complete entry
            Assert.Equal(100u, services[0].SpellId);
        }

        #endregion

        #region SMSG_TRAINER_BUY_SUCCEEDED parser — trainerGuid(8) + spellId(4), no cost

        [Fact]
        public void ParseTrainerBuySucceeded_ShouldReadSpellIdFromOffset8()
        {
            // SMSG_TRAINER_BUY_SUCCEEDED: trainerGuid(8) + spellId(4)
            var payload = new byte[12];
            BinaryPrimitives.WriteUInt64LittleEndian(payload.AsSpan(0, 8), TestTrainerGuid);
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(8, 4), 42u);

            var result = InvokeParseTrainerBuySucceeded(payload);

            Assert.Equal(42u, result.SpellId);
            Assert.Equal(0u, result.Cost); // No cost in this opcode
        }

        [Fact]
        public void ParseTrainerBuySucceeded_WithShortPayload_ShouldFallbackToOffset0()
        {
            // Malformed short payload (only 4 bytes)
            var payload = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(payload, 99u);

            var result = InvokeParseTrainerBuySucceeded(payload);

            Assert.Equal(99u, result.SpellId);
            Assert.Equal(0u, result.Cost);
        }

        [Fact]
        public void ParseTrainerBuySucceeded_WithEmptyPayload_ShouldReturnZeros()
        {
            var result = InvokeParseTrainerBuySucceeded(Array.Empty<byte>());

            Assert.Equal(0u, result.SpellId);
            Assert.Equal(0u, result.Cost);
        }

        #endregion

        #region Service Management / State Tests

        [Fact]
        public void GetAvailableProfessionServices_WithNoServices_ShouldReturnEmpty()
        {
            Assert.Empty(_agent.GetAvailableProfessionServices());
        }

        [Fact]
        public void GetAffordableProfessionServices_WithZeroMoney_ShouldReturnEmpty()
        {
            Assert.Empty(_agent.GetAffordableProfessionServices(0));
        }

        [Fact]
        public void IsProfessionSkillAvailable_WithUnknownSpell_ShouldReturnFalse()
        {
            Assert.False(_agent.IsProfessionSkillAvailable(99999));
        }

        [Fact]
        public void GetProfessionSkillCost_WithUnknownSpell_ShouldReturnZero()
        {
            Assert.Equal(0u, _agent.GetProfessionSkillCost(99999));
        }

        [Fact]
        public void HandleProfessionServicesResponse_ShouldUpdateAvailableServices()
        {
            var services = new ProfessionService[]
            {
                new() { SpellId = 111, Name = "Skill 1", Cost = 100, IsAvailable = true },
                new() { SpellId = 222, Name = "Skill 2", Cost = 200, IsAvailable = true },
                new() { SpellId = 333, Name = "Skill 3", Cost = 300, IsAvailable = false }
            };

            _agent.HandleProfessionServicesResponse(services);

            var available = _agent.GetAvailableProfessionServices();
            Assert.Equal(2, available.Length);
            Assert.True(_agent.IsProfessionSkillAvailable(111));
            Assert.True(_agent.IsProfessionSkillAvailable(222));
            Assert.False(_agent.IsProfessionSkillAvailable(333));
            Assert.Equal(100u, _agent.GetProfessionSkillCost(111));
        }

        [Fact]
        public void GetAffordableProfessionServices_ShouldFilterByCost()
        {
            _agent.HandleProfessionServicesResponse(new ProfessionService[]
            {
                new() { SpellId = 1, Cost = 100, IsAvailable = true },
                new() { SpellId = 2, Cost = 500, IsAvailable = true },
                new() { SpellId = 3, Cost = 1000, IsAvailable = true }
            });

            var affordable = _agent.GetAffordableProfessionServices(500);
            Assert.Equal(2, affordable.Length);
        }

        [Fact]
        public void CanCraftRecipe_AlwaysReturnsTrue()
        {
            Assert.True(_agent.CanCraftRecipe(12345));
        }

        [Fact]
        public void GetRecipeMaterials_AlwaysReturnsEmpty()
        {
            Assert.Empty(_agent.GetRecipeMaterials(12345));
        }

        #endregion

        #region Observable Stream Tests

        [Fact]
        public async Task TrainerWindowOpened_DoesNotEmitOnLocalOpen()
        {
            // Observable is backed by SMSG_TRAINER_LIST, not local method calls
            (ulong, ProfessionType?)? received = null;
            using var sub = _agent.TrainerWindowOpened.Subscribe(t => received = t);

            await _agent.OpenProfessionTrainerAsync(TestTrainerGuid, ProfessionType.Tailoring);

            Assert.Null(received);
        }

        [Fact]
        public async Task TrainerWindowClosed_DoesNotEmitOnLocalClose()
        {
            // Observable is backed by SMSG_GOSSIP_COMPLETE, not local method calls
            await _agent.OpenProfessionTrainerAsync(TestTrainerGuid, ProfessionType.Alchemy);
            bool fired = false;
            using var sub = _agent.TrainerWindowClosed.Subscribe(_ => fired = true);

            await _agent.CloseProfessionTrainerAsync();

            Assert.False(fired);
        }

        #endregion

        #region Dispose Test

        [Fact]
        public void Dispose_ShouldNotThrow()
        {
            _agent.Dispose();
            _agent.Dispose(); // Double dispose should be safe
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Builds an SMSG_TRAINER_LIST payload matching MaNGOS 1.12.1 format.
        /// </summary>
        private static byte[] BuildTrainerListPayload(
            ulong trainerGuid,
            uint trainerType,
            (uint spellId, byte state, uint cost, byte reqLevel,
             uint reqSkill, uint reqSkillValue, uint prereq1, uint prereq2)[] entries)
        {
            // Header: guid(8) + trainerType(4) + count(4) = 16
            // Per entry: 38 bytes
            int size = 16 + entries.Length * 38;
            var payload = new byte[size];
            int offset = 0;

            BinaryPrimitives.WriteUInt64LittleEndian(payload.AsSpan(offset, 8), trainerGuid); offset += 8;
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(offset, 4), trainerType); offset += 4;
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(offset, 4), (uint)entries.Length); offset += 4;

            foreach (var e in entries)
            {
                // 38-byte record layout matching TrainerNetworkClientComponent.ParseTrainerList()
                BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(offset, 4), e.spellId); // +0
                payload[offset + 4] = e.state; // +4
                BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(offset + 5, 4), e.cost); // +5
                BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(offset + 9, 4), 0); // profReq +9
                BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(offset + 13, 4), 0); // profConfirm +13
                payload[offset + 17] = e.reqLevel; // +17
                BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(offset + 18, 4), e.reqSkill); // +18
                BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(offset + 22, 4), e.reqSkillValue); // +22
                BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(offset + 26, 4), e.prereq1); // +26
                BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(offset + 30, 4), e.prereq2); // +30
                BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(offset + 34, 4), 0); // unk +34
                offset += 38;
            }

            return payload;
        }

        /// <summary>
        /// Invokes the private ParseTrainerServices method via reflection.
        /// </summary>
        private static ProfessionService[] InvokeParseTrainerServices(byte[] payload)
        {
            var method = typeof(ProfessionsNetworkClientComponent)
                .GetMethod("ParseTrainerServices", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            return (ProfessionService[])method.Invoke(null, new object[] { (ReadOnlyMemory<byte>)payload })!;
        }

        /// <summary>
        /// Invokes the private ParseTrainerBuySucceeded method via reflection.
        /// </summary>
        private static (uint SpellId, uint Cost) InvokeParseTrainerBuySucceeded(byte[] payload)
        {
            var method = typeof(ProfessionsNetworkClientComponent)
                .GetMethod("ParseTrainerBuySucceeded", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            return ((uint, uint))method.Invoke(null, new object[] { (ReadOnlyMemory<byte>)payload })!;
        }

        #endregion
    }
}
