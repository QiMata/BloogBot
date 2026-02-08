namespace GameData.Core.Enums;

/// <summary>
/// WoW client login state strings read from memory at offset 0xB41478.
/// These correspond to the ASCII strings stored by the client.
/// </summary>
public enum LoginStates
{
    /// <summary>At the login screen (username/password entry)</summary>
    login,

    /// <summary>At the character selection screen</summary>
    charselect,

    /// <summary>Connecting to server (realm list or world server)</summary>
    connecting,

    /// <summary>Creating a new character</summary>
    charcreate,

    /// <summary>Unknown state (fallback)</summary>
    unknown
}