using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Implementation of mail network agent that handles mail operations in World of Warcraft.
    /// Manages sending mail, receiving mail, mail attachments, and mail state tracking using the Mangos protocol.
    /// Reactive variant: exposes opcode-backed observables instead of events/subjects.
    /// </summary>
    public class MailNetworkClientComponent : NetworkClientComponent, IMailNetworkClientComponent, IDisposable
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<MailNetworkClientComponent> _logger;
        private readonly object _stateLock = new object();

        private readonly List<MailInfo> _mailbox = [];
        private bool _isMailboxOpen;
        private bool _disposed;

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

        /// <summary>
        /// Initializes a new instance of the MailNetworkClientComponent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public MailNetworkClientComponent(IWorldClient worldClient, ILogger<MailNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Mail list result -> count projection
            _mailListCounts = SafeOpcodeStream(Opcode.SMSG_MAIL_LIST_RESULT)
                .Select(ParseMailListCount)
                .Do(count => _logger.LogInformation("Mail list received with {Count} mails", count))
                .Publish()
                .RefCount();

            // Send mail result -> success/failure projection
            var sendResults = SafeOpcodeStream(Opcode.SMSG_SEND_MAIL_RESULT)
                .Select(ParseSendMailResult)
                .Publish()
                .RefCount();

            _mailSentResults = sendResults
                .Where(r => r.Success)
                .Select(_ => (Recipient: "Unknown", Subject: "Unknown")) // placeholder; server payload parsing TBD
                .Do(_ => _logger.LogInformation("Mail send result: success"))
                .Publish()
                .RefCount();

            _mailErrors = sendResults
                .Where(r => !r.Success)
                .Select(r => (Operation: "SendMail", Error: r.ErrorMessage ?? "Unknown error"))
                .Do(err => _logger.LogWarning("Mail operation failed - {Operation}: {Error}", err.Operation, err.Error))
                .Publish()
                .RefCount();

            // Unknown opcode mapping for the following, expose inert streams
            _moneyTakenResults = Observable.Never<(uint MailId, uint Amount)>();
            _itemTakenResults = Observable.Never<(uint MailId, uint ItemId, uint Quantity)>();
            _mailReadMarks = Observable.Never<uint>();
            _mailDeletes = Observable.Never<uint>();
            _mailReturns = Observable.Never<uint>();

            // Mailbox open/close: no explicit opcodes are consumed here; keep empty observables
            _mailboxWindowOpenings = Observable.Empty<ulong>();
            _mailboxWindowClosings = Observable.Empty<Unit>();
        }

        #region INetworkClientComponent Implementation
        // IsOperationInProgress and LastOperationTime provided by base class
        #endregion

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

        public async Task OpenMailboxAsync(ulong mailboxGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Opening mailbox interaction with: {MailboxGuid:X}", mailboxGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(mailboxGuid).CopyTo(payload, 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GOSSIP_HELLO, payload, cancellationToken);

                lock (_stateLock) _isMailboxOpen = true;
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

        public async Task GetMailListAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Requesting mail list from server");

                // CMSG_GET_MAIL_LIST has no payload data
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GET_MAIL_LIST, [], cancellationToken);

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
            await SendMailInternalAsync(recipient, subject, body, 0, 0, 0, 0, 0, cancellationToken);
        }

        public async Task SendMailWithMoneyAsync(string recipient, string subject, string body, uint money, CancellationToken cancellationToken = default)
        {
            await SendMailInternalAsync(recipient, subject, body, money, 0, 0, 0, 0, cancellationToken);
        }

        public async Task SendMailWithItemAsync(string recipient, string subject, string body, byte bagId, byte slotId, CancellationToken cancellationToken = default)
        {
            await SendMailInternalAsync(recipient, subject, body, 0, 0, bagId, slotId, 0, cancellationToken);
        }

        public async Task SendMailWithMoneyAndItemAsync(string recipient, string subject, string body, uint money, byte bagId, byte slotId, CancellationToken cancellationToken = default)
        {
            await SendMailInternalAsync(recipient, subject, body, money, 0, bagId, slotId, 0, cancellationToken);
        }

        public async Task SendCODMailAsync(string recipient, string subject, string body, uint codAmount, byte bagId, byte slotId, CancellationToken cancellationToken = default)
        {
            await SendMailInternalAsync(recipient, subject, body, 0, codAmount, bagId, slotId, 0, cancellationToken);
        }

        public async Task TakeMoneyFromMailAsync(uint mailId, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Taking money from mail: {MailId}", mailId);

                var payload = new byte[4];
                BitConverter.GetBytes(mailId).CopyTo(payload, 0);

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

        public async Task TakeItemFromMailAsync(uint mailId, ulong itemGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Taking item from mail: {MailId}, item: {ItemGuid:X}", mailId, itemGuid);

                var payload = new byte[12];
                BitConverter.GetBytes(mailId).CopyTo(payload, 0);
                BitConverter.GetBytes(itemGuid).CopyTo(payload, 4);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_MAIL_TAKE_ITEM, payload, cancellationToken);

                _logger.LogInformation("Item take request sent for mail: {MailId}, item: {ItemGuid:X}", mailId, itemGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to take item from mail: {MailId}, item: {ItemGuid:X}", mailId, itemGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task MarkMailAsReadAsync(uint mailId, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Marking mail as read: {MailId}", mailId);

                var payload = new byte[4];
                BitConverter.GetBytes(mailId).CopyTo(payload, 0);

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

        public async Task DeleteMailAsync(uint mailId, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Deleting mail: {MailId}", mailId);

                var payload = new byte[4];
                BitConverter.GetBytes(mailId).CopyTo(payload, 0);

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

        public async Task ReturnMailToSenderAsync(uint mailId, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Returning mail to sender: {MailId}", mailId);

                var payload = new byte[4];
                BitConverter.GetBytes(mailId).CopyTo(payload, 0);

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

        public async Task CreateTextItemFromMailAsync(uint mailId, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Creating text item from mail: {MailId}", mailId);

                var payload = new byte[4];
                BitConverter.GetBytes(mailId).CopyTo(payload, 0);

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

        public async Task QueryNextMailTimeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Querying next mail time");

                // MSG_QUERY_NEXT_MAIL_TIME has no payload data
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

                lock (_stateLock) _isMailboxOpen = false;
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

        public bool IsMailboxOpen(ulong mailboxGuid)
        {
            return _isMailboxOpen;
        }

        public async Task QuickCheckMailAsync(ulong mailboxGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Performing quick mail check with mailbox: {MailboxGuid:X}", mailboxGuid);

                await OpenMailboxAsync(mailboxGuid, cancellationToken);
                await Task.Delay(100, cancellationToken);
                await GetMailListAsync(cancellationToken);
                await Task.Delay(100, cancellationToken);
                await CloseMailboxAsync(cancellationToken);

                _logger.LogInformation("Quick mail check completed with mailbox: {MailboxGuid:X}", mailboxGuid);
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
                _logger.LogDebug("Performing quick collect all mail with mailbox: {MailboxGuid:X}", mailboxGuid);

                await OpenMailboxAsync(mailboxGuid, cancellationToken);
                await Task.Delay(100, cancellationToken);
                await GetMailListAsync(cancellationToken);
                await Task.Delay(100, cancellationToken);
                await CloseMailboxAsync(cancellationToken);

                _logger.LogInformation("Quick collect all mail completed with mailbox: {MailboxGuid:X}", mailboxGuid);
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
                _logger.LogDebug("Performing quick send mail to {Recipient} with mailbox: {MailboxGuid:X}", recipient, mailboxGuid);

                await OpenMailboxAsync(mailboxGuid, cancellationToken);
                await Task.Delay(100, cancellationToken);
                await SendMailAsync(recipient, subject, body, cancellationToken);
                await Task.Delay(100, cancellationToken);
                await CloseMailboxAsync(cancellationToken);

                _logger.LogInformation("Quick send mail completed to {Recipient} with mailbox: {MailboxGuid:X}", recipient, mailboxGuid);
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

        #region Private Helper Methods
        private IObservable<ReadOnlyMemory<byte>> SafeOpcodeStream(Opcode opcode)
            => _worldClient.RegisterOpcodeHandler(opcode) ?? Observable.Empty<ReadOnlyMemory<byte>>();
        #endregion

        /// <summary>
        /// Internal method for sending mail with various combinations of attachments.
        /// </summary>
        private async Task SendMailInternalAsync(string recipient, string subject, string body, uint money, uint codAmount, byte bagId, byte slotId, byte itemCount, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Sending mail to {Recipient}: '{Subject}'", recipient, subject);

                var recipientBytes = Encoding.UTF8.GetBytes(recipient);
                var subjectBytes = Encoding.UTF8.GetBytes(subject);
                var bodyBytes = Encoding.UTF8.GetBytes(body);

                var payloadSize = 8 +
                                  recipientBytes.Length + 1 +
                                  subjectBytes.Length + 1 +
                                  bodyBytes.Length + 1 +
                                  1 +
                                  4 +
                                  4;

                if (bagId != 0 || slotId != 0)
                {
                    payloadSize += 1 + 1;
                }

                var payload = new byte[payloadSize];
                var offset = 0;

                BitConverter.GetBytes(0UL).CopyTo(payload, offset);
                offset += 8;

                recipientBytes.CopyTo(payload, offset);
                offset += recipientBytes.Length;
                payload[offset++] = 0;

                subjectBytes.CopyTo(payload, offset);
                offset += subjectBytes.Length;
                payload[offset++] = 0;

                bodyBytes.CopyTo(payload, offset);
                offset += bodyBytes.Length;
                payload[offset++] = 0;

                payload[offset++] = itemCount;

                if (bagId != 0 || slotId != 0)
                {
                    payload[offset++] = bagId;
                    payload[offset++] = slotId;
                }

                BitConverter.GetBytes(money).CopyTo(payload, offset);
                offset += 4;

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

        #region Minimal parsers
        private uint ParseMailListCount(ReadOnlyMemory<byte> payload)
        {
            try
            {
                var span = payload.Span;
                return span.Length >= 4 ? BitConverter.ToUInt32(span[..4]) : 0u;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse mail list count ({Len} bytes)", payload.Length);
                return 0u;
            }
        }

        private (bool Success, string? ErrorMessage) ParseSendMailResult(ReadOnlyMemory<byte> payload)
        {
            try
            {
                // Heuristic: first 4 bytes result code (0 = ok)
                var span = payload.Span;
                if (span.Length < 4) return (false, "Malformed send mail result");
                var result = BitConverter.ToUInt32(span[..4]);
                return (result == 0, result == 0 ? null : $"Code {result}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse send mail result ({Len} bytes)", payload.Length);
                return (false, ex.Message);
            }
        }
        #endregion

        #region IDisposable Implementation
        public void Dispose()
        {
            if (_disposed) return;

            _logger.LogDebug("Disposing MailNetworkClientComponent");

            _disposed = true;
            _logger.LogDebug("MailNetworkClientComponent disposed");
        }
        #endregion
    }
}