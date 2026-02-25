# Task Archive

Completed items moved from TASKS.md.


## Archived Snapshot (2026-02-24 19:43:32) - Exports/WoWSharpClient/TASKS.md

- [x] Implement immediate teleport movement reset to clear stale `MOVEFLAG_FORWARD`/movement flags on teleport events.
- [x] Ensure ghost/dead state transitions are reflected immediately in object/player models (descriptor-first `InGhostForm` in `WoWLocalPlayer`).
- [x] Harden GameObject field diff numeric conversion to avoid `InvalidCastException` (`Single` -> `UInt32`) during live update processing.
- [x] Fix `SMSG_GROUP_LIST` parsing to MaNGOS 1.12.1 wire format (`groupType(1) + ownFlags(1) + memberCount(4)`).
- [x] Validate BG party leader snapshot parity in live group formation (`FG PartyLeaderGuid == BG PartyLeaderGuid`).

