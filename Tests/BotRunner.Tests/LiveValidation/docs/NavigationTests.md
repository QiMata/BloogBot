# NavigationTests

Tests pathfinding and movement: dispatch Goto action, verify bot walks to destination within timeout.

## Test Methods (2)

### 1. Navigation_ShortPath_ArrivesAtDestination

**Bots:** BG + FG

**Scenario:** Razor Hill short path (~30y)
- Start: (340, -4686, 16.5)
- End: (310, -4720, 11)
- Timeout: 20s
- Arrival radius: 8y

---

### 2. Navigation_CityPath_ArrivesAtDestination

**Bots:** BG + FG

**Scenario:** Orgrimmar city path (~50y)
- Start: (1629, -4373, 34)
- End: (1660, -4420, 34)
- Timeout: 45s
- Arrival radius: 8y

---

**Test Flow (RunSingleNavigation, same for both):**

| Step | Action | Details |
|------|--------|---------|
| 1 | Clean slate | `EnsureCleanSlateAsync()` |
| 2 | Teleport to start | `BotTeleportAsync(mapId, startX, startY, startZ)`. `WaitForTeleportSettledAsync()`. |
| 3 | Dispatch goto | **Dispatch `ActionType.Goto`** with `FloatParams = [endX, endY, endZ, 0]` (tolerance=0). Assert Success. |
| 4 | Poll movement | Every 1.5s: read position, calculate `dist2D = sqrt((x-endX)^2 + (y-endY)^2)`, track best distance, log progress. |
| 5 | Check arrival | If `dist2D <= 8.0y`: assert success, return true. |
| 6 | Timeout | If not arrived within maxSeconds: log best distance and total travel, return false. |

**StateManager/BotRunner Action Flow:**

**Goto dispatch chain:**
1. ActionMessage with `ActionType.Goto`, `FloatParams = [endX, endY, endZ, 0]`
2. `BuildGoToSequence(endX, endY, endZ, 0)` in BotRunnerService
3. Sequence:
   a. Request path from PathfindingService (port 5001) — A* on navmesh
   b. PathfindingService returns List<Vector3> waypoints
   c. For each waypoint: set movement flags (MOVEFLAG_FORWARD), face direction, send movement packets
   d. BG: MSG_MOVE_START_FORWARD + MSG_MOVE_HEARTBEAT packets
   e. FG: `ClickToMove()` via memory write or Lua `MoveForwardStart()`
   f. At each waypoint: stop, re-orient, continue to next
   g. Tree returns Success when within tolerance of final waypoint

**Key dependency:** PathfindingService must be running (port 5001). Path quality depends on navmesh data for the area.

**GM Commands:** None (setup only).

**Assertions:** Arrival within 8y of destination. Movement toward target (bestDist decreasing). Completion within timeout.
