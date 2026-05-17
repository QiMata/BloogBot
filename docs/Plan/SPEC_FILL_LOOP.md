# Spec-Fill Loop Tracker

> One pass per row. Mark `done` when (a) the spec is implementation-ready,
> (b) ML hooks are spec'd where they fit, (c) contract tests are written
> or stubbed, (d) the commit is pushed.

| # | File | Owner type | ML hook | Status | Outcome |
|---|------|------------|---------|--------|---------|
| 1 | [docs/Spec/19_AOTA_RUNTIME.md](../Spec/19_AOTA_RUNTIME.md) | Spec | Composer-tiebreaker advice | done | Added proto fields 33-37 (current_activity_id/_objective_id/_objective_type/advice_log/last_objective_failure), FailureReason enum, ObjectiveContext feature shape, three-phase ML maturity, §10 dynamic-progressive invariant; 4 Skip-stubbed contract tests in IActivityContractTests.cs. |
| 2 | [docs/Spec/20_DECISION_ENGINE.md](../Spec/20_DECISION_ENGINE.md) | Spec | This IS the ML surface | not-started | |
| 3 | [docs/Spec/21_SOCIAL_FABRIC.md](../Spec/21_SOCIAL_FABRIC.md) | Spec | Chat-template selection | not-started | |
| 4 | [docs/Spec/22_WORLD_CYCLES.md](../Spec/22_WORLD_CYCLES.md) | Spec | World-buff-window forecasting | not-started | |
| 5 | [docs/Spec/23_ONDEMAND_API.md](../Spec/23_ONDEMAND_API.md) | Spec | Request-disambiguation NLP | not-started | |
| 6 | [docs/Spec/24_BEHAVIORAL_VARIATION.md](../Spec/24_BEHAVIORAL_VARIATION.md) | Spec | Personality-clustering | not-started | |
| 7 | [docs/Plan/13_PHASE9_CATALOG_FILL.md](13_PHASE9_CATALOG_FILL.md) | Plan | Catalog-row auto-suggest | not-started | |
| 8 | [docs/Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md](14_PHASE10_DECISION_ENGINE_INTEGRATION.md) | Plan | Wires every ML hook | not-started | |
| 9 | [docs/Plan/15_PHASE11_SOCIAL_FABRIC.md](15_PHASE11_SOCIAL_FABRIC.md) | Plan | Whisper-intent classifier | not-started | |
| 10 | [docs/Plan/16_PHASE12_BEHAVIORAL_VARIATION.md](16_PHASE12_BEHAVIORAL_VARIATION.md) | Plan | Mix-distribution learner | not-started | |
| 11 | [docs/Spec/05_PROGRESSION.md](../Spec/05_PROGRESSION.md) | Spec (existing) | RosterPlanner objective scoring | not-started | |
| 12 | [docs/Spec/04_ACTIVITIES.md](../Spec/04_ACTIVITIES.md) | Spec (existing) | BotSelectionPolicy weights | not-started | |
| 13 | [docs/Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md) | Spec (existing) | IRewardSelector phase-3 ML | not-started | |
| 14 | [docs/Spec/10_METRICS.md](../Spec/10_METRICS.md) | Spec (existing) | Anomaly-detection metric | not-started | |
| 15 | [docs/Spec/12_ERROR_TAXONOMY.md](../Spec/12_ERROR_TAXONOMY.md) | Spec (existing) | Failure-cause clustering | not-started | |
| 16 | [docs/Spec/13_TESTING.md](../Spec/13_TESTING.md) | Spec (existing) | Training-trace capture | not-started | |
| 17 | [docs/Spec/15_SKILLS.md](../Spec/15_SKILLS.md) | Spec (existing) | Cross-game-skill auto-bootstrap | not-started | |
| 18 | [docs/architecture/aota/03_DYNAMIC_COMPOSITION.md](../architecture/aota/03_DYNAMIC_COMPOSITION.md) | Arch | Composer learning loop | not-started | |
| 19 | [docs/architecture/aota/04_QUEST_CHAINS.md](../architecture/aota/04_QUEST_CHAINS.md) | Arch | Quest-chain ordering optimizer | not-started | |
| 20 | [docs/architecture/aota/05_ITEM_REQUIREMENTS.md](../architecture/aota/05_ITEM_REQUIREMENTS.md) | Arch | Cheapest-source learner | not-started | |
| 21 | [docs/architecture/aota/06_WORKED_EXAMPLES.md](../architecture/aota/06_WORKED_EXAMPLES.md) | Arch | Add ML-aided example | not-started | |
| 22 | [docs/architecture/aota/07_PORTABILITY.md](../architecture/aota/07_PORTABILITY.md) | Arch | Cross-game ML reuse note | not-started | |
| 23 | [docs/Plan/Activities/00_INDEX.md](Activities/00_INDEX.md) | Plan-detail | Per-row training-trace plan | not-started | |

## Loop guard

- Operating contract: see the `/loop` prompt in the session that owns this tracker.
- ML surface authority: [docs/Spec/20_DECISION_ENGINE.md](../Spec/20_DECISION_ENGINE.md). Other specs MUST consume one of the four advisory RPCs there or extend that surface (with a corresponding Plan/14 slot).
- Dynamic-and-progressive invariant (per pass): every spec touched ships at least one contract test named `<SpecOrPlanShortName>_DynamicProgressive_<Property>Test` that asserts the composed Objective list varies with snapshot inputs AND that each Activity assignment closes distance to `CharacterRosterGoal` (Spec/05).
- Each advisor enumerates Phase 1 heuristic / Phase 2 rules+lookup / Phase 3 ONNX even if only Phase 1 ships today.

## Follow-ups (added in-pass)

<!-- Append discovered work as bullets. Do not delete completed entries; strike them with `~~text~~`. -->
