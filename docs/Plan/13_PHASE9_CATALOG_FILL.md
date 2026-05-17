# Plan 13 — Phase 9: Catalog completeness

> **Goal.** Close the gap between the 86-row initial catalog and the
> Vision acceptance criterion "every legal player activity has a
> catalog row." Add the missing dungeons, holiday events, escort
> family, social services, mage-portal / warlock-summon services, and
> dungeon-quest sub-Activities so the OnDemand UI can launch any
> activity a human would ever request.
>
> **Entry pre-requisite.** Phase 0 done (catalog scaffold exists).
> Phase 9 can run in parallel with Phases 1–8; the new rows do not
> change existing rows' behavior.

## Exit criteria

- [ ] Every dungeon in the game has a catalog row, including Scarlet Monastery's 4 wings (`dungeon.sm-cathedral`, `-library`, `-armory`, `-graveyard`) and `dungeon.stockades`.
- [ ] Dungeon-quest sub-Activities catalog: one row per `quest_template.Type=81` quest that has a unique pickup → in-dungeon → turn-in flow not already covered by the parent dungeon Activity.
- [ ] Holiday events: rows for Lunar Festival, Hallow's End, Winter Veil, Midsummer Fire Festival, Children's Week, Darkmoon Faire (if server-supported).
- [ ] Social services: `social.mage-port`, `social.warlock-summon`, `social.lfg-cycle`, `social.trade-chat-cycle`, `social.guild-events`, `social.city-ambient`.
- [ ] Escort family promoted to standalone Activity rows for every escort quest tracked in `creature_template.ScriptName LIKE '%Escort%'` or `quest_template.SpecialFlags & ESCORT`.
- [ ] World PvP zone activities: `wpvp.epl-graveyards`, `wpvp.alterac-mountains-skirmish` (if server-supported).
- [ ] `Plan/Activities/00_INDEX.md` lists every new row with slot status.
- [ ] `CatalogMarkdownDriftTests` passes against the expanded catalog (likely ~130–160 rows).

## Slots

### S9.1 — Scarlet Monastery 4-wing rows

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:** `Services/WoWStateManager/Activities/Catalog/DungeonShard.cs`, `Plan/Activities/00_INDEX.md`, `Bot/named-locations.json`
- **Goal:** Add `dungeon.sm-graveyard`, `dungeon.sm-library`, `dungeon.sm-armory`, `dungeon.sm-cathedral` rows. Each wing has a separate map+entry coord and a distinct boss list. Cross-reference [`leveling-guide/dungeons/`](../leveling-guide/dungeons/) for per-wing detail (file pending).
- **Success criteria:** rows compile; catalog test passes; OnDemand launcher accepts the new ids.

### S9.2 — Stockades row

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:** as S9.1
- **Goal:** Add `dungeon.stockades` (Alliance-only at the door but cross-faction-eligible inside; map=34). Range 22-30, MinPlayers=5.

### S9.3 — Dungeon-quest sub-Activities

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** Phase 4 (`IMangosCatalog` exists).
- **Owned paths:** `Services/WoWStateManager/Activities/Catalog/DungeonQuestShard.cs` (new), `Plan/Activities/dungeons.md`
- **Goal:** For each dungeon, enumerate the in-dungeon quests (`Type=81`) and add a catalog row per quest chain that has a unique pickup+turn-in NPC pair. Examples: "Mission: Possible But Not Probable" (Gnomeregan), "The Iron Marshal" (BRD), "Squires of the Aerie Peak" (LBRS).

### S9.4 — Escort-family Activities

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:** `Services/WoWStateManager/Activities/Catalog/EscortShard.cs` (new), `Plan/Activities/escorts.md` (new)
- **Goal:** Promote each escort quest to a standalone Activity row. Escort quests have a different snapshot profile (NPC-death failure mode, no kill counter, fixed duration) that justifies catalog separation. Examples: "Tooga's Quest", "Stinky's Escape", "Cluck!".
- **Notes:** New `Plan/Activities/escorts.md` should follow the same shape as the existing 17 family docs.

