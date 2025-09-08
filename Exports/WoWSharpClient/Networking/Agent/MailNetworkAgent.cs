using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using System.Text;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.Agent.I;

namespace WoWSharpClient.Networking.Agent
{
    /// <summary>
    /// Implementation of mail network agent that handles mail system operations in World of Warcraft.
    /// Manages sending mail to other players, retrieving mail from mailboxes, and handling mail items using the Mangos protocol.
    /// </summary>
    public class MailNetworkAgent : IMailNetworkAgent
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<MailNetworkAgent> _logger;

        private bool _isMailboxWindowOpen;
        private ulong? _currentMailboxGuid;

        /// <summary>
        /// Initializes a new instance of the MailNetworkAgent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public MailNetworkAgent(IWorldClient worldClient, ILogger<MailNetworkAgent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public bool IsMailboxWindowOpen => _isMailboxWindowOpen;

        /// <inheritdoc />
        public event Action<ulong>? MailboxWindowOpened;

        /// <inheritdoc />
        public event Action? MailboxWindowClosed;

        /// <inheritdoc />
        public event Action<uint>? MailListReceived;

        /// <inheritdoc />
        public event Action<string, string>? MailSent;

        /// <inheritdoc />
        public event Action<uint, uint>? MoneyTakenFromMail;

        /// <inheritdoc />
        public event Action<uint, uint, uint>? ItemTakenFromMail;

        /// <inheritdoc />
        public event Action<uint>? MailMarkedAsRead;

        /// <inheritdoc />
        public event Action<uint>? MailDeleted;

        /// <inheritdoc />
        public event Action<uint>? MailReturned;

        /// <inheritdoc />
        public event Action<string, string>? MailError;

