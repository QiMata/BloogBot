# README Cleanup Plan

## Overview

This document outlines the plan for cleaning up README files in the WWoW (Westworld of Warcraft) solution after a merge between two different but similar branches. The READMEs contain duplicated content, conflicting descriptions, inconsistent naming (BloogBot vs WWoW), and varied formatting standards.

**Created**: Auto-generated cleanup plan
**Status**: Complete
**Iteration**: 8

---

## Identified Issues

### 1. Duplicated Content Blocks
Many README files have clearly merged content where two different versions were concatenated rather than properly integrated:

**Examples found:**
- `Exports/BotRunner/README.md` - Has two Overview sections, two Project Structure sections, two Features lists
- `Exports/Navigation/README.md` - Contains two complete Architecture sections and duplicate File Structure
- `Services/PathfindingService/README.md` - Two Architecture diagrams (ASCII and Mermaid), duplicate Configuration sections
- `Services/StateManager/README.md` - Two distinct Overview sections with different detail levels
- `Exports/GameData.Core/README.md` - Duplicated Project Structure trees
- `BloogBot.AI/README.md` - Massive duplication with two complete feature sets and architecture descriptions

### 2. Inconsistent Project Naming
The solution has two names used interchangeably:
- **WWoW** (Westworld of Warcraft) - The new/preferred name
- **BloogBot** - The legacy name being deprecated

Per `.github/copilot-instructions.md`, "WWoW" should be used in all new documentation.

**Files using legacy name:**
- `BloogBot.AI/README.md` - Project itself uses "BloogBot" in name but README mixes both
- Various references to "BloogBot ecosystem" throughout

### 3. Formatting Inconsistencies

| Issue | Examples |
|-------|----------|
| Different heading styles | Some use emoji prefixes (??, ??), others plain text |
| Varied table formats | Some use pipes, some use lists |
| Inconsistent code block languages | Some specify `csharp`, others don't |
| Different section ordering | Overview/Features/Architecture/Usage varies |
| Varied diagram styles | ASCII art vs Mermaid vs none |

### 4. Outdated/Conflicting Information
- Different dependency versions mentioned in same file
- Conflicting port numbers for services
- Mismatched file structure trees

### 5. Missing Standard Sections
Per `copilot-instructions.md`, every project should have:
- Overview
- Architecture (with ASCII diagram preferred)
- Dependencies table
- Project references
- Related documentation

Some READMEs are missing sections or have them incomplete.

---

## Files Requiring Cleanup

### High Priority (Major duplication issues)
| File | Issues |
|------|--------|
| `Exports/BotRunner/README.md` | Dual structure blocks, duplicate usage examples, conflicting dependency tables |
| `Exports/Navigation/README.md` | Two complete Architecture sections, duplicate export descriptions |
| `Services/PathfindingService/README.md` | Two architecture diagrams, duplicate configuration sections |
| `Services/StateManager/README.md` | Two overview sections, conflicting architecture descriptions |
| `Exports/GameData.Core/README.md` | Duplicate project structure, conflicting descriptions |
| `BloogBot.AI/README.md` | Severe duplication - nearly entire content doubled |
| `Exports/WoWSharpClient/README.md` | Two architecture sections, duplicate feature lists |

### Medium Priority (Naming/formatting issues)
| File | Issues |
|------|--------|
| `Exports/BotCommLayer/README.md` | Needs review for consistency |
| `Exports/FastCall/README.md` | Needs review |
| `Services/BackgroundBotRunner/README.md` | Needs review |
| `Services/ForegroundBotRunner/README.md` | Needs review |
| `Services/DecisionEngineService/README.md` | Needs review |
| `Services/PromptHandlingService/README.md` | Needs review |

### Low Priority (Test projects - minimal content)
| File | Issues |
|------|--------|
| `Tests/PathfindingService.Tests/README.md` | Minor cleanup |
| `Tests/BotRunner.Tests/README.md` | Minor cleanup |
| `Tests/WoWSharpClient.Tests/README.md` | Minor cleanup |
| `Tests/Navigation.Physics.Tests/README.md` | Minor cleanup |
| Other test READMEs | Minor cleanup |

### UI Projects
| File | Issues |
|------|--------|
| `UI/StateManagerUI/README.md` | Needs review |
| `UI/WWoW.Systems/WWoW.Systems.AppHost/README.md` | Needs review |
| `UI/WWoW.Systems/WWoW.Systems.ServiceDefaults/README.md` | Needs review |

---

## Cleanup Strategy

### Phase 1: Template Creation
Create a standard README template following project conventions:

```markdown
# [Project Name]

[One-line description following: "A {type} that provides {functionality} for {purpose}"]

## Overview

[2-3 paragraph description covering:]
- What the component does
- Its role in the WWoW ecosystem
- Key capabilities

## Architecture

[ASCII diagram preferred]

## Project Structure

```
ProjectName/
??? ... (file tree)
```

## Key Components

[Table or list of main classes/modules]

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| ... | ... | ... |

## Project References

- **ProjectName**: Brief description

## Usage

[Code examples]

## Configuration

[Settings if applicable]

## Related Documentation

- See `path/to/related/README.md` for details
- See `ARCHITECTURE.md` for system overview

---

*This component is part of the WWoW (Westworld of Warcraft) simulation platform.*
```

