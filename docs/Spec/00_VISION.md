# Spec 00 — Vision and Acceptance Criteria

## Final-state definition

The WWoW server is **alive** in the sense that a new human player logging in
cannot tell, from gameplay observation alone, that the population is
machine-controlled. The four observable properties:

1. **Always-available activities.** Any legal activity (questing zone,
   dungeon, raid, battleground, profession route, world event, world boss)
   has bots available to participate within the activity's normal group-form
   timeframe. A human request for a level-appropriate dungeon group resolves
   in under 5 minutes.
2. **Continuous progression.** Bots not serving a human request progress
   toward their assigned roster goals (level, gear, attunement, reputation,
   profession, gold, mount, PvP rank). Idle bots are a bug.
3. **Living economy.** The Auction House has 24-hour posting and bidding
   activity. Vendors see traffic. Banks see deposits. Mail moves between
   characters. Trade chat carries trade offers. The economy reaches steady
   state without seeding.
4. **Operator clarity.** The WPF Dashboard shows the highest-volume errors,
   the active activities, the bot population, and the scaling pressure
   points. Choosing the next engineering task is a query, not a guess.

## Bot population shape

3,000 bots distributed:

- **Faction split:** roughly 50/50 Horde/Alliance with intentional imbalance
  configurable per server (default: 50/50).
- **Class/spec coverage:** every race/class/spec combination represented at
  every 5-level bracket, weighted toward the level cap (60).
- **Profession coverage:** every primary profession (Alchemy, Blacksmithing,
  Enchanting, Engineering, Herbalism, Leatherworking, Mining, Skinning,
  Tailoring) has multiple bots at 300 skill. Secondary professions
  (Cooking, First Aid, Fishing) are universal.
- **PvP coverage:** sufficient queue depth for WSG (10v10), AB (15v15), AV
  (40v40) within bracket boundaries.
- **Raid coverage:** sufficient attuned 60s for at least one concurrent
  raid in each tier (Onyxia, MC, BWL, ZG, AQ20, AQ40, Naxx).

## On-Demand Activity contract

A human player can request any catalog activity at any time. The request
shape:

```text
OnDemandActivity {
  Activity,        // e.g. "Dungeon"
  Location,        // e.g. "Wailing Caverns"
  LevelRange,      // e.g. "17-24"
  Params?          // role preference, loot policy, start-when-ready
}
```

The system resolves the request into:

- A legality check (faction reach, attunement, lockout, key, party limits).
- A bot selection scoring pass over the available roster.
- A group form (invite, accept, optional travel meetup).
- The activity execution (dungeon clear, BG match, etc.).
- A return-to-progression step for each leased bot.

The human caller is **always legal**. The scheduler does not refuse based on
caller identity or progression state. If the human cannot legally enter the
activity (wrong faction, missing attunement), the scheduler picks bots that
can carry the human via summon, escort, or alternative entry. If no legal
path exists, the scheduler returns a structured rejection with suggested
alternatives.

## Acceptance criteria

The spec is satisfied when:

- [ ] Every catalog activity in [`Spec/04_ACTIVITIES.md`](04_ACTIVITIES.md)
  has an automated test that drives a request through to a successful
  group form and activity completion, asserted via StateManager APIs.
- [ ] All 27 class/spec combat profiles pass FG and BG live-validation
  rotations against level-appropriate mobs.
- [ ] The WPF Dashboard renders bot population, active activities, lease
  state, top errors, queue depth, and config editing for every catalog
  activity, with hot-reload taking effect without service restart.
- [ ] A staged load run reaches 3,000 concurrent bots with snapshot latency
  P99 < 500 ms, pathfinding latency P99 < 2 s, disconnect rate < 0.1% per
  hour, and activity completion rate > 95%.
- [ ] Docker logs in normal operation are quiet enough that any Warning is
  signal, not noise. Achieved by burst suppression + Docker log rotation.
- [ ] Every reproducible WoW.exe crash has either a code-side hardening
  fix or a documented mitigation in [`Plan/Crashes/`](../Plan/) (created
  on first crash entry).
- [ ] Every pattern that landed in WWoW is documented as a portable skill
  in [`Spec/15_SKILLS.md`](15_SKILLS.md) and exercised by at least one
  cross-game smoke test (FF XI is the validation target).

## Non-goals

- The server does not need to be visually indistinguishable from a human
  server (no fake mistakes, no fake idle wandering). Behavioral parity is
  protocol-level only.
- Bots do not need to chat realistically. Chat is functional (trade offers,
  group invites, GM whispers) but not conversational. PromptHandlingService
  exists for AI-augmented chat when desired, but it is optional.
- No anti-detection beyond what already exists (Warden bypass in FG). The
  server is private; bots run in the open.

## Load-bearing invariants

These survive every refactor. Breaking one is a P0 bug.

1. **No blind sequences.** Counters, sleeps, fixed `repeat-N-times` —
   banned for state validation. Always gate on memory, packet, snapshot,
   pixel, or explicit API state.
2. **FG is ground truth.** When FG and BG disagree, FG wins; BG is wrong.
   See [`Spec/07_PHYSICS.md`](07_PHYSICS.md).
3. **StateManager owns orchestration.** Tests, the UI, and external
   callers never bypass StateManager to talk to BotRunner or to game
   services.
4. **PathfindingService and SceneDataService are the only owners of world
   geometry answers.** Bot code does not load mmap/vmap/scene tiles
   directly.
5. **Tests assert through StateManager.** A test that asserts on internal
   BotRunner state without going through a StateManager snapshot is wrong.
6. **No skip-for-resource-not-found.** A natural-spawn miss is a real
   pathfinding/detection failure. Walk further; do not spawn.
7. **No GM toggles in tests.** `.gm on` corrupts UnitReaction bits. GM
   access is account-level only.
8. **Catalog drives legality.** Every illegal activity rejection cites a
   specific catalog field. No ad-hoc legality logic in BotRunner.
