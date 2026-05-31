# WinImports — Windows API Wrappers

P/Invoke wrappers and helpers for Windows process + UI automation. Project:
`WinProcessImports.csproj`. Primarily consumed by `Services/ForegroundBotRunner`
(process detection + injection target discovery).

## Key files

| File | Purpose |
|------|---------|
| `WinProcessImports.cs` | Win32 P/Invoke declarations |
| `WoWProcessDetector.cs` | Locate running `WoW.exe` processes |
| `WoWProcessMonitor.cs` | Watch process lifecycle |
| `WoWUIAutomation.cs` | UI automation helpers |

## Special rules

- Windows-only by nature; keep platform-specific code isolated here.
- **Process safety:** never blanket-kill by image name — operate on specific
  PIDs only (`AGENTS.md` §6).
- Part of the shared `Exports/*` layer — no dependency on `Services/*` or `UI/*`.

> Path-specific agent rules: `.github/instructions/shared-libraries.instructions.md`.
