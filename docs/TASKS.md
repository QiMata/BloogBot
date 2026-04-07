# Master Tasks — Test & Validate Everything

## Rules
1. **Use ONE continuous session.** Auto-compaction handles context limits.
2. Execute tasks in priority order.
3. Move completed items to `docs/ARCHIVE.md`.
4. **The MaNGOS server is ALWAYS live** (Docker).
5. **WoW.exe binary parity is THE rule** for physics/movement.
6. **GM Mode OFF after setup** — `.gm on` corrupts UnitReaction bits. Always `.gm off` before test actions.
7. **Kill WoW.exe before building.** DLL injection locks output files.
8. **Previous phases archived** — see `docs/ARCHIVE.md`.

---

## Test Baseline (2026-04-07 — All phases complete)

| Suite | Passed | Failed | Skipped | Notes |
|-------|--------|--------|---------|-------|
| WoWSharpClient.Tests | 1437 | 0 | 1 | +20 SceneData pipeline tests |
| Navigation.Physics.Tests | 666 | 2 | 1 | 2 pre-existing elevator |
| BotRunner.Tests (unit) | 1626 | 0 | 4 | Confirmed |
| BotRunner.Tests (LiveValidation) | 7+ | 0 | 0 | BasicLoop + DualClientParity pass |
| WSG (20 bots) | 12/20 entered | — | — | First successful BG entry |
| AV (80 bots) | 37/40 Horde | — | — | Alliance not launched |

---

## R1-R7 — All Complete (see docs/ARCHIVE.md)

---

## Outstanding — Open Issues

### P1: Alliance Faction Bot Support
StateManager does not launch Alliance accounts (AVBOTA1-40). AV and AB tests cannot form both factions. Needs: StateManager settings to support Alliance race character creation and launch.

### P2: BG Queue Pop
AB (15v15) and AV (40v40) queued but never popped. VMaNGOS BG queue may need server-side config: minimum player count override, or `.bg start` GM command to force-start.

### P3: WSG Transfer Stalls
8/20 bots stalled during WSG map transfer (stale timeout). May need longer `WaitForBgEntryAsync` timeout, or investigation into why some bots don't complete `SMSG_TRANSFER_PENDING` → `SMSG_NEW_WORLD` sequence.

### P4: GoTo Z Alignment
`BuildGoToSequence` pathfinding returns navmesh waypoints at navmesh Z (~34y) but bot physics ground is at a different Z (~37y). `MoveToward` sets waypoint below bot → stuck detection fires immediately. Needs: snap navmesh waypoint Z to physics ground Z before passing to MovementController.

### P5: Server Position Resets
During TravelTo navigation, bot occasionally snaps back to teleport origin (seen as 74y→106y jump at 45s). Likely server-side position correction rejecting client movement. Needs: investigate whether movement packets are being rejected by VMaNGOS anti-cheat or position validation.

### P6: Navigation.Physics.Tests Elevator Failures (2)
Two pre-existing elevator test failures in Navigation.Physics.Tests. Low priority — elevator physics edge case, not regression.

---

## Canonical Commands

```bash
# Kill everything before building
taskkill //F //IM WoW.exe 2>/dev/null
taskkill //F //IM BackgroundBotRunner.exe 2>/dev/null
taskkill //F //IM WoWStateManager.exe 2>/dev/null
taskkill //F //IM testhost.x86.exe 2>/dev/null

# Build .NET + C++ (both architectures)
dotnet build WestworldOfWarcraft.sln --configuration Release
MSBUILD="C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe"
"$MSBUILD" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145
"$MSBUILD" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x86 -p:PlatformToolset=v145

# Tests
dotnet test Tests/WoWSharpClient.Tests/ --configuration Release --filter "Category!=RequiresInfrastructure" --no-build
dotnet test Tests/Navigation.Physics.Tests/ --configuration Release --no-build
dotnet test Tests/BotRunner.Tests/ --configuration Release --filter "Category!=RequiresInfrastructure&FullyQualifiedName!~LiveValidation" --no-build
dotnet test Tests/BotRunner.Tests/ --configuration Release --filter "FullyQualifiedName~LiveValidation" --no-build --blame-hang --blame-hang-timeout 10m
```
