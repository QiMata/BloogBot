using System.Text;
using System.Reactive.Subjects;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents;

namespace WoWSharpClient.Tests.Agent
{
    public class MailNetworkClientComponentTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILogger<MailNetworkClientComponent>> _mockLogger;
        private readonly MailNetworkClientComponent _mailAgent;
        private const ulong TestMailboxGuid = 0x123456789ABCDEF0UL;

        public MailNetworkClientComponentTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLogger = new Mock<ILogger<MailNetworkClientComponent>>();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(It.IsAny<Opcode>()))
                .Returns((IObservable<ReadOnlyMemory<byte>>)new Subject<ReadOnlyMemory<byte>>());
            _mailAgent = new MailNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
        }

        /// <summary>Opens the mailbox to store the GUID for subsequent operations.</summary>
        private async Task OpenTestMailbox()
        {
            await _mailAgent.OpenMailboxAsync(TestMailboxGuid);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            Assert.NotNull(_mailAgent);
            Assert.False(_mailAgent.IsMailboxWindowOpen);
        }

        [Fact]
        public void Constructor_WithNullWorldClient_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new MailNetworkClientComponent(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new MailNetworkClientComponent(_mockWorldClient.Object, null!));
        }

        #endregion

        #region State Tests

        [Fact]
        public void IsMailboxWindowOpen_InitiallyFalse()
        {
            Assert.False(_mailAgent.IsMailboxWindowOpen);
        }

        [Fact]
        public void IsMailboxOpen_WithClosedMailbox_ReturnsFalse()
        {
            Assert.False(_mailAgent.IsMailboxOpen(TestMailboxGuid));
        }

        [Fact]
        public async Task OpenMailboxAsync_SetsIsMailboxWindowOpenTrue()
        {
            await _mailAgent.OpenMailboxAsync(TestMailboxGuid);
            Assert.True(_mailAgent.IsMailboxWindowOpen);
        }

        [Fact]
        public async Task CloseMailboxAsync_SetsIsMailboxWindowOpenFalse()
        {
            await _mailAgent.OpenMailboxAsync(TestMailboxGuid);
            Assert.True(_mailAgent.IsMailboxWindowOpen);
            await _mailAgent.CloseMailboxAsync();
            Assert.False(_mailAgent.IsMailboxWindowOpen);
        }

        #endregion

        #region OpenMailboxAsync - CMSG_GOSSIP_HELLO

        [Fact]
        public async Task OpenMailboxAsync_ShouldSendGossipHelloWithMailboxGuid()
        {
            await _mailAgent.OpenMailboxAsync(TestMailboxGuid);

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GOSSIP_HELLO,
                It.Is<byte[]>(p =>
                    p.Length == 8 &&
                    BitConverter.ToUInt64(p, 0) == TestMailboxGuid),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region GetMailListAsync - CMSG_GET_MAIL_LIST

        [Fact]
        public async Task GetMailListAsync_ShouldSendMailboxGuid()
        {
            await OpenTestMailbox();

            await _mailAgent.GetMailListAsync();

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GET_MAIL_LIST,
                It.Is<byte[]>(p =>
                    p.Length == 8 &&
                    BitConverter.ToUInt64(p, 0) == TestMailboxGuid),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetMailListAsync_WhenMailboxNotOpen_ShouldThrow()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => _mailAgent.GetMailListAsync());
        }

        #endregion

        #region SendMailAsync - CMSG_SEND_MAIL

        [Fact]
        public async Task SendMailAsync_ShouldSendCorrectFormat()
        {
            await OpenTestMailbox();

            const string recipient = "TestPlayer";
            const string subject = "Hello";
            const string body = "Hi there";

            await _mailAgent.SendMailAsync(recipient, subject, body);

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_SEND_MAIL,
                It.Is<byte[]>(p => VerifySendMailPayload(p, TestMailboxGuid, recipient, subject, body, 0, 0, 0)),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SendMailAsync_WhenMailboxNotOpen_ShouldThrow()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _mailAgent.SendMailAsync("Player", "Subject", "Body"));
        }

        [Fact]
        public async Task SendMailWithMoneyAsync_ShouldIncludeMoneyInPayload()
        {
            await OpenTestMailbox();

            await _mailAgent.SendMailWithMoneyAsync("Player", "Gold", "Here's gold", 10000);

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_SEND_MAIL,
                It.Is<byte[]>(p => VerifySendMailPayload(p, TestMailboxGuid, "Player", "Gold", "Here's gold", 10000, 0, 0)),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SendMailAsync_PayloadShouldHaveCorrectTotalSize()
        {
            await OpenTestMailbox();
            const string recipient = "A";
            const string subject = "B";
            const string body = "C";

            await _mailAgent.SendMailAsync(recipient, subject, body);

            // Expected: mailboxGuid(8) + "A\0"(2) + "B\0"(2) + "C\0"(2)
            //         + stationery(4) + unk(4) + itemGuid(8) + money(4) + COD(4)
            // = 8 + 2 + 2 + 2 + 4 + 4 + 8 + 4 + 4 = 38
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_SEND_MAIL,
                It.Is<byte[]>(p => p.Length == 38),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region TakeMoneyFromMailAsync - CMSG_MAIL_TAKE_MONEY

        [Fact]
        public async Task TakeMoneyFromMailAsync_ShouldSendMailboxGuidAndMailId()
        {
            await OpenTestMailbox();
            const uint mailId = 42;

            await _mailAgent.TakeMoneyFromMailAsync(mailId);

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_MAIL_TAKE_MONEY,
                It.Is<byte[]>(p =>
                    p.Length == 12 &&
                    BitConverter.ToUInt64(p, 0) == TestMailboxGuid &&
                    BitConverter.ToUInt32(p, 8) == mailId),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task TakeMoneyFromMailAsync_WhenMailboxNotOpen_ShouldThrow()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => _mailAgent.TakeMoneyFromMailAsync(1));
        }

        #endregion

        #region TakeItemFromMailAsync - CMSG_MAIL_TAKE_ITEM

        [Fact]
        public async Task TakeItemFromMailAsync_ShouldSendMailboxGuidAndMailIdOnly()
        {
            await OpenTestMailbox();
            const uint mailId = 99;
            const ulong itemGuid = 0xDEADBEEFUL; // should be ignored

            await _mailAgent.TakeItemFromMailAsync(mailId, itemGuid);

            // MaNGOS 1.12.1: CMSG_MAIL_TAKE_ITEM = mailboxGuid(8) + mailId(4)
            // itemGuid is NOT part of the protocol
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_MAIL_TAKE_ITEM,
                It.Is<byte[]>(p =>
                    p.Length == 12 &&
                    BitConverter.ToUInt64(p, 0) == TestMailboxGuid &&
                    BitConverter.ToUInt32(p, 8) == mailId),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task TakeItemFromMailAsync_WhenMailboxNotOpen_ShouldThrow()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => _mailAgent.TakeItemFromMailAsync(1, 0));
        }

        #endregion

        #region MarkMailAsReadAsync - CMSG_MAIL_MARK_AS_READ

        [Fact]
        public async Task MarkMailAsReadAsync_ShouldSendMailboxGuidAndMailId()
        {
            await OpenTestMailbox();
            const uint mailId = 7;

            await _mailAgent.MarkMailAsReadAsync(mailId);

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_MAIL_MARK_AS_READ,
                It.Is<byte[]>(p =>
                    p.Length == 12 &&
                    BitConverter.ToUInt64(p, 0) == TestMailboxGuid &&
                    BitConverter.ToUInt32(p, 8) == mailId),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task MarkMailAsReadAsync_WhenMailboxNotOpen_ShouldThrow()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => _mailAgent.MarkMailAsReadAsync(1));
        }

        #endregion

        #region DeleteMailAsync - CMSG_MAIL_DELETE

        [Fact]
        public async Task DeleteMailAsync_ShouldSendMailboxGuidAndMailId()
        {
            await OpenTestMailbox();
            const uint mailId = 50;

            await _mailAgent.DeleteMailAsync(mailId);

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_MAIL_DELETE,
                It.Is<byte[]>(p =>
                    p.Length == 12 &&
                    BitConverter.ToUInt64(p, 0) == TestMailboxGuid &&
                    BitConverter.ToUInt32(p, 8) == mailId),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteMailAsync_WhenMailboxNotOpen_ShouldThrow()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => _mailAgent.DeleteMailAsync(1));
        }

        #endregion

        #region ReturnMailToSenderAsync - CMSG_MAIL_RETURN_TO_SENDER

        [Fact]
        public async Task ReturnMailToSenderAsync_ShouldSendMailboxGuidAndMailId()
        {
            await OpenTestMailbox();
            const uint mailId = 33;

            await _mailAgent.ReturnMailToSenderAsync(mailId);

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_MAIL_RETURN_TO_SENDER,
                It.Is<byte[]>(p =>
                    p.Length == 12 &&
                    BitConverter.ToUInt64(p, 0) == TestMailboxGuid &&
                    BitConverter.ToUInt32(p, 8) == mailId),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ReturnMailToSenderAsync_WhenMailboxNotOpen_ShouldThrow()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => _mailAgent.ReturnMailToSenderAsync(1));
        }

        #endregion

        #region CreateTextItemFromMailAsync - CMSG_MAIL_CREATE_TEXT_ITEM

        [Fact]
        public async Task CreateTextItemFromMailAsync_ShouldSendMailboxGuidAndMailId()
        {
            await OpenTestMailbox();
            const uint mailId = 88;

            await _mailAgent.CreateTextItemFromMailAsync(mailId);

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_MAIL_CREATE_TEXT_ITEM,
                It.Is<byte[]>(p =>
                    p.Length == 12 &&
                    BitConverter.ToUInt64(p, 0) == TestMailboxGuid &&
                    BitConverter.ToUInt32(p, 8) == mailId),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateTextItemFromMailAsync_WhenMailboxNotOpen_ShouldThrow()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => _mailAgent.CreateTextItemFromMailAsync(1));
        }

        #endregion

        #region QueryNextMailTimeAsync - MSG_QUERY_NEXT_MAIL_TIME

        [Fact]
        public async Task QueryNextMailTimeAsync_ShouldSendEmptyPayload()
        {
            await _mailAgent.QueryNextMailTimeAsync();

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.MSG_QUERY_NEXT_MAIL_TIME,
                It.Is<byte[]>(p => p.Length == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region SMSG_MAIL_LIST_RESULT Parsing Tests

        [Fact]
        public void MailListCounts_ShouldParseMailCount()
        {
            var listSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Reset();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(It.Is<Opcode>(o => o != Opcode.SMSG_MAIL_LIST_RESULT)))
                .Returns((IObservable<ReadOnlyMemory<byte>>)new Subject<ReadOnlyMemory<byte>>());
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(Opcode.SMSG_MAIL_LIST_RESULT))
                .Returns(listSubject);

            var component = new MailNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
            uint? result = null;
            using var sub = component.MailListCounts.Subscribe(r => result = r);

            // Build a mail list with 1 normal mail
            var payload = BuildMailListPayload([
                (messageId: 100, messageType: 0, senderGuid: 500UL, subject: "Test Mail",
                 itemEntry: 0, itemCount: 0, money: 1000, cod: 0, checkedFlags: 0, expireDays: 4.5f)
            ]);
            listSubject.OnNext(payload);

            Assert.NotNull(result);
            Assert.Equal(1U, result.Value);
        }

        [Fact]
        public void MailListCounts_ShouldParseMultipleMails()
        {
            var listSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Reset();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(It.Is<Opcode>(o => o != Opcode.SMSG_MAIL_LIST_RESULT)))
                .Returns((IObservable<ReadOnlyMemory<byte>>)new Subject<ReadOnlyMemory<byte>>());
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(Opcode.SMSG_MAIL_LIST_RESULT))
                .Returns(listSubject);

            var component = new MailNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
            uint? result = null;
            using var sub = component.MailListCounts.Subscribe(r => result = r);

            var payload = BuildMailListPayload([
                (messageId: 1, messageType: 0, senderGuid: 100UL, subject: "Mail 1",
                 itemEntry: 0, itemCount: 0, money: 500, cod: 0, checkedFlags: 0, expireDays: 3.0f),
                (messageId: 2, messageType: 0, senderGuid: 200UL, subject: "Mail 2",
                 itemEntry: 12345, itemCount: 5, money: 0, cod: 0, checkedFlags: 1, expireDays: 2.0f),
                (messageId: 3, messageType: 2, senderGuid: 300UL, subject: "Auction Won",
                 itemEntry: 6789, itemCount: 1, money: 0, cod: 0, checkedFlags: 0, expireDays: 5.0f)
            ]);
            listSubject.OnNext(payload);

            Assert.NotNull(result);
            Assert.Equal(3U, result.Value);
        }

        [Fact]
        public void MailListCounts_EmptyPayload_ShouldReturnZero()
        {
            var listSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Reset();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(It.Is<Opcode>(o => o != Opcode.SMSG_MAIL_LIST_RESULT)))
                .Returns((IObservable<ReadOnlyMemory<byte>>)new Subject<ReadOnlyMemory<byte>>());
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(Opcode.SMSG_MAIL_LIST_RESULT))
                .Returns(listSubject);

            var component = new MailNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
            uint? result = null;
            using var sub = component.MailListCounts.Subscribe(r => result = r);

            listSubject.OnNext(Array.Empty<byte>());

            Assert.NotNull(result);
            Assert.Equal(0U, result.Value);
        }

        [Fact]
        public void MailListCounts_ZeroCount_ShouldReturnZero()
        {
            var listSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Reset();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(It.Is<Opcode>(o => o != Opcode.SMSG_MAIL_LIST_RESULT)))
                .Returns((IObservable<ReadOnlyMemory<byte>>)new Subject<ReadOnlyMemory<byte>>());
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(Opcode.SMSG_MAIL_LIST_RESULT))
                .Returns(listSubject);

            var component = new MailNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
            uint? result = null;
            using var sub = component.MailListCounts.Subscribe(r => result = r);

            // Just the count byte = 0
            listSubject.OnNext(new byte[] { 0 });

            Assert.NotNull(result);
            Assert.Equal(0U, result.Value);
        }

        #endregion

        #region SMSG_SEND_MAIL_RESULT Parsing Tests

        [Fact]
        public void MailSentResults_ShouldEmitOnSuccess()
        {
            var resultSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Reset();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(It.Is<Opcode>(o => o != Opcode.SMSG_SEND_MAIL_RESULT)))
                .Returns((IObservable<ReadOnlyMemory<byte>>)new Subject<ReadOnlyMemory<byte>>());
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(Opcode.SMSG_SEND_MAIL_RESULT))
                .Returns(resultSubject);

            var component = new MailNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
            (string Recipient, string Subject)? result = null;
            using var sub = component.MailSentResults.Subscribe(r => result = r);

            // mailId=0 + mailAction=0 (MAIL_SEND) + mailError=0 (MAIL_OK)
            var payload = BuildSendMailResultPayload(0, 0, 0);
            resultSubject.OnNext(payload);

            Assert.NotNull(result);
        }

        [Fact]
        public void MailErrors_ShouldEmitOnFailure()
        {
            var resultSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Reset();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(It.Is<Opcode>(o => o != Opcode.SMSG_SEND_MAIL_RESULT)))
                .Returns((IObservable<ReadOnlyMemory<byte>>)new Subject<ReadOnlyMemory<byte>>());
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(Opcode.SMSG_SEND_MAIL_RESULT))
                .Returns(resultSubject);

            var component = new MailNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
            (string Operation, string Error)? result = null;
            using var sub = component.MailErrors.Subscribe(r => result = r);

            // mailId=0 + mailAction=0 (MAIL_SEND) + mailError=4 (RecipientNotFound)
            var payload = BuildSendMailResultPayload(0, 0, 4);
            resultSubject.OnNext(payload);

            Assert.NotNull(result);
            Assert.Equal("Send", result.Value.Operation);
            Assert.Contains("RecipientNotFound", result.Value.Error);
        }

        [Fact]
        public void MailErrors_ShouldReportNotEnoughMoney()
        {
            var resultSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Reset();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(It.Is<Opcode>(o => o != Opcode.SMSG_SEND_MAIL_RESULT)))
                .Returns((IObservable<ReadOnlyMemory<byte>>)new Subject<ReadOnlyMemory<byte>>());
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(Opcode.SMSG_SEND_MAIL_RESULT))
                .Returns(resultSubject);

            var component = new MailNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
            (string Operation, string Error)? result = null;
            using var sub = component.MailErrors.Subscribe(r => result = r);

            // mailId=5 + mailAction=0 (MAIL_SEND) + mailError=3 (NotEnoughMoney)
            var payload = BuildSendMailResultPayload(5, 0, 3);
            resultSubject.OnNext(payload);

            Assert.NotNull(result);
            Assert.Contains("NotEnoughMoney", result.Value.Error);
        }

        [Fact]
        public void SendMailResult_ShortPayload_ShouldReportError()
        {
            var resultSubject = new Subject<ReadOnlyMemory<byte>>();
            _mockWorldClient.Reset();
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(It.Is<Opcode>(o => o != Opcode.SMSG_SEND_MAIL_RESULT)))
                .Returns((IObservable<ReadOnlyMemory<byte>>)new Subject<ReadOnlyMemory<byte>>());
            _mockWorldClient.Setup(c => c.RegisterOpcodeHandler(Opcode.SMSG_SEND_MAIL_RESULT))
                .Returns(resultSubject);

            var component = new MailNetworkClientComponent(_mockWorldClient.Object, _mockLogger.Object);
            (string Operation, string Error)? result = null;
            using var sub = component.MailErrors.Subscribe(r => result = r);

            // Only 8 bytes — too short (need 12)
            resultSubject.OnNext(new byte[8]);

            Assert.NotNull(result);
            Assert.Contains("Malformed", result.Value.Error);
        }

        #endregion

        #region Quick Operation Tests

        [Fact]
        public async Task QuickCheckMailAsync_ShouldOpenGetListAndClose()
        {
            await _mailAgent.QuickCheckMailAsync(TestMailboxGuid);

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GOSSIP_HELLO, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GET_MAIL_LIST, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
            Assert.False(_mailAgent.IsMailboxWindowOpen); // closed at end
        }

        [Fact]
        public async Task QuickSendMailAsync_ShouldOpenSendAndClose()
        {
            await _mailAgent.QuickSendMailAsync(TestMailboxGuid, "Player", "Subject", "Body");

            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_GOSSIP_HELLO, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockWorldClient.Verify(x => x.SendOpcodeAsync(
                Opcode.CMSG_SEND_MAIL, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
            Assert.False(_mailAgent.IsMailboxWindowOpen);
        }

        #endregion

        #region Payload Builder Helpers

        private static bool VerifySendMailPayload(byte[] p, ulong mailboxGuid, string recipient,
            string subject, string body, uint money, uint cod, ulong itemGuid)
        {
            int offset = 0;

            // mailboxGuid(8)
            if (BitConverter.ToUInt64(p, offset) != mailboxGuid) return false;
            offset += 8;

            // recipient\0
            var recipientBytes = Encoding.UTF8.GetBytes(recipient);
            for (int i = 0; i < recipientBytes.Length; i++)
                if (p[offset + i] != recipientBytes[i]) return false;
            offset += recipientBytes.Length;
            if (p[offset++] != 0) return false;

            // subject\0
            var subjectBytes = Encoding.UTF8.GetBytes(subject);
            for (int i = 0; i < subjectBytes.Length; i++)
                if (p[offset + i] != subjectBytes[i]) return false;
            offset += subjectBytes.Length;
            if (p[offset++] != 0) return false;

            // body\0
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            for (int i = 0; i < bodyBytes.Length; i++)
                if (p[offset + i] != bodyBytes[i]) return false;
            offset += bodyBytes.Length;
            if (p[offset++] != 0) return false;

            // stationery(4) = 41
            if (BitConverter.ToUInt32(p, offset) != 41) return false;
            offset += 4;

            // unk(4) = 0
            if (BitConverter.ToUInt32(p, offset) != 0) return false;
            offset += 4;

            // itemGuid(8)
            if (BitConverter.ToUInt64(p, offset) != itemGuid) return false;
            offset += 8;

            // money(4)
            if (BitConverter.ToUInt32(p, offset) != money) return false;
            offset += 4;

            // COD(4)
            if (BitConverter.ToUInt32(p, offset) != cod) return false;

            return true;
        }

        private static byte[] BuildSendMailResultPayload(uint mailId, uint mailAction, uint mailError)
        {
            var payload = new byte[12];
            BitConverter.TryWriteBytes(payload.AsSpan(0), mailId);
            BitConverter.TryWriteBytes(payload.AsSpan(4), mailAction);
            BitConverter.TryWriteBytes(payload.AsSpan(8), mailError);
            return payload;
        }

        /// <summary>
        /// Builds SMSG_MAIL_LIST_RESULT payload matching MaNGOS 1.12.1 format.
        /// </summary>
        private static byte[] BuildMailListPayload(
            (uint messageId, byte messageType, ulong senderGuid, string subject,
             uint itemEntry, byte itemCount, uint money, uint cod,
             uint checkedFlags, float expireDays)[] mails)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

            // mailCount (uint8)
            bw.Write((byte)mails.Length);

            foreach (var m in mails)
            {
                bw.Write(m.messageId);       // messageID (uint32)
                bw.Write(m.messageType);     // messageType (uint8)

                // Sender info
                if (m.messageType == 0) // MAIL_NORMAL
                    bw.Write(m.senderGuid);  // ObjectGuid (8 bytes)
                else if (m.messageType == 2 || m.messageType == 3 || m.messageType == 4)
                    bw.Write((uint)m.senderGuid); // uint32 sender ID

                // subject\0
                bw.Write(Encoding.UTF8.GetBytes(m.subject));
                bw.Write((byte)0);

                // itemTextId(4) + unk(4) + stationery(4)
                bw.Write(0U); // itemTextId
                bw.Write(0U); // unk
                bw.Write(41U); // stationery = MAIL_STATIONERY_DEFAULT

                // Item data - always 33 bytes
                bw.Write(m.itemEntry);       // itemEntry
                bw.Write(0U);                // enchantId
                bw.Write(0U);                // randomProperty
                bw.Write(0U);                // suffixFactor
                bw.Write(m.itemCount);       // itemCount (uint8)
                bw.Write(0U);                // spellCharges
                bw.Write(0U);                // maxDurability
                bw.Write(0U);                // curDurability

                // money(4) + COD(4) + checked(4) + expireTime(4) + mailTemplateId(4)
                bw.Write(m.money);
                bw.Write(m.cod);
                bw.Write(m.checkedFlags);
                bw.Write(m.expireDays);
                bw.Write(0U); // mailTemplateId
            }

            return ms.ToArray();
        }

        #endregion
    }
}