### Phase 2: High-Priority Cleanup
For each high-priority file:
1. Identify the best/most complete sections from merged content
2. Remove duplicate sections
3. Consolidate information
4. Standardize naming to "WWoW"
5. Apply consistent formatting

### Phase 3: Medium/Low Priority Cleanup
- Apply template structure
- Fix naming inconsistencies
- Update formatting

### Phase 4: Validation
- Verify all cross-references work
- Check consistency across all READMEs
- Validate code examples are accurate

---

## Cleanup Prompt Template

Use this prompt for each cleanup session:

---

**Task**: Clean up README file for `[path/to/file]`

**Context**:
- This is part of the WWoW (Westworld of Warcraft) solution
- The file has merge conflicts from combining two branches
- Use "WWoW" as the project name, not "BloogBot" (legacy)
- Follow the template structure defined in `docs/README_CLEANUP_PLAN.md`

**Actions Required**:
1. Remove duplicate sections (keep the most complete/accurate version)
2. Consolidate overlapping content
3. Standardize to "WWoW" naming (BloogBot is deprecated)
4. Apply consistent formatting (no emoji prefixes, ASCII diagrams preferred)
5. Ensure all standard sections are present
6. Verify technical accuracy against actual code

**Standard Section Order**:
1. Title and one-line description
2. Overview (2-3 paragraphs)
3. Architecture (ASCII diagram)
4. Project Structure (file tree)
5. Key Components
6. Dependencies (table)
7. Project References
8. Usage/Examples
9. Configuration (if applicable)
10. Related Documentation
11. Footer

**Reference Files**:
- `.github/copilot-instructions.md` for coding/naming conventions
- `docs/README_CLEANUP_PLAN.md` for template and guidelines

---

## Progress Tracking

### Completed
- [x] Template created (Phase 1)
- [x] `Exports/BotRunner/README.md` - Iteration 1 (consolidated duplicates, standardized to WWoW, ASCII diagrams)
- [x] `Exports/Navigation/README.md` - Iteration 1 (consolidated duplicates, standardized to WWoW, ASCII diagrams)
- [x] `Services/PathfindingService/README.md` - Iteration 1 (consolidated duplicates, standardized to WWoW, ASCII diagrams)
- [x] `Services/StateManager/README.md` - Iteration 2 (consolidated duplicates, standardized to WWoW, ASCII diagrams)
- [x] `Exports/GameData.Core/README.md` - Iteration 2 (consolidated duplicates, standardized to WWoW, ASCII diagrams)
- [x] `BloogBot.AI/README.md` - Iteration 2 (consolidated duplicates, standardized to WWoW, ASCII diagrams)
- [x] `Exports/WoWSharpClient/README.md` - Iteration 2 (consolidated duplicates, standardized to WWoW, ASCII diagrams)
- [x] `Exports/Loader/README.md` - Iteration 3 (removed duplicates, standardized to WWoW, ASCII diagrams)
- [x] `UI/README.md` - Iteration 4 (standardized "BloogBot" to "WWoW" throughout, updated footer)
- [x] `Services/README.md` - Iteration 4 (standardized "BloogBot" to "WWoW" throughout, updated footer)
- [x] `README.md` (root) - Iteration 4 (reviewed for WWoW consistency, updated AI component reference)
- [x] `Services/BackgroundBotRunner/README.md` - Iteration 5 (removed duplicates, removed Mermaid, standardized to WWoW, ASCII diagrams)
- [x] `Services/ForegroundBotRunner/README.md` - Iteration 5 (removed duplicates, removed Mermaid, standardized to WWoW, ASCII diagrams)
- [x] `Services/DecisionEngineService/README.md` - Iteration 5 (removed duplicates, standardized to WWoW, ASCII diagrams)
- [x] `Services/PromptHandlingService/README.md` - Iteration 5 (removed duplicates, standardized to WWoW, clean footer)
- [x] `Exports/BotCommLayer/README.md` - Iteration 6 (removed duplicates, consolidated overlapping content, standardized to WWoW, ASCII diagrams)
- [x] `Exports/FastCall/README.md` - Iteration 6 (removed duplicates, consolidated overlapping content, standardized to WWoW, ASCII diagrams)
- [x] `Exports/WinImports/README.md` - Iteration 6 (removed duplicates, consolidated overlapping content, standardized to WWoW, ASCII diagrams)
- [x] `UI/StateManagerUI/README.md` - Iteration 7 (removed duplicates, consolidated overlapping content, standardized to WWoW, clean footer)
- [x] `UI/WWoW.Systems/WWoW.Systems.AppHost/README.md` - Iteration 7 (removed duplicates, consolidated overlapping content, standardized to WWoW, clean footer)
- [x] `UI/WWoW.Systems/WWoW.Systems.ServiceDefaults/README.md` - Iteration 7 (removed duplicates, consolidated overlapping content, standardized to WWoW, clean footer)

