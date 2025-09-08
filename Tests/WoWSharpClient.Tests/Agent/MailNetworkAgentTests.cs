using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.Agent;
using WoWSharpClient.Networking.Agent.I;
using Xunit;

namespace WoWSharpClient.Tests.Agent
{
    public class MailNetworkAgentTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILogger<MailNetworkAgent>> _mockLogger;
        private readonly MailNetworkAgent _mailAgent;

        public MailNetworkAgentTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLogger = new Mock<ILogger<MailNetworkAgent>>();
            _mailAgent = new MailNetworkAgent(_mockWorldClient.Object, _mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Act & Assert
            Assert.NotNull(_mailAgent);
            Assert.False(_mailAgent.IsMailboxWindowOpen);
        }

        [Fact]
        public void Constructor_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new MailNetworkAgent(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new MailNetworkAgent(_mockWorldClient.Object, null!));
        }

        #endregion

        #region Property Tests

        [Fact]
        public void IsMailboxWindowOpen_InitiallyFalse()
        {
            // Assert
            Assert.False(_mailAgent.IsMailboxWindowOpen);
        }

        [Fact]
        public void IsMailboxOpen_WithClosedMailbox_ReturnsFalse()
        {
            // Arrange
            var mailboxGuid = 0x123456789ABCDEF0UL;

            // Act & Assert
            Assert.False(_mailAgent.IsMailboxOpen(mailboxGuid));
        }

        #endregion

        #region OpenMailboxAsync Tests

        [Fact]
        public async Task OpenMailboxAsync_WithValidGuid_SendsCorrectPacket()
        {
            // Arrange
            var mailboxGuid = 0x123456789ABCDEF0UL;
            var expectedPayload = BitConverter.GetBytes(mailboxGuid);

            // Act
            await _mailAgent.OpenMailboxAsync(mailboxGuid);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    GameData.Core.Enums.Opcode.CMSG_GOSSIP_HELLO,
                    It.Is<byte[]>(payload => payload.SequenceEqual(expectedPayload)),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task OpenMailboxAsync_WithException_RaisesMailErrorEvent()
        {
            // Arrange
            var mailboxGuid = 0x123456789ABCDEF0UL;
            var exception = new InvalidOperationException("Test exception");
            string? capturedOperation = null;
            string? capturedError = null;

            _mockWorldClient.Setup(x => x.SendMovementAsync(It.IsAny<GameData.Core.Enums.Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);

            _mailAgent.MailError += (operation, error) =>
            {
                capturedOperation = operation;
                capturedError = error;
            };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _mailAgent.OpenMailboxAsync(mailboxGuid));
            Assert.Equal("OpenMailbox", capturedOperation);
            Assert.Contains("Failed to open mailbox", capturedError);
        }

        #endregion

        #region GetMailListAsync Tests

        [Fact]
        public async Task GetMailListAsync_SendsCorrectPacket()
        {
            // Act
            await _mailAgent.GetMailListAsync();

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    GameData.Core.Enums.Opcode.CMSG_GET_MAIL_LIST,
                    It.Is<byte[]>(payload => payload.Length == 0),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region SendMailAsync Tests

        [Fact]
        public async Task SendMailAsync_WithValidParameters_SendsCorrectPacket()
        {
            // Arrange
            var recipient = "TestPlayer";
            var subject = "Test Subject";
            var body = "Test Body";

            // Act
            await _mailAgent.SendMailAsync(recipient, subject, body);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    GameData.Core.Enums.Opcode.CMSG_SEND_MAIL,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SendMailWithMoneyAsync_WithValidParameters_SendsCorrectPacket()
        {
            // Arrange
            var recipient = "TestPlayer";
            var subject = "Test Subject";
            var body = "Test Body";
            var money = 1000u;

            // Act
            await _mailAgent.SendMailWithMoneyAsync(recipient, subject, body, money);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    GameData.Core.Enums.Opcode.CMSG_SEND_MAIL,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SendMailWithItemAsync_WithValidParameters_SendsCorrectPacket()
        {
            // Arrange
            var recipient = "TestPlayer";
            var subject = "Test Subject";
            var body = "Test Body";
            var bagId = (byte)1;
            var slotId = (byte)5;

            // Act
            await _mailAgent.SendMailWithItemAsync(recipient, subject, body, bagId, slotId);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    GameData.Core.Enums.Opcode.CMSG_SEND_MAIL,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SendCODMailAsync_WithValidParameters_SendsCorrectPacket()
        {
            // Arrange
            var recipient = "TestPlayer";
            var subject = "Test Subject";
            var body = "Test Body";
            var codAmount = 5000u;
            var bagId = (byte)1;
            var slotId = (byte)5;

            // Act
            await _mailAgent.SendCODMailAsync(recipient, subject, body, codAmount, bagId, slotId);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    GameData.Core.Enums.Opcode.CMSG_SEND_MAIL,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region TakeMoneyFromMailAsync Tests

        [Fact]
        public async Task TakeMoneyFromMailAsync_WithValidMailId_SendsCorrectPacket()
        {
            // Arrange
            var mailId = 123u;
            var expectedPayload = BitConverter.GetBytes(mailId);

            // Act
            await _mailAgent.TakeMoneyFromMailAsync(mailId);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    GameData.Core.Enums.Opcode.CMSG_MAIL_TAKE_MONEY,
                    It.Is<byte[]>(payload => payload.SequenceEqual(expectedPayload)),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region TakeItemFromMailAsync Tests

        [Fact]
        public async Task TakeItemFromMailAsync_WithValidParameters_SendsCorrectPacket()
        {
            // Arrange
            var mailId = 123u;
            var itemGuid = 0x987654321ABCDEF0UL;

            // Act
            await _mailAgent.TakeItemFromMailAsync(mailId, itemGuid);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    GameData.Core.Enums.Opcode.CMSG_MAIL_TAKE_ITEM,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region MarkMailAsReadAsync Tests

        [Fact]
        public async Task MarkMailAsReadAsync_WithValidMailId_SendsCorrectPacket()
        {
            // Arrange
            var mailId = 123u;
            var expectedPayload = BitConverter.GetBytes(mailId);

            // Act
            await _mailAgent.MarkMailAsReadAsync(mailId);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    GameData.Core.Enums.Opcode.CMSG_MAIL_MARK_AS_READ,
                    It.Is<byte[]>(payload => payload.SequenceEqual(expectedPayload)),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region DeleteMailAsync Tests

        [Fact]
        public async Task DeleteMailAsync_WithValidMailId_SendsCorrectPacket()
        {
            // Arrange
            var mailId = 123u;
            var expectedPayload = BitConverter.GetBytes(mailId);

            // Act
            await _mailAgent.DeleteMailAsync(mailId);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    GameData.Core.Enums.Opcode.CMSG_MAIL_DELETE,
                    It.Is<byte[]>(payload => payload.SequenceEqual(expectedPayload)),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region ReturnMailToSenderAsync Tests

        [Fact]
        public async Task ReturnMailToSenderAsync_WithValidMailId_SendsCorrectPacket()
        {
            // Arrange
            var mailId = 123u;
            var expectedPayload = BitConverter.GetBytes(mailId);

            // Act
            await _mailAgent.ReturnMailToSenderAsync(mailId);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    GameData.Core.Enums.Opcode.CMSG_MAIL_RETURN_TO_SENDER,
                    It.Is<byte[]>(payload => payload.SequenceEqual(expectedPayload)),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region CreateTextItemFromMailAsync Tests

        [Fact]
        public async Task CreateTextItemFromMailAsync_WithValidMailId_SendsCorrectPacket()
        {
            // Arrange
            var mailId = 123u;
            var expectedPayload = BitConverter.GetBytes(mailId);

            // Act
            await _mailAgent.CreateTextItemFromMailAsync(mailId);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    GameData.Core.Enums.Opcode.CMSG_MAIL_CREATE_TEXT_ITEM,
                    It.Is<byte[]>(payload => payload.SequenceEqual(expectedPayload)),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region QueryNextMailTimeAsync Tests

        [Fact]
        public async Task QueryNextMailTimeAsync_SendsCorrectPacket()
        {
            // Act
            await _mailAgent.QueryNextMailTimeAsync();

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    GameData.Core.Enums.Opcode.MSG_QUERY_NEXT_MAIL_TIME,
                    It.Is<byte[]>(payload => payload.Length == 0),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region CloseMailboxAsync Tests

        [Fact]
        public async Task CloseMailboxAsync_UpdatesStateAndRaisesEvent()
        {
            // Arrange
            var eventRaised = false;
            _mailAgent.MailboxWindowClosed += () => eventRaised = true;

            // Simulate mailbox being open
            _mailAgent.HandleMailboxWindowOpened(123UL);
            Assert.True(_mailAgent.IsMailboxWindowOpen);

            // Act
            await _mailAgent.CloseMailboxAsync();

            // Assert
            Assert.False(_mailAgent.IsMailboxWindowOpen);
            Assert.True(eventRaised);
        }

        #endregion

        #region Convenience Methods Tests

        [Fact]
        public async Task QuickCheckMailAsync_CallsCorrectSequence()
        {
            // Arrange
            var mailboxGuid = 0x123456789ABCDEF0UL;

            // Act
            await _mailAgent.QuickCheckMailAsync(mailboxGuid);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    GameData.Core.Enums.Opcode.CMSG_GOSSIP_HELLO,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    GameData.Core.Enums.Opcode.CMSG_GET_MAIL_LIST,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task QuickSendMailAsync_CallsCorrectSequence()
        {
            // Arrange
            var mailboxGuid = 0x123456789ABCDEF0UL;
            var recipient = "TestPlayer";
            var subject = "Test Subject";
            var body = "Test Body";

            // Act
            await _mailAgent.QuickSendMailAsync(mailboxGuid, recipient, subject, body);

            // Assert
            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    GameData.Core.Enums.Opcode.CMSG_GOSSIP_HELLO,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _mockWorldClient.Verify(
                x => x.SendMovementAsync(
                    GameData.Core.Enums.Opcode.CMSG_SEND_MAIL,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region Event Handler Tests

        [Fact]
        public void HandleMailboxWindowOpened_UpdatesStateAndRaisesEvent()
        {
            // Arrange
            var mailboxGuid = 0x123456789ABCDEF0UL;
            var eventRaised = false;
            ulong? capturedGuid = null;

            _mailAgent.MailboxWindowOpened += (guid) =>
            {
                eventRaised = true;
                capturedGuid = guid;
            };

            // Act
            _mailAgent.HandleMailboxWindowOpened(mailboxGuid);

            // Assert
            Assert.True(_mailAgent.IsMailboxWindowOpen);
            Assert.True(_mailAgent.IsMailboxOpen(mailboxGuid));
            Assert.True(eventRaised);
            Assert.Equal(mailboxGuid, capturedGuid);
        }

        [Fact]
        public void HandleMailListReceived_RaisesEvent()
        {
            // Arrange
            var mailCount = 5u;
            var eventRaised = false;
            uint? capturedCount = null;

            _mailAgent.MailListReceived += (count) =>
            {
                eventRaised = true;
                capturedCount = count;
            };

            // Act
            _mailAgent.HandleMailListReceived(mailCount);

            // Assert
            Assert.True(eventRaised);
            Assert.Equal(mailCount, capturedCount);
        }

        [Fact]
        public void HandleMailSent_RaisesEvent()
        {
            // Arrange
            var recipient = "TestPlayer";
            var subject = "Test Subject";
            var eventRaised = false;
            string? capturedRecipient = null;
            string? capturedSubject = null;

            _mailAgent.MailSent += (r, s) =>
            {
                eventRaised = true;
                capturedRecipient = r;
                capturedSubject = s;
            };

            // Act
            _mailAgent.HandleMailSent(recipient, subject);

            // Assert
            Assert.True(eventRaised);
            Assert.Equal(recipient, capturedRecipient);
            Assert.Equal(subject, capturedSubject);
        }

        [Fact]
        public void HandleMoneyTakenFromMail_RaisesEvent()
        {
            // Arrange
            var mailId = 123u;
            var amount = 1000u;
            var eventRaised = false;
            uint? capturedMailId = null;
            uint? capturedAmount = null;

            _mailAgent.MoneyTakenFromMail += (id, amt) =>
            {
                eventRaised = true;
                capturedMailId = id;
                capturedAmount = amt;
            };

            // Act
            _mailAgent.HandleMoneyTakenFromMail(mailId, amount);

            // Assert
            Assert.True(eventRaised);
            Assert.Equal(mailId, capturedMailId);
            Assert.Equal(amount, capturedAmount);
        }

        [Fact]
        public void HandleItemTakenFromMail_RaisesEvent()
        {
            // Arrange
            var mailId = 123u;
            var itemId = 456u;
            var quantity = 2u;
            var eventRaised = false;
            uint? capturedMailId = null;
            uint? capturedItemId = null;
            uint? capturedQuantity = null;

            _mailAgent.ItemTakenFromMail += (id, item, qty) =>
            {
                eventRaised = true;
                capturedMailId = id;
                capturedItemId = item;
                capturedQuantity = qty;
            };

            // Act
            _mailAgent.HandleItemTakenFromMail(mailId, itemId, quantity);

            // Assert
            Assert.True(eventRaised);
            Assert.Equal(mailId, capturedMailId);
            Assert.Equal(itemId, capturedItemId);
            Assert.Equal(quantity, capturedQuantity);
        }

        [Fact]
        public void HandleMailError_RaisesEvent()
        {
            // Arrange
            var operation = "SendMail";
            var errorMessage = "Test error";
            var eventRaised = false;
            string? capturedOperation = null;
            string? capturedError = null;

            _mailAgent.MailError += (op, err) =>
            {
                eventRaised = true;
                capturedOperation = op;
                capturedError = err;
            };

            // Act
            _mailAgent.HandleMailError(operation, errorMessage);

            // Assert
            Assert.True(eventRaised);
            Assert.Equal(operation, capturedOperation);
            Assert.Equal(errorMessage, capturedError);
        }

        #endregion
    }
}