# Spec 05 — Progression

## Three planning layers

```
RosterPlanner          // long-horizon (account-level)
   ↓
ProgressionPlanner     // per-character next-objective
   ↓
ActivityScheduler      // multi-character coordination
```

Each layer is a service under `Services/WoWStateManager/Progression/`.

## RosterPlanner

**Owns:** account-level decisions about which characters exist on the
roster and which long-horizon goals they carry.

```csharp
public sealed record CharacterRosterGoal
{
    public required string AccountName { get; init; }
    public required Race Race { get; init; }
    public required Class Class { get; init; }
    public required string SpecName { get; init; }            // catalog reference
    public required IReadOnlyList<Profession> Professions { get; init; }
    public required GearTier TargetGearTier { get; init; }    // PreRaid | T1 | T2 | T2.5 | T3
    public required IReadOnlyList<ReputationGoal> Reputations { get; init; }
    public required IReadOnlyList<AttunementGoal> Attunements { get; init; }
    public required MountTier MountTier { get; init; }
    public required long GoldTargetCopper { get; init; }
    public required PvPRank? PvPRankTarget { get; init; }
    public required IReadOnlyList<int> RareItemTargets { get; init; }
}
```

Coverage rules the RosterPlanner enforces (in order):

1. **Faction-side bootstrap.** If the account plan needs a Shaman and
   the account has 0 Horde characters, the planner schedules a Horde
   character creation first.
2. **Class coverage.** All 9 classes appear at level 60 before any class
   is duplicated at 60.
3. **Profession coverage.** All 9 primary professions distributed across
   the roster; no profession unrepresented at 300 skill.
4. **Spec diversity.** Each class has at least one of each spec at 60
   (tank, healer, DPS where applicable).
5. **PvP rank.** Roster contains at least one character at each PvP
   rank band needed for AV objectives.

## ProgressionPlanner

**Owns:** the next objective for a single character given its current
snapshot.

```csharp
public sealed record ProgressionObjective
{
    public required string Type { get; init; }    // "Quest" | "Dungeon" | "Profession" | ...
    public required string CatalogId { get; init; } // ActivityDefinition.Id
    public required int Priority { get; init; }
    public required string Rationale { get; init; }
    public required IReadOnlyDictionary<string, string> Parameters { get; init; }
}
```

Priority bands (highest first; matches `docs/leveling-guide/decision-engine/leveling-priority.md`):

1. **Survival** — corpse run, spirit healer, food/water if HP/Mana < threshold.
2. **Training** — class trainer + weapon trainer if available skill points exist.
3. **Gear** — equip available gear; visit vendor for missing slot; chase BiS if eligible.
4. **Attunement** — current bracket's MC/Ony/BWL chain if not complete.
5. **Reputation** — current bracket's required factions (Argent Dawn for EPL, etc.).
6. **Mount** — at 40 (or 60 epic) if gold target met.
7. **Gold** — farm gold if below `GoldTargetCopper * 1.1`.
8. **Profession** — train + level professions to bracket cap.
9. **Default grind** — zone quests for current bracket from
   [`Plan/Activities/quests.md`](../Plan/Activities/quests.md).

The planner respects:

- **Lockouts.** Dungeon/raid lockouts gate Attunement and Gear bands
  (autonomous progression honors real lockouts; only OnDemand
  circumvents them).
- **Server capabilities.** Disabled raids omit from Attunement band.
- **Account-level state.** RosterPlanner overrides individual choices
  when account-level coverage demands it.

## Group formation is organic (no scheduler)

Per the 2026-05-12 design refinement: **there is no `ActivityScheduler`
holding leases and routing bots.** Autonomous bots are always on,
running their own behavior trees toward their own objectives. Groups
form when level/role-compatible bots converge on the same group
activity organically — e.g. five 17-24 level bots independently
decide "next: Wailing Caverns" and the QuestCoordinator detects the
quorum and triggers the group-invite flow.

The "How do bots act when they can't raid?" question (e.g. attuned
60s waiting for raid lockout to reset, or short of a 40-man quorum)
resolves through the ProgressionPlanner's priority bands:

- Gear-chase dungeons (Strat live for Cape, Scholo for trinket, etc.).
- World buff farming during pre-raid windows.
- Profession leveling / AH posting.
- Reputation grinds (Argent Dawn, Cenarion Circle, Thorium Brotherhood).
- PvP queues (BGs to fill toward PvP rank goal).
- Mount/gold farm (low-priority but ever-present fallback).

The QuestCoordinator, DungeoneeringCoordinator, BattlegroundCoordinator,
and RaidCoordinator from
[`Spec/02_STATEMANAGER.md#coordinators`](02_STATEMANAGER.md#coordinators)
detect group quorums and orchestrate the form-invite-travel-engage
flow, but they do NOT preempt or lease bots — they just react to
snapshot conditions.

## Test realm acceleration

For test realm (`Westworld-Test`, see
[`Spec/16_REALMS_AND_ACCOUNTS.md`](16_REALMS_AND_ACCOUNTS.md)),
mangosd config accelerates lockout refresh and respawn timers so the
progression loop can be exercised without waiting real-world days.
Tests assert against accelerated timings; production realm uses
default Vanilla 1.12.1 timings.

## Character build templates

Pre-built templates ship in `Services/WoWStateManager/Progression/Templates/`:

- `FuryWarriorPreRaid.json` — Lionheart Helm, Drake Tooth Necklace, etc.
- `HolyPriestMCReady.json` — Robes of the Exalted, Aurastone Hammer.
- `FrostMageAoEFarmer.json` — Lord Valthalak's Robes, Master's Hat.
- ... (one per representative spec; ~15 total)

A `CharacterRosterGoal` can reference a template by name; the
`CharacterBuildConfig` inherits the template's `TargetGearSet`,
`ReputationGoals`, `MountGoal`, `GoldTargetCopper`, etc.

## Account-level state model

```csharp
public sealed record AccountRoster
{
    public required string AccountName { get; init; }
    public required IReadOnlyList<CharacterRosterGoal> Characters { get; init; }
    public required IReadOnlyDictionary<int, int> FactionStandings { get; init; } // factionId → standing
    public required IReadOnlyList<int> CompletedAttunements { get; init; }
    public required long SharedGoldCopper { get; init; }
}
```

Account state is **not on per-character snapshots.** It lives in
StateManager memory (rebuildable from snapshots) and is persisted to a
small JSON file on shutdown.

## Existing code anchors

| Concept | File |
|---|---|
| Progression planner | `Services/WoWStateManager/Progression/ProgressionPlanner.cs` |
| Raid composition | `Services/WoWStateManager/Progression/RaidCompositionService.cs` |
| Character settings | `Services/WoWStateManager/Settings/CharacterSettings.cs` |
| Loadout converter | `Services/WoWStateManager/Settings/LoadoutSpecConverter.cs` |
| Loadout task | `Exports/BotRunner/Tasks/LoadoutTask.cs` |
