# Handoff Prompt: Fix Game State Detection and Snapshot Communication

> **Quick Start Priority Order:**
> 1. **Step 3** (Lua Fallback for Name) - Fixes immediate snapshot blocking issue
> 2. **Step 1** (Reset Player on Disconnect) - Prevents stale state
> 3. **Step 2** (Logout Event) - Handles graceful logout
> 4. **Steps 4-5** (IsInWorld) - Clean architecture improvement

## Problem Summary
The ForegroundBotRunner successfully injects into WoW and communicates with StateManager for account assignment, but:
1. **Game state detection is broken** - `HasEnteredWorld` stays `true` even after logout/disconnect
2. **Player.Name returns empty** - Name cache timing issue on first login
3. **Snapshot never sent** - Because CharacterName is empty, the guard condition blocks sending

## Evidence from Breadcrumbs (2/4/2026 test run)

| Time | Event | Issue |
|------|-------|-------|
| 9:23:14 PM | Account assigned: ORWR1 | ✓ Working |
| 9:23:19 PM | Player set! Name= | ✗ **Name is EMPTY** |
| 9:23:19 PM | HasEnteredWorld=True | ✓ Set correctly |
| 9:23:19 PM | UpdateProbe #1: Player.Name= | ✗ **Still empty** |
| 9:25:39 PM | IsLoggedIn=False, HasEnteredWorld=True | ✗ **Inconsistent state** |

## Root Causes

### 1. HasEnteredWorld Never Resets
In `ObjectManager.cs`, `HasEnteredWorld` is set to `true` when player is found, but **never reset** when:
- Character disconnects
- Character logs out
- Returns to character select screen

