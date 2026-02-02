namespace GameData.Core.Enums;

public enum TransferAbortReason
{
    TRANSFER_ABORT_MAX_PLAYERS = 0x01,     // Transfer Aborted: instance is full
    TRANSFER_ABORT_NOT_FOUND = 0x02,     // Transfer Aborted: instance not found
    TRANSFER_ABORT_TOO_MANY_INSTANCES = 0x03,     // You have entered too many instances recently.
    TRANSFER_ABORT_SILENTLY = 0x04,     // no message shown the same effect give values above 5
    TRANSFER_ABORT_ZONE_IN_COMBAT = 0x05,     // Unable to zone in while an encounter is in progress.
}