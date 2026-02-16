using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Implementation of mail network agent that handles mail operations in World of Warcraft.
    /// Manages sending mail, receiving mail, mail attachments, and mail state tracking using the MaNGOS protocol.
    /// Reactive variant: exposes opcode-backed observables instead of events/subjects.
    /// </summary>
    public class MailNetworkClientComponent : NetworkClientComponent, IMailNetworkClientComponent, IDisposable
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<MailNetworkClientComponent> _logger;
        private readonly object _stateLock = new object();

        private readonly List<MailInfo> _mailbox = [];
        private ulong _currentMailboxGuid;
        private bool _isMailboxOpen;
        private bool _disposed;

        /// <summary>MaNGOS MAIL_STATIONERY_DEFAULT = 41</summary>
        private const uint MAIL_STATIONERY_DEFAULT = 41;

        // MaNGOS MailResponseType enum values
        private const uint MAIL_SEND = 0;
        private const uint MAIL_MONEY_TAKEN = 1;
        private const uint MAIL_ITEM_TAKEN = 2;
        private const uint MAIL_RETURNED_TO_SENDER = 3;
        private const uint MAIL_DELETED = 4;
        private const uint MAIL_MADE_PERMANENT = 5;

        // MaNGOS MailMessageType byte values
        private const byte MAIL_TYPE_NORMAL = 0;
        private const byte MAIL_TYPE_AUCTION = 2;
        private const byte MAIL_TYPE_CREATURE = 3;
        private const byte MAIL_TYPE_GAMEOBJECT = 4;

        // Reactive streams
        private readonly IObservable<ulong> _mailboxWindowOpenings;
        private readonly IObservable<Unit> _mailboxWindowClosings;
        private readonly IObservable<uint> _mailListCounts;
        private readonly IObservable<(string Recipient, string Subject)> _mailSentResults;
        private readonly IObservable<(string Operation, string Error)> _mailErrors;

        // Optional streams (no direct opcode mapping known) -> Never/Empty to satisfy interface
        private readonly IObservable<(uint MailId, uint Amount)> _moneyTakenResults;
        private readonly IObservable<(uint MailId, uint ItemId, uint Quantity)> _itemTakenResults;
        private readonly IObservable<uint> _mailReadMarks;
        private readonly IObservable<uint> _mailDeletes;
        private readonly IObservable<uint> _mailReturns;

        public MailNetworkClientComponent(IWorldClient worldClient, ILogger<MailNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // SMSG_MAIL_LIST_RESULT: parse full mail list, emit count
            _mailListCounts = SafeOpcodeStream(Opcode.SMSG_MAIL_LIST_RESULT)
                .Select(ParseMailList)
                .Do(count => _logger.LogInformation("Mail list received with {Count} mails", count))
                .Publish()
                .RefCount();

            // SMSG_SEND_MAIL_RESULT: mailId(4) + mailAction(4) + mailError(4) + optional fields
            var sendResults = SafeOpcodeStream(Opcode.SMSG_SEND_MAIL_RESULT)
                .Select(ParseSendMailResult)
                .Publish()
                .RefCount();

            _mailSentResults = sendResults
                .Where(r => r.Success)
                .Select(r => (Recipient: "Unknown", Subject: "Unknown"))
                .Do(_ => _logger.LogInformation("Mail operation succeeded"))
                .Publish()
                .RefCount();

            _mailErrors = sendResults
                .Where(r => !r.Success)
                .Select(r => (Operation: r.Action, Error: r.ErrorMessage ?? "Unknown error"))
                .Do(err => _logger.LogWarning("Mail operation failed - {Operation}: {Error}", err.Operation, err.Error))
                .Publish()
                .RefCount();

            // No direct opcode mapping for these in 1.12.1 — operations are confirmed
            // via SMSG_SEND_MAIL_RESULT with different mailAction values
            _moneyTakenResults = Observable.Never<(uint MailId, uint Amount)>();
            _itemTakenResults = Observable.Never<(uint MailId, uint ItemId, uint Quantity)>();
            _mailReadMarks = Observable.Never<uint>();
            _mailDeletes = Observable.Never<uint>();
            _mailReturns = Observable.Never<uint>();

            _mailboxWindowOpenings = Observable.Empty<ulong>();
            _mailboxWindowClosings = Observable.Empty<Unit>();
        }

        #region IMailNetworkClientComponent (reactive properties)
        public bool IsMailboxWindowOpen => _isMailboxOpen;
        public IObservable<ulong> MailboxWindowOpenings => _mailboxWindowOpenings;
        public IObservable<Unit> MailboxWindowClosings => _mailboxWindowClosings;
        public IObservable<uint> MailListCounts => _mailListCounts;
        public IObservable<(string Recipient, string Subject)> MailSentResults => _mailSentResults;
        public IObservable<(uint MailId, uint Amount)> MoneyTakenResults => _moneyTakenResults;
        public IObservable<(uint MailId, uint ItemId, uint Quantity)> ItemTakenResults => _itemTakenResults;
        public IObservable<uint> MailReadMarks => _mailReadMarks;
        public IObservable<uint> MailDeletes => _mailDeletes;
        public IObservable<uint> MailReturns => _mailReturns;
        public IObservable<(string Operation, string Error)> MailErrors => _mailErrors;
        #endregion

        /// <summary>
        /// Opens mailbox interaction. CMSG_GOSSIP_HELLO: mailboxGuid(8).
        /// Stores the mailbox GUID for subsequent operations.
        /// </summary>
        public async Task OpenMailboxAsync(ulong mailboxGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Opening mailbox interaction with: {MailboxGuid:X}", mailboxGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(mailboxGuid).CopyTo(payload, 0);
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GOSSIP_HELLO, payload, cancellationToken);

                lock (_stateLock)
                {
                    _currentMailboxGuid = mailboxGuid;
                    _isMailboxOpen = true;
                }
                _logger.LogInformation("Mailbox interaction initiated with: {MailboxGuid:X}", mailboxGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open mailbox interaction with: {MailboxGuid:X}", mailboxGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <summary>
        /// Requests mail list. CMSG_GET_MAIL_LIST: mailboxGuid(8).
        /// </summary>
        public async Task GetMailListAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                EnsureMailboxOpen();
                SetOperationInProgress(true);
                _logger.LogDebug("Requesting mail list from server");

                var payload = new byte[8];
                BitConverter.GetBytes(_currentMailboxGuid).CopyTo(payload, 0);
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GET_MAIL_LIST, payload, cancellationToken);

                _logger.LogInformation("Mail list request sent to server");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to request mail list");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task SendMailAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default)
        {
            await SendMailInternalAsync(recipient, subject, body, 0, 0, 0, cancellationToken);
        }

        public async Task SendMailWithMoneyAsync(string recipient, string subject, string body, uint money, CancellationToken cancellationToken = default)
        {
            await SendMailInternalAsync(recipient, subject, body, money, 0, 0, cancellationToken);
        }

        public async Task SendMailWithItemAsync(string recipient, string subject, string body, byte bagId, byte slotId, CancellationToken cancellationToken = default)
        {
            // NOTE: MaNGOS CMSG_SEND_MAIL expects an item GUID, not bag/slot.
            // Item sending requires inventory tracking to resolve bag+slot → itemGuid.
            // For now, log a warning and send itemGuid=0 (no item attached).
            _logger.LogWarning("SendMailWithItemAsync: item sending via bag/slot not supported in 1.12.1 protocol " +
                              "(requires item GUID resolution). Mail will be sent without item attachment.");
            await SendMailInternalAsync(recipient, subject, body, 0, 0, 0, cancellationToken);
        }

        public async Task SendMailWithMoneyAndItemAsync(string recipient, string subject, string body, uint money, byte bagId, byte slotId, CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("SendMailWithMoneyAndItemAsync: item sending via bag/slot not supported. Sending money only.");
            await SendMailInternalAsync(recipient, subject, body, money, 0, 0, cancellationToken);
        }

        public async Task SendCODMailAsync(string recipient, string subject, string body, uint codAmount, byte bagId, byte slotId, CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("SendCODMailAsync: item sending via bag/slot not supported. COD mail requires item attachment.");
            await SendMailInternalAsync(recipient, subject, body, 0, codAmount, 0, cancellationToken);
        }

        /// <summary>
        /// Takes money from a mail. CMSG_MAIL_TAKE_MONEY: mailboxGuid(8) + mailId(4).
        /// </summary>
        public async Task TakeMoneyFromMailAsync(uint mailId, CancellationToken cancellationToken = default)
        {
            try
            {
                EnsureMailboxOpen();
                SetOperationInProgress(true);
                _logger.LogDebug("Taking money from mail: {MailId}", mailId);

                var payload = new byte[12]; // mailboxGuid(8) + mailId(4)
                BitConverter.GetBytes(_currentMailboxGuid).CopyTo(payload, 0);
                BitConverter.GetBytes(mailId).CopyTo(payload, 8);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_MAIL_TAKE_MONEY, payload, cancellationToken);
                _logger.LogInformation("Money take request sent for mail: {MailId}", mailId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to take money from mail: {MailId}", mailId);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <summary>
        /// Takes item from a mail. CMSG_MAIL_TAKE_ITEM: mailboxGuid(8) + mailId(4).
        /// In 1.12.1, only 1 item per mail, so the itemGuid parameter is ignored.
        /// </summary>
        public async Task TakeItemFromMailAsync(uint mailId, ulong itemGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                EnsureMailboxOpen();
                SetOperationInProgress(true);
                _logger.LogDebug("Taking item from mail: {MailId}", mailId);

                // MaNGOS 1.12.1: CMSG_MAIL_TAKE_ITEM = mailboxGuid(8) + mailId(4)
                // itemGuid parameter is not part of the protocol (only 1 item per mail in vanilla)
                var payload = new byte[12]; // mailboxGuid(8) + mailId(4)
                BitConverter.GetBytes(_currentMailboxGuid).CopyTo(payload, 0);
                BitConverter.GetBytes(mailId).CopyTo(payload, 8);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_MAIL_TAKE_ITEM, payload, cancellationToken);
                _logger.LogInformation("Item take request sent for mail: {MailId}", mailId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to take item from mail: {MailId}", mailId);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <summary>
        /// Marks mail as read. CMSG_MAIL_MARK_AS_READ: mailboxGuid(8) + mailId(4).
        /// </summary>
        public async Task MarkMailAsReadAsync(uint mailId, CancellationToken cancellationToken = default)
        {
            try
            {
                EnsureMailboxOpen();
                SetOperationInProgress(true);
                _logger.LogDebug("Marking mail as read: {MailId}", mailId);

                var payload = new byte[12]; // mailboxGuid(8) + mailId(4)
                BitConverter.GetBytes(_currentMailboxGuid).CopyTo(payload, 0);
                BitConverter.GetBytes(mailId).CopyTo(payload, 8);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_MAIL_MARK_AS_READ, payload, cancellationToken);
                _logger.LogInformation("Mark as read request sent for mail: {MailId}", mailId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to mark mail as read: {MailId}", mailId);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <summary>
        /// Deletes a mail. CMSG_MAIL_DELETE: mailboxGuid(8) + mailId(4).
        /// </summary>
        public async Task DeleteMailAsync(uint mailId, CancellationToken cancellationToken = default)
        {
            try
            {
                EnsureMailboxOpen();
                SetOperationInProgress(true);
                _logger.LogDebug("Deleting mail: {MailId}", mailId);

                var payload = new byte[12]; // mailboxGuid(8) + mailId(4)
                BitConverter.GetBytes(_currentMailboxGuid).CopyTo(payload, 0);
                BitConverter.GetBytes(mailId).CopyTo(payload, 8);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_MAIL_DELETE, payload, cancellationToken);
                _logger.LogInformation("Delete request sent for mail: {MailId}", mailId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete mail: {MailId}", mailId);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <summary>
        /// Returns mail to sender. CMSG_MAIL_RETURN_TO_SENDER: mailboxGuid(8) + mailId(4).
        /// </summary>
        public async Task ReturnMailToSenderAsync(uint mailId, CancellationToken cancellationToken = default)
        {
            try
            {
                EnsureMailboxOpen();
                SetOperationInProgress(true);
                _logger.LogDebug("Returning mail to sender: {MailId}", mailId);

                var payload = new byte[12]; // mailboxGuid(8) + mailId(4)
                BitConverter.GetBytes(_currentMailboxGuid).CopyTo(payload, 0);
                BitConverter.GetBytes(mailId).CopyTo(payload, 8);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_MAIL_RETURN_TO_SENDER, payload, cancellationToken);
                _logger.LogInformation("Return to sender request sent for mail: {MailId}", mailId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to return mail to sender: {MailId}", mailId);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <summary>
        /// Creates text item from mail. CMSG_MAIL_CREATE_TEXT_ITEM: mailboxGuid(8) + mailId(4).
        /// </summary>
        public async Task CreateTextItemFromMailAsync(uint mailId, CancellationToken cancellationToken = default)
        {
            try
            {
                EnsureMailboxOpen();
                SetOperationInProgress(true);
                _logger.LogDebug("Creating text item from mail: {MailId}", mailId);

                var payload = new byte[12]; // mailboxGuid(8) + mailId(4)
                BitConverter.GetBytes(_currentMailboxGuid).CopyTo(payload, 0);
                BitConverter.GetBytes(mailId).CopyTo(payload, 8);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_MAIL_CREATE_TEXT_ITEM, payload, cancellationToken);
                _logger.LogInformation("Create text item request sent for mail: {MailId}", mailId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create text item from mail: {MailId}", mailId);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <summary>
        /// Queries next mail time. MSG_QUERY_NEXT_MAIL_TIME: empty payload.
        /// </summary>
        public async Task QueryNextMailTimeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Querying next mail time");

                await _worldClient.SendOpcodeAsync(Opcode.MSG_QUERY_NEXT_MAIL_TIME, [], cancellationToken);
                _logger.LogInformation("Next mail time query sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query next mail time");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task CloseMailboxAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Closing mailbox window");

                lock (_stateLock)
                {
                    _isMailboxOpen = false;
                    _currentMailboxGuid = 0;
                }
                _logger.LogInformation("Mailbox window closed");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to close mailbox window");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public bool IsMailboxOpen(ulong mailboxGuid) => _isMailboxOpen;

        public async Task QuickCheckMailAsync(ulong mailboxGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                await OpenMailboxAsync(mailboxGuid, cancellationToken);
                await Task.Delay(100, cancellationToken);
                await GetMailListAsync(cancellationToken);
                await Task.Delay(100, cancellationToken);
                await CloseMailboxAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Quick mail check failed with mailbox: {MailboxGuid:X}", mailboxGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task QuickCollectAllMailAsync(ulong mailboxGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                await OpenMailboxAsync(mailboxGuid, cancellationToken);
                await Task.Delay(100, cancellationToken);
                await GetMailListAsync(cancellationToken);
                await Task.Delay(100, cancellationToken);
                await CloseMailboxAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Quick collect all mail failed with mailbox: {MailboxGuid:X}", mailboxGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task QuickSendMailAsync(ulong mailboxGuid, string recipient, string subject, string body, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                await OpenMailboxAsync(mailboxGuid, cancellationToken);
                await Task.Delay(100, cancellationToken);
                await SendMailAsync(recipient, subject, body, cancellationToken);
                await Task.Delay(100, cancellationToken);
                await CloseMailboxAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Quick send mail failed to {Recipient} with mailbox: {MailboxGuid:X}", recipient, mailboxGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        #region Private Helpers
        private IObservable<ReadOnlyMemory<byte>> SafeOpcodeStream(Opcode opcode)
            => _worldClient.RegisterOpcodeHandler(opcode) ?? Observable.Empty<ReadOnlyMemory<byte>>();

        private void EnsureMailboxOpen()
        {
            if (!_isMailboxOpen)
                throw new InvalidOperationException("Mailbox is not open. Call OpenMailboxAsync first.");
        }

        /// <summary>
        /// Builds and sends CMSG_SEND_MAIL.
        /// MaNGOS format: mailboxGuid(8) + recipient\0 + subject\0 + body\0
        ///   + stationery(4) + unk(4) + itemGuid(8) + money(4) + COD(4)
        /// </summary>
        private async Task SendMailInternalAsync(string recipient, string subject, string body,
            uint money, uint codAmount, ulong itemGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                EnsureMailboxOpen();
                SetOperationInProgress(true);
                _logger.LogDebug("Sending mail to {Recipient}: '{Subject}'", recipient, subject);

                var recipientBytes = Encoding.UTF8.GetBytes(recipient);
                var subjectBytes = Encoding.UTF8.GetBytes(subject);
                var bodyBytes = Encoding.UTF8.GetBytes(body);

                // MaNGOS CMSG_SEND_MAIL:
                //   mailboxGuid(8) + recipient\0 + subject\0 + body\0
                //   + stationery(4) + unk(4) + itemGuid(8) + money(4) + COD(4)
                var payloadSize = 8 +                        // mailboxGuid
                                  recipientBytes.Length + 1 + // recipient\0
                                  subjectBytes.Length + 1 +   // subject\0
                                  bodyBytes.Length + 1 +      // body\0
                                  4 +                         // stationery (uint32)
                                  4 +                         // unk2 (uint32, always 0)
                                  8 +                         // itemGuid (ObjectGuid)
                                  4 +                         // money (uint32)
                                  4;                          // COD (uint32)

                var payload = new byte[payloadSize];
                var offset = 0;

                // mailboxGuid(8)
                BitConverter.GetBytes(_currentMailboxGuid).CopyTo(payload, offset); offset += 8;

                // recipient\0
                recipientBytes.CopyTo(payload, offset); offset += recipientBytes.Length;
                payload[offset++] = 0;

                // subject\0
                subjectBytes.CopyTo(payload, offset); offset += subjectBytes.Length;
                payload[offset++] = 0;

                // body\0
                bodyBytes.CopyTo(payload, offset); offset += bodyBytes.Length;
                payload[offset++] = 0;

                // stationery(4) = MAIL_STATIONERY_DEFAULT
                BitConverter.GetBytes(MAIL_STATIONERY_DEFAULT).CopyTo(payload, offset); offset += 4;

                // unk2(4) = 0
                BitConverter.GetBytes(0U).CopyTo(payload, offset); offset += 4;

                // itemGuid(8) — 0 if no item
                BitConverter.GetBytes(itemGuid).CopyTo(payload, offset); offset += 8;

                // money(4)
                BitConverter.GetBytes(money).CopyTo(payload, offset); offset += 4;

                // COD(4)
                BitConverter.GetBytes(codAmount).CopyTo(payload, offset);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_SEND_MAIL, payload, cancellationToken);
                _logger.LogInformation("Mail sent to {Recipient}: '{Subject}' (Money: {Money}, COD: {COD})", recipient, subject, money, codAmount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send mail to {Recipient}: '{Subject}'", recipient, subject);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }
        #endregion

        #region SMSG Parsers

        /// <summary>
        /// Parses SMSG_MAIL_LIST_RESULT. MaNGOS 1.12.1 format:
        ///   mailCount(1, uint8) + [per mail: messageID(4) + messageType(1)
        ///   + sender(variable) + subject\0 + itemTextId(4) + unk(4) + stationery(4)
        ///   + item data(33 bytes) + money(4) + COD(4) + checked(4)
        ///   + expireTime(4, float) + mailTemplateId(4)]
        /// Returns the mail count and populates internal _mailbox cache.
        /// </summary>
        private uint ParseMailList(ReadOnlyMemory<byte> payload)
        {
            try
            {
                var span = payload.Span;
                if (span.Length < 1) return 0;

                int offset = 0;
                byte mailCount = span[offset]; offset += 1;

                var mails = new List<MailInfo>();

                for (int i = 0; i < mailCount && offset < span.Length; i++)
                {
                    try
                    {
                        if (offset + 5 > span.Length) break; // need messageID(4) + messageType(1)
                        uint messageId = BitConverter.ToUInt32(span.Slice(offset, 4)); offset += 4;
                        byte messageType = span[offset]; offset += 1;

                        // Sender info varies by type
                        ulong senderGuid = 0;
                        uint senderId = 0;
                        if (messageType == MAIL_TYPE_NORMAL)
                        {
                            if (offset + 8 > span.Length) break;
                            senderGuid = BitConverter.ToUInt64(span.Slice(offset, 8)); offset += 8;
                        }
                        else if (messageType == MAIL_TYPE_AUCTION || messageType == MAIL_TYPE_CREATURE || messageType == MAIL_TYPE_GAMEOBJECT)
                        {
                            if (offset + 4 > span.Length) break;
                            senderId = BitConverter.ToUInt32(span.Slice(offset, 4)); offset += 4;
                        }
                        // MAIL_TYPE_ITEM (5): no sender data (NYI in MaNGOS)

                        // subject\0
                        string subject = ReadCString(span, ref offset);

                        // itemTextId(4) + unk(4) + stationery(4)
                        if (offset + 12 > span.Length) break;
                        uint itemTextId = BitConverter.ToUInt32(span.Slice(offset, 4)); offset += 4;
                        offset += 4; // unk
                        offset += 4; // stationery

                        // Item data: always 33 bytes (8×uint32 + 1×uint8)
                        // itemEntry(4) + enchantId(4) + randomProperty(4) + suffixFactor(4)
                        // + itemCount(1) + spellCharges(4) + maxDurability(4) + curDurability(4)
                        if (offset + 33 > span.Length) break;
                        uint itemEntry = BitConverter.ToUInt32(span.Slice(offset, 4)); offset += 4;
                        uint enchantId = BitConverter.ToUInt32(span.Slice(offset, 4)); offset += 4;
                        uint randomProperty = BitConverter.ToUInt32(span.Slice(offset, 4)); offset += 4;
                        uint suffixFactor = BitConverter.ToUInt32(span.Slice(offset, 4)); offset += 4;
                        byte itemCount = span[offset]; offset += 1;
                        uint spellCharges = BitConverter.ToUInt32(span.Slice(offset, 4)); offset += 4;
                        offset += 4; // maxDurability
                        offset += 4; // curDurability

                        // money(4) + COD(4) + checked(4) + expireTime(4, float) + mailTemplateId(4)
                        if (offset + 20 > span.Length) break;
                        uint money = BitConverter.ToUInt32(span.Slice(offset, 4)); offset += 4;
                        uint cod = BitConverter.ToUInt32(span.Slice(offset, 4)); offset += 4;
                        uint checkedFlags = BitConverter.ToUInt32(span.Slice(offset, 4)); offset += 4;
                        float expireDays = BitConverter.ToSingle(span.Slice(offset, 4)); offset += 4;
                        offset += 4; // mailTemplateId (1.10+)

                        var mailType = messageType switch
                        {
                            MAIL_TYPE_NORMAL => MailType.Normal,
                            MAIL_TYPE_AUCTION => MailType.Auction,
                            MAIL_TYPE_CREATURE => MailType.Creature,
                            MAIL_TYPE_GAMEOBJECT => MailType.GameObject,
                            _ => MailType.Normal
                        };

                        var mail = new MailInfo
                        {
                            MailId = messageId,
                            MailType = mailType,
                            Subject = subject,
                            Money = money,
                            COD = cod,
                            Flags = (MailFlags)(checkedFlags & 0xFF),
                            ExpiryTime = DateTime.UtcNow.AddDays(expireDays),
                        };

                        if (itemEntry != 0)
                        {
                            mail.Attachments = [new MailAttachment
                            {
                                ItemId = itemEntry,
                                Count = itemCount,
                                Charges = spellCharges,
                                Enchantments = enchantId != 0 ? [enchantId] : [],
                            }];
                        }

                        mails.Add(mail);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse mail entry {Index}, stopping", i);
                        break;
                    }
                }

                lock (_stateLock)
                {
                    _mailbox.Clear();
                    _mailbox.AddRange(mails);
                }

                return mailCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse mail list ({Len} bytes)", payload.Length);
                return 0;
            }
        }

        /// <summary>
        /// Parses SMSG_SEND_MAIL_RESULT. MaNGOS format:
        ///   mailId(4) + mailAction(4) + mailError(4)
        ///   [if mailError == EQUIP_ERROR: equipError(4)]
        ///   [if mailAction == ITEM_TAKEN and success: itemId(4) + itemCount(4)]
        /// </summary>
        private (bool Success, string Action, string? ErrorMessage) ParseSendMailResult(ReadOnlyMemory<byte> payload)
        {
            try
            {
                var span = payload.Span;
                if (span.Length < 12) return (false, "Unknown", "Malformed send mail result (< 12 bytes)");

                uint mailId = BitConverter.ToUInt32(span[..4]);
                uint mailAction = BitConverter.ToUInt32(span.Slice(4, 4));
                uint mailError = BitConverter.ToUInt32(span.Slice(8, 4));

                var actionName = mailAction switch
                {
                    MAIL_SEND => "Send",
                    MAIL_MONEY_TAKEN => "TakeMoney",
                    MAIL_ITEM_TAKEN => "TakeItem",
                    MAIL_RETURNED_TO_SENDER => "ReturnToSender",
                    MAIL_DELETED => "Delete",
                    MAIL_MADE_PERMANENT => "MakePermanent",
                    _ => $"Unknown({mailAction})"
                };

                if (mailError == 0) // MAIL_OK
                    return (true, actionName, null);

                var errorName = mailError switch
                {
                    1 => "EquipError",
                    2 => "CannotSendToSelf",
                    3 => "NotEnoughMoney",
                    4 => "RecipientNotFound",
                    5 => "NotYourTeam",
                    6 => "InternalError",
                    14 => "DisabledForTrialAcc",
                    15 => "RecipientCapReached",
                    _ => $"UnknownError({mailError})"
                };

                return (false, actionName, $"MailId={mailId} Error={errorName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse send mail result ({Len} bytes)", payload.Length);
                return (false, "Unknown", ex.Message);
            }
        }

        private static string ReadCString(ReadOnlySpan<byte> span, ref int offset)
        {
            int start = offset;
            while (offset < span.Length && span[offset] != 0)
                offset++;
            var result = offset > start ? Encoding.UTF8.GetString(span.Slice(start, offset - start)) : string.Empty;
            if (offset < span.Length) offset++; // skip null terminator
            return result;
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _logger.LogDebug("MailNetworkClientComponent disposed");
        }
        #endregion
    }
}
