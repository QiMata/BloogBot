---
name: failure-reason-mapping
description: Map a new failure path to the canonical FailureReason enum, keeping it in 1:1 sync with the error-taxonomy spec (enforced by a drift test). Use when code needs a failure classification that does not yet exist.
trigger: add a FailureReason, error taxonomy, classify a failure, new failure code, FailureReasonCatalogTests, map a failure path
---

# Failure Reason Mapping

## Goal

Add a new failure classification so a Task/coordinator/service can report *why* it
failed, keeping the `FailureReason` enum a faithful mirror of the error-taxonomy
spec — the enum and the spec are kept 1:1 by a drift test.

## Inputs

- The failure path and which taxonomy category it belongs to.
- Key files (verified):
  - Enum: `Exports/GameData.Core/Enums/FailureReason.cs`.
  - **Authoritative catalog:** `docs/Spec/12_ERROR_TAXONOMY.md` (snake_case reasons
    grouped by category: pathfinding, transport, socket, bot lifecycle, physics,
    task execution, activity legality, inventory, server, catalog, …).
  - Drift test: `Tests/BotRunner.Tests/Spec/FailureReasonCatalogTests.cs`
    (asserts enum ⇄ spec parity).
- Area rules: `.github/instructions/shared-libraries.instructions.md`.

## Preconditions

- No existing `FailureReason` already fits (search the enum + spec first).
- `dotnet build` is green.

## Procedure

1. **Spec first**: add the new snake_case reason to `docs/Spec/12_ERROR_TAXONOMY.md`
   under the correct category section.
2. Add the matching value to `Exports/GameData.Core/Enums/FailureReason.cs`,
   keeping the category grouping aligned.
3. Use `FailureReason.<Value>` at the failure site (task completion, metrics,
   logging) — never invent an ad-hoc string.
4. Run the drift test; if it changed shape, update
   `FailureReasonCatalogTests.cs` deliberately.

## Verification

- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~FailureReasonCatalogTests"`
  — must pass (enum and spec in sync).
- `.\scripts\test-fast.ps1` for no broader regression.

## Outputs

- New entry in both `docs/Spec/12_ERROR_TAXONOMY.md` and `FailureReason.cs`.
- Failure site(s) reporting the new reason.

## Failure modes and recovery

- **Enum-only change** (no spec update) → drift test fails; add to the spec too.
- **Wrong category** → keep the spec grouping and enum grouping aligned.
- **Inventing a string reason** in code instead of using the enum — the enum is the
  single source.

## Related skills

- [[botrunner-task-implementation]] — Tasks report `FailureReason` on failure.
- [[config-hot-reload-subscriber]] — NACKs carry a `FailureReason`.
- [[crash-cluster-triage]] — hardening patches often add a failure classification.
- Reference: `docs/Spec/12_ERROR_TAXONOMY.md`.
