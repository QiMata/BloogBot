namespace GameData.Core.Enums;

public enum PlayerFlags
{
    PLAYER_FLAGS_NONE = 0x00000000,
    PLAYER_FLAGS_GROUP_LEADER = 0x00000001,
    PLAYER_FLAGS_AFK = 0x00000002,
    PLAYER_FLAGS_DND = 0x00000004,
    PLAYER_FLAGS_GM = 0x00000008,
    PLAYER_FLAGS_GHOST = 0x00000010,
    PLAYER_FLAGS_RESTING = 0x00000020,
    PLAYER_FLAGS_UNK7 = 0x00000040,       // admin?
    PLAYER_FLAGS_FFA_PVP = 0x00000080,
    PLAYER_FLAGS_CONTESTED_PVP = 0x00000100,       // Player has been involved in a PvP combat and will be attacked by contested guards
    PLAYER_FLAGS_IN_PVP = 0x00000200,
    PLAYER_FLAGS_HIDE_HELM = 0x00000400,
    PLAYER_FLAGS_HIDE_CLOAK = 0x00000800,
    PLAYER_FLAGS_PARTIAL_PLAY_TIME = 0x00001000,       // played long time
    PLAYER_FLAGS_NO_PLAY_TIME = 0x00002000,       // played too long time
    PLAYER_FLAGS_UNK15 = 0x00004000,
    PLAYER_FLAGS_UNK16 = 0x00008000,       // strange visual effect (2.0.1), looks like PLAYER_FLAGS_GHOST flag
    PLAYER_FLAGS_SANCTUARY = 0x00010000,       // player entered sanctuary
    PLAYER_FLAGS_TAXI_BENCHMARK = 0x00020000,       // taxi benchmark mode (on/off) (2.0.1)
    PLAYER_FLAGS_PVP_TIMER = 0x00040000,       // 3.0.2, pvp timer active (after you disable pvp manually)
    PLAYER_FLAGS_XP_USER_DISABLED = 0x02000000,
}