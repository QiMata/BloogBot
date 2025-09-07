namespace GameData.Core.Enums;

public enum PlayerFieldByteFlags
{
    PLAYER_FIELD_BYTE_TRACK_STEALTHED = 0x02,
    PLAYER_FIELD_BYTE_RELEASE_TIMER = 0x08,             // Display time till auto release spirit
    PLAYER_FIELD_BYTE_NO_RELEASE_WINDOW = 0x10              // Display no "release spirit" window at all
}