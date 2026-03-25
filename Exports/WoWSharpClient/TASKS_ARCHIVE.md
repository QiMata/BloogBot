# Task Archive

Completed items moved from TASKS.md.

## Archived Snapshot (2026-03-25 23:59:00) - Exports/WoWSharpClient/TASKS.md

- [x] `WSC-PAR-01` Capture a matched FG/BG trace that proves exact heartbeat-before-stop ordering.
  - Closed by the forced-turn Durotar live route after the session 187 arrival + grounded-stop fixes. FG and BG now both end on outbound `MSG_MOVE_STOP`, neither emits late outbound `SET_FACING` after the opening pair, and the latest stop-edge delta is `50ms`.

## Archived Snapshot (2026-02-24 19:43:32) - Exports/WoWSharpClient/TASKS.md

- [x] Implement immediate teleport movement reset to clear stale `MOVEFLAG_FORWARD`/movement flags on teleport events.
- [x] Ensure ghost/dead state transitions are reflected immediately in object/player models (descriptor-first `InGhostForm` in `WoWLocalPlayer`).
- [x] Harden GameObject field diff numeric conversion to avoid `InvalidCastException` (`Single` -> `UInt32`) during live update processing.
- [x] Fix `SMSG_GROUP_LIST` parsing to MaNGOS 1.12.1 wire format (`groupType(1) + ownFlags(1) + memberCount(4)`).
- [x] Validate BG party leader snapshot parity in live group formation (`FG PartyLeaderGuid == BG PartyLeaderGuid`).

