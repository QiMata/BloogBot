using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using GameData.Core.Enums;
using WoWSharpClient.Networking.ClientComponents;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;
using System.Reactive.Subjects;

namespace WoWSharpClient.Tests.Agent
{
    public class AttackNetworkClientComponentTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILogger<AttackNetworkClientComponent>> _mockLogger;
        private readonly AttackNetworkClientComponent _attackClientComponent;

        // Reactive test streams
        private readonly Subject<(bool IsAttacking, ulong AttackerGuid, ulong VictimGuid)> _attackStateSubject = new();
        private readonly Subject<string> _attackErrorsSubject = new();
        private readonly Subject<ReadOnlyMemory<byte>> _weaponSwingSubject = new();

        public AttackNetworkClientComponentTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLogger = new Mock<ILogger<AttackNetworkClientComponent>>();

            // Wire reactive streams
            _mockWorldClient.SetupGet(x => x.AttackStateChanged).Returns(_attackStateSubject);
            _mockWorldClient.SetupGet(x => x.AttackErrors).Returns(_attackErrorsSubject);
            _mockWorldClient.Setup(x => x.RegisterOpcodeHandler(Opcode.SMSG_ATTACKERSTATEUPDATE)).Returns(_weaponSwingSubject);

            _attackClientComponent = new AttackNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
        }

        [Fact]
        public void IsAttacking_InitiallyFalse()
        {
            // Arrange & Act
            var isAttacking = _attackClientComponent.IsAttacking;

            // Assert
            Assert.False(isAttacking);
        }

        [Fact]
        public async Task StartAttackAsync_SendsCorrectPacket()
        {
            // Arrange
            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _attackClientComponent.StartAttackAsync();

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_ATTACKSWING,
                    It.Is<byte[]>(payload => payload.Length == 0), // Empty payload
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task StopAttackAsync_SendsCorrectPacket()
        {
            // Arrange
            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _attackClientComponent.StopAttackAsync();

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(
                    Opcode.CMSG_ATTACKSTOP,
                    It.Is<byte[]>(payload => payload.Length == 0), // Empty payload
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task ToggleAttackAsync_WhenNotAttacking_StartsAttack()
        {
            // Arrange
            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _attackClientComponent.ToggleAttackAsync();

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(Opcode.CMSG_ATTACKSWING, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Once
            );
        }

        [Fact]
        public async Task ToggleAttackAsync_WhenAttacking_StopsAttack()
        {
            // Arrange
            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Set attacking state to true via stream
            _attackStateSubject.OnNext((true, 0x12345678, 0x87654321));

            // Act
            await _attackClientComponent.ToggleAttackAsync();

            // Assert
            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(Opcode.CMSG_ATTACKSTOP, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Once
            );
        }

        [Fact]
        public async Task AttackTargetAsync_CallsTargetingAgentThenAttacks()
        {
            // Arrange
            ulong targetGuid = 0x12345678;
            var mockTargetingAgent = new Mock<ITargetingNetworkClientComponent>();
            
            mockTargetingAgent
                .Setup(x => x.SetTargetAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _attackClientComponent.AttackTargetAsync(targetGuid, mockTargetingAgent.Object);

            // Assert
            mockTargetingAgent.Verify(
                x => x.SetTargetAsync(targetGuid, It.IsAny<CancellationToken>()),
                Times.Once
            );

            _mockWorldClient.Verify(
                x => x.SendOpcodeAsync(Opcode.CMSG_ATTACKSWING, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Once
            );
        }

        [Fact]
        public async Task AttackTargetAsync_WithNullTargetingAgent_ThrowsArgumentNullException()
        {
            // Arrange
            ulong targetGuid = 0x12345678;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _attackClientComponent.AttackTargetAsync(targetGuid, null!));
        }

        [Fact]
        public void AttackState_FromFalseToTrue_RaisesAttackStateChangesObservable()
        {
            // Arrange
            ulong attackerGuid = 0x12345678;
            ulong victimGuid = 0x87654321;
            AttackStateData? receivedData = null;
            bool observableTriggered = false;

            // Subscribe to the reactive observable
            _attackClientComponent.AttackStateChanges.Subscribe(data =>
            {
                receivedData = data;
                observableTriggered = true;
            });

            // Act
            _attackStateSubject.OnNext((true, attackerGuid, victimGuid));

            // Assert
            Assert.True(_attackClientComponent.IsAttacking);
            Assert.True(observableTriggered);
            Assert.NotNull(receivedData);
            Assert.True(receivedData.IsAttacking);
            Assert.Equal(victimGuid, receivedData.VictimGuid);
        }

        [Fact]
        public void AttackState_FromTrueToFalse_RaisesAttackStateChangesObservable()
        {
            // Arrange
            AttackStateData? receivedData = null;
            bool observableTriggered = false;

            // Set initial attacking state
            _attackStateSubject.OnNext((true, 0x12345678, 0x87654321));

            // Subscribe to the reactive observable
            _attackClientComponent.AttackStateChanges.Subscribe(data =>
            {
                receivedData = data;
                observableTriggered = true;
            });

            // Act
            _attackStateSubject.OnNext((false, 0x12345678, 0x87654321));

            // Assert
            Assert.False(_attackClientComponent.IsAttacking);
            Assert.True(observableTriggered);
            Assert.NotNull(receivedData);
            Assert.False(receivedData.IsAttacking);
        }

        [Fact]
        public void AttackState_SameState_DoesNotTriggerObservable()
        {
            // Arrange
            int observableCount = 0;

            // Subscribe to the reactive observable
            _attackClientComponent.AttackStateChanges.Subscribe(data => observableCount++);

            // Act
            _attackStateSubject.OnNext((false, 0x12345678, 0x87654321)); // Initial state is already false
            _attackStateSubject.OnNext((false, 0x12345678, 0x87654321)); // Same state again

            // Assert
            Assert.Equal(0, observableCount); // No observables should be triggered since state didn't change
        }

        [Fact]
        public void AttackError_RaisesAttackErrorObservable()
        {
            // Arrange
            string errorMessage = "Target not in range";
            AttackErrorData? receivedData = null;
            bool observableTriggered = false;

            // Subscribe to the reactive observable
            _attackClientComponent.AttackErrors.Subscribe(data =>
            {
                receivedData = data;
                observableTriggered = true;
            });

            // Act
            _attackErrorsSubject.OnNext(errorMessage);

            // Assert
            Assert.True(observableTriggered);
            Assert.NotNull(receivedData);
            Assert.Equal(errorMessage, receivedData.ErrorMessage);
        }

        [Fact]
        public void AttackStateChanges_Observable_WorksCorrectly()
        {
            // Arrange
            var receivedData = new List<AttackStateData>();
            
            // Subscribe to the observable
            _attackClientComponent.AttackStateChanges.Subscribe(data => receivedData.Add(data));

            // Act
            _attackStateSubject.OnNext((true, 0x12345678, 0x87654321));
            _attackStateSubject.OnNext((false, 0x12345678, 0x87654321));

            // Assert
            Assert.Equal(2, receivedData.Count);
            
            var startData = receivedData[0];
            Assert.True(startData.IsAttacking);
            Assert.Equal((ulong)0x87654321, startData.VictimGuid);

            var stopData = receivedData[1];
            Assert.False(stopData.IsAttacking);
            Assert.Null(stopData.VictimGuid);
        }

        [Fact]
        public async Task StartAttackAsync_WithNetworkError_ThrowsException()
        {
            // Arrange
            var expectedException = new InvalidOperationException("Network error");

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(expectedException);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _attackClientComponent.StartAttackAsync());
            Assert.Equal("Network error", exception.Message);
        }

        [Fact]
        public async Task StopAttackAsync_WithNetworkError_ThrowsException()
        {
            // Arrange
            var expectedException = new InvalidOperationException("Network error");

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(expectedException);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _attackClientComponent.StopAttackAsync());
            Assert.Equal("Network error", exception.Message);
        }

        [Theory]
        [InlineData(true, false)] // If attacking, toggle should stop
        [InlineData(false, true)] // If not attacking, toggle should start
        public async Task ToggleAttackAsync_VariousStates_TogglesCorrectly(bool initialState, bool shouldStart)
        {
            // Arrange
            if (initialState)
            {
                _attackStateSubject.OnNext((true, 0x12345678, 0x87654321));
            }

            _mockWorldClient
                .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _attackClientComponent.ToggleAttackAsync();

            // Assert
            if (shouldStart)
            {
                _mockWorldClient.Verify(
                    x => x.SendOpcodeAsync(Opcode.CMSG_ATTACKSWING, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                    Times.Once
                );
            }
            else
            {
                _mockWorldClient.Verify(
                    x => x.SendOpcodeAsync(Opcode.CMSG_ATTACKSTOP, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                    Times.Once
                );
            }
        }

        [Fact]
        public void AttackState_WithoutVictimGuid_TriggersObservable()
        {
            // Arrange
            bool observableTriggered = false;
            _attackClientComponent.AttackStateChanges.Subscribe(data => observableTriggered = true);

            // Act - Start attacking
            _attackStateSubject.OnNext((true, 0x12345678, 0x87654321));

            // Assert
            Assert.True(_attackClientComponent.IsAttacking);
            Assert.True(observableTriggered); // Observable should be triggered for valid state change
        }

        [Fact]
        public void WeaponSwing_RaisesWeaponSwingObservable()
        {
            // Arrange
            ulong attackerGuid = 0x12345678;
            ulong victimGuid = 0x87654321;
            uint damage = 150;
            bool isCritical = true;
            WeaponSwingData? receivedData = null;
            bool observableTriggered = false;

            // Subscribe to the reactive observable
            _attackClientComponent.WeaponSwings.Subscribe(data =>
            {
                receivedData = data;
                observableTriggered = true;
            });

            // Build payload according to parser expectations
            var payload = new byte[21];
            BitConverter.GetBytes(attackerGuid).CopyTo(payload, 0);
            BitConverter.GetBytes(victimGuid).CopyTo(payload, 8);
            BitConverter.GetBytes(damage).CopyTo(payload, 16);
            payload[20] = (byte)(isCritical ? 1 : 0);

            // Act
            _weaponSwingSubject.OnNext(new ReadOnlyMemory<byte>(payload));

            // Assert
            Assert.True(observableTriggered);
            Assert.NotNull(receivedData);
            Assert.Equal(attackerGuid, receivedData.AttackerGuid);
            Assert.Equal(victimGuid, receivedData.VictimGuid);
            Assert.Equal(damage, receivedData.Damage);
            Assert.Equal(isCritical, receivedData.IsCritical);
        }

        [Fact]
        public void GetCurrentVictim_InitiallyNull()
        {
            // Arrange & Act
            var currentVictim = _attackClientComponent.CurrentVictim;

            // Assert
            Assert.Null(currentVictim);
        }

        [Fact]
        public void IsOperationInProgress_InitiallyFalse()
        {
            // Arrange & Act
            var isInProgress = _attackClientComponent.IsOperationInProgress;

            // Assert
            Assert.False(isInProgress);
        }
    }
}