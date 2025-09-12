namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Interface for handling mail system operations in World of Warcraft.
    /// Manages sending mail to other players, retrieving mail from mailboxes, and handling mail items.
    /// </summary>
    public interface IMailNetworkAgent
    {
        /// <summary>
        /// Gets a value indicating whether a mailbox window is currently open.
        /// </summary>
        bool IsMailboxWindowOpen { get; }

        /// <summary>
        /// Event fired when a mailbox window is opened.
        /// </summary>
        event Action<ulong>? MailboxWindowOpened;

        /// <summary>
        /// Event fired when a mailbox window is closed.
        /// </summary>
        event Action? MailboxWindowClosed;

        /// <summary>
        /// Event fired when the mail list is received from the server.
        /// </summary>
        /// <param name="mailCount">The number of mails in the mailbox.</param>
        event Action<uint>? MailListReceived;

        /// <summary>
        /// Event fired when mail is successfully sent.
        /// </summary>
        /// <param name="recipient">The recipient's name.</param>
        /// <param name="subject">The mail subject.</param>
        event Action<string, string>? MailSent;

        /// <summary>
        /// Event fired when money is taken from mail.
        /// </summary>
        /// <param name="mailId">The mail ID.</param>
        /// <param name="amount">The amount of money taken in copper.</param>
        event Action<uint, uint>? MoneyTakenFromMail;

        /// <summary>
        /// Event fired when an item is taken from mail.
        /// </summary>
        /// <param name="mailId">The mail ID.</param>
        /// <param name="itemId">The item ID taken.</param>
        /// <param name="quantity">The quantity taken.</param>
        event Action<uint, uint, uint>? ItemTakenFromMail;

        /// <summary>
        /// Event fired when mail is marked as read.
        /// </summary>
        /// <param name="mailId">The mail ID.</param>
        event Action<uint>? MailMarkedAsRead;

        /// <summary>
        /// Event fired when mail is deleted.
        /// </summary>
        /// <param name="mailId">The mail ID.</param>
        event Action<uint>? MailDeleted;

        /// <summary>
        /// Event fired when mail is returned to sender.
        /// </summary>
        /// <param name="mailId">The mail ID.</param>
        event Action<uint>? MailReturned;

        /// <summary>
        /// Event fired when a mail operation fails.
        /// </summary>
        /// <param name="operation">The failed operation.</param>
        /// <param name="error">The error message.</param>
        event Action<string, string>? MailError;

        /// <summary>
        /// Opens a mailbox by interacting with a mailbox game object.
        /// Sends CMSG_GOSSIP_HELLO to initiate mailbox interaction.
        /// </summary>
        /// <param name="mailboxGuid">The GUID of the mailbox game object.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task OpenMailboxAsync(ulong mailboxGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Requests the mail list from the server.
        /// Sends CMSG_GET_MAIL_LIST to retrieve all mails in the mailbox.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task GetMailListAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends mail to another player with text content only.
        /// Sends CMSG_SEND_MAIL with the specified recipient, subject, and body.
        /// </summary>
        /// <param name="recipient">The name of the recipient player.</param>
        /// <param name="subject">The mail subject.</param>
        /// <param name="body">The mail body text.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SendMailAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends mail to another player with money attached.
        /// Sends CMSG_SEND_MAIL with the specified recipient, subject, body, and money amount.
        /// </summary>
        /// <param name="recipient">The name of the recipient player.</param>
        /// <param name="subject">The mail subject.</param>
        /// <param name="body">The mail body text.</param>
        /// <param name="money">The amount of money to send in copper.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SendMailWithMoneyAsync(string recipient, string subject, string body, uint money, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends mail to another player with an item attached.
        /// Sends CMSG_SEND_MAIL with the specified recipient, subject, body, and item.
        /// </summary>
        /// <param name="recipient">The name of the recipient player.</param>
        /// <param name="subject">The mail subject.</param>
        /// <param name="body">The mail body text.</param>
        /// <param name="bagId">The bag ID where the item is located.</param>
        /// <param name="slotId">The slot ID where the item is located.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SendMailWithItemAsync(string recipient, string subject, string body, byte bagId, byte slotId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends mail to another player with both money and an item attached.
        /// Sends CMSG_SEND_MAIL with the specified recipient, subject, body, money, and item.
        /// </summary>
        /// <param name="recipient">The name of the recipient player.</param>
        /// <param name="subject">The mail subject.</param>
        /// <param name="body">The mail body text.</param>
        /// <param name="money">The amount of money to send in copper.</param>
        /// <param name="bagId">The bag ID where the item is located.</param>
        /// <param name="slotId">The slot ID where the item is located.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SendMailWithMoneyAndItemAsync(string recipient, string subject, string body, uint money, byte bagId, byte slotId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a Cash-on-Delivery (COD) mail to another player.
        /// The recipient must pay the specified amount to receive the attached item.
        /// </summary>
        /// <param name="recipient">The name of the recipient player.</param>
        /// <param name="subject">The mail subject.</param>
        /// <param name="body">The mail body text.</param>
        /// <param name="codAmount">The COD amount in copper that recipient must pay.</param>
        /// <param name="bagId">The bag ID where the item is located.</param>
        /// <param name="slotId">The slot ID where the item is located.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SendCODMailAsync(string recipient, string subject, string body, uint codAmount, byte bagId, byte slotId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Takes money from a specific mail.
        /// Sends CMSG_MAIL_TAKE_MONEY to retrieve money from the mail.
        /// </summary>
        /// <param name="mailId">The ID of the mail to take money from.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task TakeMoneyFromMailAsync(uint mailId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Takes an item from a specific mail.
        /// Sends CMSG_MAIL_TAKE_ITEM to retrieve an item from the mail.
        /// </summary>
        /// <param name="mailId">The ID of the mail to take the item from.</param>
        /// <param name="itemGuid">The GUID of the item to take.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task TakeItemFromMailAsync(uint mailId, ulong itemGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks a mail as read.
        /// Sends CMSG_MAIL_MARK_AS_READ to mark the mail as read.
        /// </summary>
        /// <param name="mailId">The ID of the mail to mark as read.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task MarkMailAsReadAsync(uint mailId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a mail from the mailbox.
        /// Sends CMSG_MAIL_DELETE to delete the mail.
        /// </summary>
        /// <param name="mailId">The ID of the mail to delete.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DeleteMailAsync(uint mailId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns a mail to its sender.
        /// Sends CMSG_MAIL_RETURN_TO_SENDER to return the mail.
        /// </summary>
        /// <param name="mailId">The ID of the mail to return.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ReturnMailToSenderAsync(uint mailId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a text item from mail content.
        /// Sends CMSG_MAIL_CREATE_TEXT_ITEM to create a readable item from mail text.
        /// </summary>
        /// <param name="mailId">The ID of the mail to create text item from.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CreateTextItemFromMailAsync(uint mailId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries the next mail time from the server.
        /// Sends MSG_QUERY_NEXT_MAIL_TIME to check when new mail will arrive.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task QueryNextMailTimeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Closes the mailbox window.
        /// This typically happens automatically when moving away from the mailbox.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CloseMailboxAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if the specified mailbox GUID has an open mailbox window.
        /// </summary>
        /// <param name="mailboxGuid">The GUID to check.</param>
        /// <returns>True if the mailbox window is open for the specified GUID, false otherwise.</returns>
        bool IsMailboxOpen(ulong mailboxGuid);

        /// <summary>
        /// Performs a complete mailbox interaction: open, get mail list, close.
        /// This is a convenience method for checking mail.
        /// </summary>
        /// <param name="mailboxGuid">The GUID of the mailbox game object.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task QuickCheckMailAsync(ulong mailboxGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a complete mailbox interaction: open, take all money and items, close.
        /// This is a convenience method for collecting all mail contents.
        /// </summary>
        /// <param name="mailboxGuid">The GUID of the mailbox game object.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task QuickCollectAllMailAsync(ulong mailboxGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a complete mail sending operation: open mailbox, send mail, close.
        /// This is a convenience method for quick mail sending.
        /// </summary>
        /// <param name="mailboxGuid">The GUID of the mailbox game object.</param>
        /// <param name="recipient">The name of the recipient player.</param>
        /// <param name="subject">The mail subject.</param>
        /// <param name="body">The mail body text.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task QuickSendMailAsync(ulong mailboxGuid, string recipient, string subject, string body, CancellationToken cancellationToken = default);
    }
}