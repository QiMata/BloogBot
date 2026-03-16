# GroupFormationTests

Tests party invite/accept flow and group state tracking between FG and BG bots.

## Bot Execution Mode

**Dual-Bot Sync (Required)** — Both FG and BG must be available. Invite/accept flow requires two live clients. FG is REQUIRED, not optional. See [TEST_EXECUTION_MODES.md](TEST_EXECUTION_MODES.md).

## Test Methods (1)

### GroupFormation_InviteAccept_StateIsTrackedAndCleanedUp

**Bots:** FG (TESTBOT1) + BG (TESTBOT2) - **both required**.

**Fixture Setup:** Standard `LiveBotFixture` init, then `EnsureCleanSlateAsync()` for BG + FG and a live `CheckFgActionableAsync()` probe before invite dispatch.

**Test Flow:**

| Step | Action | Details |
|------|--------|---------|
| 1 | Clean slate | `EnsureCleanSlateAsync()` revives and teleports both bots to the safe zone before any group work starts. |
| 2 | Probe FG | `CheckFgActionableAsync()` confirms FG is still processing actions after any prior suite instability; otherwise the test skips instead of timing out. |
| 3 | Ensure ungrouped | `EnsureNotGroupedAsync()` for both bots - up to 5 attempts. If `PartyLeaderGuid != 0`: dispatch `DisbandGroup` (leader) or `LeaveGroup` (member). 1s between attempts. |
| 4 | Verify initial state | Snapshot both: assert `PartyLeaderGuid == 0` |
| 5 | FG invites BG | **Dispatch `ActionType.SendGroupInvite`** with `StringParam = bgCharacterName`. Assert Success. Wait 1.2s. |
| 6 | BG accepts | **Dispatch `ActionType.AcceptGroupInvite`** (no params). Assert Success. Wait 1.5s. |
| 7 | Verify group formed | `WaitForGroupFormationAsync(20s)`: poll every 1s for `fgLeader != 0 && fgLeader == bgLeader && fgLeader == fgGuid` (FG is leader) |
| 8 | Cleanup | `EnsureNotGroupedAsync()` for both. Verify `PartyLeaderGuid == 0`. |

**StateManager/BotRunner Action Flow:**

- **SendGroupInvite:** `BuildSendGroupInviteByNameSequence(bgCharName)` -> `_objectManager.InviteToGroup(name)` -> `CMSG_GROUP_INVITE`
- **AcceptGroupInvite:** `AcceptGroupInviteSequence` -> `_objectManager.AcceptGroupInvite()` -> `CMSG_GROUP_ACCEPT`
- **DisbandGroup:** `DisbandGroupSequence` -> `_objectManager.DisbandGroup()` -> `CMSG_GROUP_DISBAND`
- **LeaveGroup:** `LeaveGroupSequence` -> `_objectManager.LeaveGroup()` -> `CMSG_GROUP_DISBAND` (same opcode, different context)
- **FG readiness gate:** `LiveBotFixture.CheckFgActionableAsync()` now requires both a successful `.targetself` dispatch and a teleport/snapshot round-trip via `BotTeleportAsync(...)` + `WaitForSnapshotConditionAsync(...)` before FG-dependent assertions continue.

**Snapshot Fields:**
- `Player.PartyLeaderGuid` - `0` if not in group, else the leader GUID
- Used to verify group formation and cleanup

**GM Commands:** None.

**Assertions:** Both bots ungrouped initially. Group forms with FG as leader. Both bots see the same `PartyLeaderGuid`. Cleanup restores the ungrouped state.

## Current Status

- `2026-03-11` hardening added clean-slate recovery plus an explicit FG action probe so post-herbalism FG restarts skip early instead of cascading into a misleading timeout.
- `2026-03-11` the FG probe was strengthened further to verify snapshot responsiveness after teleport, which cleared the later `GroupFormationTests`/`NpcInteractionTests`/`SpellCastOnTargetTests` stale-FG cascade.
- Validation after the change:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~GatheringProfessionTests|FullyQualifiedName~GroupFormationTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `2 passed, 1 skipped`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~GatheringProfessionTests|FullyQualifiedName~GroupFormationTests|FullyQualifiedName~NpcInteractionTests|FullyQualifiedName~QuestInteractionTests|FullyQualifiedName~SpellCastOnTargetTests|FullyQualifiedName~UnequipItemTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `10 passed, 2 skipped`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~LiveValidation" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `32 passed, 0 failed, 3 skipped`
