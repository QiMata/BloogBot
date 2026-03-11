# NpcInteractionTests

Tests NPC interactions: vendor, trainer, flight master discovery, and NPC flag detection in snapshots.

## Test Methods (6)

### 1. Vendor_OpenAndSeeInventory

**Bots:** BG + FG (parity testing)

**Test Flow:**
1. Teleport to Razor Hill vendor (340.36, -4686.29, 16.54)
2. `EnsureReadyAtLocationAsync()` — verify within 40y or re-teleport
3. Find NPC with `UNIT_NPC_FLAG_VENDOR` (0x80) in NearbyUnits
4. **Dispatch `ActionType.InteractWith`** with `LongParam = vendorGuid`
5. Wait 1s for interaction
6. Assert dispatch Success

---

### 2. Vendor_SellJunkItems

Same as Vendor_OpenAndSeeInventory but pre-adds 5x Linen Cloth (item 2589) via `EnsureBagHasItemAsync()` before vendor interaction.

---

### 3. Trainer_OpenAndSeeSpells

**Test Flow:**
1. Teleport to Razor Hill trainer (311.35, -4827.79, 9.66)
2. Find NPC with `UNIT_NPC_FLAG_TRAINER`
3. **Dispatch `ActionType.InteractWith`** with trainer GUID

---

### 4. Trainer_LearnAvailableSpells

Same as Trainer_OpenAndSeeSpells but pre-ensures money >= 10000 copper (`.modify money`) and level >= 10 (`.character level`).

---

### 5. FlightMaster_DiscoverNodes

**Test Flow:**
1. Teleport to Orgrimmar flight master (1676.25, -4313.45, 61.72)
2. Find NPC with `UNIT_NPC_FLAG_FLIGHTMASTER` (0x400)
3. **Dispatch `ActionType.InteractWith`** with FM GUID

---

### 6. ObjectManager_DetectsNpcFlags

**Test Flow:**
1. Teleport to Razor Hill vendor area
2. Retry up to 3 times with 1s delays
3. `RefreshSnapshotsAsync()`
4. Assert: at least one unit in NearbyUnits has non-zero `NpcFlags`
5. Log all NPCs with flags

**StateManager/BotRunner Role:** Passive — NpcFlags populated from SMSG_UPDATE_OBJECT (BG) or memory read (FG).

---

## StateManager/BotRunner Action Flow

**InteractWith (NPC):** `BuildInteractWithSequence(npcGuid)` → detects unit type → `_objectManager.InteractWithNpcAsync(guid)`:
- FG: right-click NPC via memory call at 0x60BEA0
- BG: CMSG_GOSSIP_HELLO or CMSG_NPC_TEXT_QUERY depending on NPC flags

**Key Coordinates:**

| Location | X | Y | Z |
|----------|---|---|---|
| Razor Hill Vendor | 340.36 | -4686.29 | 16.54 |
| Razor Hill Trainer | 311.35 | -4827.79 | 9.66 |
| Orgrimmar FM | 1676.25 | -4313.45 | 61.72 |

**GM Commands:** `.modify money`, `.character level` (trainer test only).

**Assertions:** NPC found with correct flags. InteractWith dispatches succeed. NpcFlags populated in snapshots.
