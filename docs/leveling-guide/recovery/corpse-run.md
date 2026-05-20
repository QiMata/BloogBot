---
title: "Recovery — Corpse Run (Release → Reclaim / Spirit-Healer)"
patch: "1.12.1 (Drums of War, Sept 2006)"
sources_crawled:
  - D:/MaNGOS/source/src/game/Handlers/MiscHandler.cpp
  - D:/MaNGOS/source/src/game/Handlers/NPCHandler.cpp
  - D:/MaNGOS/source/src/game/Objects/Player.cpp
  - D:/MaNGOS/source/src/game/Objects/Player.h
  - D:/MaNGOS/source/src/game/Objects/Corpse.h
  - D:/MaNGOS/source/src/game/Objects/ObjectDefines.h
  - D:/MaNGOS/source/src/game/Objects/UnitDefines.h
  - D:/MaNGOS/source/src/game/SharedDefines.h
  - D:/MaNGOS/source/src/game/Commands/TeleportCommands.cpp
crawl_date: 2026-05-19
---

# Corpse Run — Death → Release → Reclaim / Spirit Heal / Unstuck

Foundational recovery cycle. Every other Activity assumes the bot can die, release, reach its corpse (or accept Spirit Healer cost) and resume objective execution. Live integration tests rely on this for R8 (every cross-test starts from a clean post-death state). The server-side state machine is a four-stage transition — `JUST_DIED → CORPSE → ghost (`PLAYER_FLAGS_GHOST = 0x10`) → ALIVE — gated by `CORPSE_REPOP_TIME` (6 min auto-release), `copseReclaimDelay` (30 / 60 / 120 s scaling per recent-deaths counter), `CORPSE_RECLAIM_RADIUS = 39 yd`, `INTERACTION_DISTANCE = 5.0 yd` for spirit healers, and `SPELL_ID_PASSIVE_RESURRECTION_SICKNESS = 15007` (10 min full, scaled 1 min/level over 10 between 11-19, none below 11).

---

## Overview

When the player dies (`KillPlayer`) the server sets `DeathState = CORPSE`, starts a 6-minute auto-repop timer (`CORPSE_REPOP_TIME`), and waits for `CMSG_REPOP_REQUEST`. On release the player becomes a ghost (`PLAYER_FLAGS_GHOST = 0x10`, transformed via `SPELL_AURA_GHOST` spell 8326 + NE-only `20584`), is teleported to the nearest `game_graveyard`, and may (a) walk to corpse and reclaim within `CORPSE_RECLAIM_RADIUS = 39 yd` after `GetCorpseReclaimDelay` seconds (CMSG_RECLAIM_CORPSE = 0x1D2), (b) talk to a spirit healer NPC (`UNIT_NPC_FLAG_SPIRITHEALER = 0x20`) within `INTERACTION_DISTANCE = 5.0 yd` to resurrect with 25% durability loss and resurrection sickness, or (c) invoke `.unstuck` (spell 20939, 1 h CD, restricted) when geometry blocks the corpse run.

---

## Pre-conditions

| Condition | Source citation |
|---|---|
| Player is dead (`DeathState == CORPSE` or `JUST_DIED`) | `D:/MaNGOS/source/src/game/Handlers/MiscHandler.cpp:52` — `HandleRepopRequestOpcode` rejects if alive AND ghost not yet set |
| Not in battleground with `STATUS_IN_PROGRESS` mismatch (corpse-reclaim guard) | `D:/MaNGOS/source/src/game/Handlers/MiscHandler.cpp:659` |
| Not taxi-flying (interaction blocked) | Implied by `IsTaxiFlying()` checks in same handler family — see `D:/MaNGOS/source/src/game/Commands/TeleportCommands.cpp:1068` for `.unstuck` exclusion |
| Map graveyard exists (`GetClosestGraveYard` returns non-null) | `D:/MaNGOS/source/src/game/Objects/Player.cpp:5468` — `RepopAtGraveyard` falls through to "stay at current location" if none |
| Bot has a `Corpse*` object created by `BuildPlayerRepop()` | `D:/MaNGOS/source/src/game/Objects/Player.cpp:5065` — `CreateCorpse()` call inside `BuildPlayerRepop` |
| For `.unstuck`: `Level >= 10`, not in combat, not in BG, spell 20939 ready | `D:/MaNGOS/source/src/game/Commands/TeleportCommands.cpp:1068` |

---

## Decision-Engine Rules

| # | Predicate | Action | Threshold / Constant | Source citation |
|---|---|---|---|---|
| R1 | `player.PlayerFlags & 0x10` is FALSE AND `player.Health == 0` AND `player.DeathState == CORPSE` | Send `CMSG_REPOP_REQUEST` to release spirit | — (any tick after KillPlayer; before `CORPSE_REPOP_TIME` auto-release) | `D:/MaNGOS/source/src/game/Objects/Player.h:319` (PLAYER_FLAGS_GHOST = 0x00000010); `D:/MaNGOS/source/src/game/Handlers/MiscHandler.cpp:48-68` (HandleRepopRequestOpcode) |
| R2 | Time since death > `CORPSE_REPOP_TIME` | Server auto-fires release; engine must NOT race with another `CMSG_REPOP_REQUEST` (idempotent server-side but generates LOG_LVL_DEBUG noise) | `CORPSE_REPOP_TIME = 6 * MINUTE * IN_MILLISECONDS = 360000 ms` | `D:/MaNGOS/source/src/game/Objects/Player.h:66`; `D:/MaNGOS/source/src/game/Objects/Player.cpp:5170` |
| R3 | After release: distance to corpse > `CORPSE_RECLAIM_RADIUS` | Route bot to corpse via `TravelTask` (corpse position from `Corpse` object on map) | `CORPSE_RECLAIM_RADIUS = 39` yd | `D:/MaNGOS/source/src/game/Objects/Corpse.h:40`; `D:/MaNGOS/source/src/game/Handlers/MiscHandler.cpp:655` |
| R4 | After release: distance to corpse <= `CORPSE_RECLAIM_RADIUS` AND ghost time elapsed >= `GetCorpseReclaimDelay(pvp)` | Send `CMSG_RECLAIM_CORPSE` (opcode 0x1D2 = 466) targeting corpse GUID | Delay scales: `copseReclaimDelay[count]` = 30 / 60 / 120 s where `count = (m_deathExpireTime - now) / DEATH_EXPIRE_STEP`, capped at index 2 | `D:/MaNGOS/source/src/game/Protocol/Opcodes_1_12_1.h:469`; `D:/MaNGOS/source/src/game/Objects/Player.cpp:140-142`; `D:/MaNGOS/source/src/game/Objects/Player.cpp:20514-20525` |
| R5 | After release: corpse is unreachable (no path / impassable geometry) AND nearby spirit healer creature has `UNIT_NPC_FLAG_SPIRITHEALER` AND distance <= `INTERACTION_DISTANCE` | Send `CMSG_SPIRIT_HEALER_ACTIVATE` (opcode 0x21C = 540). Server responds with `ResurrectPlayer(0.5f, true)` + `DurabilityLossAll(0.25f)` | `INTERACTION_DISTANCE = 5.0 yd`; `UNIT_NPC_FLAG_SPIRITHEALER = 0x00000020` | `D:/MaNGOS/source/src/game/Objects/ObjectDefines.h:24`; `D:/MaNGOS/source/src/game/Objects/UnitDefines.h:665`; `D:/MaNGOS/source/src/game/Handlers/NPCHandler.cpp:433-447`; `D:/MaNGOS/source/src/game/Handlers/NPCHandler.cpp:450-497` |
| R6 | Bot is `player.Level >= 11` after Spirit Healer resurrect | Expect `SPELL_ID_PASSIVE_RESURRECTION_SICKNESS = 15007` debuff: 10 min full at L20+; `(level - 10) * 1 min` at L11-19; absent at L1-10 | Sickness duration `MINUTE * (level - startLevel + 1) * IN_MILLISECONDS` where `startLevel = CONFIG_INT32_DEATH_SICKNESS_LEVEL` (default 11) | `D:/MaNGOS/source/src/game/SharedDefines.h:1170`; `D:/MaNGOS/source/src/game/Objects/Player.cpp:5138-5158` |
| R7 | Corpse run blocked AND `.unstuck` available (`Level >= 10`, not in combat, not in BG, not taxi-flying, spell 20939 ready) AND no nearer Spirit Healer | Issue chat command `.unstuck`. While DEAD this teleports to `ClosestGrave` + applies `SPELL_ID_PASSIVE_RESURRECTION_SICKNESS` + 1 h cooldown on spell 20939. While ALIVE it casts spell 20939 (Undying Soul) | `.unstuck` guards: combat, BG, taxi-fly, spell-not-ready, corpse-state, `Level < 10`; CD 1 h after dead-use | `D:/MaNGOS/source/src/game/Commands/TeleportCommands.cpp:1061-1094`; `D:/MaNGOS/source/src/game/Spells/SpellAuras.cpp:2000` (20939 = Undying Soul) |
| R8 | Bot has `PvP-flagged` death (`PLAYER_EXTRA_PVP_DEATH`) | Use `CORPSE_RESURRECTABLE_PVP` reclaim-delay branch (same 30/60/120 s curve gated on `CONFIG_BOOL_DEATH_CORPSE_RECLAIM_DELAY_PVP`) | Same delay table; gating config differs | `D:/MaNGOS/source/src/game/Objects/Player.cpp:5186` (PVP flag → CORPSE_RESURRECTABLE_PVP); `D:/MaNGOS/source/src/game/Objects/Player.cpp:20516-20518` |
| R9 | `player.Race == RACE_NIGHTELF` AND just released | Expect ghost wisp form (`spell 20584`) in addition to common Ghost aura (`spell 8326`) — engine should NOT treat the extra aura as a hostile debuff | NE wisp form is `Player::BuildPlayerRepop`-applied | `D:/MaNGOS/source/src/game/Objects/Player.cpp:5049-5051`; `D:/MaNGOS/source/src/game/Objects/Player.cpp:5103-5105` (removed on Resurrect) |
| R10 | Reclaim succeeded AND bot is in battleground | Battleground reclaim sets `restore_percent = 1.0f` (full HP/mana). PvE reclaim sets `restore_percent = 0.5f`. Engine should not push `RestTask` immediately on BG reclaim but should on PvE reclaim | `restore_percent = InBattleGround() ? 1.0f : 0.5f` | `D:/MaNGOS/source/src/game/Handlers/MiscHandler.cpp:663` |
| R11 | Spirit Healer used | Expect server-side `DurabilityLossAll(0.25f, true)` — engine should flip `Snapshot.RepairDue = true` and queue `RepairTask` at next vendor | 25 % flat durability loss on all items including inventory | `D:/MaNGOS/source/src/game/Handlers/NPCHandler.cpp:454` |
| R12 | After third death within `MAX_DEATH_COUNT * DEATH_EXPIRE_STEP` window | Reclaim delay clamps at index 2 (120 s) until `m_deathExpireTime` decays | `MAX_DEATH_COUNT = 3`; index-clamp at `count >= 2 → count = 2` | `D:/MaNGOS/source/src/game/Objects/Player.cpp:140`; `D:/MaNGOS/source/src/game/Objects/Player.cpp:20522-20524` |
| R13 | Resurrect-by-other-player request pending (`SMSG_RESURRECT_REQUEST` received) AND friendly caster | Send `CMSG_RESURRECT_RESPONSE` with `status = 1` (accept). Server-side guard: empty `guid` triggers `PassiveAnticheat` ("Instant resurrect hack detected") — engine MUST round-trip the request GUID | Reject branch `status = 0` clears request data | `D:/MaNGOS/source/src/game/Handlers/MiscHandler.cpp:669-695` |
| R14 | After any in-world resurrect: corpse-grave may differ from ghost-grave | Engine should re-query position post-resurrect via snapshot rather than assuming pre-release coordinates | `SendSpiritResurrect` teleports to corpse-grave if `corpseGrave != ghostGrave` | `D:/MaNGOS/source/src/game/Handlers/NPCHandler.cpp:457-483` |

---

## Failure modes

| Failure | Detection | Recovery |
|---|---|---|
| **Stuck ghost** (corpse on no-graveyard map, `RepopAtGraveyard` falls through) | `player.PlayerFlags & 0x10 == 0x10` AND `player.MapId` has no `game_graveyard` row | Issue `.unstuck` (R7); on success bot teleports to faction-fallback graveyard (Westfall ID 4 / Barrens ID 10 per `TeleportCommands.cpp:1087`) |
| **Reclaim-too-soon** (server silently rejects `CMSG_RECLAIM_CORPSE` while `GetGhostTime() + GetCorpseReclaimDelay() > now`) | Snapshot shows `PLAYER_FLAGS_GHOST` still set after reclaim send; no `SMSG_RESURRECT` follows | Poll `Snapshot.GhostTimeElapsedMs >= reclaim_delay_ms` predicate before resending; back off and retry. See `MiscHandler.cpp:652` for the gate |
| **Spirit Healer interaction blocked** (channelling spell, mounted, in combat) | `CMSG_SPIRIT_HEALER_ACTIVATE` produces `LOG_LVL_DEBUG` "not found or you can't interact" trace | Cancel channels + dismount + flee combat radius (≥5 yd from any aggro NPC) before retry. Server interrupts on accept anyway (`NPCHandler.cpp:445-446`) but the precondition gate fires first |
| **`.unstuck` 1-hour CD** | Spell 20939 `IsSpellReady() == false` | Fall back to Spirit Healer R5 if reachable; else escalate to `LeaseReturnTask` (return character to lease pool for manual GM intervention) |
| **Resurrect-request anticheat trip** (empty GUID in `CMSG_RESURRECT_RESPONSE`) | Server logs "Instant resurrect hack detected" and emits `CHEAT_ACTION_REPORT_GMS` | NEVER send empty-GUID accept. Always round-trip `guid` from `SMSG_RESURRECT_REQUEST`. See `MiscHandler.cpp:676-680` |

---

## Live-test acceptance

A live integration test exercising this Activity must satisfy R8 (clean cross-test state) by:

1. **Server-up assertion** — `ServerHealthcheck` returns 200 before the test starts; if not, FAIL FAST (do not retry-loop on a dead server).
2. **Pre-test snapshot predicate** — `player.Health > 0 AND player.PlayerFlags & 0x10 == 0` before triggering death (clean start).
3. **Kill trigger** — issue GM `.die` or equivalent; poll snapshot until `player.DeathState == CORPSE AND player.Health == 0` with timeout ≤ 5 s.
4. **Release predicate** — after `ReleaseCorpseTask` push, poll `player.PlayerFlags & 0x10 == 0x10` with timeout ≤ 3 s (sub-second normal; 3 s captures one server tick of latency).
5. **Reclaim or spirit-heal predicate** — depending on test mode:
   - Corpse-walk mode: poll `player.Position` reaching corpse coords within `CORPSE_RECLAIM_RADIUS` (39 yd) with timeout proportional to map distance; then poll `player.PlayerFlags & 0x10 == 0 AND player.Health == (MaxHealth * 0.5)` after reclaim send.
   - Spirit-heal mode: poll `player.Position == nearest_spirit_healer ± INTERACTION_DISTANCE`, send activate, then poll `player.PlayerFlags & 0x10 == 0 AND HasAura(15007) == true AND player.Health == (MaxHealth * 0.5)`.
6. **Disconnect guard** — mid-test client disconnect (per `WorldSocket.cpp:91`) must fail the test (R8) — do not paper over with reconnect-and-retry inside the corpse-run test (`ReconnectTask` is a different Activity).
7. **Screenshot capture** — per R11, capture ForegroundBotRunner client screenshot at: (a) just-died, (b) ghost form post-release, (c) post-reclaim/spirit-heal. Overwrite `Tests/artifacts/recovery/corpse-run/latest-{died,ghost,resurrected}.png`.
8. **Final state dump** — write `Snapshot.player` JSON + last 20 `WoWActivitySnapshot` deltas to `Tests/artifacts/recovery/corpse-run/snapshot-final.json` so an agent can post-mortem without re-running.

Existing test anchor: `BotRunner.Tests.LiveValidation.DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsBackgroundPlayer` at `Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs:52` covers the corpse-walk path. Spirit-heal path is `BotRunner.Tests.LiveValidation.SpiritHealerTests.SpiritHealer_Resurrect_PlayerAliveWithSickness` at `Tests/BotRunner.Tests/LiveValidation/SpiritHealerTests.cs:36`. `.unstuck` and reclaim-too-soon paths are <TBD: no live test anchor — author under S1.13 Recovery slot>.

---

## Cross-References

- Plan: [../../Plan/Activities/recovery.md](../../Plan/Activities/recovery.md) — task families, current shipped status, S1.13 async refactor slot
- Spec: [../../Spec/03_BOTRUNNER.md#catalog-of-task-families](../../Spec/03_BOTRUNNER.md) row "Recovery"
- Decision-Engine state flags: [../decision-engine/state-flags.md](../decision-engine/state-flags.md) — `Snapshot.player.PlayerFlags` (`PLAYER_FLAGS_GHOST` bit), `Snapshot.player.DeathState`
- Decision-Engine priority: [../decision-engine/leveling-priority.md](../decision-engine/leveling-priority.md) — recovery preempts all other Activities while `DeathState == CORPSE`
- Travel infrastructure (corpse-to-body routing): [../systems/flight-paths.md](../systems/flight-paths.md) — FP NOT usable while ghost (movement restricted to walk + swim)
- Reagent / cost: Spirit Healer = 25 % durability loss (see [../systems/mail-auction-bank.md](../systems/mail-auction-bank.md) for repair-vendor proximity rules — <TBD: dedicated repair.md not yet authored>)
