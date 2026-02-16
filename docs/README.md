# WWoW Documentation

Technical documentation for the WWoW (Westworld of Warcraft) simulation platform.

## Directory Structure

```
docs/
  README.md                    # This file
  TASKS.md                     # Active/open task list (phases 1-7)
  ARCHIVE.md                   # Completed task history
  CLAUDE.md                    # Claude Code project instructions & session protocol
  ARCHITECTURE.md              # High-level system architecture
  PROJECT_STRUCTURE.md         # Detailed file/folder layouts
  DEVELOPMENT_GUIDE.md         # Developer onboarding guide
  CODING_STANDARDS.md          # C# coding conventions
  IPC_COMMUNICATION.md         # Inter-process communication
  BUILD.md                     # CMake build system & CI/CD
  DEVELOPING.md                # Per-project build configs & debug setup
  TECHNICAL_NOTES.md           # Constants, env paths, recording mappings, known issues
  PHYSICS_ENGINE_PROMPT.md     # Movement recording format reference
  physics/                     # PhysX CCT-style physics system documentation
    README.md                  # Physics system overview and index
    01_CALL_GRAPH.md           # Character Controller call graph
    ...                        # 10 numbered docs + reference docs
  server-protocol/             # WoW 1.12.1 protocol reference
    auth-protocol.md
    world-login.md
    movement-protocol.md
    object-updates.md
    opcodes-1.12.1.md
    transport-protocol.md
    update-fields-1.12.1.md
  testing/
    end-to-end-integration-test.md  # E2E integration test guide
```

## Quick Links

| Document | Description |
|----------|-------------|
| [TASKS.md](TASKS.md) | Active task list with phases and priorities |
| [ARCHIVE.md](ARCHIVE.md) | Completed tasks and session history |
| [CLAUDE.md](CLAUDE.md) | Claude Code instructions and session handoff protocol |
| [ARCHITECTURE.md](ARCHITECTURE.md) | High-level system architecture |
| [TECHNICAL_NOTES.md](TECHNICAL_NOTES.md) | Constants, env paths, recording mappings |
| [physics/README.md](physics/README.md) | Physics engine documentation index |

## Documentation Standards

### File Naming

| Location | Convention | Example |
|----------|------------|---------|
| `docs/` | `SCREAMING_SNAKE_CASE.md` | `ARCHITECTURE.md` |
| Numbered sequences | `NN_SCREAMING_SNAKE_CASE.md` | `01_CALL_GRAPH.md` |
| Project READMEs | `README.md` | Always `README.md` |

### Content Structure

1. **Title** - H1 heading
2. **Overview** - Brief description
3. **Main Content** - H2/H3 headings
4. **Related Documentation** - Links to related docs
