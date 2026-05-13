# Westworld of Warcraft — Docs Index

> **Start at [`SPEC.md`](SPEC.md).** This README is for orientation only.
> Everything an autonomous agent needs is reachable from `SPEC.md`.

## Top-level entry points

| File | What it is |
|---|---|
| [`SPEC.md`](SPEC.md) | Single entry point. Lists all Spec contracts + Plan phases + decisions of record. |
| [`TASKS.md`](TASKS.md) | Rolling task board. Which slots are in flight today. |
| [`TASKS_ARCHIVE.md`](TASKS_ARCHIVE.md) | Historical handoffs (2026-04 → 2026-05-06). Institutional memory; read-only. |
| [`ARCHIVE.md`](ARCHIVE.md) | Earlier completed phases archived (P0/P1/P2 etc.). |

## Stable contracts

| Path | Purpose |
|---|---|
| [`Spec/`](Spec/) | Architecture, components, telemetry, testing — the rules. |
| [`Plan/`](Plan/) | Phased implementation roadmap with dispatch-ready slots. |
| [`Plan/Activities/`](Plan/Activities/) | Per-activity-family implementation detail. |

## Reference (read-only)

| Path | Purpose |
|---|---|
| [`physics/`](physics/) | Reverse-engineered WoW.exe PhysX CCT-style physics. |
| [`server-protocol/`](server-protocol/) | WoW 1.12.1 protocol reference (7 docs). |
| [`leveling-guide/`](leveling-guide/) | Game knowledge base — zones, classes, professions, dungeons, raids, attunements, reputations. |
| [`TECHNICAL_NOTES.md`](TECHNICAL_NOTES.md) | Constants, env paths, known issues. |
| [`TRAVEL_PLANNING.md`](TRAVEL_PLANNING.md) | Travel modes and route-pack design notes. |
| [`PROJECT_STRUCTURE.md`](PROJECT_STRUCTURE.md) | Per-directory layout. |
| [`BUILD.md`](BUILD.md) | Build system + CI. |
| [`DOCKER_STACK.md`](DOCKER_STACK.md) | Docker compose stack. |
| [`DEVELOPMENT_GUIDE.md`](DEVELOPMENT_GUIDE.md) | Developer onboarding + per-project build matrix. |
| [`CODING_STANDARDS.md`](CODING_STANDARDS.md) | C# coding conventions. |
| [`IPC_COMMUNICATION.md`](IPC_COMMUNICATION.md) | Protobuf/TCP IPC detail. |
| [`CONFIG_SCHEMA.md`](CONFIG_SCHEMA.md) | Config file shape. |
| [`testing/`](testing/) | End-to-end integration test reference. |

## Audits (point-in-time)

| Path | Captured |
|---|---|
| [`Audits/CAPABILITY_AUDIT.md`](Audits/CAPABILITY_AUDIT.md) | 2026-03-07 ActionType implementation status |
| [`Audits/LIVEVALIDATION_AUDIT.md`](Audits/LIVEVALIDATION_AUDIT.md) | LiveValidation suite snapshot |
| [`Audits/INTEGRATION_FINDINGS.md`](Audits/INTEGRATION_FINDINGS.md) | Bad-behavior findings (formerly BAD_BEHAVIORS.md) |
| [`Audits/LIVEVALIDATION_FINDINGS.md`](Audits/LIVEVALIDATION_FINDINGS.md) | Test anti-pattern findings (formerly BAD_TEST_BEHAVIORS.md) |
| [`Audits/dll-separation-audit.md`](Audits/dll-separation-audit.md) | Navigation.dll export → DLL mapping |
| [`Audits/test_cleanup_audit.md`](Audits/test_cleanup_audit.md) | Test cleanup audit |

## Archive (superseded specs)

`Archive/` holds superseded specs preserved for institutional memory:

- `LIVING_SERVER_AUTOMATION_SPEC.md` — replaced by [`Spec/00_VISION.md`](Spec/00_VISION.md), [`Spec/04_ACTIVITIES.md`](Spec/04_ACTIVITIES.md), [`Spec/10_METRICS.md`](Spec/10_METRICS.md), [`Spec/11_LOGGING.md`](Spec/11_LOGGING.md).
- `WESTWORLD_ARCHITECTURE.md` — replaced by [`Spec/01_ARCHITECTURE.md`](Spec/01_ARCHITECTURE.md).
- `ELEVATOR_PITCH.md` — replaced by [`Spec/00_VISION.md`](Spec/00_VISION.md).
- `statemanager_modes_design.md` — replaced by [`Spec/02_STATEMANAGER.md`](Spec/02_STATEMANAGER.md).
- `ARCHITECTURE.md` (old) — replaced by [`Spec/01_ARCHITECTURE.md`](Spec/01_ARCHITECTURE.md).
- `BEHAVIOR_MATRIX.md` — replaced by [`Plan/`](Plan/) structure.
- `PHYSICS_DLL_SPLIT_PLAN.md`, `WOW_EXE_PACKET_PARITY_PLAN.md`,
  `physicsengine-calibration.md` — replaced by [`Spec/07_PHYSICS.md`](Spec/07_PHYSICS.md), [`Spec/08_PROTOCOLS.md`](Spec/08_PROTOCOLS.md).

Do not edit Archive/ files. If you need to revisit a decision, open a Spec PR.

## File naming

| Location | Convention | Example |
|---|---|---|
| `docs/Spec/`, `docs/Plan/` | `NN_TITLE.md` | `02_STATEMANAGER.md` |
| `docs/Plan/Activities/` | lowercase-hyphen.md | `professions-gathering.md` |
| Top-level | `SCREAMING_SNAKE_CASE.md` | `BUILD.md` |
| Numbered references (physics) | `NN_TITLE.md` | `01_CALL_GRAPH.md` |
