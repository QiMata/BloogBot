# Master Tasks

## Rules
1. **Use ONE continuous session.** Auto-compaction handles context limits.
2. Execute one local `TASKS.md` at a time in queue order.
3. Move completed items to `docs/ARCHIVE.md`.
4. **The MaNGOS server is ALWAYS live.** Never defer live validation tests.
5. **Compare to VMaNGOS server code** when implementing packet-based functionality.
6. Every implementation slice must add or update focused unit tests.
7. After each shipped delta, commit and push before ending the pass.

---

## P3 - Fishing Parity (Low Priority)

**FishingTask is implemented and passing live validation for both BG and FG.** Remaining work is packet-level optimization, not core mechanics.

| # | Task | Status |
|---|------|--------|
| 3.1 | Capture FG fishing packets (cast → channel → bobber → custom anim) | Open — packet infra ready |
| 3.2 | Compare BG fishing packets against FG capture | Blocked on 3.1 |
| 3.3 | Harden BG fishing parity to match FG packet/timing | Blocked on 3.2 |

---

## P4 - Movement Flags After Teleport (BT-MOVE-001/002)

ConnectionStateMachine handles MSG_MOVE_TELEPORT/ACK. MovementController.Reset() clears flags to MOVEFLAG_NONE. Remaining: formal FG packet capture test to verify no flag divergence.

| # | Task | Status |
|---|------|--------|
| 4.1 | Capture FG teleport packets (MSG_MOVE_TELEPORT_ACK → first heartbeats) | Open — packet infra ready |
| 4.2 | Compare BG teleport behavior — identify remaining flag divergence | Blocked on 4.1 |
| 4.3 | Fix any remaining MovementController flag issues found | Blocked on 4.2 |

---

## Blocked - Storage Stubs (Needs NuGet)

| ID | Task | Blocker |
|----|------|---------|
| `RTS-MISS-001` | S3 ops in `RecordedTests.Shared` | Requires `AWSSDK.S3` |
| `RTS-MISS-002` | Azure ops in `RecordedTests.Shared` | Requires `Azure.Storage.Blobs` |

## BG/FG Parity Gaps

| ID | Issue | Priority | Status |
|----|-------|----------|--------|
| `BG-MERCHANT-001` | Legacy MerchantFrame sequences guarded against NullRef | CRITICAL | **Done** (commit 75039ed) |
| `BG-PET-001` | BG pet discovery + Attack/Follow commands wired via CMSG_PET_ACTION | CRITICAL | **Done** — Cast() pending pet action bar |
| `BG-FRAMES-001` | GossipFrame, TrainerFrame, TaxiFrame, QuestFrame, TalentFrame, CraftFrame null-guarded on BG | HIGH | **Done** |
| `FISH-UNIT-001` | 7 FishingData/FishingTask unit tests failing (pre-existing, not regression) | MEDIUM | **Done** (commit facd3e7) |

---

## Canonical Commands

```bash
# Full LiveValidation suite
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~LiveValidation" --blame-hang --blame-hang-timeout 10m

# Corpse-run only
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m

# Combat tests only
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~CombatLoopTests"

# Physics calibration
dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --settings Tests/Navigation.Physics.Tests/test.runsettings

# Full solution
dotnet test WestworldOfWarcraft.sln --configuration Release
```

## Session Handoff
- **Last updated:** 2026-03-18 (session 113)
- **Branch:** `cpp_physics_system`
- **Completed this session:**
  - BG-MERCHANT-001: Guard legacy MerchantFrame sequences against NullRef on BG bot (commit 75039ed)
  - FISH-UNIT-001: Fix 7 pre-existing FishingData/FishingTask unit test failures (commit facd3e7)
    - 1315/1315 BotRunner unit tests now pass (was 1308/1315)
  - BG-FRAMES-001: Null guard all UI frame sequences (Gossip, Trainer, Taxi, Quest, Talent, Craft)
  - BG-PET-001: Pet discovery from SMSG_UPDATE_OBJECT + Attack/Follow via CMSG_PET_ACTION
  - Prior session: P7 complete (4 tasks), D.1 data centralization shipped
- **Test baseline:** 136/137 physics (1 skip), 1327/1327 BotRunner unit, 1266/1266 WoWSharpClient (all pass)
- **Data dirs:** Server reads from `D:/MaNGOS/data/`. VMaNGOS tools at `D:/vmangos-server/`. Source at `D:/vmangos/`.
- **Next:**
  1. Wire pet Cast() via SMSG_PET_SPELLS action bar parsing
  2. P3/P4: FG packet capture tests (fishing parity, teleport flags)
  3. Collision-aware path following (already implemented, verify live)
