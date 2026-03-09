# Master Tasks

## Role
- `docs/TASKS.md` is the master coordination list for all local `TASKS.md` files.
- Local files hold implementation details; this file sets priority and execution order.
- When priorities conflict, this file wins.

## Rules
1. Execute one local `TASKS.md` at a time in queue order.
2. Keep handoff pointers (`current file`, `next file`) updated before switching.
3. Prefer concrete file/symbol tasks over broad behavior buckets.
4. Never blanket-kill `dotnet` or `Game.exe` — cleanup must be PID-scoped.
5. Move completed items to `docs/ARCHIVE.md`.
6. Before session handoff, update `Session Handoff` in both this file and the active local file.
7. If two consecutive passes produce no delta, record the blocker and advance to the next queued file.
8. **The MaNGOS server is ALWAYS live.** Never defer live validation tests — run them every session.

---

## P0 — Test Fixture & Harness Hardening (FOUNDATION)

**Rationale:** Test infrastructure must be solid before implementing more bot behavior. Flaky tests, fixture contamination, and client crashes waste more time than they save. Fix the foundation first.

See `docs/BAD_TEST_BEHAVIORS.md` for full anti-pattern catalog.

| ID | Task | Owner | Severity | Status |
|----|------|-------|----------|--------|
| `BT-COMBAT-002` | **Fix creature teleport ACK bug.** BG sends teleport ACK for creature MSG_MOVE_TELEPORT — disrupts heartbeat → combat fails. ACK path must only ACK player teleports. | `Exports/WoWSharpClient/Handlers/MovementHandler.cs` | Critical | Open |
| `BT-COMBAT-001` | **Implement proper auto-attack toggle.** BG sends single CMSG_ATTACKSWING without verifying SMSG_ATTACKSTART. Should verify server accepted attack, retry if rejected, match BloogBot toggle pattern. | `Exports/WoWSharpClient/` | High | Open |
| `BT-SETUP-001` | **Standardized test cleanup pattern.** Create `EnsureCleanSlateAsync(account)`: `.reset items` + revive + teleport to safe zone. Call at start of every test. | `Tests/Tests.Infrastructure/LiveBotFixture.cs` | High | Open |
| `BT-DEATH-001` | **Move death test to Orgrimmar.** Current Durotar road location causes 80+y corpse runs, FG crashes. Orgrimmar graveyard = <30y run. | `Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs` | High | Open |
| `BT-LOGIC-002` | **Make FG failures hard failures.** Stop silently downgrading FG test failures to warnings. FG should fail same as BG, or use `Skip.If` with documented reason. | All LiveValidation tests | High | Open |
| `BT-TELE-001` | **Safe teleport helper for FG.** Limit FG teleports to pre-validated coordinates. Wait for terrain load. Catch crash and mark as skipped. | `Tests/Tests.Infrastructure/LiveBotFixture.cs` | Critical | Open |
| `BT-ITEM-001` | **Centralize item/spell setup.** Shared `TestItems`/`TestSpells` constants. `EnsureItemAsync`/`EnsureSpellAsync` helpers that check before adding. | `Tests/Tests.Infrastructure/` | Medium | Open |
| `BT-LOGIC-001` | **Consolidate distance helpers.** Move `Distance2D`/`Distance3D` to LiveBotFixture shared helpers. | `Tests/BotRunner.Tests/LiveValidation/` | Low | Open |

## P1 — Open Bug Fixes

| ID | Task | Owner | Status |
|----|------|-------|--------|
| `BB-COMBAT-006` | **UnitReaction unreliable in snapshots.** BG defaults to Hated(0), FG returns Friendly(4) for hostile mobs. Need FactionTemplate→Reaction mapping for BG using DBC data. Workaround: entry-based filter in place. | `Exports/WoWSharpClient/` | Open (workaround in place) |
| `FG-GHOST-STUCK-001` | **Ghost form stuck on Orgrimmar catapult geometry.** Pathfinding/stuck-recovery doesn't handle dense M2 geometry. | `Exports/Navigation/` | Open |
| `LV-AUDIT-002` | **FG fishing test (TIM-7).** BG fishing CMSG_CAST_SPELL channel not starting. | `Tests/BotRunner.Tests/LiveValidation/FishingProfessionTests.cs` | Open |

## P2 — FG Client Stability

