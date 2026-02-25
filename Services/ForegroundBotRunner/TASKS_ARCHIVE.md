# Task Archive

Completed items moved from TASKS.md.


## Archived Snapshot (2026-02-24 19:43:32) - Services/ForegroundBotRunner/TASKS.md

- [x] Ensure FG death/ghost state detection is stable enough for corpse-run parity.
- [x] Implement missing descriptor-backed life-state fields (`WoWPlayer.PlayerFlags`, `WoWPlayer.Bytes/Bytes3`, `WoWUnit.Bytes0/1/2`) used by `ActivitySnapshot`.
- [x] Reduce remaining Lua-only FG life-state paths (`LocalPlayer.InGhostForm`, reclaim-delay fallbacks) now that descriptor fields are available.
- [x] Implement descriptor-backed FG `WoWPlayer.QuestLog` reads so quest log slots flow into snapshots.
- [x] Guarantee non-null `PathfindingClient` injection into FG `ClassContainer`.