        /// <inheritdoc />
        public async Task OpenMailboxAsync(ulong mailboxGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Opening mailbox interaction with: {MailboxGuid:X}", mailboxGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(mailboxGuid).CopyTo(payload, 0);

                await _worldClient.SendMovementAsync(Opcode.CMSG_GOSSIP_HELLO, payload, cancellationToken);

                _logger.LogInformation("Mailbox interaction initiated with: {MailboxGuid:X}", mailboxGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open mailbox interaction with: {MailboxGuid:X}", mailboxGuid);
                MailError?.Invoke("OpenMailbox", $"Failed to open mailbox: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task GetMailListAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Requesting mail list from server");

                // CMSG_GET_MAIL_LIST has no payload data
                await _worldClient.SendMovementAsync(Opcode.CMSG_GET_MAIL_LIST, Array.Empty<byte>(), cancellationToken);

                _logger.LogInformation("Mail list request sent to server");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to request mail list");
                MailError?.Invoke("GetMailList", $"Failed to get mail list: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task SendMailAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default)
        {
            await SendMailInternalAsync(recipient, subject, body, 0, 0, 0, 0, 0, cancellationToken);
        }

        /// <inheritdoc />
        public async Task SendMailWithMoneyAsync(string recipient, string subject, string body, uint money, CancellationToken cancellationToken = default)
        {
            await SendMailInternalAsync(recipient, subject, body, money, 0, 0, 0, 0, cancellationToken);
        }

        /// <inheritdoc />
        public async Task SendMailWithItemAsync(string recipient, string subject, string body, byte bagId, byte slotId, CancellationToken cancellationToken = default)
        {
            await SendMailInternalAsync(recipient, subject, body, 0, 0, bagId, slotId, 0, cancellationToken);
        }

        /// <inheritdoc />
        public async Task SendMailWithMoneyAndItemAsync(string recipient, string subject, string body, uint money, byte bagId, byte slotId, CancellationToken cancellationToken = default)
        {
            await SendMailInternalAsync(recipient, subject, body, money, 0, bagId, slotId, 0, cancellationToken);
        }

        /// <inheritdoc />
        public async Task SendCODMailAsync(string recipient, string subject, string body, uint codAmount, byte bagId, byte slotId, CancellationToken cancellationToken = default)
        {
            await SendMailInternalAsync(recipient, subject, body, 0, codAmount, bagId, slotId, 0, cancellationToken);
        }

        /// <inheritdoc />
        public async Task TakeMoneyFromMailAsync(uint mailId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Taking money from mail: {MailId}", mailId);

                var payload = new byte[4];
                BitConverter.GetBytes(mailId).CopyTo(payload, 0);

                await _worldClient.SendMovementAsync(Opcode.CMSG_MAIL_TAKE_MONEY, payload, cancellationToken);

                _logger.LogInformation("Money take request sent for mail: {MailId}", mailId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to take money from mail: {MailId}", mailId);
                MailError?.Invoke("TakeMoney", $"Failed to take money from mail: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task TakeItemFromMailAsync(uint mailId, ulong itemGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Taking item from mail: {MailId}, item: {ItemGuid:X}", mailId, itemGuid);

                var payload = new byte[12];
                BitConverter.GetBytes(mailId).CopyTo(payload, 0);
                BitConverter.GetBytes(itemGuid).CopyTo(payload, 4);

                await _worldClient.SendMovementAsync(Opcode.CMSG_MAIL_TAKE_ITEM, payload, cancellationToken);

                _logger.LogInformation("Item take request sent for mail: {MailId}, item: {ItemGuid:X}", mailId, itemGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to take item from mail: {MailId}, item: {ItemGuid:X}", mailId, itemGuid);
                MailError?.Invoke("TakeItem", $"Failed to take item from mail: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task MarkMailAsReadAsync(uint mailId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Marking mail as read: {MailId}", mailId);

                var payload = new byte[4];
                BitConverter.GetBytes(mailId).CopyTo(payload, 0);

                await _worldClient.SendMovementAsync(Opcode.CMSG_MAIL_MARK_AS_READ, payload, cancellationToken);

                _logger.LogInformation("Mark as read request sent for mail: {MailId}", mailId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to mark mail as read: {MailId}", mailId);
                MailError?.Invoke("MarkAsRead", $"Failed to mark mail as read: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task DeleteMailAsync(uint mailId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Deleting mail: {MailId}", mailId);

                var payload = new byte[4];
                BitConverter.GetBytes(mailId).CopyTo(payload, 0);

                await _worldClient.SendMovementAsync(Opcode.CMSG_MAIL_DELETE, payload, cancellationToken);

                _logger.LogInformation("Delete request sent for mail: {MailId}", mailId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete mail: {MailId}", mailId);
                MailError?.Invoke("DeleteMail", $"Failed to delete mail: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task ReturnMailToSenderAsync(uint mailId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Returning mail to sender: {MailId}", mailId);

                var payload = new byte[4];
                BitConverter.GetBytes(mailId).CopyTo(payload, 0);

                await _worldClient.SendMovementAsync(Opcode.CMSG_MAIL_RETURN_TO_SENDER, payload, cancellationToken);

                _logger.LogInformation("Return to sender request sent for mail: {MailId}", mailId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to return mail to sender: {MailId}", mailId);
                MailError?.Invoke("ReturnToSender", $"Failed to return mail to sender: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task CreateTextItemFromMailAsync(uint mailId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Creating text item from mail: {MailId}", mailId);

                var payload = new byte[4];
                BitConverter.GetBytes(mailId).CopyTo(payload, 0);

                await _worldClient.SendMovementAsync(Opcode.CMSG_MAIL_CREATE_TEXT_ITEM, payload, cancellationToken);

                _logger.LogInformation("Create text item request sent for mail: {MailId}", mailId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create text item from mail: {MailId}", mailId);
                MailError?.Invoke("CreateTextItem", $"Failed to create text item: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task QueryNextMailTimeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Querying next mail time");

                // MSG_QUERY_NEXT_MAIL_TIME has no payload data
                await _worldClient.SendMovementAsync(Opcode.MSG_QUERY_NEXT_MAIL_TIME, Array.Empty<byte>(), cancellationToken);

                _logger.LogInformation("Next mail time query sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query next mail time");
                MailError?.Invoke("QueryNextMailTime", $"Failed to query next mail time: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task CloseMailboxAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Closing mailbox window");

                // Mailbox windows typically close automatically when moving away
                // But we can update our internal state
                _isMailboxWindowOpen = false;
                _currentMailboxGuid = null;
                MailboxWindowClosed?.Invoke();

                _logger.LogInformation("Mailbox window closed");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to close mailbox window");
                MailError?.Invoke("CloseMailbox", $"Failed to close mailbox: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public bool IsMailboxOpen(ulong mailboxGuid)
        {
            return _isMailboxWindowOpen && _currentMailboxGuid == mailboxGuid;
        }

        /// <inheritdoc />
        public async Task QuickCheckMailAsync(ulong mailboxGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Performing quick mail check with mailbox: {MailboxGuid:X}", mailboxGuid);

                await OpenMailboxAsync(mailboxGuid, cancellationToken);
                
                // Small delay to allow mailbox window to open
                await Task.Delay(100, cancellationToken);
                
                await GetMailListAsync(cancellationToken);
                
                // Small delay to allow mail list to load
                await Task.Delay(100, cancellationToken);
                
                await CloseMailboxAsync(cancellationToken);

                _logger.LogInformation("Quick mail check completed with mailbox: {MailboxGuid:X}", mailboxGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Quick mail check failed with mailbox: {MailboxGuid:X}", mailboxGuid);
                MailError?.Invoke("QuickCheck", $"Quick mail check failed: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task QuickCollectAllMailAsync(ulong mailboxGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Performing quick collect all mail with mailbox: {MailboxGuid:X}", mailboxGuid);

                await OpenMailboxAsync(mailboxGuid, cancellationToken);
                
                // Small delay to allow mailbox window to open
                await Task.Delay(100, cancellationToken);
                
                await GetMailListAsync(cancellationToken);
                
                // Small delay to allow mail list to load
                await Task.Delay(100, cancellationToken);
                
                // Note: In a real implementation, you would iterate through the mail list
                // and take money/items from each mail. This would require parsing the
                // SMSG_MAIL_LIST_RESULT response to get the list of mails.
                // For now, this is a placeholder for the quick collection logic.
                
                await CloseMailboxAsync(cancellationToken);

                _logger.LogInformation("Quick collect all mail completed with mailbox: {MailboxGuid:X}", mailboxGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Quick collect all mail failed with mailbox: {MailboxGuid:X}", mailboxGuid);
                MailError?.Invoke("QuickCollectAll", $"Quick collect all mail failed: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task QuickSendMailAsync(ulong mailboxGuid, string recipient, string subject, string body, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Performing quick send mail to {Recipient} with mailbox: {MailboxGuid:X}", recipient, mailboxGuid);

                await OpenMailboxAsync(mailboxGuid, cancellationToken);
                
                // Small delay to allow mailbox window to open
                await Task.Delay(100, cancellationToken);
                
                await SendMailAsync(recipient, subject, body, cancellationToken);
                
                // Small delay to allow mail to send
                await Task.Delay(100, cancellationToken);
                
                await CloseMailboxAsync(cancellationToken);

                _logger.LogInformation("Quick send mail completed to {Recipient} with mailbox: {MailboxGuid:X}", recipient, mailboxGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Quick send mail failed to {Recipient} with mailbox: {MailboxGuid:X}", recipient, mailboxGuid);
                MailError?.Invoke("QuickSend", $"Quick send mail failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Internal method for sending mail with various combinations of attachments.
        /// </summary>
        /// <param name="recipient">The recipient name.</param>
        /// <param name="subject">The mail subject.</param>
        /// <param name="body">The mail body.</param>
        /// <param name="money">Money to attach (0 for none).</param>
        /// <param name="codAmount">COD amount (0 for none).</param>
        /// <param name="bagId">Item bag ID (0 for none).</param>
        /// <param name="slotId">Item slot ID (0 for none).</param>
        /// <param name="itemCount">Item count (0 for none).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task SendMailInternalAsync(string recipient, string subject, string body, uint money, uint codAmount, byte bagId, byte slotId, byte itemCount, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Sending mail to {Recipient}: '{Subject}'", recipient, subject);

                // Convert strings to UTF-8 bytes
                var recipientBytes = Encoding.UTF8.GetBytes(recipient);
                var subjectBytes = Encoding.UTF8.GetBytes(subject);
                var bodyBytes = Encoding.UTF8.GetBytes(body);

                // Calculate total payload size
                var payloadSize = 8 + // receiver GUID (usually 0 for name lookup)
                                  recipientBytes.Length + 1 + // recipient name + null terminator
                                  subjectBytes.Length + 1 + // subject + null terminator
                                  bodyBytes.Length + 1 + // body + null terminator
                                  1 + // item count
                                  4 + // money
                                  4; // COD amount

                // Add item data size if an item is attached
                if (bagId != 0 || slotId != 0)
                {
                    payloadSize += 1 + 1; // bag ID + slot ID
                }

                var payload = new byte[payloadSize];
                var offset = 0;

                // Receiver GUID (8 bytes, usually 0 for name lookup)
                BitConverter.GetBytes(0UL).CopyTo(payload, offset);
                offset += 8;

                // Recipient name
                recipientBytes.CopyTo(payload, offset);
                offset += recipientBytes.Length;
                payload[offset++] = 0; // null terminator

                // Subject
                subjectBytes.CopyTo(payload, offset);
                offset += subjectBytes.Length;
                payload[offset++] = 0; // null terminator

                // Body
                bodyBytes.CopyTo(payload, offset);
                offset += bodyBytes.Length;
                payload[offset++] = 0; // null terminator

                // Item count
                payload[offset++] = itemCount;

                // If we have an item, add its data
                if (bagId != 0 || slotId != 0)
                {
                    payload[offset++] = bagId;
                    payload[offset++] = slotId;
                }

                // Money
                BitConverter.GetBytes(money).CopyTo(payload, offset);
                offset += 4;

                // COD amount
                BitConverter.GetBytes(codAmount).CopyTo(payload, offset);

                await _worldClient.SendMovementAsync(Opcode.CMSG_SEND_MAIL, payload, cancellationToken);

                _logger.LogInformation("Mail sent to {Recipient}: '{Subject}' (Money: {Money}, COD: {COD})", recipient, subject, money, codAmount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send mail to {Recipient}: '{Subject}'", recipient, subject);
                MailError?.Invoke("SendMail", $"Failed to send mail: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Handles server responses for mailbox window opening.
        /// This method should be called when mailbox interaction is successful.
        /// </summary>
        /// <param name="mailboxGuid">The GUID of the mailbox.</param>
        public void HandleMailboxWindowOpened(ulong mailboxGuid)
        {
            _isMailboxWindowOpen = true;
            _currentMailboxGuid = mailboxGuid;
            MailboxWindowOpened?.Invoke(mailboxGuid);
            _logger.LogDebug("Mailbox window opened for: {MailboxGuid:X}", mailboxGuid);
        }

        /// <summary>
        /// Handles server responses for mail list.
        /// This method should be called when SMSG_MAIL_LIST_RESULT is received.
        /// </summary>
        /// <param name="mailCount">The number of mails in the mailbox.</param>
        public void HandleMailListReceived(uint mailCount)
        {
            MailListReceived?.Invoke(mailCount);
            _logger.LogDebug("Mail list received with {MailCount} mails", mailCount);
        }

        /// <summary>
        /// Handles server responses for successful mail sending.
        /// This method should be called when SMSG_SEND_MAIL_RESULT indicates success.
        /// </summary>
        /// <param name="recipient">The recipient's name.</param>
        /// <param name="subject">The mail subject.</param>
        public void HandleMailSent(string recipient, string subject)
        {
            MailSent?.Invoke(recipient, subject);
            _logger.LogDebug("Mail sent successfully to {Recipient}: '{Subject}'", recipient, subject);
        }

        /// <summary>
        /// Handles server responses for money taken from mail.
        /// This method should be called when money is successfully taken from mail.
        /// </summary>
        /// <param name="mailId">The mail ID.</param>
        /// <param name="amount">The amount taken in copper.</param>
        public void HandleMoneyTakenFromMail(uint mailId, uint amount)
        {
            MoneyTakenFromMail?.Invoke(mailId, amount);
            _logger.LogDebug("Money taken from mail {MailId}: {Amount} copper", mailId, amount);
        }

        /// <summary>
        /// Handles server responses for items taken from mail.
        /// This method should be called when an item is successfully taken from mail.
        /// </summary>
        /// <param name="mailId">The mail ID.</param>
        /// <param name="itemId">The item ID taken.</param>
        /// <param name="quantity">The quantity taken.</param>
        public void HandleItemTakenFromMail(uint mailId, uint itemId, uint quantity)
        {
            ItemTakenFromMail?.Invoke(mailId, itemId, quantity);
            _logger.LogDebug("Item taken from mail {MailId}: {Quantity}x item {ItemId}", mailId, quantity, itemId);
        }

        /// <summary>
        /// Handles server responses for mail marked as read.
        /// This method should be called when mail is successfully marked as read.
        /// </summary>
        /// <param name="mailId">The mail ID.</param>
        public void HandleMailMarkedAsRead(uint mailId)
        {
            MailMarkedAsRead?.Invoke(mailId);
            _logger.LogDebug("Mail marked as read: {MailId}", mailId);
        }

        /// <summary>
        /// Handles server responses for mail deletion.
        /// This method should be called when mail is successfully deleted.
        /// </summary>
        /// <param name="mailId">The mail ID.</param>
        public void HandleMailDeleted(uint mailId)
        {
            MailDeleted?.Invoke(mailId);
            _logger.LogDebug("Mail deleted: {MailId}", mailId);
        }

        /// <summary>
        /// Handles server responses for mail returned to sender.
        /// This method should be called when mail is successfully returned.
        /// </summary>
        /// <param name="mailId">The mail ID.</param>
        public void HandleMailReturned(uint mailId)
        {
            MailReturned?.Invoke(mailId);
            _logger.LogDebug("Mail returned to sender: {MailId}", mailId);
        }

        /// <summary>
        /// Handles server responses for mail operation failures.
        /// This method should be called when SMSG_SEND_MAIL_RESULT or other mail responses indicate failure.
        /// </summary>
        /// <param name="operation">The failed operation.</param>
        /// <param name="errorMessage">The error message.</param>
        public void HandleMailError(string operation, string errorMessage)
        {
            MailError?.Invoke(operation, errorMessage);
            _logger.LogWarning("Mail operation failed - {Operation}: {Error}", operation, errorMessage);
        }
    }
}