### High Priority Files
- [x] `Exports/BotRunner/README.md`
- [x] `Exports/Navigation/README.md`
- [x] `Services/PathfindingService/README.md`
- [x] `Services/StateManager/README.md`
- [x] `Exports/GameData.Core/README.md`
- [x] `BloogBot.AI/README.md`
- [x] `Exports/WoWSharpClient/README.md`

### Medium Priority Files
- [x] `Exports/BotCommLayer/README.md`
- [x] `Exports/FastCall/README.md`
- [x] `Services/BackgroundBotRunner/README.md`
- [x] `Services/ForegroundBotRunner/README.md`
- [x] `Services/DecisionEngineService/README.md`
- [x] `Services/PromptHandlingService/README.md`
- [x] `Exports/WinImports/README.md`

### UI Projects
- [x] `UI/StateManagerUI/README.md` - Iteration 7 (removed duplicates, consolidated overlapping content, standardized to WWoW, clean footer)
- [x] `UI/WWoW.Systems/WWoW.Systems.AppHost/README.md` - Iteration 7 (removed duplicates, consolidated overlapping content, standardized to WWoW, clean footer)
- [x] `UI/WWoW.Systems/WWoW.Systems.ServiceDefaults/README.md` - Iteration 7 (removed duplicates, consolidated overlapping content, standardized to WWoW, clean footer)

### Test Projects
- [x] `Tests/PathfindingService.Tests/README.md` - Iteration 8 (added standard WWoW footer)
- [x] `Tests/BotRunner.Tests/README.md` - Iteration 8 (added standard WWoW footer)
- [x] `Tests/WoWSharpClient.Tests/README.md` - Iteration 8 (added standard WWoW footer)
- [x] `Tests/Navigation.Physics.Tests/README.md` - Iteration 8 (added standard WWoW footer)
- [x] `Tests/PromptHandlingService.Tests/README.md` - Iteration 8 (added standard WWoW footer)
- [x] `Tests/WWoW.Tests.Infrastructure/README.md` - Iteration 8 (added standard WWoW footer)
- [x] `Tests/WWoW.RecordedTests.Shared.Tests/README.md` - Iteration 8 (added standard WWoW footer)

### Other
- [x] `Exports/README.md` - Iteration 8 (completely rewrote to remove massive duplication, standardized to WWoW, clean structure)
- [x] `Exports/GameData.Core/Enums/README.md` - Iteration 8 (added standard WWoW footer)
- [x] `Exports/WoWSharpClient/Networking/README.md` - Iteration 8 (added standard WWoW footer)
- [x] `Exports/WoWSharpClient/Networking/ClientComponents/README.md` - Iteration 8 (added standard WWoW footer)
- [x] `WWoW.RecordedTests.Shared/README.md` - Iteration 8 (added standard WWoW footer)
- [x] `WWoW.RecordedTests.PathingTests/README.md` - Iteration 8 (fixed BloogBot to WWoW, added standard footer)

### Root/High-Level Directories (NEWLY IDENTIFIED)
- [x] `README.md` (root) - Iteration 4 (reviewed for WWoW consistency, updated AI component reference)
- [x] `UI/README.md` - Iteration 4 (standardized "BloogBot" to "WWoW" throughout, updated footer)
- [x] `Services/README.md` - Iteration 4 (standardized "BloogBot" to "WWoW" throughout, updated footer)
- [x] `Exports/Loader/README.md` - Iteration 3 (removed duplicates, standardized to WWoW, ASCII diagrams)

### Validation
- [x] All cross-references verified
- [x] Consistent naming throughout
- [x] Code examples validated

---

## Review Findings - Iteration 8 (Final)

### All Files Complete
All README files in the project have been successfully cleaned up:
- ✓ All files use "WWoW" instead of "BloogBot"
- ✓ All files have standard footer: "*This component is part of the WWoW (Westworld of Warcraft) simulation platform.*"
- ✓ Duplicate content has been removed from all files
- ✓ ASCII diagrams are used (Mermaid removed)
- ✓ Consistent section structure maintained

### Summary by Category
- **High Priority (7 files)**: Complete - All duplicates removed, standardized
- **Medium Priority (7 files)**: Complete - All duplicates removed, standardized
- **UI Projects (3 files)**: Complete - All standardized to WWoW
- **Test Projects (7 files)**: Complete - All have standard footers
- **Other (6 files)**: Complete - Exports/README.md completely rewritten, all footers added
- **Root/High-Level (4 files)**: Complete - All standardized to WWoW

**Total files cleaned**: 35+ README files across 8 iterations

---

## Notes

- README cleanup is now complete
- All files follow consistent WWoW naming
- All files have standard footers
- Future README files should follow the template in this document

---

*Last Updated: Iteration 8 - README cleanup complete! All test project READMEs now have standard WWoW footers. Exports/README.md was completely rewritten to remove massive duplication issues from merge. All Other category files completed including networking documentation. WWoW.RecordedTests.PathingTests fixed BloogBot reference. Total: 35+ README files cleaned up across all iterations.*
