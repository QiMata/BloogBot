# OrgrimmarGroundZAnalysisTests

Tests ground Z calibration accuracy between FG (WoW memory), BG (physics engine), and known reference values.

## Bot Execution Mode

**Dual-Bot Conditional** — Both bots probe post-teleport ground Z. FG is optional diagnostic. See [TEST_EXECUTION_MODES.md](TEST_EXECUTION_MODES.md).

## Test Methods (2)

### 1. DualClient_OrgrimmarGroundZ_PostTeleportSnap

**Bots:** BG + FG

**Test Flow:**

Tests 3 probe positions in Orgrimmar (worst-case ground Z calibration points):

| Probe | Location | RecZ | SimZ | Expected Gap |
|-------|----------|------|------|--------------|
| ValleyOfStrength_A | (1637.264, -4374.140) | 29.369 | 28.850 | 0.519y |
| ValleyOfStrength_B | (1671.257, -4356.295) | 29.856 | 29.443 | 0.413y |
| UpperLevel | (1660.734, -4332.938) | 61.669 | 61.266 | 0.403y |

Per position:
1. Teleport BG and FG to `teleZ = recZ + 3.0` (adds 3y to avoid undermap detection)
2. `WaitForTeleportSettledAsync()` — gravity settles clients to ground
3. Read Z from both snapshots after settlement
4. Calculate deltas: `bgSimDelta = bgZ - simZ`, `fgBgDelta = fgZ - bgZ`

**Validation thresholds:**
- `bgZ` is NaN → **FAIL** (no position data)
- `bgZ - teleZ < 0.5y` → Z_CLAMP (navmesh gap, stayed at teleport height) — PASS with warning
- `|bgZ - simZ| > 1.5y` → Z_MINOR (acceptable for multi-level terrain) — PASS with warning
- `|bgZ - simZ| > 5.0y` → Z_DRIFT — **FAIL** (too far from expected ground)

---

### 2. DualClient_OrgrimmarGroundZ_StandAndWalk

**Bots:** BG + FG

**Test Flow:**
1. Teleport both to worst-error position (ValleyOfStrength_A)
2. Wait for settlement
3. Sample position 10 times at 1-second intervals (idle standing)
4. Record BG_X, BG_Y, BG_Z, FG_X, FG_Y, FG_Z, Z_Delta per sample
5. Analyze Z separation consistency between BG and FG

---

## StateManager/BotRunner Role

**No actions dispatched.** This is a pure physics measurement test. BotRunnerService captures Z from:
- **BG:** Physics engine gravity simulation (PathfindingService calculates ground Z from navmesh/ADT data)
- **FG:** Direct memory read from WoW.exe player position struct

The test validates that the BG physics engine's ground Z matches the FG client's native ground Z within acceptable tolerance.

**GM Commands:** None.

**Assertions:** No major Z drifts (> 5y). Z values stabilize after teleport. BG and FG Z values within acceptable range of each other.
