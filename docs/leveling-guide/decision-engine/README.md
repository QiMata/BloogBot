# Decision-Engine Contracts

> **Pass 1 skeleton.** Defines the vocabulary the StateManager DecisionEngine uses to consume the rest of the leveling guide. Filled in iteratively as later passes add content.

## What lives here

| File | Role |
|---|---|
| [state-flags.md](state-flags.md) | The set of `WoWActivitySnapshot` fields the engine reads. Source of truth for the snapshot schema *as far as the leveling guide is concerned*. |
| [unlock-graph.md](unlock-graph.md) | Directed acyclic graph of prerequisites: *node A unlocks node B*. Each node maps to a class quest, attunement, profession milestone, key, faction tier, etc. |
| [leveling-priority.md](leveling-priority.md) | When multiple unlocked actions are eligible at once, which wins? Priority weights + tie-breakers. |
| [per-bracket-actions/](per-bracket-actions/) | One file per level bracket — the menu of actions the engine considers in that bracket. |

## How the engine consumes this folder

Pseudocode the engine implements:

```text
function PickNextAction(snapshot):
    eligible = []
    for action in CurrentBracket(snapshot.Level).Actions:
        if action.Preconditions(snapshot, unlock_graph):
            eligible.append((action, action.Priority(snapshot)))

    if eligible is empty:
        return Action.GrindMobs(snapshot.OptimalGrindZone())

    return eligible.SortBy(priority desc, then deterministic_id asc).First()
```

Three concepts the engine cares about, defined in this folder:

1. **State flags** — which snapshot fields are read for predicates.
2. **Unlock graph** — has the prerequisite of *X* been satisfied?
3. **Priority** — when *X*, *Y*, *Z* are all unlocked, which wins?

Per-bracket files supply the **action menu** at each level range; class / zone / dungeon / raid files (passes 2+) supply the **rules** that go into the menu.

## Shape of an action rule (what later passes write)

Every content file in `classes/`, `zones/`, etc. ends with a `## Decision-Engine Rules` section in this form:

```markdown
- **id:** `class.warrior.weapon-skill.first-aid-kit-quest`
  **bracket:** 10-20
  **precondition:**
    - `snapshot.Class == Warrior`
    - `snapshot.Level >= 10`
    - `!snapshot.QuestsCompleted.Contains("Hot and Cold")`
  **action:** travel to <hub>, accept quest <id>, complete steps <…>
  **priority:** 60 (class identity > zone questing > grind)
```

The engine compiles rules into the in-memory action menu at startup. The leveling guide is the only authoritative source for these rules — code does not bake them in.

## Snapshot Fields Needed (this folder)

Defined and itemized in [state-flags.md](state-flags.md). Every other file references that file rather than redefining fields.
