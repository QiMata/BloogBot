namespace GameData.Core.Enums;

/// <summary>
/// Comprehensive WoW client screen states determined by combining multiple memory values.
/// This provides a higher-level view than just LoginStates.
/// </summary>
public enum WoWScreenState
{
    /// <summary>
    /// Unknown state - unable to determine screen.
    /// Memory conditions: Any unexpected combination
    /// </summary>
    Unknown,

    /// <summary>
    /// Game not running or process not accessible.
    /// Memory conditions: Can't read memory or process handle invalid
    /// </summary>
    ProcessNotAvailable,

    /// <summary>
    /// At the login screen - entering username/password.
    /// Memory conditions:
    /// - LoginState (0xB41478) = "login"
    /// - ClientConnection (0xB41DA0) = NULL or non-zero
    /// - PlayerGuid = 0
    /// </summary>
    LoginScreen,

    /// <summary>
    /// Connecting to realm/authentication server.
    /// Memory conditions:
    /// - LoginState (0xB41478) = "connecting"
    /// - ClientConnection (0xB41DA0) = non-zero (or transitioning)
    /// - PlayerGuid = 0
    /// </summary>
    Connecting,

    /// <summary>
    /// At the realm selection screen.
    /// Memory conditions:
    /// - LoginState (0xB41478) = "login" (may vary)
    /// - Specific frame visible (needs frame detection)
    /// Note: May be difficult to distinguish from login screen via memory alone
    /// </summary>
    RealmSelect,

    /// <summary>
    /// At the character selection screen.
    /// Memory conditions:
    /// - LoginState (0xB41478) = "charselect"
    /// - ClientConnection (0xB41DA0) = non-zero
    /// - PlayerGuid = 0 (not logged in yet)
    /// - CharacterCount (0xB42140) >= 0
    /// </summary>
    CharacterSelect,

    /// <summary>
    /// Creating a new character.
    /// Memory conditions:
    /// - LoginState (0xB41478) = "charcreate" or "charselect"
    /// - Specific frame visible
    /// </summary>
    CharacterCreate,

    /// <summary>
    /// Loading into the game world.
    /// Memory conditions:
    /// - LoginState (0xB41478) = "charselect" (briefly)
    /// - ContinentId (0x86F694) = 0xFF
    /// - PlayerGuid may be non-zero
    /// </summary>
    LoadingWorld,

    /// <summary>
    /// Fully logged in and in the game world.
    /// Memory conditions:
    /// - LoginState (0xB41478) = "charselect"
    /// - ContinentId (0x86F694) != 0xFF (valid map ID)
    /// - ClientConnection (0xB41DA0) != NULL
    /// - PlayerGuid (ManagerBase + 0xC0) != 0
    /// - Player object exists in object manager
    /// </summary>
    InWorld,

    /// <summary>
    /// Disconnected from server while was in world.
    /// Memory conditions:
    /// - ClientConnection (0xB41DA0) = NULL
    /// - Previously had valid PlayerGuid
    /// </summary>
    Disconnected,

    /// <summary>
    /// At a loading screen between zones (not initial load).
    /// Memory conditions:
    /// - Already was InWorld
    /// - ContinentId (0x86F694) = 0xFF temporarily
    /// </summary>
    ZoneLoading,

    /// <summary>
    /// Dead and at the release spirit dialog.
    /// Memory conditions:
    /// - InWorld conditions met
    /// - IsGhost (0x835A48) = specific value
    /// - Player health = 0
    /// </summary>
    DeadRelease,

    /// <summary>
    /// Ghost form running back to corpse.
    /// Memory conditions:
    /// - InWorld conditions met
    /// - IsGhost (0x835A48) = 1
    /// - Corpse position set
    /// </summary>
    GhostMode
}
