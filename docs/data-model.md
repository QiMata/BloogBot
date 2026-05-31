# Data Model

> Two distinct "data models" matter in WWoW: the **behavior model** (the
> Activity→Objective→Task→Action hierarchy that structures everything a bot
> does) and the **persistent stores**. This page is the practical index; the
> canonical definitions are in [`Spec/18_TERMINOLOGY.md`](Spec/18_TERMINOLOGY.md)
> and [`architecture/aota/`](architecture/aota/README.md).

## The behavior hierarchy (read this before reusing the words)

```
Activity  →  Objective  →  Task  →  Action
```

| Layer | What it is | Crosses the wire? |
|---|---|---|
| **Activity** | A major, usually-dynamic event supporting any number of characters (raid, battleground, dungeon run, multi-hour farm). | No |
| **Objective** | A high-level state change composed of Tasks. Travels as `ObjectiveMessage`. BotRunner decomposes it via behavior tree + game-DB lookups. | **Yes** (the only layer that does) |
| **Task** | A behavior-tree node (`IBotTask`) on the LIFO task stack driving ONE state change with verification + failure handling. Pushes child Tasks (`GoToTask` is universal). | No |
| **Action** | An **atomic** local primitive: one memory read, one bit write, one opcode send, one key press. | No (pure local code) |

`IObjectManager` methods like `MoveToAsync`/`CastSpellAsync` are **Task-level
wrappers**, not Actions. Compound things (`MoveToCoord`, `LootCorpse`,
`InviteToParty`) are Tasks composed of many Actions over many ticks.

**Worked example** (`dungeon.ubrs` Activity → `reach-flame-crest` Objective →
`TravelToTask(coord)` → atomic Actions per tick): see
[`architecture/aota/06_WORKED_EXAMPLES.md`](architecture/aota/06_WORKED_EXAMPLES.md).
Recursive composition and how the DecisionEngine builds Objectives from the
MaNGOS database: [`architecture/aota/03_DYNAMIC_COMPOSITION.md`](architecture/aota/03_DYNAMIC_COMPOSITION.md).

## Accounts, realms & loadout

- Realm/account structure (including the test accounts and the Shodan GM
  liaison): [`Spec/16_REALMS_AND_ACCOUNTS.md`](Spec/16_REALMS_AND_ACCOUNTS.md).
- Character loadout (gear/inventory/spec) model: [`Spec/17_LOADOUT.md`](Spec/17_LOADOUT.md).

## Persistent stores

| Store | Type | Owner | Purpose |
|---|---|---|---|
| MaNGOS world / realmd | MySQL/MariaDB | **External** (game server) | Game world state (creatures, quests, items, spells, loot). **Read-mostly** — mutate only via SOAP ([`security.md`](security.md)). |
| `bloogbot_memory` | PostgreSQL | WWoW | Decision-engine knowledge base (quest/item DAGs, learned skills). |
| `storyline_runtime` | SQLite | WWoW | Dialog/quest narrative state for PromptHandling. |

There are **no EF Core migrations**; the app-owned stores are managed by service
startup logic. The `ProtoDef/database.proto` messages mirror MaNGOS tables for
read access — see [`api-contracts.md`](api-contracts.md).

> Note on online characters: a MaNGOS DB read for a logged-in character is
> **stale** (server holds state in memory until save/logout). Use StateManager
> snapshots (`ActivitySnapshot`) as the source of truth for live state.

## See also

- [`Spec/04_ACTIVITIES.md`](Spec/04_ACTIVITIES.md) — activity catalogue
- [`Spec/05_PROGRESSION.md`](Spec/05_PROGRESSION.md) — progression model
- [`Spec/20_DECISION_ENGINE.md`](Spec/20_DECISION_ENGINE.md) — how Objectives are chosen
- [`Plan/Activities/`](Plan/Activities/) — per-activity implementation slots
