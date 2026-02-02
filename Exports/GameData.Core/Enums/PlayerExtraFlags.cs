namespace GameData.Core.Enums;

public enum PlayerExtraFlags
{
    // gm abilities
    PLAYER_EXTRA_GM_ON = 0x0001,
    PLAYER_EXTRA_GM_ACCEPT_TICKETS = 0x0002,
    PLAYER_EXTRA_ACCEPT_WHISPERS = 0x0004,
    PLAYER_EXTRA_TAXICHEAT = 0x0008,
    PLAYER_EXTRA_GM_INVISIBLE = 0x0010,
    PLAYER_EXTRA_GM_CHAT = 0x0020,               // Show GM badge in chat messages
    PLAYER_EXTRA_AUCTION_NEUTRAL = 0x0040,
    PLAYER_EXTRA_AUCTION_ENEMY = 0x0080,               // overwrite PLAYER_EXTRA_AUCTION_NEUTRAL

    // other states
    PLAYER_EXTRA_PVP_DEATH = 0x0100                // store PvP death status until corpse creating.
}