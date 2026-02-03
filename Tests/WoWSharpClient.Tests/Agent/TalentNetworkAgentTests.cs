using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents;
using WoWSharpClient.Networking.ClientComponents.I;

namespace WoWSharpClient.Tests.Agent
{
    /// <summary>
    /// Tests for the TalentNetworkClientComponent class.
    /// Verifies talent allocation, respec operations, and event handling.
    /// </summary>
    public class TalentNetworkClientComponentTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILogger<TalentNetworkClientComponent>> _mockLogger;
        private readonly TalentNetworkClientComponent _talentAgent;

        public TalentNetworkClientComponentTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLogger = new Mock<ILogger<TalentNetworkClientComponent>>();
            _talentAgent = new TalentNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_InitializesSuccessfully()
        {
            // Act & Assert
            Assert.NotNull(_talentAgent);
            Assert.False(_talentAgent.IsTalentWindowOpen);
            Assert.Equal(0u, _talentAgent.AvailableTalentPoints);
            Assert.Equal(0u, _talentAgent.TotalTalentPointsSpent);
            Assert.Equal(0u, _talentAgent.RespecCost);
        }

        [Fact]
        public void Constructor_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new TalentNetworkClientComponent(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new TalentNetworkClientComponent(_mockWorldClient.Object, null!));
        }

        #endregion

        #region Talent Window Tests

        [Fact]
        public async Task OpenTalentWindowAsync_CallsCorrectly_UpdatesState()
        {
            // Act
            await _talentAgent.OpenTalentWindowAsync();

            // Assert
            Assert.True(_talentAgent.IsTalentWindowOpen);
        }

        [Fact]
        public async Task CloseTalentWindowAsync_CallsCorrectly_UpdatesState()
        {
            // Arrange
            await _talentAgent.OpenTalentWindowAsync();
            Assert.True(_talentAgent.IsTalentWindowOpen);

            // Act
            await _talentAgent.CloseTalentWindowAsync();

            // Assert
            Assert.False(_talentAgent.IsTalentWindowOpen);
        }

        [Fact]
        public async Task OpenTalentWindowAsync_FiresEvent()
        {
            // Arrange
            bool eventFired = false;
            _talentAgent.TalentWindowOpened += () => eventFired = true;

            // Act
            await _talentAgent.OpenTalentWindowAsync();

            // Assert
            Assert.True(eventFired);
        }

        [Fact]
        public async Task CloseTalentWindowAsync_FiresEvent()
        {
            // Arrange
            await _talentAgent.OpenTalentWindowAsync();
            bool eventFired = false;
            _talentAgent.TalentWindowClosed += () => eventFired = true;

            // Act
            await _talentAgent.CloseTalentWindowAsync();

            // Assert
            Assert.True(eventFired);
        }

        #endregion

        #region Learn Talent Tests

        [Fact]
        public async Task LearnTalentAsync_WithValidTalentId_SendsCorrectPacket()
        {
            // Arrange
            const uint talentId = 12345;
            _talentAgent.UpdateAvailableTalentPoints(1);

            // Setup mock talent cache to allow learning
            var talentInfo = new TalentInfo
            {
                TalentId = talentId,
                CurrentRank = 0,
                MaxRank = 5,
                TabIndex = 0,
                Prerequisites = Array.Empty<TalentPrerequisite>(),
                CanLearn = true,
                RequiredTreePoints = 0
            };

            var talentTree = new TalentTreeInfo
            {
                TabIndex = 0,
                PointsSpent = 0,
                Talents = new[] { talentInfo }
            };

            _talentAgent.HandleTalentInfoReceived(1, 0, new[] { talentTree });

            // Act
            await _talentAgent.LearnTalentAsync(talentId);

            // Assert
            _mockWorldClient.Verify(
                c => c.SendOpcodeAsync(
                    Opcode.CMSG_LEARN_TALENT,
                    It.Is<byte[]>(payload => BitConverter.ToUInt32(payload, 0) == talentId),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task LearnTalentAsync_WithNoAvailablePoints_DoesNotSendPacket()
        {
            // Arrange
            const uint talentId = 12345;
            // No talent points available (default is 0)

            // Act
            await _talentAgent.LearnTalentAsync(talentId);

            // Assert
            _mockWorldClient.Verify(
                c => c.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task LearnTalentByPositionAsync_WithValidPosition_CallsLearnTalent()
        {
            // Arrange
            const uint tabIndex = 0;
            const uint talentIndex = 5;
            const uint talentId = 12345;

            _talentAgent.UpdateAvailableTalentPoints(1);

            var talentInfo = new TalentInfo
            {
                TalentId = talentId,
                TalentIndex = talentIndex,
                TabIndex = tabIndex,
                CurrentRank = 0,
                MaxRank = 5,
                Prerequisites = Array.Empty<TalentPrerequisite>(),
                CanLearn = true,
                RequiredTreePoints = 0
            };

            var talentTree = new TalentTreeInfo
            {
                TabIndex = tabIndex,
                PointsSpent = 0,
                Talents = new[] { talentInfo }
            };

            _talentAgent.HandleTalentInfoReceived(1, 0, new[] { talentTree });

            // Act
            await _talentAgent.LearnTalentByPositionAsync(tabIndex, talentIndex);

            // Assert
            _mockWorldClient.Verify(
                c => c.SendOpcodeAsync(
                    Opcode.CMSG_LEARN_TALENT,
                    It.Is<byte[]>(payload => BitConverter.ToUInt32(payload, 0) == talentId),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task LearnTalentByPositionAsync_WithInvalidPosition_DoesNotSendPacket()
        {
            // Arrange
            const uint tabIndex = 99; // Invalid tab
            const uint talentIndex = 99; // Invalid index

            // Act
            await _talentAgent.LearnTalentByPositionAsync(tabIndex, talentIndex);

            // Assert
            _mockWorldClient.Verify(
                c => c.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        #endregion

        #region Respec Tests

        [Fact]
        public async Task RequestTalentRespecAsync_SendsCorrectPacket()
        {
            // Act
            await _talentAgent.RequestTalentRespecAsync();

            // Assert
            _mockWorldClient.Verify(
                c => c.SendOpcodeAsync(
                    Opcode.CMSG_UNLEARN_TALENTS,
                    It.Is<byte[]>(payload => payload.Length == 0),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ConfirmTalentRespecAsync_WithConfirm_SendsCorrectPacket()
        {
            // Arrange
            _talentAgent.HandleRespecConfirmationRequest(10000);

            // Act
            await _talentAgent.ConfirmTalentRespecAsync(true);

            // Assert
            _mockWorldClient.Verify(
                c => c.SendOpcodeAsync(
                    Opcode.MSG_TALENT_WIPE_CONFIRM,
                    It.Is<byte[]>(payload => payload.Length == 1 && payload[0] == 1),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ConfirmTalentRespecAsync_WithDecline_SendsCorrectPacket()
        {
            // Arrange
            _talentAgent.HandleRespecConfirmationRequest(10000);

            // Act
            await _talentAgent.ConfirmTalentRespecAsync(false);

            // Assert
            _mockWorldClient.Verify(
                c => c.SendOpcodeAsync(
                    Opcode.MSG_TALENT_WIPE_CONFIRM,
                    It.Is<byte[]>(payload => payload.Length == 1 && payload[0] == 0),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ConfirmTalentRespecAsync_WithoutPendingConfirmation_DoesNotSendPacket()
        {
            // Act (no prior respec request)
            await _talentAgent.ConfirmTalentRespecAsync(true);

            // Assert
            _mockWorldClient.Verify(
                c => c.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        #endregion

        #region Talent Build Tests

        [Fact]
        public async Task ApplyTalentBuildAsync_WithValidBuild_LearnsAllTalents()
        {
            // Arrange
            _talentAgent.UpdateAvailableTalentPoints(10);

            var talentBuild = new TalentBuild
            {
                Name = "Test Build",
                ClassId = 1,
                Allocations = new[]
                {
                    new TalentAllocation { TalentId = 101, TargetRank = 2, Priority = 1 },
                    new TalentAllocation { TalentId = 102, TargetRank = 1, Priority = 2 }
                },
                LearningOrder = new uint[] { 101, 102 }
            };

            // Setup talent cache
            var talents = new[]
            {
                new TalentInfo
                {
                    TalentId = 101,
                    CurrentRank = 0,
                    MaxRank = 5,
                    TabIndex = 0,
                    Prerequisites = Array.Empty<TalentPrerequisite>(),
                    CanLearn = true,
                    RequiredTreePoints = 0
                },
                new TalentInfo
                {
                    TalentId = 102,
                    CurrentRank = 0,
                    MaxRank = 3,
                    TabIndex = 0,
                    Prerequisites = Array.Empty<TalentPrerequisite>(),
                    CanLearn = true,
                    RequiredTreePoints = 0
                }
            };

            var talentTree = new TalentTreeInfo
            {
                TabIndex = 0,
                PointsSpent = 0,
                Talents = talents
            };

            _talentAgent.HandleTalentInfoReceived(10, 0, new[] { talentTree });

            // Act
            await _talentAgent.ApplyTalentBuildAsync(talentBuild);

            // Assert
            _mockWorldClient.Verify(
                c => c.SendOpcodeAsync(
                    Opcode.CMSG_LEARN_TALENT,
                    It.Is<byte[]>(payload => BitConverter.ToUInt32(payload, 0) == 101),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(2)); // Should be called twice for rank 2

            _mockWorldClient.Verify(
                c => c.SendOpcodeAsync(
                    Opcode.CMSG_LEARN_TALENT,
                    It.Is<byte[]>(payload => BitConverter.ToUInt32(payload, 0) == 102),
                    It.IsAny<CancellationToken>()),
                Times.Once); // Should be called once for rank 1
        }

        [Fact]
        public async Task ApplyTalentBuildAsync_WithInvalidBuild_DoesNotLearnTalents()
        {
            // Arrange
            var talentBuild = new TalentBuild
            {
                Name = "Invalid Build",
                ClassId = 1,
                Allocations = Array.Empty<TalentAllocation>() // No allocations
            };

            // Act
            await _talentAgent.ApplyTalentBuildAsync(talentBuild);

            // Assert
            _mockWorldClient.Verify(
                c => c.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        #endregion

        #region Helper Method Tests

        [Fact]
        public void GetTalentRank_WithExistingTalent_ReturnsCorrectRank()
        {
            // Arrange
            const uint talentId = 101;
            const uint expectedRank = 3;

            var talentInfo = new TalentInfo
            {
                TalentId = talentId,
                CurrentRank = expectedRank,
                MaxRank = 5,
                TabIndex = 0
            };

            var talentTree = new TalentTreeInfo
            {
                TabIndex = 0,
                Talents = new[] { talentInfo }
            };

            _talentAgent.HandleTalentInfoReceived(5, 3, new[] { talentTree });

            // Act
            var actualRank = _talentAgent.GetTalentRank(talentId);

            // Assert
            Assert.Equal(expectedRank, actualRank);
        }

        [Fact]
        public void GetTalentRank_WithNonExistentTalent_ReturnsZero()
        {
            // Arrange
            const uint nonExistentTalentId = 999;

            // Act
            var rank = _talentAgent.GetTalentRank(nonExistentTalentId);

            // Assert
            Assert.Equal(0u, rank);
        }

        [Fact]
        public void CanLearnTalent_WithNoAvailablePoints_ReturnsFalse()
        {
            // Arrange
            const uint talentId = 101;
            _talentAgent.UpdateAvailableTalentPoints(0);

            // Act
            var canLearn = _talentAgent.CanLearnTalent(talentId);

            // Assert
            Assert.False(canLearn);
        }

        [Fact]
        public void CanLearnTalent_WithAvailablePointsAndValidTalent_ReturnsTrue()
        {
            // Arrange
            const uint talentId = 101;
            _talentAgent.UpdateAvailableTalentPoints(5);

            var talentInfo = new TalentInfo
            {
                TalentId = talentId,
                CurrentRank = 0,
                MaxRank = 5,
                TabIndex = 0,
                Prerequisites = Array.Empty<TalentPrerequisite>(),
                RequiredTreePoints = 0
            };

            var talentTree = new TalentTreeInfo
            {
                TabIndex = 0,
                PointsSpent = 5,
                Talents = new[] { talentInfo }
            };

            _talentAgent.HandleTalentInfoReceived(5, 0, new[] { talentTree });

            // Act
            var canLearn = _talentAgent.CanLearnTalent(talentId);

            // Assert
            Assert.True(canLearn);
        }

        [Fact]
        public void GetPointsInTree_WithValidTab_ReturnsCorrectPoints()
        {
            // Arrange
            const uint tabIndex = 0;
            const uint expectedPoints = 15;

            var talentTree = new TalentTreeInfo
            {
                TabIndex = tabIndex,
                PointsSpent = expectedPoints
            };

            _talentAgent.HandleTalentInfoReceived(20, expectedPoints, new[] { talentTree });

            // Act
            var actualPoints = _talentAgent.GetPointsInTree(tabIndex);

            // Assert
            Assert.Equal(expectedPoints, actualPoints);
        }

        [Fact]
        public void GetPointsInTree_WithInvalidTab_ReturnsZero()
        {
            // Arrange
            const uint invalidTabIndex = 99;

            // Act
            var points = _talentAgent.GetPointsInTree(invalidTabIndex);

            // Assert
            Assert.Equal(0u, points);
        }

        #endregion

        #region Validation Tests

        [Fact]
        public void ValidateTalentBuild_WithValidBuild_ReturnsValid()
        {
            // Arrange
            _talentAgent.UpdateAvailableTalentPoints(10);

            var talentBuild = new TalentBuild
            {
                Allocations = new[]
                {
                    new TalentAllocation { TalentId = 101, TargetRank = 2 }
                }
            };

            var talentInfo = new TalentInfo
            {
                TalentId = 101,
                CurrentRank = 0,
                MaxRank = 5,
                TabIndex = 0,
                Prerequisites = Array.Empty<TalentPrerequisite>(),
                RequiredTreePoints = 0
            };

            var talentTree = new TalentTreeInfo
            {
                TabIndex = 0,
                PointsSpent = 10,
                Talents = new[] { talentInfo }
            };

            _talentAgent.HandleTalentInfoReceived(10, 0, new[] { talentTree });

            // Act
            var result = _talentAgent.ValidateTalentBuild(talentBuild);

            // Assert
            Assert.True(result.IsValid);
            Assert.Equal(2u, result.RequiredPoints);
            Assert.Equal(2u, result.ApplicablePoints);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ValidateTalentBuild_WithEmptyBuild_ReturnsInvalid()
        {
            // Arrange
            var talentBuild = new TalentBuild
            {
                Allocations = Array.Empty<TalentAllocation>()
            };

            // Act
            var result = _talentAgent.ValidateTalentBuild(talentBuild);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains("no allocations", result.Errors[0]);
        }

        #endregion

        #region Event Tests

        [Fact]
        public void HandleTalentLearned_FiresCorrectEvent()
        {
            // Arrange
            const uint talentId = 101;
            const uint newRank = 2;
            uint eventTalentId = 0;
            uint eventRank = 0;
            uint eventPointsRemaining = 0;

            _talentAgent.TalentLearned += (tId, rank, points) =>
            {
                eventTalentId = tId;
                eventRank = rank;
                eventPointsRemaining = points;
            };

            _talentAgent.UpdateAvailableTalentPoints(5);

            // Act
            _talentAgent.HandleTalentLearned(talentId, newRank);

            // Assert
            Assert.Equal(talentId, eventTalentId);
            Assert.Equal(newRank, eventRank);
            Assert.Equal(4u, eventPointsRemaining); // Should decrease by 1
        }

        [Fact]
        public void HandleTalentsUnlearned_FiresCorrectEvent()
        {
            // Arrange
            const uint cost = 10000;
            const uint pointsRefunded = 15;
            uint eventCost = 0;
            uint eventPoints = 0;

            _talentAgent.TalentsUnlearned += (c, p) =>
            {
                eventCost = c;
                eventPoints = p;
            };

            // Act
            _talentAgent.HandleTalentsUnlearned(cost, pointsRefunded);

            // Assert
            Assert.Equal(cost, eventCost);
            Assert.Equal(pointsRefunded, eventPoints);
        }

        [Fact]
        public void HandleTalentError_FiresCorrectEvent()
        {
            // Arrange
            const string errorMessage = "Test error";
            string? eventError = null;

            _talentAgent.TalentError += (error) => eventError = error;

            // Act
            _talentAgent.HandleTalentError(errorMessage);

            // Assert
            Assert.Equal(errorMessage, eventError);
        }

        #endregion

        #region Server Response Tests

        [Fact]
        public void HandleTalentInfoReceived_UpdatesStateCorrectly()
        {
            // Arrange
            const uint availablePoints = 5;
            const uint spentPoints = 10;

            var talentTree = new TalentTreeInfo
            {
                TabIndex = 0,
                PointsSpent = spentPoints,
                Talents = new[]
                {
                    new TalentInfo { TalentId = 101, CurrentRank = 2 },
                    new TalentInfo { TalentId = 102, CurrentRank = 1 }
                }
            };

            // Act
            _talentAgent.HandleTalentInfoReceived(availablePoints, spentPoints, new[] { talentTree });

            // Assert
            Assert.Equal(availablePoints, _talentAgent.AvailableTalentPoints);
            Assert.Equal(spentPoints, _talentAgent.TotalTalentPointsSpent);
            Assert.Single(_talentAgent.GetAllTalentTrees());
            Assert.Equal(2u, _talentAgent.GetTalentRank(101));
            Assert.Equal(1u, _talentAgent.GetTalentRank(102));
        }

        [Fact]
        public void HandleRespecConfirmationRequest_UpdatesRespecCost()
        {
            // Arrange
            const uint expectedCost = 50000;

            // Act
            _talentAgent.HandleRespecConfirmationRequest(expectedCost);

            // Assert
            Assert.Equal(expectedCost, _talentAgent.RespecCost);
        }

        [Fact]
        public void UpdateAvailableTalentPoints_UpdatesPointsCorrectly()
        {
            // Arrange
            const uint newPoints = 7;

            // Act
            _talentAgent.UpdateAvailableTalentPoints(newPoints);

            // Assert
            Assert.Equal(newPoints, _talentAgent.AvailableTalentPoints);
        }

        #endregion
    }
}