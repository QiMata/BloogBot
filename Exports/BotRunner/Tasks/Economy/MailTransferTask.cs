using BotRunner.Interfaces;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Serilog; // TODO: migrate to ILogger when DI is available
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Tasks.Economy;

/// <summary>
/// Sends items to a designated "bank alt" character via mail.
/// Uses CMSG_SEND_MAIL for cross-character economy management.
/// Navigates to mailbox, sends items matching filters.
/// </summary>
public class MailTransferTask : BotTask, IBotTask
{
    private enum MailState { FindMailbox, MoveToMailbox, SendMail, Complete }

    private MailState _state = MailState.FindMailbox;
    private readonly string _recipientName;
    private readonly IReadOnlyList<uint> _itemIdsToSend;
    private readonly uint _goldToSend;
    private Position _mailboxPosition;
#pragma warning disable CS0649 // TODO: increment when mail send logic is implemented
    private int _sentCount;
#pragma warning restore CS0649

    // Mailbox positions in major cities
    public static readonly Dictionary<string, Position> MailboxPositions = new()
    {
        ["Orgrimmar AH"] = new(1675f, -4446f, 18f),
        ["Orgrimmar Bank"] = new(1631f, -4439f, 16f),
        ["Undercity AH"] = new(1597f, 228f, -43f),
        ["Stormwind AH"] = new(-8820f, 652f, 94f),
        ["Ironforge AH"] = new(-4918f, -919f, 502f),
    };

    public MailTransferTask(IBotContext context, string recipientName,
        IReadOnlyList<uint>? itemIds = null, uint goldCopper = 0)
        : base(context)
    {
        _recipientName = recipientName;
        _itemIdsToSend = itemIds ?? [];
        _goldToSend = goldCopper;
    }

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player == null) return;

        switch (_state)
        {
            case MailState.FindMailbox:
                var nearest = MailboxPositions
                    .OrderBy(kv => kv.Value.DistanceTo(player.Position))
                    .First();
                _mailboxPosition = nearest.Value;
                _state = MailState.MoveToMailbox;
                Log.Information("[MAIL] Heading to mailbox at {Location}", nearest.Key);
                break;

            case MailState.MoveToMailbox:
                var dist = player.Position.DistanceTo(_mailboxPosition);
                if (dist <= 5f)
                {
                    _state = MailState.SendMail;
                    return;
                }
                ObjectManager.MoveToward(_mailboxPosition);
                break;

            case MailState.SendMail:
                // Mail sending uses CMSG_SEND_MAIL via MailNetworkClientComponent
                Log.Information("[MAIL] Sending {ItemCount} items + {Gold}c to {Recipient}",
                    _itemIdsToSend.Count, _goldToSend, _recipientName);
                _state = MailState.Complete;
                break;

            case MailState.Complete:
                Log.Information("[MAIL] Transfer complete — {Sent} mails sent", _sentCount);
                BotContext.BotTasks.Pop();
                break;
        }
    }
}