| ID | Issue | Owner | Status |
|----|-------|-------|--------|
| `FG-REALM-STUCK-001` | FG client stuck on Realm Selection/Language dialog. | `Services/ForegroundBotRunner/` | **Fixed** `2301f0a` |
| `TEST-FGPACKET-001` | FG packet capture — recv hook pending (needs ProcessMessage vtable). | `Services/ForegroundBotRunner/Mem/Hooks/` | **Partial** |
| `FG-CRASH-TELE` | FG ERROR #132 during teleport to mining locations. ACCESS_VIOLATION at 0x006FA780 referencing 0x00000005. Terrain tile not loaded before ObjectManager access. | `Services/ForegroundBotRunner/` | Open |

## P3 — Capability Gaps (Low Priority)

| ID | Issue | Owner | Status |
|----|-------|-------|--------|
| `CAP-GAP-003` | TrainerFrame status unknown — may also be null. | `Exports/WoWSharpClient/` | Open (low priority) |
| `BG-PET-001` | BG pet support — Pet returns null. Hunter/Warlock won't work. | `Exports/WoWSharpClient/` | Open |

## Blocked — Storage Stubs (Needs NuGet)

| ID | Task | Blocker |
|----|------|---------|
| `RTS-MISS-001` | S3 ops in RecordedTests.Shared | Requires AWSSDK.S3 |
| `RTS-MISS-002` | Azure ops in RecordedTests.Shared | Requires Azure.Storage.Blobs |

## Completed Phases (See `docs/ARCHIVE.md`)

- Phase 3: Documentation (9 CLAUDE.md files)
- Phase 4: Large File Refactoring (5 monolith files split)
- Phase 5: Command Rate-Limiting & Stability (RATELIMIT-001/002, CRASH-001)
- AI Parity (all 3 gates pass live)
- Live Validation Failures (2026-02-28 batch)
- Pathfinding / Physics (all resolved)
- Test Infrastructure: TEST-TRAM-001, TEST-CRASH-001

## Canonical Commands

```bash
# Full LiveValidation suite
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~LiveValidation" --blame-hang --blame-hang-timeout 10m

# Corpse-run only
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m

# Combat tests only
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~CombatLoopTests|FullyQualifiedName~CombatRangeTests"

# Physics calibration
dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --settings Tests/Navigation.Physics.Tests/test.runsettings

# AI tests
dotnet test Tests/WWoWBot.AI.Tests/WWoWBot.AI.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"

# Full solution (all test projects)
dotnet test WestworldOfWarcraft.sln --configuration Release
```

## Session Handoff
- **Last updated:** 2026-03-09 (session 45)
- **Current work:** Solution cleanup, test bad behavior audit, documentation overhaul.
- **Completed this session:**
  1. **BAD_TEST_BEHAVIORS.md created:** 8 categories, 18 anti-patterns documented across all 24 LiveValidation test classes. See `docs/BAD_TEST_BEHAVIORS.md`.
  2. **Stale files removed from git:** 13 files untracked (scripts, FastCall.dll, next-session-prompt.md). `.gitignore` updated.
  3. **TASKS.md rewritten:** Archived completed phases. Reprioritized: test fixture/harness hardening (P0) before capabilities (P3). Open items verified accurate.
  4. **BAD_BEHAVIORS.md:** Updated with combat findings from session 44.
- **LiveValidation results (this session): 47 passed, 2 failed, 1 skipped (50 total)**
  - **Failed (2):** CombatRangeTests.MeleeAttack (creature teleport ACK bug), GatheringProfessionTests.Mining (FG crash ERROR #132 during Barrens teleport).
  - **Skipped (1):** CombatRangeTests.RangedAttack (no ranged weapon equipped).
- **Known issues documented:**
  - BT-COMBAT-002: Creature teleport ACK still sent for non-player GUIDs — disrupts BG melee combat
  - BT-DEATH-001: Death test should use Orgrimmar, not remote Durotar road
  - BT-COMBAT-001: Auto-attack doesn't match BloogBot toggle pattern
  - FG-CRASH-TELE: FG crashes during multi-location teleport sequences (mining test)
- **Test counts:** LiveValidation 47/50, WoWSharpClient 1254, Physics 97, AI 119.
- **Sessions 1-44:** See `docs/ARCHIVE.md` for full history.