### S9.5 — Holiday event Activities

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** Spec/22 `IGameEventProvider` lands (Phase 5/6 substrate).
- **Owned paths:** `Services/WoWStateManager/Activities/Catalog/HolidayShard.cs` (new), `Plan/Activities/world-events.md`
- **Goal:** Add one Activity row per `game_event` row that has bot-relevant content:
  - `event.lunar-festival.coin-cycle`
  - `event.hallows-end.candy-bucket-tour`
  - `event.hallows-end.headless-horseman` (gated by server cap)
  - `event.winter-veil.gift-collection`
  - `event.midsummer.bonfire-tour`
  - `event.children-week.orphan-escort` (technically escort-family; cross-referenced)
  - `event.darkmoon-faire.tickets-and-vendor` (if server-supported)
  - `event.stv-fishing-extravaganza` (already exists; rename for consistency)

### S9.6 — Social-service Activities

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** Phase 11 social-fabric substrate ([`15_PHASE11_SOCIAL_FABRIC.md`](15_PHASE11_SOCIAL_FABRIC.md)).
- **Owned paths:** `Services/WoWStateManager/Activities/Catalog/SocialShard.cs` (new), `Plan/Activities/social.md`
- **Goal:** Add:
  - `social.mage-port` — a mage bot escorts a human to a major city via Teleport / Portal spells.
  - `social.warlock-summon` — two pool bots help summon a human across the world.
  - `social.lfg-cycle` — a bot posts to LFG channel + accepts dungeon invites.
  - `social.trade-chat-cycle` — a bot rotates AH chatter (Spec/21).
  - `social.guild-events` — bot participates in guild MOTD / chat / bank cycles.
  - `social.city-ambient` — idle-city service traffic (Spec/21 §7).

### S9.7 — World PvP rows (gated)

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:** as S9.1
- **Goal:** Add `wpvp.epl-graveyards` (Eastern Plaguelands graveyard tower captures) and `wpvp.silithyst-shard-runs` (Silithus). Gate behind `ServerCapabilities.WorldPvp = true`.

### S9.8 — Catalog tests update

- **Owner:** `monorepo-test-runner`
- **Status:** open
- **Depends on:** S9.1..S9.7
- **Owned paths:** `Tests/BotRunner.Tests/Activities/CatalogMarkdownDriftTests.cs`, `Tests/BotRunner.Tests/Activities/ActivityCatalogTests.cs`
- **Goal:** Update the drift test to accept the expanded row count (~130–160). Add a new invariant: every `Family=Social` row has a `social.*` id prefix; every `Family=WorldEvent` row references a valid `game_event` row.

### S9.9 — Live-validation for one row per new family

- **Owner:** `monorepo-test-runner`
- **Status:** open
- **Depends on:** Phase 2 (OnDemand launcher), S9.1..S9.7
- **Goal:** One LiveValidation test per new family:
  - `OnDemand_SmCathedral_Dungeon` — Alliance group, full clear.
  - `OnDemand_StvFishingExtravaganza_WorldEvent` — bot wins or places.
  - `OnDemand_MagePort_SocialService` — mage bot ports a test human.
  - `OnDemand_HallowsEnd_CandyBucketTour` — bot completes 5 buckets.

## Slot ownership

All slots `owned-paths` are non-overlapping with Phase 1–8 slots; this
plan can run in parallel without conflict.

## Failure recovery

- **MaNGOS DB row missing** for a referenced quest / creature → mark the
  Activity row as `gated` and skip in `IActivityCatalog`.
- **Catalog test fails on count mismatch** → update the index markdown,
  not the test (the test enforces single-source-of-truth).
- **OnDemand launcher rejects the new id** → check that the `TaskFamily`
  field is in the canonical list (Spec/03 §catalog-of-task-families).

## Related specs

- [`Spec/04_ACTIVITIES.md`](../Spec/04_ACTIVITIES.md) — `ActivityDefinition` shape.
- [`Spec/22_WORLD_CYCLES.md`](../Spec/22_WORLD_CYCLES.md) — holiday-event eligibility.
- [`Spec/21_SOCIAL_FABRIC.md`](../Spec/21_SOCIAL_FABRIC.md) — social-service Activities.
- [`architecture/aota/04_QUEST_CHAINS.md`](../architecture/aota/04_QUEST_CHAINS.md) — dungeon-quest sub-Activity composition.
