# Task Archive

Completed items moved from TASKS.md.

## Archived Snapshot (2026-04-09) - Realm Wizard No-Sweep Stabilization

- [x] Replaced realm-wizard Lua fallback sweeps with state-based, named-control actions for English selection, realm suggestion, and suggestion confirmation.
- [x] Kept realm-wizard handoff detection state-based (`charselect`) so empty character lists are treated as valid post-realm transitions.
- [x] Revalidated dedicated FG new-account/new-character live flow across repeated runs (`fg_new_account_flow_latest/rerun1/rerun2/no_sweep.trx`).

## Archived Snapshot (2026-03-23) - Services/ForegroundBotRunner/TASKS.md

- [x] Finish the remaining FG runtime parity surfaces that still inherited defaults: `QuestGreetingFrame`, `TradeFrame`, and the task-owned bank/AH/craft helper methods.

## Archived Snapshot (2026-02-24 19:43:32) - Services/ForegroundBotRunner/TASKS.md

- [x] Ensure FG death/ghost state detection is stable enough for corpse-run parity.
- [x] Implement missing descriptor-backed life-state fields (`WoWPlayer.PlayerFlags`, `WoWPlayer.Bytes/Bytes3`, `WoWUnit.Bytes0/1/2`) used by `ActivitySnapshot`.
- [x] Reduce remaining Lua-only FG life-state paths (`LocalPlayer.InGhostForm`, reclaim-delay fallbacks) now that descriptor fields are available.
- [x] Implement descriptor-backed FG `WoWPlayer.QuestLog` reads so quest log slots flow into snapshots.
- [x] Guarantee non-null `PathfindingClient` injection into FG `ClassContainer`.
