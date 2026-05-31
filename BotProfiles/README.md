# BotProfiles/

Class/specialization combat profiles — combat rotation, buff management, rest
behavior, and target selection for each WoW class+spec. 27 spec folders plus
`Common/` (shared base classes) and `ProgressionProfiles/` (leveling configs).

A profile composes **Actions** into combat decisions; it is consumed by both the
ForegroundBotRunner and BackgroundBotRunner. New behaviors that drive a world-state
change belong in an `IBotTask` (see the `botrunner-task-implementation` skill), not
in a profile.

- **Agent rules & conventions:** [CLAUDE.md](CLAUDE.md) and
  `.github/instructions/bot-profiles.instructions.md`.
- **Adding a profile:** the `bot-profile` skill (`.claude/skills/bot-profile/`) and
  the "Adding a New Profile" section of [CLAUDE.md](CLAUDE.md).