**Location**: [ObjectManager.cs:873-878](Services/ForegroundBotRunner/Statics/ObjectManager.cs#L873-L878)

### 2. Player.Name Returns Empty (Timing Issue)
The name cache at `0xC0E230` isn't populated immediately after entering world. The code returns empty string instead of retrying or using Lua fallback.

**Location**: [WoWObject.cs:118-145](Services/ForegroundBotRunner/Objects/WoWObject.cs#L118-L145)

### 3. Snapshot Guard Blocks Send
`SendActivitySnapshot()` has guard: `if (!string.IsNullOrEmpty(snapshot.CharacterName))` - so empty name = no send.

**Location**: [ForegroundBotWorker.cs:458](Services/ForegroundBotRunner/ForegroundBotWorker.cs#L458)

## What Was Working Before (from MEMORY.md)
- `callback_player_set.txt` previously showed: `Player set! Name=Dralrahgra`
- The name cache lookup DID work at some point
- The issue may be timing-related or the character was already in the name cache from a previous session

## Proposed Fixes

### Fix 1: Reset HasEnteredWorld on Disconnect/Logout
In `ForegroundBotWorker.cs` event handler:
```csharp
eventHandler.OnDisconnect += (_, _) =>
{
    _logger.LogWarning("Disconnected from server");
    _loginAttempted = false;
    _enterWorldAttempted = false;
    _isLoadingWorld = false;
    if (_objectManager != null)
    {
        _objectManager.HasEnteredWorld = false;  // Already here
        _objectManager.Player = null;  // ADD THIS - clear player reference
    }
};
```

Also reset in `ObjectManager.EnumerateVisibleObjects()` when `IsLoggedIn` becomes false.

### Fix 2: Use Lua Fallback for Player Name
Add fallback in `WoWObject.Name` or `UpdateProbe()`:
```csharp
var playerName = Player.Name;
if (string.IsNullOrEmpty(playerName))
{
    // Fallback to Lua
    var result = Functions.LuaCallWithResult("{0} = UnitName('player')");
    if (result.Length > 0 && !string.IsNullOrEmpty(result[0]))
    {
        playerName = result[0];
    }
}
```

### Fix 3: Better IsInWorld Detection
Create a robust `IsInWorld` property that checks multiple signals:
```csharp
public bool IsInWorld => IsLoggedIn &&
                         Player != null &&
                         !string.IsNullOrEmpty(Player.Name) &&
                         LoginState != LoginStates.login &&
                         LoginState != LoginStates.charselect;
```

## Key Files to Modify

1. **ObjectManager.cs** - State management and detection
   - Path: `Services/ForegroundBotRunner/Statics/ObjectManager.cs`
   - Reset `HasEnteredWorld` and `Player` on logout/disconnect
   - Add `IsInWorld` property

2. **WoWObject.cs** - Name retrieval
   - Path: `Services/ForegroundBotRunner/Objects/WoWObject.cs`
   - Add Lua fallback for empty name

3. **ForegroundBotWorker.cs** - Main loop and event handling
   - Path: `Services/ForegroundBotRunner/ForegroundBotWorker.cs`
   - Use `IsInWorld` instead of `IsLoggedIn && HasEnteredWorld`

## Test Command
```bash
cd "E:\repos\BloogBot"
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~StateManager_ShouldSpawnAndInjectWoWClient" --logger "console;verbosity=detailed"
```

## Breadcrumb Files Location
`E:\repos\BloogBot\Bot\Debug\net8.0\*.txt`

## Success Criteria
1. `SNAPSHOT_RECEIVED: Account='ORWR1', Character='Dralrahgra'` appears in StateManager logs
2. Test assertion passes: `snapshotResult.SnapshotReceived && !string.IsNullOrEmpty(snapshotResult.CharacterName)`

## Reference Implementation
Working name cache lookup: `BloogBot-Grouping&Dungeons/BloogBot/Game/ObjectManager.cs`

## Memory File
Project memory with key insights: `C:\Users\lrhod\.claude\projects\e--repos-BloogBot\memory\MEMORY.md`

---

## Detailed Implementation Guide

### Step 1: Add Player Reset to OnDisconnect Handler

**Current code** in [ForegroundBotWorker.cs:246-256](Services/ForegroundBotRunner/ForegroundBotWorker.cs#L246-L256):
```csharp
eventHandler.OnDisconnect += (_, _) =>
{
    _logger.LogWarning("Disconnected from server - will attempt to reconnect");
    _loginAttempted = false;
    _enterWorldAttempted = false;
    _isLoadingWorld = false;  // Reset loading state on disconnect
    if (_objectManager != null)
    {
        _objectManager.HasEnteredWorld = false;  // Reset world state on disconnect
    }
};
```

**Problem**: `Player` reference is not cleared, causing stale data.

**Fix**: Add `_objectManager.Player = null;` inside the if block:
```csharp
if (_objectManager != null)
{
    _objectManager.HasEnteredWorld = false;
    _objectManager.Player = null;  // ADD THIS LINE
}
```

### Step 2: Add Logout Event Handler

Add a new handler for PLAYER_LOGOUT event in [WoWEventHandler.cs](Services/ForegroundBotRunner/Statics/WoWEventHandler.cs).

**Add new event** around line 67:
```csharp
public event EventHandler OnLogout;
```

**Fire the event** in `ProcessEvent()` around line 464:
```csharp
else if (parEvent == "PLAYER_LOGOUT")
{
    OnLogout?.Invoke(this, new EventArgs());
}
```

**Subscribe in ForegroundBotWorker.cs** after the OnDisconnect handler (~line 257):
```csharp
eventHandler.OnLogout += (_, _) =>
{
    _logger.LogWarning("Player logged out");
    _loginAttempted = false;
    _enterWorldAttempted = false;
    _isLoadingWorld = false;
    if (_objectManager != null)
    {
        _objectManager.HasEnteredWorld = false;
        _objectManager.Player = null;
    }
};
```

### Step 3: Add Lua Fallback for Player Name

**Option A: In UpdateProbe()** [ObjectManager.cs:882](Services/ForegroundBotRunner/Statics/ObjectManager.cs#L882)

Replace:
```csharp
var playerName = Player.Name ?? "";
```

With:
```csharp
var playerName = Player.Name ?? "";
if (string.IsNullOrEmpty(playerName))
{
    try
    {
        var result = Functions.LuaCallWithResult("{0} = UnitName('player')");
        if (result != null && result.Length > 0 && !string.IsNullOrEmpty(result[0]))
        {
            playerName = result[0];
            File.WriteAllText(BREADCRUMB_DIR + "lua_name_fallback.txt",
                $"Got name from Lua: {playerName} at {DateTime.Now}");
        }
    }
    catch (Exception ex)
    {
        File.WriteAllText(BREADCRUMB_DIR + "lua_name_error.txt",
            $"Lua name fallback failed: {ex.Message} at {DateTime.Now}");
    }
}
```

**Option B: Add a lazy-loading name property to LocalPlayer** (more robust)

In [LocalPlayer.cs](Services/ForegroundBotRunner/Objects/LocalPlayer.cs), add a cached name with Lua fallback:
```csharp
private string? _cachedName;
public string CachedName
{
    get
    {
        if (string.IsNullOrEmpty(_cachedName))
        {
            _cachedName = base.Name;
            if (string.IsNullOrEmpty(_cachedName))
            {
                var result = Functions.LuaCallWithResult("{0} = UnitName('player')");
                if (result?.Length > 0)
                    _cachedName = result[0];
            }
        }
        return _cachedName ?? "";
    }
}
```

### Step 4: Create Robust IsInWorld Property

**Add to ObjectManager.cs** around line 100:
```csharp
/// <summary>
/// Robust check for whether the player is actually in the game world.
/// Unlike HasEnteredWorld, this actively validates multiple signals.
/// </summary>
public bool IsInWorld
{
    get
    {
        // Must be logged in
        if (!IsLoggedIn) return false;

        // Must have a player object
        if (Player == null) return false;

        // Must have a valid name (indicates name cache is populated)
        var name = Player.Name;
        if (string.IsNullOrEmpty(name))
        {
            // Try Lua fallback
            try
            {
                var result = Functions.LuaCallWithResult("{0} = UnitName('player')");
                if (result == null || result.Length == 0 || string.IsNullOrEmpty(result[0]))
                    return false;
            }
            catch
            {
                return false;
            }
        }

        // Must not be on login/charselect screen
        var state = LoginState;
        if (state == LoginStates.login || state == LoginStates.charselect)
            return false;

        return true;
    }
}
```

### Step 5: Use IsInWorld in Main Loop

In [ForegroundBotWorker.cs](Services/ForegroundBotRunner/ForegroundBotWorker.cs), update the main loop guard conditions to use `IsInWorld`.

**Find the existing check** (around line 380 in ProcessBotActionsAsync):
```csharp
if (_objectManager.IsLoggedIn && _objectManager.HasEnteredWorld)
```

**Replace with**:
```csharp
if (_objectManager.IsInWorld)
```

---

## Debugging Tips

### Check Name Cache Population
Add this debug code to see what's in the name cache:
```csharp
var namePtr = MemoryManager.ReadIntPtr(0xC0E230);
int count = 0;
while (namePtr != nint.Zero && count < 10)
{
    var guid = MemoryManager.ReadUlong(nint.Add(namePtr, 0xC));
    var name = MemoryManager.ReadString(nint.Add(namePtr, 0x14));
    File.AppendAllText(DEBUG_DIR + "name_cache_dump.txt",
        $"Entry {count}: GUID={guid:X16}, Name={name}\n");
    namePtr = MemoryManager.ReadIntPtr(namePtr);
    count++;
}
```

### Verify PLAYER_LOGOUT Event Fires
The WoW client fires these events on logout:
- `PLAYER_LOGOUT` - when logout begins
- `PLAYER_LEAVING_WORLD` - just before leaving world
- `PLAYER_ENTERING_WORLD` - on re-entry (can be used to reset state)

Check [WoWEventHandler.cs:457-464](Services/ForegroundBotRunner/Statics/WoWEventHandler.cs#L457-L464) to see which events are currently handled.

---

## Verification Checklist

After implementing fixes:

- [ ] Start WoW client and inject bot
- [ ] Login to character - verify `Player.Name` is populated (check breadcrumb)
- [ ] Verify `snapshot_sending.txt` appears with character name
- [ ] Verify StateManager logs show `SNAPSHOT_RECEIVED: Account='xxx', Character='yyy'`
- [ ] Logout character - verify `HasEnteredWorld=false` in breadcrumbs
- [ ] Re-login - verify name is captured again
- [ ] Disconnect (kill network) - verify state resets

## Related WoW Events to Consider

Events that might help detect world state:
- `PLAYER_ENTERING_WORLD` - Fired when player enters world
- `PLAYER_LEAVING_WORLD` - Fired when leaving world
- `PLAYER_LOGOUT` - Fired on logout
- `DISCONNECTED_FROM_SERVER` - Already handled
- `PLAYER_ALIVE` - Fired when player is alive (respawn)

Reference: [WoWEventHandler.cs](Services/ForegroundBotRunner/Statics/WoWEventHandler.cs)

---

## Game State Detection (Comprehensive)

### State Overview

The WoW Vanilla 1.12.1 client has these primary states:

| State | LoginState String | IsLoggedIn | HasEnteredWorld | Key UI Element |
|-------|-------------------|------------|-----------------|----------------|
| Login Screen | `"login"` | false | false | `AccountLoginAccountEdit` |
| Character Select | `"charselect"` | false | false | `CharSelectEnterWorldButton` |
| Character Create | `"charselect"` | false | false | `CharacterCreateFrame` |
| Loading Screen | - | true | false | (no UI) |
| In World | - | true | true | (game UI) |

### Memory Offset: LoginState (0xB41478)

Located at [Offsets.cs:55](Services/ForegroundBotRunner/Mem/Offsets.cs#L55):
```csharp
public static class CharacterScreen
{
    public static nint LoginState = 0xB41478;  // Reads as string: "login" or "charselect"
}
```

**Reading the value:**
```csharp
public LoginStates LoginState =>
    (LoginStates)Enum.Parse(typeof(LoginStates),
        MemoryManager.ReadString(Offsets.CharacterScreen.LoginState));
```

### Current LoginStates Enum (Needs Expansion)

Located at [LoginStates.cs](Exports/GameData.Core/Enums/LoginStates.cs):
```csharp
public enum LoginStates
{
    login,      // Login screen
    charselect  // Character select (also includes char create)
}
```

**Recommendation**: The enum may need additional values if the memory offset stores other strings. However, the current values cover the main use cases.

### Detection Methods

#### Method 1: Memory-Based Detection (Current)

```csharp
// Check if player GUID exists in ObjectManager
private ulong GetPlayerGuidFromMemory()
{
    var managerPtr = MemoryManager.ReadIntPtr(0xB41414);  // ObjectManager.ManagerBase
    if (managerPtr == nint.Zero) return 0;
    return MemoryManager.ReadUlong(nint.Add(managerPtr, 0xC0));  // PlayerGuid offset
}

public bool IsLoggedIn => GetPlayerGuidFromMemory() != 0;
```

**Note**: The offset `0xB4B424` (IsIngame) returns 0 on Vanilla 1.12.1 Elysium - do NOT use it.

#### Method 2: Lua UI Element Visibility

Use `IsElementVisible()` pattern from [FrameHelper.cs](BloogBot-Questing/BloogBot/AI/FrameHelper.cs):

```csharp
public static bool IsElementVisible(string elementName)
{
    var result = Functions.LuaCallWithResult(
        $"{{0}} = {elementName} ~= nil and {elementName}:IsVisible()");
    if (result[0] == "1")
    {
        var alpha = float.Parse(Functions.LuaCallWithResult(
            $"{{0}} = {elementName}:GetAlpha()")[0]);
        return alpha == 1;
    }
    return false;
}
```

### Screen-Specific Detection

#### 1. Login Screen
```csharp
bool IsOnLoginScreen =>
    LoginState == LoginStates.login ||
    IsElementVisible("AccountLoginAccountEdit");
```

**Key UI Elements**:
- `AccountLoginAccountEdit` - Username text box
- `AccountLoginPasswordEdit` - Password text box
- `AccountLoginLoginButton` - Login button

#### 2. Character Select Screen
```csharp
bool IsOnCharacterSelect =>
    LoginState == LoginStates.charselect &&
    !IsLoggedIn &&
    IsElementVisible("CharSelectEnterWorldButton");
```

**Key UI Elements**:
- `CharSelectEnterWorldButton` - Enter World button
- `CharSelectCharacterButton1` through `10` - Character slots
- `CharSelectDeleteButton` - Delete character button
- `CharSelectCreateCharacterButton` - Create new character button

#### 3. Character Creation Screen
```csharp
bool IsOnCharacterCreate =>
    LoginState == LoginStates.charselect &&
    !IsLoggedIn &&
    IsElementVisible("CharacterCreateFrame");
```

**Key UI Elements**:
- `CharacterCreateFrame` - Main create frame
- `CharacterCreateNameEdit` - Character name input
- `CharacterCreateOkayButton` - Create button
- `CharacterCreateBackButton` - Back to select button

#### 4. Loading Screen (World Load)
```csharp
bool IsLoadingWorld =>
    IsLoggedIn &&           // Player GUID exists in memory
    !HasEnteredWorld &&     // Not yet fully loaded
    Player == null;         // No player object yet
```

This is a transitional state - `IsLoggedIn` becomes true before full world load.

#### 5. In World (Playing)
```csharp
bool IsInWorld =>
    IsLoggedIn &&
    HasEnteredWorld &&
    Player != null &&
    !string.IsNullOrEmpty(Player.Name);
```

### Proposed Comprehensive State Property

Add to [ObjectManager.cs](Services/ForegroundBotRunner/Statics/ObjectManager.cs):

```csharp
public enum GameState
{
    Unknown,
    LoginScreen,
    CharacterSelect,
    CharacterCreate,
    LoadingWorld,
    InWorld
}

public GameState CurrentGameState
{
    get
    {
        // In world check first (most common during bot operation)
        if (IsLoggedIn && HasEnteredWorld && Player != null)
            return GameState.InWorld;

        // Loading world (transitional)
        if (IsLoggedIn && !HasEnteredWorld)
            return GameState.LoadingWorld;

        // At glue screens
        try
        {
            var loginState = LoginState;

            if (loginState == LoginStates.login)
                return GameState.LoginScreen;

            if (loginState == LoginStates.charselect)
            {
                // Differentiate between char select and char create
                // by checking visible UI elements
                var result = Functions.LuaCallWithResult(
                    "{0} = CharacterCreateFrame ~= nil and CharacterCreateFrame:IsVisible()");
                if (result?.Length > 0 && result[0] == "1")
                    return GameState.CharacterCreate;

                return GameState.CharacterSelect;
            }
        }
        catch
        {
            // If we can't read LoginState, we're in an unknown state
        }

        return GameState.Unknown;
    }
}
```

### State Transition Events

Events that signal state changes (from [WoWEventHandler.cs](Services/ForegroundBotRunner/Statics/WoWEventHandler.cs)):

| WoW Event | Transition |
|-----------|------------|
| `CHARACTER_LIST_LOADED` | → Character Select |
| `PLAYER_ENTERING_WORLD` | → In World |
| `PLAYER_LEAVING_WORLD` | → Loading/Logout |
| `PLAYER_LOGOUT` | → Character Select |
| `DISCONNECTED_FROM_SERVER` | → Login Screen |

### Implementation Priority

1. **Immediate**: Add Lua fallback for `Player.Name` to unblock snapshots
2. **High**: Reset `HasEnteredWorld` and `Player` on disconnect/logout events
3. **Medium**: Add `GameState` enum and `CurrentGameState` property
4. **Low**: Expand `LoginStates` enum if needed for character create detection
