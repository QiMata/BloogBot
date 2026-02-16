using GameData.Core.Enums;
using System;
using System.Collections.Generic;

namespace WoWSharpClient.Networking.ClientComponents.Models
{
    /// <summary>
    /// Represents mail information.
    /// </summary>
    public class MailInfo
    {
        /// <summary>
        /// Gets or sets the mail ID.
        /// </summary>
        public uint MailId { get; set; }

        /// <summary>
        /// Gets or sets the mail type.
        /// </summary>
        public MailType MailType { get; set; }

        /// <summary>
        /// Gets or sets the sender name.
        /// </summary>
        public string SenderName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the mail subject.
        /// </summary>
        public string Subject { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the mail body text.
        /// </summary>
        public string Body { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets when the mail was sent.
        /// </summary>
        public DateTime SentTime { get; set; }

        /// <summary>
        /// Gets or sets when the mail expires.
        /// </summary>
        public DateTime ExpiryTime { get; set; }

        /// <summary>
        /// Gets or sets when the mail was delivered.
        /// </summary>
        public DateTime DeliveryTime { get; set; }

        /// <summary>
        /// Gets or sets the attached money amount in copper.
        /// </summary>
        public uint Money { get; set; }

        /// <summary>
        /// Gets or sets the COD (Cash on Delivery) amount in copper.
        /// </summary>
        public uint COD { get; set; }

        /// <summary>
        /// Gets or sets the mail flags.
        /// </summary>
        public MailFlags Flags { get; set; }

        /// <summary>
        /// Gets or sets the attached items.
        /// </summary>
        public MailAttachment[] Attachments { get; set; } = Array.Empty<MailAttachment>();

        /// <summary>
        /// Gets whether the mail has been read.
        /// </summary>
        public bool IsRead => Flags.HasFlag(MailFlags.Read);

        /// <summary>
        /// Gets whether the mail has attachments.
        /// </summary>
        public bool HasAttachments => Attachments.Length > 0 || Money > 0;

        /// <summary>
        /// Gets whether the mail is COD (Cash on Delivery).
        /// </summary>
        public bool IsCOD => COD > 0;

        /// <summary>
        /// Gets whether the mail has expired.
        /// </summary>
        public bool IsExpired => DateTime.UtcNow > ExpiryTime;
    }

    /// <summary>
    /// Represents a mail attachment.
    /// </summary>
    public class MailAttachment
    {
        /// <summary>
        /// Gets or sets the attachment slot.
        /// </summary>
        public byte Slot { get; set; }

        /// <summary>
        /// Gets or sets the item ID.
        /// </summary>
        public uint ItemId { get; set; }

        /// <summary>
        /// Gets or sets the item GUID.
        /// </summary>
        public ulong ItemGuid { get; set; }

        /// <summary>
        /// Gets or sets the item name.
        /// </summary>
        public string ItemName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the item count.
        /// </summary>
        public uint Count { get; set; }

        /// <summary>
        /// Gets or sets the item charges.
        /// </summary>
        public uint Charges { get; set; }

        /// <summary>
        /// Gets or sets the item quality.
        /// </summary>
        public ItemQuality Quality { get; set; }

        /// <summary>
        /// Gets or sets whether the item is bound.
        /// </summary>
        public bool IsBound { get; set; }

        /// <summary>
        /// Gets or sets the item enchantments.
        /// </summary>
        public uint[] Enchantments { get; set; } = Array.Empty<uint>();
    }

    /// <summary>
    /// Represents mail flags.
    /// </summary>
    [Flags]
    public enum MailFlags : byte
    {
        None = 0,
        Read = 1,
        Returned = 2,
        Copied = 4,
        COD = 8,
        HasBody = 16
    }

    /// <summary>
    /// Represents mail operation results.
    /// </summary>
    public enum MailResult
    {
        Success = 0,
        MailAttachmentInvalid = 1,
        RecipientNotFound = 2,
        NotEnoughMoney = 3,
        InternalError = 4,
        DisabledForTrialAcc = 5,
        RecipientCapReached = 6,
        CantSendToSelf = 7,
        NotEnoughMoney2 = 8,
        NotFadedItem = 9,
        ReputationRequirement = 10,
        BagFull = 11,
        CantSendWrappedCOD = 12,
        CantSendBound = 13,
        CantSendPermanent = 14,
        ItemBagFull = 15,
        TargetNotFound = 16,
        CantCreateMail = 17
    }

    /// <summary>
    /// Represents a mail draft for sending.
    /// </summary>
    public class MailDraft
    {
        /// <summary>
        /// Gets or sets the recipient name.
        /// </summary>
        public string RecipientName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the mail subject.
        /// </summary>
        public string Subject { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the mail body.
        /// </summary>
        public string Body { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the attached money in copper.
        /// </summary>
        public uint Money { get; set; }

        /// <summary>
        /// Gets or sets the COD amount in copper.
        /// </summary>
        public uint COD { get; set; }

        /// <summary>
        /// Gets or sets the items to attach.
        /// </summary>
        public List<MailAttachmentDraft> Attachments { get; set; } = [];

        /// <summary>
        /// Gets whether this is a COD mail.
        /// </summary>
        public bool IsCOD => COD > 0;

        /// <summary>
        /// Gets whether this mail has attachments.
        /// </summary>
        public bool HasAttachments => Attachments.Count > 0 || Money > 0;
    }

    /// <summary>
    /// Represents an item to attach to mail.
    /// </summary>
    public class MailAttachmentDraft
    {
        /// <summary>
        /// Gets or sets the bag where the item is located.
        /// </summary>
        public byte Bag { get; set; }

        /// <summary>
        /// Gets or sets the slot where the item is located.
        /// </summary>
        public byte Slot { get; set; }

        /// <summary>
        /// Gets or sets the number of items to attach.
        /// </summary>
        public uint Count { get; set; } = 1;

        /// <summary>
        /// Gets or sets the item GUID.
        /// </summary>
        public ulong ItemGuid { get; set; }

        /// <summary>
        /// Gets or sets the item ID.
        /// </summary>
        public uint ItemId { get; set; }
    }

    /// <summary>
    /// Represents mail send operation data.
    /// </summary>
    public class MailSendData
    {
        /// <summary>
        /// Gets or sets the recipient name.
        /// </summary>
        public string RecipientName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the mail subject.
        /// </summary>
        public string Subject { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total cost of sending.
        /// </summary>
        public uint TotalCost { get; set; }

        /// <summary>
        /// Gets or sets the send result.
        /// </summary>
        public MailResult Result { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when sent.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Represents mail received data.
    /// </summary>
    public class MailReceivedData
    {
        /// <summary>
        /// Gets or sets the mail ID.
        /// </summary>
        public uint MailId { get; set; }

        /// <summary>
        /// Gets or sets the sender name.
        /// </summary>
        public string SenderName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the mail subject.
        /// </summary>
        public string Subject { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the mail has attachments.
        /// </summary>
        public bool HasAttachments { get; set; }

        /// <summary>
        /// Gets or sets whether the mail is COD.
        /// </summary>
        public bool IsCOD { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when received.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Represents mail deletion data.
    /// </summary>
    public class MailDeletedData
    {
        /// <summary>
        /// Gets or sets the mail ID that was deleted.
        /// </summary>
        public uint MailId { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when deleted.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}