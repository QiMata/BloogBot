# BasicLoopTests

Tests fundamental bot lifecycle: login, physics stability, teleportation, and snapshot data population.

## Test Methods (6)

### 1. LoginAndEnterWorld_BothBotsPresent

**Bots:** BG (TESTBOT2) + FG (TESTBOT1)

**Fixture Setup:** Standard `LiveBotFixture` init — both bots already InWorld by test start.

**Test Flow:**
1. `RefreshSnapshotsAsync()` — query StateManager for latest snapshots
2. Assert BG snapshot exists with: GUID != 0, Position != null, Health > 0, Level > 0
3. Assert FG snapshot (if available) has same properties

**StateManager/BotRunner Role:** Passive — snapshots are populated by BotRunnerService's 100ms tick loop calling `PopulateSnapshotFromObjectManager()` → `SendMemberStateUpdate()`. No actions dispatched.

**Assertions:** Snapshot data integrity (GUID, position, health, level populated).

---

### 2. Physics_PlayerNotFallingThroughWorld

**Bots:** BG + FG

**Fixture Setup:** `EnsureCleanSlateAsync()` for both bots (revive + Orgrimmar + GM on).

**Test Flow:**
1. Wait for Z stabilization via `WaitForZStabilizationAsync()` (3000ms)
2. Read final Z from snapshot
3. Assert: `finalZ > -500` (world floor threshold) AND stable flag true

**StateManager/BotRunner Role:** BotRunnerService runs physics tick — BG uses PathfindingService gravity simulation, FG reads memory position from WoW.exe. No actions dispatched.

**Assertions:** Z coordinate doesn't fall below world floor (-500). Z stabilizes (stops changing) within 3s.

---

### 3. Teleport_PlayerMovesToNewPosition

**Bots:** BG + FG

**Fixture Setup:** `EnsureCleanSlateAsync()`.

**Test Flow:**
1. `BotTeleportAsync()` to Razor Hill (Map=1, X=326.81, Y=-4706.65, Z=15.37)
   - Internally: `.go xyz 326.81 -4706.65 15.37 1` via bot chat
2. `WaitForNearPositionAsync()` — poll until within 35y of target
3. Wait for Z stabilization
4. Assert: `movementFlags & MOVEFLAG_FORWARD == 0` (movement flag cleared post-teleport)
5. Assert: displacement > 5y if not already near target

**StateManager/BotRunner Role:** Bot chat command `.go xyz` is typed by the bot — BotRunnerService processes `SendChat` action which calls `_objectManager.SendChatMessage()`. Server processes teleport. BG client handles `MSG_MOVE_TELEPORT_ACK` packet.

**Assertions:** Arrival within 35y. MOVEFLAG_FORWARD (0x01) cleared. Z stable.

---

### 4. Snapshot_SeesNearbyUnits

**Bots:** BG + FG (at Razor Hill)

**Fixture Setup:** Teleport to Razor Hill area.

**Test Flow:**
1. Teleport both bots to Razor Hill
2. `RefreshSnapshotsAsync()`
3. Assert: `snapshot.NearbyUnits.Count > 0`

**StateManager/BotRunner Role:** BotRunnerService populates `NearbyUnits` from ObjectManager enumeration each tick. FG reads WoW memory; BG processes SMSG_UPDATE_OBJECT packets.

**Assertions:** NearbyUnits populated (NPCs visible at Razor Hill).

---

### 5. Snapshot_SeesNearbyGameObjects

**Bots:** BG + FG

**Test Flow:** Same as NearbyUnits but asserts `NearbyObjects.Count > 0`.

**StateManager/BotRunner Role:** Same ObjectManager enumeration path.

---

### 6. SetLevel_ChangesPlayerLevel

**Bots:** BG + FG

**Test Flow:**
1. `.character level {charName} 10` via `SendGmChatCommandTrackedAsync()`
2. If chat command rejected, fallback to SOAP `SetLevelAsync()`
3. `WaitForLevelAtLeastAsync()` — poll until level >= 10
4. Assert: `snapshot.Player.Level >= 10`

**StateManager/BotRunner Role:** GM command processed by MaNGOS server. Level change arrives via SMSG_UPDATE_OBJECT packet → ObjectManager updates player level field → next snapshot tick captures it.

**Assertions:** Level readable from snapshot and >= 10.

---

## Cleanup/Teardown

No explicit cleanup — tests are read-only observations of snapshot state.
