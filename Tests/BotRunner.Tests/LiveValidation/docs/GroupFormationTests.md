# GroupFormationTests

Tests party invite/accept flow and group state tracking between FG and BG bots.

## Test Methods (1)

### GroupFormation_InviteAccept_StateIsTrackedAndCleanedUp

**Bots:** FG (TESTBOT1) + BG (TESTBOT2) — **both required**.

**Fixture Setup:** Standard `LiveBotFixture` init.

**Test Flow:**

| Step | Action | Details |
|------|--------|---------|
| 1 | Ensure ungrouped | `EnsureNotGroupedAsync()` for both bots — up to 5 attempts. If `PartyLeaderGuid != 0`: dispatch `DisbandGroup` (leader) or `LeaveGroup` (member). 1s between attempts. |
| 2 | Verify initial state | Snapshot both: assert `PartyLeaderGuid == 0` |
| 3 | FG invites BG | **Dispatch `ActionType.SendGroupInvite`** with `StringParam = bgCharacterName`. Assert Success. Wait 1.2s. |
| 4 | BG accepts | **Dispatch `ActionType.AcceptGroupInvite`** (no params). Assert Success. Wait 1.5s. |
| 5 | Verify group formed | `WaitForGroupFormationAsync(20s)`: poll every 1s for `fgLeader != 0 && fgLeader == bgLeader && fgLeader == fgGuid` (FG is leader) |
| 6 | Cleanup | `EnsureNotGroupedAsync()` for both. Verify `PartyLeaderGuid == 0`. |

**StateManager/BotRunner Action Flow:**

- **SendGroupInvite:** `BuildSendGroupInviteByNameSequence(bgCharName)` → `_objectManager.InviteToGroup(name)` → CMSG_GROUP_INVITE packet with player name
- **AcceptGroupInvite:** `AcceptGroupInviteSequence` → `_objectManager.AcceptGroupInvite()` → CMSG_GROUP_ACCEPT packet
- **DisbandGroup:** `DisbandGroupSequence` → `_objectManager.DisbandGroup()` → CMSG_GROUP_DISBAND
- **LeaveGroup:** `LeaveGroupSequence` → `_objectManager.LeaveGroup()` → CMSG_GROUP_DISBAND (same opcode, different context)

**Snapshot Fields:**
- `Player.PartyLeaderGuid` — 0 if not in group, else leader's GUID
- Used to verify group formation and cleanup

**GM Commands:** None.

**Assertions:** Both bots ungrouped initially. Group forms with FG as leader. Both bots see same PartyLeaderGuid. Cleanup restores ungrouped state.
