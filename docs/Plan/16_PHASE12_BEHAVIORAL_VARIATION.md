# Plan 16 — Phase 12: Behavioral variation

> **Goal.** Ship the `PersonalityProfile` per-bot knobs that satisfy
> Vision acceptance criterion 4 — a passive observer of the population
> cannot pick out the bots by behavioral signatures.
>
> **Entry pre-requisite.** Phase 11 done (social fabric is in place;
> chat and AH-posting variance is the most visible knob). Phase 1
> done (`IBotTask` substrate stable enough to absorb jitter).

## Exit criteria

- [ ] `PersonalityProfile` record + `IPersonalityFactory` ship per [`Spec/24_BEHAVIORAL_VARIATION.md`](../Spec/24_BEHAVIORAL_VARIATION.md).
- [ ] Every personality knob has at least one consumer wired:
  - Timing knobs in `BotTask.OnPushedAsync`, `PvERotationTask`, `LootCorpseTask`, `InteractWithNpcTask`.
  - Route knobs in `PathfindingClient` post-processor + `KillObjectiveTask.PickHotspot`.
  - Economy knobs in `AuctionHousePostTask`.
  - Social knobs in `WhisperReplyHandler`, `IChatGenerator`.
  - Reward knob in `IRewardSelector`.
- [ ] Mix config `Config/personalities.json` hot-reloadable; default mix Quiet/Normal/Talkative 45/45/10.
- [ ] Operator UI exposes per-bot personality editor; overrides persist in `Config/character-overrides.json`.
- [ ] `Deterministic` profile available for unit tests; live-validation tests opt into deterministic mode.
- [ ] Distribution test: over 1000-bot mix sample, knob distributions match config ±5%.
- [ ] Correctness invariant test: high-jitter bot still completes a representative combat / quest / travel Objective within the budget.
- [ ] Snapshot projection: `WoWActivitySnapshot.Personality.Hash` carries an integer derived from the profile so tests can verify the right personality applied.

## Slots

### S12.1 — `PersonalityProfile` record + factory

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:** `Exports/GameData.Core/Models/Personality/`, `Exports/BotRunner/Personality/`
- **Spec contracts:** [`Spec/24_BEHAVIORAL_VARIATION.md`](../Spec/24_BEHAVIORAL_VARIATION.md)
- **Goal:** Implement the record + `HashStableRandom(accountName)` PRNG + `PersonalityFactory.Create(accountName)`. Ship `PersonalityProfile.Deterministic` as a static singleton.

### S12.2 — `BotTaskContext` carries `Personality`

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S12.1
- **Owned paths:** `Exports/BotRunner/Tasks/BotTaskContext.cs`, `Exports/BotRunner/BotRunnerService.cs`
- **Goal:** Add `PersonalityProfile Personality { get; }` to the context; populate from `IPersonalityFactory.Create(accountName)` at BotRunner construction. Add `Jitter(string knob)` helper on `BotTask` base.

### S12.3 — Timing-knob consumers

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S12.2
- **Owned paths:** `Exports/BotRunner/Tasks/BotTask.cs`, `Exports/BotRunner/Tasks/Combat/PvERotationTask.cs`, `Exports/BotRunner/Tasks/LootCorpseTask.cs`, `Exports/BotRunner/Tasks/InteractWithNpcTask.cs`, `Exports/BotRunner/Tasks/Travel/GoToTask.cs`
- **Goal:** Wire `ReactionTimeJitterMs`, `IntraRotationJitterMs`, `WaypointPatienceMs`, `PostKillLootDelayMs`, `NpcInteractApproachMs` into the listed tasks.

### S12.4 — Route-knob consumers

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S12.2
- **Owned paths:** `Exports/BotRunner/Clients/PathfindingClient.cs`, `Exports/BotRunner/Tasks/Questing/QuestingTask.cs`
- **Goal:** `PathfindingClient` post-processor adds lateral wiggle within `RouteWiggleRadiusYd` (constrained to walkable navmesh — see [`Plan/02_PHASE1#R13`](02_PHASE1_ACTION_TASK_FOUNDATION.md)). `KillObjectiveTask.PickHotspot` applies `HotspotShuffleStrength` to the candidate ranking.

### S12.5 — Economy-knob consumers

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S12.2, Phase 11 mail/AH tasks land.
- **Owned paths:** `Exports/BotRunner/Tasks/Economy/AuctionHousePostTask.cs`
- **Goal:** `AhPostingUnderscutPercent` and `AhPostingCadenceMin` consumed.

### S12.6 — Social-knob consumers

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S12.2, Phase 11 chat tasks land.
- **Owned paths:** `Exports/BotRunner/Social/WhisperReplyHandler.cs`, `Exports/BotRunner/Social/TemplateChatGenerator.cs`
- **Goal:** `WhisperReplyDelayMs`, `ChattyLevel` (affects post probability per opportunity), `JoinWorldChatChannels` consumed.

### S12.7 — Reward-knob consumer

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S12.2, Phase 10 reward selector advisor lands.
- **Owned paths:** `Exports/BotRunner/Activities/RewardSelector.cs`
- **Goal:** `RewardPriority` knob feeds into the deterministic fallback path when DecisionEngine returns `NoAdvice`.

### S12.8 — `Config/personalities.json` hot-reload

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:** `Config/personalities.json`, `Services/WoWStateManager/Config/PersonalityConfig.cs`
- **Goal:** Mix config + hot-reload per [`Spec/14`](../Spec/14_CONFIG.md).

### S12.9 — Per-bot operator override

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S12.1, S12.8
- **Owned paths:** `UI/WoWStateManagerUI/Views/PersonalityEditor.xaml`, `Config/character-overrides.json`
- **Goal:** UI panel for per-bot knob editing. Overrides persist + take effect on the bot's next snapshot tick.

### S12.10 — Snapshot projection

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S12.1
- **Owned paths:** `Exports/BotCommLayer/Models/ProtoDef/communication.proto`, `Exports/BotRunner/SnapshotBuilder.cs`
- **Goal:** Add `PersonalityHash` to `WoWActivitySnapshot.Player`. Tests verify the expected personality applied.

### S12.11 — Distribution + correctness LiveValidation

- **Owner:** `monorepo-test-runner`
- **Status:** open
- **Depends on:** S12.1..S12.10
- **Goal:** Two suites:
  - `PersonalityDistributionTests` — 1000-bot factory sample matches `Config/personalities.json` ±5% per knob.
  - `PersonalityCorrectnessTests` — high-jitter Fury Warrior bot still kills its level-appropriate target within 1.5× expected encounter duration; high-jitter quest-completing bot still finishes a sample quest within budget.

## Failure recovery

- **Personality config invalid** (bad JSON) → fall back to `Deterministic` for every bot; emit error log.
- **Operator override conflicts** (out-of-range knob value) → reject at config-save time with a schema validation error.
- **Distribution test fails on a CI run** (random-sample variance) → re-run with a larger sample (factory is deterministic; the test should be deterministic too — likely a config drift).

## Related specs

- [`Spec/24_BEHAVIORAL_VARIATION.md`](../Spec/24_BEHAVIORAL_VARIATION.md) — contract.
- [`Spec/21_SOCIAL_FABRIC.md`](../Spec/21_SOCIAL_FABRIC.md) — Phase 11 surfaces consumed.
- [`Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md`](14_PHASE10_DECISION_ENGINE_INTEGRATION.md) — reward-knob hand-off to DecisionEngine.
