# Execution Plans for Large or Risky Changes

> **Plan before you edit broadly.** For any change in the categories below,
> write an execution plan **before** touching code. The plan is cheap; an
> unplanned multi-package refactor that violates a freeze or a wire contract is
> not. Small, localized edits do not need one — see
> [When a plan is NOT required](#when-a-plan-is-not-required).

This convention exists because coding agents (and humans) can otherwise jump
straight into wide-reaching edits without first stating *what* will change,
*how* it's validated, and *how* it rolls back. WWoW also enforces several
CRITICAL freezes and contracts (pathfinding freeze, protobuf wire contract,
Shodan / test-isolation rules) that an unplanned change can silently break. A
short, reviewed plan catches that before the diff exists.

## When an execution plan is required

Create (or update) an execution plan before starting work whenever the change
involves any of:

- **Multi-package / multi-project refactors** — anything that edits more than
  one project under `Exports/`, `Services/`, `UI/`, or `Tests/` in a coordinated
  way (the strict top-to-bottom dependency flow means one change ripples).
- **Database / schema changes** — MaNGOS-adjacent data shapes, persisted state,
  or any stored-format change. (Never edit the MaNGOS DB directly — SOAP only.)
- **Authentication or authorization changes** — auth/login flow, GM-access
  paths, account-level permissions, or anything touching credentials.
- **Public API changes** — interfaces in `Exports/GameData.Core`
  (`IObjectManager`, `IWoWUnit`, etc.), the IPC/protobuf contracts, or any
  cross-service surface other code depends on.
- **Infrastructure changes** — `docker/`, `compose*.yml`,
  `.github/workflows/*.yml`, `Directory.Build.{props,targets}`, `.editorconfig`.
- **Production migration work** — anything that runs once against, or changes
  the shape of, live/production state.
- **Large dependency upgrades** — framework, SDK, or major NuGet/package bumps
  (anything beyond a patch bump of a single transitive dependency).

**Repo-specific add-ons** — a plan is also required for any change that touches:

- The **pathfinding freeze** — `Services/PathfindingService`,
  `Exports/Navigation`, `tools/MmapGen`, or the BotRunner movement/transport
  code (see [docs/physics/README.md](../docs/physics/README.md)).
- The **protobuf wire contract** — `Exports/BotCommLayer/Models/ProtoDef/*.proto`
  and the generated `*.cs`.
- The **Shodan** production-GM rules or the **test-isolation** rules (tests
  drive Activities, not Actions) — see [AGENTS.md](../AGENTS.md) /
  [CLAUDE.md](../CLAUDE.md).

When in doubt, write the plan — it is a few minutes, and reviewers will ask for
one anyway on a change of this size.

## When a plan is NOT required

Skip the plan for genuinely small, low-risk work:

- Typo, comment, or docs-only fixes.
- Single-file bug fixes with existing test coverage.
- Localized edits inside one project that don't change a public surface,
  schema, or contract, and don't touch a frozen area.

If a "small" change starts spreading across packages or into a frozen area,
stop and write the plan before continuing.

## How to use this

1. **Copy the template below** into a new file at `.agent/plans/<slug>.md`
   (commit it alongside the change), **or** paste it directly into the pull
   request description.
2. **Fill in every section.** "N/A" is a valid answer — an empty section is not.
3. **Get it reviewed before broad edits begin**, not after the diff exists.
4. **Keep it current.** If scope changes mid-implementation, update the plan
   (especially *Files likely to change*, *Compatibility concerns*, and *Open
   questions*).

The pull request and issue templates under [`.github/`](../.github/) reference
this convention, and the
[Pull Request Process](../.github/CONTRIBUTING.md#pull-request-process) in
`CONTRIBUTING.md` lists it in the pre-submit checklist.

## Execution Plan Template

Copy everything inside the block below.

````markdown
# Execution Plan: <short title>

## Goal
What outcome this change delivers and why it's worth doing.

## Current behavior
How the system behaves today in the area being changed.

## Proposed behavior
How the system should behave after the change.

## Files likely to change
The packages, projects, and representative file paths expected to change.
Call out any frozen areas (pathfinding, protobuf wire contract) explicitly.

## Tests to add/update
New or updated unit / LiveValidation / parity tests, and what each proves.

## Compatibility concerns
Breaking changes to public APIs, IPC/protobuf contracts, snapshots, or
FG↔BG parity. Who/what depends on the changed surface?

## Migration concerns
Data, schema, config, or persisted-state migrations; ordering and one-time
steps; how a half-applied migration is detected and recovered.

## Validation commands
The exact commands to build and verify the change. For example:

```bash
dotnet build WestworldOfWarcraft.sln
dotnet test WestworldOfWarcraft.sln --configuration Release
```

## Rollback plan
How to revert safely if this regresses production — revert commit, flag flip,
or migration reversal — and what to watch to confirm the rollback worked.

## Open questions
Unresolved decisions or unknowns that need an answer before/while implementing.
````
