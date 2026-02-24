# WWoW Documentation

Technical documentation for the WWoW (Westworld of Warcraft) simulation platform.

## Canonical Task Workflow

Cross-session implementation tracking is maintained through:
- `docs/TASKS.md` (master orchestrator)
- directory-local `TASKS.md` files (project-level execution backlogs)
- directory-local `TASKS_ARCHIVE.md` files (completed items)

## Directory Structure

```
docs/
  README.md                    # This file
  TASKS.md                     # Master task orchestrator
  ARCHIVE.md                   # Historical archive
  CLAUDE.md                    # Project instructions and architecture notes
  ARCHITECTURE.md              # High-level system architecture
  PROJECT_STRUCTURE.md         # Detailed file/folder layouts
  DEVELOPMENT_GUIDE.md         # Developer onboarding guide
  CODING_STANDARDS.md          # C# coding conventions
  IPC_COMMUNICATION.md         # Inter-process communication
  BUILD.md                     # Build system and CI notes
  DEVELOPING.md                # Per-project build configs and debug setup
  TECHNICAL_NOTES.md           # Constants, env paths, known issues
  PHYSICS_ENGINE_PROMPT.md     # Movement recording format reference
  physics/                     # PhysX CCT-style physics documentation
  server-protocol/             # WoW 1.12.1 protocol references
  testing/                     # End-to-end testing guides
```

## Quick Links

| Document | Description |
|----------|-------------|
| [TASKS.md](TASKS.md) | Master task orchestration and handoff |
| [ARCHIVE.md](ARCHIVE.md) | Completed tasks and historical records |
| [ARCHITECTURE.md](ARCHITECTURE.md) | High-level system architecture |
| [TECHNICAL_NOTES.md](TECHNICAL_NOTES.md) | Constants and implementation notes |
| [physics/README.md](physics/README.md) | Physics documentation index |

## Documentation Standards

### File Naming

| Location | Convention | Example |
|----------|------------|---------|
| `docs/` | `SCREAMING_SNAKE_CASE.md` | `ARCHITECTURE.md` |
| Numbered sequences | `NN_SCREAMING_SNAKE_CASE.md` | `01_CALL_GRAPH.md` |
| Project READMEs | `README.md` | Always `README.md` |

### Content Structure

1. Title (H1)
2. Overview
3. Main content
4. Related documentation links
