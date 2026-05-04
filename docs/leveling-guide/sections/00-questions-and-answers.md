# Questions & Answers

> **Pass 1 scratchpad.** Open design questions for the leveling guide and the DecisionEngine that consumes it. As later passes resolve a question, move it from **Open** to **Resolved** with a citation to the file that closed it.

## Open

### Q1 — How does the engine differentiate live 1.12.1 facts from Classic 2019 re-release tweaks?

**Context.** Most current wiki pages mix vanilla, TBC, and re-release info. Examples that matter:
- **Black Lotus spawns** were heavily limited on Blizzard's 1.12 servers but are scripted differently on most private servers and on Classic 2019.
- **Dire Maul tribute mechanics** had specific NPC behavior in 1.12 that Classic 2019 changed slightly (Tendris Warpwood threat, Mizzle the Crafty rep gain).
- **World boss respawn timers** on real 1.12 retail differ from VMaNGOS defaults (which differ again from Classic 2019).
- **AV** in 1.12.1 is the post-1.11 short version (avg 30-40 min), not the original 1.5 multi-hour version.

**Status.** Open. The README's "Source priority" section lists the wikis to prefer, but the rule for resolving conflicts isn't yet codified. **Decision needed:** when wiki sources disagree, default to (a) WoWWiki Archive period-tagged pages, (b) Wowhead with `?vanilla=1` filtering, or (c) MaNGOS-Zero source code as ground truth?

### Q2 — How does the engine handle VMaNGOS vs retail 1.12.1 divergence on raid content?

**Context.** Naxxramas, AQ40, and parts of BWL are notoriously incomplete on private server cores. The engine should not schedule a raid action that the server cannot actually execute (e.g., Sapphiron's frostbreath aura on a server where it isn't scripted will trivially wipe the raid).

**Status.** Open. **Proposal:** add a `ServerCapabilities` config block to `stateManager.json` that flags `naxx.implemented`, `aq40.implemented`, `cthun.implemented`, etc. Engine consults this before adding raid actions to the menu.

### Q3 — Does the DecisionEngine support multi-character account-level planning?

**Context.** The end-state goal is "all 9 classes leveled, all professions, all reputations, all attunements, highest PvP rank." That isn't a per-character predicate — it's an account-level one. Some priorities (creating a Druid alt because no Druid is on the account yet vs. creating a 2nd Warrior) require account-level state.

**Status.** Open. Suggested in [leveling-priority.md](../decision-engine/leveling-priority.md#faction-side-priority) as a layer above per-character action selection. Need to define the data structure that crosses character boundaries — likely a StateManager-resident `AccountRoster` table not on the snapshot.

### Q4 — What's the canonical "first action when faction switching is needed"?

**Context.** Paladin (Alliance only) and Shaman (Horde only) lock the faction. If the account plan calls for a Shaman and the account currently has 0 Horde characters, the engine must spin up a Horde character on a paired account.

**Status.** Open. Likely needs a "faction-side bootstrap" rule that's invoked exactly once per account-side, prior to any class-specific plan.

### Q5 — How are world buffs scheduled into the engine?

**Context.** World buffs (Onyxia head, Nef head, Rend, Songflower, DM tribute) significantly improve raid performance. They have travel-cost prerequisites (DM tribute requires a 30-40 min DM N run) and decay timers. A bot that enters MC without picking up Songflower has *measurably* worse outcomes than one that does.

**Status.** Open. Likely a sub-engine triggered by `WorldBuffWindowOpen` (planned snapshot field) — only fires inside a configurable pre-raid window (typically T-90 minutes to T-15 minutes). Out-of-raid-prep, world buffs are not worth the travel.

### Q6 — Does the engine prefer questing or grinding at any specific bracket?

**Context.** In 1.12.1 the quest XP / kill XP ratio is bracket-dependent. Below ~50 questing dominates. From 50-58 there's a brief window where mob grinding (e.g., the lvl 53-55 elementals in Un'Goro or the Plagued Hatchlings in EPL) competes with questing. From 58-60 dungeon-XP (Strat/Scholo/UBRS) usually wins.

**Status.** Open. Not deciding pass 1. Plan to back this with measured XP/hour data when the engine ships and we have telemetry.

### Q7 — How does the engine handle profession specialization commitment points?

**Context.** Engineering at 200 = irreversible Goblin/Gnomish split. Blacksmithing at 200 = Armorsmith/Weaponsmith split (and at 250+ a sub-spec for Weaponsmith: Sword/Mace/Axe). Leatherworking at 225 = Dragonscale/Elemental/Tribal (changing later costs gold + reset and a recipe re-grind). These decisions are **account-level strategic** not per-tick.

**Status.** Open. Likely need to surface as **decision requests** to whoever runs the account plan rather than auto-pick. See the rule note in [04-l30-l40.md](../decision-engine/per-bracket-actions/04-l30-l40.md#decision-engine-rules) for Engineering specifically.

### Q8 — What's the right granularity for "PartyComposition" eligibility?

**Context.** Some dungeons need very specific comp (Strat UD really wants a paladin or priest for undead crowd-control; LBRS without a hunter or warlock is significantly slower; ZG needs specific-class-attuned coordinated pulls). The engine reads `PartyComposition` (existing) but the **rules** for "is this comp viable for this dungeon" aren't written yet.

**Status.** Open. Will resolve in dungeons/ pass 4 — every dungeon file gets a `## Recommended Composition` section with `(role, count)` minimums.

## Resolved

(Empty in pass 1. Future passes append entries here as questions close.)

## How to use this file

- When adding a new file in passes 2+, scan **Open** for any question that touches the file's topic. If you can resolve it, do so in this file (move to **Resolved** with the citation). Don't silently work around it.
- New cross-cutting questions discovered during research go to **Open**, not into the relevant content file.
- The DecisionEngine team tracks this file as a backlog of "things the engine still doesn't know how to decide."
