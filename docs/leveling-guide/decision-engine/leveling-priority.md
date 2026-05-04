# Leveling Priority

> **Pass 1 skeleton.** When the unlock graph says multiple actions are eligible at once, this file decides which the engine picks. Pass 1 sets the priority bands; later passes assign concrete actions to bands.

## The five priority bands

The engine uses a coarse band first, fine-grained tie-breaker second. Higher band wins; within a band, higher numeric weight wins; within identical weight, deterministic by node id.

| Band | Weight range | Examples | Why this band |
|---|---|---|---|
| **Survival** | 1000–1999 | corpse-run, food/drink to refill below 30% mana, repair when item-broken, escape combat with mob ≥3 levels above | Failure here costs play-time and risks character loss; trumps everything. |
| **Class identity** | 800–999 | weapon-skill quests at level cap, totem quests, Bear Form, Travel Form, Charge, Shadowform, Polymorph variants, Vanish, Soul Shard demo, epic class mount chain (Charger / Dreadsteed) | These permanently unlock kit. Skipping leaves the character mechanically incomplete. |
| **Critical-path progression** | 600–799 | attunement steps when other prereqs are already met (UBRS → MC, Marshal Windsor chain, Naxx attune), key acquisition (Shadowforge, Crescent, Master's, Skeleton, Workshop), epic mount training when 1000g banked | These gate everything downstream — delaying them is wasted XP because re-runs to fetch them later are pure overhead. |
| **Optimal questing** | 400–599 | following the bracket-recommended quest hub, dungeon-quest pickups before a planned run, profession skill-ups while passing through a node | The default loop for ~80% of /played time. |
| **Background / opportunistic** | 100–399 | profession nodes off the questing path, world-buff collection when not raid-prep, BG queues when nothing else is pending, rep grinds for non-attune factions | Filler when nothing higher is eligible. |

## Tie-breakers within a band

When two rules tie on weight:

1. **Soonest expiring** wins. World-buff windows, lockout timers, and "this guild raid is in 30 min" beat plain quest pickups.
2. **Lowest travel cost** wins. If two quest hubs are eligible and one is in the current zone, prefer that one.
3. **Most precondition-children** wins. Picking a node that unlocks more downstream nodes beats one that unlocks fewer (DAG fan-out heuristic).
4. **Deterministic node-id sort** breaks the final tie. Same character + same snapshot must yield the same action across runs.

## Bracket modifiers

Priorities are sometimes shifted up/down based on bracket:

| Bracket | Modifier |
|---|---|
| 1-10 | Class identity rules at lvl 1-4 (e.g. Warlock Imp, Hunter pet at 10) get +200 — these unlock baseline kit. |
| 50-60 | Critical-path progression rules get +100 — at this point everything downstream of dinging 60 starts mattering. |
| 60 | Class identity rules for epic class mount get **+200** if `gold ≥ class-mount-cost-threshold`. Otherwise the rule is **suppressed** until gold accumulates (no half-finished chains). |
| 60 | Attunement rules get +100 each if the character is flagged as a raider on the account. |

## Faction-side priority

The DecisionEngine accepts an *account-level* end-state plan. If the account already has a Paladin at 60 on Alliance, scheduling a *second* Paladin alt is band 100 (background). If a class is missing from the account roster entirely, *creating* it on the appropriate faction-locked side is band 600 once the current main is at 60.

## Suppressions (rules that *never* fire)

- **Cross-faction class quests.** Engine never schedules Paladin actions for Horde toon, never Shaman actions for Alliance toon.
- **TBC / Wrath / Cata content.** All such rules must be tagged `expansion: tbc+` and the engine refuses to load them on a 1.12.1 build.
- **Classic re-release-only mechanics.** Layering, summon-stones-without-warlock-port, Meeting-Stone-as-portal, etc. are tagged `client: classic-2019-only` and suppressed.

## Decision-Engine Rules

- `engine.Pick := argmax(action ∈ eligible) of (band, weight, soonest-expiring, lowest-travel, fanout, id)`
- If `eligible` is empty after preconditions, fall back to `Action.GrindMobs(snapshot.OptimalGrindZone())`.
- The engine emits a structured trace per tick: which actions were eligible, which won, why (which tie-breaker fired). Required for debugging "why did the bot do *that*?".

## Snapshot Fields Needed

- All fields listed in [state-flags.md](state-flags.md) — this file is pure policy and reads no fresh fields.
- `account-level state` — not on a single snapshot; provided by the StateManager's cross-character roster, which is consulted only at action-selection time, not on the wire.
