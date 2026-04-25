namespace BotRunner.Tests.LiveValidation;

internal static class OrgrimmarServiceLocations
{
    public const int MapId = 1;

    // Z values keep the bot above the service floor so the post-teleport ground snap
    // lands on the intended indoor level instead of the lower city floor.
    public const float BankX = 1627.32f;
    public const float BankY = -4376.07f;
    public const float BankZ = 17.81f;

    public const float AuctionHouseX = 1687.26f;
    public const float AuctionHouseY = -4464.71f;
    public const float AuctionHouseZ = 26.15f;

    public const float MailboxX = 1615.58f;
    public const float MailboxY = -4391.60f;
    public const float MailboxZ = 16.11f;

    public const float TradeX = 1629.00f;
    public const float TradeY = -4373.00f;
    public const float TradeZ = 34.00f;

    public const float FlightMasterX = 1676.25f;
    public const float FlightMasterY = -4313.45f;
    public const float FlightMasterZ = 67.72f;
}
