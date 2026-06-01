---
applyTo: "Services/**/*.cs"
---

# Worker services (`Services/*`)

Runnable C# worker microservices built on the `Exports/*` layer. Two bot
execution modes plus supporting services.

## Execution modes

- **ForegroundBotRunner** — injected into `WoW.exe`; direct memory read/write +
  Lua. FG work must be **state-gated** and must **never steal focus or capture
  the cursor**.
- **BackgroundBotRunner** — headless, pure-C# protocol emulation (no client).
  When parity matters, validate BG behavior against FG packet/event recordings.

## Service / port map

| Service | Port(s) | Role |
|---------|---------|------|
| PathfindingService | 5001 | A* + physics validation (native `Navigation.dll`) |
| WoWStateManager | 5002, 8088 | FSM / activity orchestration hub |
| DecisionEngineService | — | ML decision engine |
| PromptHandlingService | — | dialog / gossip / quest prompts |
| SceneDataService | TCP socket | world tile + object snapshots |

## Conventions

- Runtime StateManager ↔ BotRunner traffic is **protobuf over TCP, length-framed**.
- `ActivitySnapshot` carries major state **deltas**, not full enemy/object payloads.
- ⚠️ **PathfindingService is in an architectural freeze (2026-05-06).** Read the
  physics doc index `docs/physics/README.md` (pathfinding-overhaul section)
  before editing it or movement/transport code. Mesh/route fixes go in
  `tools/MmapGen/`, **not** new managed repair logic;
  never hardcode route-specific blocker coords or live-position guards.

## MaNGOS data — SOAP only

Never write the MaNGOS MySQL DB directly. Use the SOAP API
(`http://127.0.0.1:7878/`, `ADMINISTRATOR:PASSWORD`). Read-only MySQL is fine
for connectivity checks. See `AGENTS.md` §7.

## Validate with

```powershell
.\scripts\build.ps1
.\scripts\test-fast.ps1                 # unit
.\scripts\test-integration.ps1          # live MaNGOS stack (Layer 4)
```

## Process safety

Never blanket-kill `dotnet.exe` / `Game.exe` / `WoW.exe` — kill only PIDs you
started; use `.\run-tests.ps1 -CleanupRepoScopedOnly`. See `AGENTS.md` §6.

## See also

- Per-service context: each service's `CLAUDE.md`
  (e.g. `Services/WoWStateManager/CLAUDE.md`).
