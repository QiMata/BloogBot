# BloogBot - Claude Code Project Instructions

## Session Handoff Protocol

Before completing ANY session, Claude MUST create `next-session-prompt.md` in the project root.

### Requirements

The file must contain a single fenced code block tagged as `prompt` with a complete, self-contained prompt for the next session:

````markdown
```prompt
<Your complete handoff prompt here>
```
````

### The prompt MUST include:

1. **What was accomplished** - Summary of completed work this session
2. **What to work on next** - Specific files, functions, and approach
3. **Blockers or failed attempts** - What didn't work and why
4. **Decisions made** - Design choices, trade-offs, rationale
5. **Current state of in-progress work** - Partial implementations, uncommitted changes, test status

### Completion

If all tasks in TASKS.md are complete, write `ALL_TASKS_COMPLETE` inside the code block instead:

````markdown
```prompt
ALL_TASKS_COMPLETE
```
````

### Important

- The handoff prompt must be **self-contained** - the next session starts fresh with no prior context
- Include exact file paths and line numbers for any in-progress work
- If a task was partially completed, describe what's done and what remains
- Reference TASKS.md for the overall task list
