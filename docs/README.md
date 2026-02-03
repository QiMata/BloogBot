# WWoW Documentation

This directory contains technical documentation for the WWoW (Westworld of Warcraft) simulation platform.

## Directory Structure

```
docs/
??? README.md              # This file
??? physics/               # PhysX CCT-style physics system documentation
    ??? README.md          # Physics system overview and index
    ??? 01_CALL_GRAPH.md   # Character Controller call graph
    ??? 02_PARAMS_AND_STATE.md
    ??? 03_PERFRAME_MOVE_PIPELINE.md
    ??? 04_SWEEP_TEST_MOVE_CHARACTER.md
    ??? 05_DO_SWEEP_TEST.md
    ??? 06_COLLISION_RESPONSE.md
    ??? 07_OVERLAP_RECOVERY_COMPUTE_MTD.md
    ??? 08_SLOPE_STEP_CEILING_RULES.md
    ??? 09_RIDE_ON_TOUCHED_OBJECT.md
    ??? 10_PARITY_TEST_HARNESS.md
    ??? PHYSX_CAPSULE_SWEEP_RULES.md
    ??? PHYSX_CCT_RULES.md
    ??? SWEEP_TEST_MOVE_CHARACTER_REFERENCE.md
    ??? VANILLA_WOW_PHYSICS_INTENTION_RESEARCH.md
    ??? WOW_PHYSICS_SERVICE_GUIDE.md
```

## Documentation Standards

All markdown documentation in this repository follows these conventions:

### File Naming

| Location | Convention | Example |
|----------|------------|---------|
| Root directory | `SCREAMING_SNAKE_CASE.md` | `ARCHITECTURE.md`, `README.md` |
| `.github/` | `SCREAMING_SNAKE_CASE.md` | `CONTRIBUTING.md`, `COPILOT_CONTEXT.md` |
| `docs/` | `SCREAMING_SNAKE_CASE.md` | `PHYSICS_OVERVIEW.md` |
| Numbered sequences | `NN_SCREAMING_SNAKE_CASE.md` | `01_CALL_GRAPH.md` |
| Project READMEs | `README.md` | Always `README.md` |

### Content Structure

Every major documentation file should include:

1. **Title** - H1 heading with the document name
2. **Overview** - Brief description of the content
3. **Main Content** - Organized with H2/H3 headings
4. **Related Documentation** - Links to related docs
5. **Signature Line** (for component docs):
   ```markdown
   ---

   *This component is part of the WWoW (Westworld of Warcraft) simulation platform.*
   ```

### Diagrams

Use ASCII/box-drawing characters for architecture diagrams to ensure compatibility:

```
???????????????     ???????????????
? Component A ??????? Component B ?
???????????????     ???????????????
```

## Quick Links

| Document | Description |
|----------|-------------|
| [ARCHITECTURE.md](../ARCHITECTURE.md) | High-level system architecture |
| [PROJECT_STRUCTURE.md](../PROJECT_STRUCTURE.md) | Detailed file/folder layouts |
| [DEVELOPMENT_GUIDE.md](../DEVELOPMENT_GUIDE.md) | Developer onboarding guide |
| [CODING_STANDARDS.md](../CODING_STANDARDS.md) | C# coding conventions |
| [IPC_COMMUNICATION.md](../IPC_COMMUNICATION.md) | Inter-process communication |
| [Physics Documentation](./physics/README.md) | Character controller physics |

## Contributing to Documentation

When adding or updating documentation:

1. Follow the file naming conventions above
2. Use consistent heading hierarchy (H1 for title, H2 for sections)
3. Include cross-references to related documentation
4. Add tables for structured information
5. Use code blocks with language hints for examples

---

*This component is part of the WWoW (Westworld of Warcraft) simulation platform.*
