# Plan 05 — Phase 4: Activity Registry (Lookup + Cross-Validate)

## Why this is small now

In the original phase plan this was a much bigger phase that wrapped
legality, BotSelector, LeaseLedger, OnDemand mode handler, Shodan
whisper, plus all coordinators. The 2026-05-12 design shifted most of
that work into **Phase 2 (OnDemand Engine)** because that's where the
catalog actually gets consumed by humans.

What remains in Phase 4 is the **catalog-side support** that the
autonomous progression engine (Phase 6) needs — registry lookups,
legality cross-validation, and the per-row reference data the
ProgressionPlanner consults.

## Entry pre-requisite

Phase 0 (catalog compiled) + Phase 1 (task families) + Phase 2
(OnDemand engine landed, exercises catalog).

## Exit criteria

- [ ] `ActivityRegistry` answers `GetById`, `GetByLocation`,
      `GetByLevelBand`, `GetByFamily` for every catalog row.
- [ ] `LegalityValidator.CheckAutonomous(bot, activity)` returns
      structured failures for autonomous progression callers (without
      fixup plan — that's OnDemand only, from Phase 2).
- [ ] `LockoutVerifier` (formerly slot S2.12a) implemented: reads
      `mangos.character_instance` via SOAP/MySQL and answers
      "is this character locked from this instance?" — used by
      autonomous progression and by OnDemand's circumvent step.
- [ ] Markdown drift test: `Plan/Activities/00_INDEX.md` ↔
      `ActivityCatalog.cs` stays in sync.
- [ ] Server capability check wired (`Naxx`, `AQ40`, etc. from config).

## Slots

### S4.1 — `ActivityRegistry`

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Services/WoWStateManager/Activities/ActivityRegistry.cs`
  - `Tests/BotRunner.Tests/Activities/ActivityRegistryTests.cs`
- **Goal:** Lookup helpers. Compiled from `ActivityCatalog`.

### S4.2 — `LegalityValidator` (autonomous-mode)

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Services/WoWStateManager/Activities/Legality/LegalityValidator.cs`
  - `Tests/BotRunner.Tests/Activities/LegalityValidatorTests.cs`
- **Goal:** The 7 legality steps from
  [`Spec/04_ACTIVITIES.md#legality-validation`](../Spec/04_ACTIVITIES.md#legality-validation),
  returning a `LegalityResult` (pass / fail + reasons). The fixup-plan
  variant lives in Phase 2.

### S4.3 — `LockoutVerifier`

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Services/WoWStateManager/Activities/Legality/LockoutVerifier.cs`
  - `Tests/BotRunner.Tests/Activities/LockoutVerifierTests.cs`
- **Goal:** DB-authoritative lockout state lookups. Used by Phase 6
  autonomous progression for "can this bot run X now?"

### S4.4 — Catalog drift test

- **Owner:** `monorepo-worker`
- **Status:** open
- **Goal:** Test asserts `Plan/Activities/00_INDEX.md` row-by-row matches
  `ActivityCatalog.cs` (same IDs, same level ranges).
